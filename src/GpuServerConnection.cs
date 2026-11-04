using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
namespace GRAL_2001
{
    // Persistent subprocess transport only; callers must implement phase semantics and output application.
    internal sealed class GpuServerConnection : IDisposable
    {
        private readonly Process process;
        private readonly Stream input, output;
        private readonly FileStream stderrLog;
        private readonly System.Threading.Tasks.Task stderrCopy;
        private bool disposed;
        private readonly CancellationToken cancellationToken;
        private readonly CancellationTokenRegistration cancellationRegistration;
        internal long BytesSent { get; private set; }
        internal long BytesReceived { get; private set; }
        internal GpuServerConnection(string python, string script, string stderrPath)
        {
            cancellationToken=Program.CancelToken.Token;
            cancellationToken.ThrowIfCancellationRequested();
            var info=new ProcessStartInfo(python) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true };
            info.ArgumentList.Add("-X"); info.ArgumentList.Add("utf8"); info.ArgumentList.Add("-B"); info.ArgumentList.Add(script);
            stderrLog=GpuLogFiles.OpenExclusive(stderrPath,out string actualStderr);
            StderrPath=actualStderr;
            // The server names its diagnostic files after this log, so they follow the per-process choice.
            info.Environment["INU_GPU_STDERR"]=actualStderr;
            try { process=Process.Start(info) ?? throw new InvalidOperationException("GPU server failed to start"); }
            catch(System.ComponentModel.Win32Exception error) { stderrLog.Dispose(); throw new InvalidOperationException("Cannot start the GPU server with INU_GPU_PYTHON="+python+": "+error.Message,error); }
            catch { stderrLog.Dispose(); throw; }
            stderrCopy=process.StandardError.BaseStream.CopyToAsync(stderrLog);
            input=new BufferedStream(process.StandardInput.BaseStream,1024*1024); output=new BufferedStream(process.StandardOutput.BaseStream,1024*1024);
            cancellationRegistration=cancellationToken.Register(() =>
            {
                try { if(!process.HasExited)process.Kill(entireProcessTree:true); }
                catch(InvalidOperationException) when(process.HasExited) { }
            });
            try { Probe(); }
            catch(Exception startupError) when(startupError is not OperationCanceledException)
            {
                // The probe error carries the server's reason; a nonzero exit code during cleanup adds nothing.
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
            try { if(!process.HasExited)process.Kill(entireProcessTree:true); }
            catch(InvalidOperationException) when(process.HasExited) { }
            catch(Exception cleanup) { throw new AggregateException("GPU write and abort failed",failure,cleanup); }
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
            cancellationRegistration.Dispose();
            var failures=new List<Exception>();bool timedOut=false;
            try
            {
                try { input.Dispose(); } catch(Exception error) { failures.Add(error); }
                try
                {
                    if(!process.WaitForExit(5000))
                    {
                        timedOut=true;
                        // This process reference belongs to the child started by this connection.
                        try { process.Kill(entireProcessTree:true); }
                        catch(InvalidOperationException) when(process.HasExited) { }
                        if(!process.WaitForExit(5000))throw new TimeoutException("GPU server did not exit after termination");
                        failures.Add(new TimeoutException("GPU server shutdown timed out; owned process tree terminated"));
                    }
                    if(!timedOut && !cancellationToken.IsCancellationRequested && process.ExitCode!=0)failures.Add(new InvalidOperationException("GPU server exit code "+process.ExitCode));
                }
                catch(Exception error) { failures.Add(error); }
                try { stderrCopy.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
                catch(Exception error) { failures.Add(error);process.StandardError.Dispose(); }
            }
            finally { output.Dispose();stderrLog.Dispose();process.Dispose(); }
            if(failures.Count==1)throw failures[0];
            if(failures.Count>1)throw new AggregateException("GPU server cleanup failed",failures);
        }
    }
}
