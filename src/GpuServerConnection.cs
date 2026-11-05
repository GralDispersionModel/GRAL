using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
namespace GRAL_2001
{
    // Transport to the GPU runtime (GpuRuntimeServer). Request and response frames travel through in-process pipes to a
    // dedicated thread that owns the CUDA context. Callers implement phase semantics and output application.
    internal sealed class GpuServerConnection : IDisposable
    {
        private readonly Thread runtimeThread;
        private readonly GpuRuntimeServer runtime;
        private readonly TextWriter runtimeLog;
        private readonly Stream input, output;
        private readonly FileStream stderrLog;
        private bool disposed;
        private readonly CancellationToken cancellationToken;
        internal long BytesSent { get; private set; }
        internal long BytesReceived { get; private set; }
        internal GpuServerConnection(string manifestPath, string stderrPath)
        {
            cancellationToken=Program.CancelToken.Token;
            cancellationToken.ThrowIfCancellationRequested();
            stderrLog=GpuLogFiles.OpenExclusive(stderrPath,out string actualStderr);
            StderrPath=actualStderr;
            var request=new AnonymousPipeServerStream(PipeDirection.Out,HandleInheritability.None,1024*1024);
            var requestReader=new AnonymousPipeClientStream(PipeDirection.In,request.ClientSafePipeHandle);
            var response=new AnonymousPipeServerStream(PipeDirection.Out,HandleInheritability.None,1024*1024);
            var responseReader=new AnonymousPipeClientStream(PipeDirection.In,response.ClientSafePipeHandle);
            try
            {
                runtimeLog=TextWriter.Synchronized(new StreamWriter(stderrLog,new UTF8Encoding(false),4096,true){AutoFlush=true});
                runtime=new GpuRuntimeServer(new BufferedStream(requestReader,1024*1024),new BufferedStream(response,1024*1024),manifestPath,actualStderr,runtimeLog.WriteLine,cancellationToken);
            }
            catch { request.Dispose();requestReader.Dispose();response.Dispose();responseReader.Dispose();runtimeLog?.Dispose();stderrLog.Dispose();throw; }
            runtimeThread=new Thread(() =>
            {
                // Run reports its own failures through error frames; anything else must not take down GRAL.
                try { runtime.Run(); }
                catch(Exception error) { try { runtimeLog.WriteLine("GPU runtime thread failed: "+error); } catch(Exception) { } }
            }) { IsBackground=true, Name="GRAL GPU runtime" };
            runtimeThread.Start();
            input=new BufferedStream(request,1024*1024); output=new BufferedStream(responseReader,1024*1024);
            // The runtime checks the token between launches, transfers and startup steps and then answers with an error frame.
            try { Probe(); }
            catch
            {
                // Closing the request pipe ends the runtime thread, also after a stop request.
                try { Dispose(); } catch(Exception) { }
                throw;
            }
        }
        internal string StderrPath { get; }
        // Startup handshake: the server answers after CUDA, NVRTC and the kernel module are ready, or sends its startup error.
        private void Probe()
        {
            IOException sendFailure=null;
            try { SendControl("probe"); }
            catch(IOException error) { sendFailure=error; } // the server may already have exited; its error frame is still readable
            JsonDocument reply;
            try { reply=ReadHeader(); }
            catch(IOException readFailure)
            { throw new IOException("GPU server exited during startup without a reason; see "+StderrPath,sendFailure ?? readFailure); }
            using(reply)
            {
                if(!reply.RootElement.TryGetProperty("probe",out var probe))throw new InvalidDataException("GPU server did not answer the startup probe");
                string Text(string name) => probe.TryGetProperty(name,out var value) ? value.ToString() : "?";
                Console.WriteLine("GPU device "+Text("ordinal")+": "+Text("name")+", CC "+Text("compute_capability")+", "+Text("total_memory_mib")+" MiB, driver CUDA "+Text("driver_cuda")+
                                  ", NVRTC "+Text("nvrtc_version")+" "+Text("binary_kind")+" "+Text("target")+", kernel_exec_timeout="+Text("kernel_exec_timeout")+", tcc="+Text("tcc_driver"));
            }
        }
        private void AbortFailedWrite(Exception failure)
        {
            // The runtime stopped reading because it failed; its error frame names the reason.
            if(failure is IOException)
            {
                try { using var reply=ReadHeader(); }
                catch(InvalidDataException serverError) { throw new InvalidDataException(serverError.Message,failure); }
                catch(Exception) { }
            }
        }
        internal void Init(IEnumerable<string> fields, IReadOnlyDictionary<string, object> extra = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { BytesSent=checked(BytesSent+GpuFieldExport.Export(input,fields,"init",extra:extra)); }
            catch(Exception failure) { AbortFailedWrite(failure);throw; }
            cancellationToken.ThrowIfCancellationRequested();
        }
        internal void Execute(string phase, int first, int count, IEnumerable<string> fields, IReadOnlyDictionary<string, object> extra = null, int chunk = 4096, int block = 128)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { BytesSent=checked(BytesSent+GpuFieldExport.Export(input,fields,"execute",phase,first,count,extra,chunk,block)); }
            catch(Exception failure) { AbortFailedWrite(failure);throw; }
            cancellationToken.ThrowIfCancellationRequested();
        }
        internal void SendControl(string command, object extra = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] header=JsonSerializer.SerializeToUtf8Bytes(new {command, extra, payload_bytes=0});
            using var w=new BinaryWriter(input,System.Text.Encoding.UTF8,true);
            w.Write((ulong)header.Length);w.Write(header);w.Flush();BytesSent=checked(BytesSent+8+header.Length);
        }
        internal JsonDocument ReadHeader()
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
            using var r=new BinaryReader(output,System.Text.Encoding.UTF8,true);
            ulong size=r.ReadUInt64();
            if(size>1024*1024) throw new InvalidDataException("GPU response header too large");
            byte[] data=r.ReadBytes(checked((int)size));
            if(data.Length!=(int)size) throw new EndOfStreamException("Truncated GPU response header");
            BytesReceived=checked(BytesReceived+8+(long)size);
            JsonDocument doc=JsonDocument.Parse(data);
            if(!doc.RootElement.TryGetProperty("status",out var status) || status.GetString()!="ok")
            {
                string error=doc.RootElement.ToString();doc.Dispose();throw new InvalidDataException("GPU server failed: "+error);
            }
            return doc;
            }
            catch(Exception error) when(cancellationToken.IsCancellationRequested)
            { throw new OperationCanceledException("GPU server read canceled",error,cancellationToken); }
        }
        internal void ApplyResult(JsonDocument header)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BytesReceived=checked(BytesReceived+GpuFieldImport.Apply(output,header.RootElement));
            cancellationToken.ThrowIfCancellationRequested();
        }
        internal JsonElement ReadResultMetrics(JsonDocument result)
        {
            var metrics=result.RootElement.GetProperty("metrics_cumulative").Clone();
            if(result.RootElement.TryGetProperty("metrics_follow",out var follow) && follow.GetBoolean())
            {
                using var footer=ReadHeader();
                if(footer.RootElement.GetProperty("frame").GetString()!="result_metrics" || footer.RootElement.GetProperty("payload_bytes").GetInt64()!=0)
                    throw new InvalidDataException("Invalid GPU result metrics footer");
                metrics=footer.RootElement.GetProperty("metrics_cumulative").Clone();
            }
            return metrics;
        }
        internal void ReadPayload(Span<byte> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.ReadExactly(buffer);BytesReceived=checked(BytesReceived+buffer.Length);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            var failures=new List<Exception>();
            bool stopped;
            try
            {
                // End of input lets the runtime finish its close sequence.
                try { input.Dispose(); } catch(Exception error) { failures.Add(error); }
                stopped=runtimeThread.Join(10000);
                if(!stopped)
                {
                    // A runtime blocked writing a response that nobody reads is released by closing the response pipe.
                    try { output.Dispose(); } catch(Exception error) { failures.Add(error); }
                    stopped=runtimeThread.Join(5000);
                    failures.Add(new TimeoutException(stopped ? "GPU runtime stopped only after its response pipe was closed"
                                                              : "GPU runtime thread did not stop within 15 s; it may still be inside a CUDA call"));
                }
                else if(!cancellationToken.IsCancellationRequested && runtime.ExitCode!=0)failures.Add(new InvalidOperationException("GPU server exit code "+runtime.ExitCode));
            }
            finally
            {
                try { output.Dispose(); } catch(Exception) { }
            }
            // A thread that is still alive may still log; keep its log open rather than pull it away.
            if(stopped) { runtimeLog.Dispose();stderrLog.Dispose(); }
            if(failures.Count==1)throw failures[0];
            if(failures.Count>1)throw new AggregateException("GPU server cleanup failed",failures);
        }
    }
}
