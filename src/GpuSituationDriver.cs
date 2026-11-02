using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace GRAL_2001
{
    // Opt-in situation driver; residual and fresh kernels finish before host output processing.
    internal sealed class GpuSituationDriver : IDisposable
    {
        private readonly GpuServerConnection server;
        private readonly GpuInputCache inputCache=new GpuInputCache();
        private readonly string metricsPath;
        private readonly int chunk, block;
        private readonly Dictionary<string,object> receptors;
        private bool receptorsCommitted;
        private static int Setting(string name,int fallback,int min,int max)
        {
            string raw=Environment.GetEnvironmentVariable(name);
            if(raw==null)return fallback;
            if(!int.TryParse(raw,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int value) || value<min || value>max)
                throw new InvalidOperationException("Invalid "+name);
            return value;
        }
        private readonly List<string> fields=new List<string>();
        internal long BytesSent => server.BytesSent;
        internal long BytesReceived => server.BytesReceived;
        internal GpuSituationDriver(string python,string script,string manifest,string metricsPath,string stderrPath)
        {
            using var json=JsonDocument.Parse(File.ReadAllText(manifest));
            var omit=new HashSet<string>{"PS_TimeSerTempValues","PS_TimeSerVelValues","TS_TimeSerTempValues","TS_TimeSerVelValues","TransientDepo","Conz4d","SourceGroups"};
            foreach(var field in json.RootElement.GetProperty("fields").EnumerateArray())
            {
                string name=field.GetProperty("name").GetString();
                if(!field.GetProperty("constant").GetBoolean() && !omit.Contains(name)) fields.Add(name);
            }
            this.metricsPath=metricsPath;
            chunk=Setting("INU_GRAL_GPU_CHUNK",4096,1,65536);
            block=Setting("INU_GRAL_GPU_BLOCK",128,128,256);
            if(block!=128 && block!=256)throw new InvalidOperationException("Invalid INU_GRAL_GPU_BLOCK");
            receptors=ReceptorCsr();
            server=new GpuServerConnection(python,script,stderrPath);
        }
        internal static string GetUnsupportedReason(int first,int count)
        {
            Program.CancelToken.Token.ThrowIfCancellationRequested();
            if(first<1 || count<0) throw new InvalidOperationException("Invalid particle range");
            if(Program.ISTATIONAER!=Consts.TransientMode || Program.Topo!=Consts.TerrainAvailable || Program.IStatistics!=4 ||
               !Program.UseFixedRndSeedVal || Program.BuildingsExist || Program.WetDeposition || Program.DepositionExist || Program.Odour ||
               Program.WriteVerticalConcentration || Program.TunnelEntr || Program.TunnelOppLane || Program.AdaptiveRoughnessMax!=0 || Program.LogLevel!=0 ||
               Program.NS<1 || Program.NS>9 || Program.ReceptorNumber<0 || Program.ReceptorNumber>50 ||
               Program.PS_Count!=0 || Program.TS_Count!=0)
                return "Unsupported GPU configuration";
            if(Program.AS_Count<0 || Program.LS_Count<0 ||
               (Program.AS_Count>0 && (Program.AS_Mode==null || Program.AS_Mode.Length<=Program.AS_Count)) ||
               (Program.LS_Count>0 && (Program.LS_Mode==null || Program.LS_Mode.Length<=Program.LS_Count)) ||
               Program.DecayRate==null || Program.ReceptorNearbyBuilding==null)
                throw new InvalidOperationException("Invalid GPU configuration array length");
            if((Program.AS_Count>0 && Program.AS_Mode.Skip(1).Take(Program.AS_Count).Any(x=>x!=0)) ||
               (Program.LS_Count>0 && Program.LS_Mode.Skip(1).Take(Program.LS_Count).Any(x=>x!=0)) ||
               Program.DecayRate.Any(x=>x!=0))
                return "Unsupported GPU configuration: zero-decay line or area emissions without deposition required";
            if(Program.ReceptorNearbyBuilding.Any(x=>x) || (Program.ReceptorNumber>0 && Program.GridCellReceptors==null))
                return "Unsupported GPU receptor configuration";
            if(Program.SourceType==null || (long)first+count>Program.SourceType.Length ||
               Program.ParticleMode==null || (long)first+count>Program.ParticleMode.Length)
                throw new InvalidOperationException("Invalid particle range");
            for(int i=first;i<first+count;i++)
            {
                if(Program.SourceType[i]!=Consts.SourceTypeArea && Program.SourceType[i]!=Consts.SourceTypeLine)
                    return "Unsupported GPU particle source type";
                if(Program.ParticleMode[i]!=0) return "Unsupported GPU particle deposition configuration";
            }
            return null;
        }
        internal static void Guard(int first,int count)
        {
            string reason=GetUnsupportedReason(first,count);
            if(reason!=null)throw new InvalidOperationException(reason);
        }
        private static Dictionary<string,object> ReceptorCsr()
        {
            if(Program.GridCellReceptors==null)
                return new Dictionary<string,object>{{"GridRecRow",0},{"GridRecStart",Array.Empty<int>()},{"GridRecList",Array.Empty<int>()}};
            int row=checked(Program.NYL+1), cells=checked((Program.NXL+1)*row);
            var start=new int[checked(cells+1)];var list=new List<int>();
            for(int i=0;i<=Program.NXL;i++)
            for(int j=0;j<=Program.NYL;j++)
            {
                int cell=checked(i*row+j);start[cell]=list.Count;
                if(Program.GridCellReceptors.TryGetValue((i,j),out var values)) list.AddRange(values);
            }
            start[cells]=list.Count;
            return new Dictionary<string,object>{{"GridRecRow",row},{"GridRecStart",start},{"GridRecList",list.ToArray()}};
        }
        private static long ActiveFreshCount(int first,int count)
        {
            long active=0;
            for(int i=first;i<first+count;i++)
            {
                if(Program.ParticleSource[i]==0)continue;
                double mass=Program.ParticleMass[i];
                if(Program.ISTATIONAER==Consts.TransientMode && Program.EmissionTimeseriesExist)
                {
                    int sg=Program.ParticleSG[i];
                    int internalSg=Program.SourceGroups.IndexOf(sg);
                    if(internalSg>=0)sg=internalSg;
                    // Preserve V2701's upper-bound guard, including the final IWET behavior.
                    if(Program.IWET<=Program.EmFacTimeSeries.GetUpperBound(0) && sg<=Program.EmFacTimeSeries.GetUpperBound(1))
                        mass*=Program.EmFacTimeSeries[Program.IWET-1,sg];
                }
                if(!double.IsFinite(mass))throw new InvalidOperationException("Unsupported GPU non-finite modulated particle mass");
                if(mass>0)active++;
            }
            return active;
        }
        private static bool TraceThisSituation() => Program.IWET==Setting("INU_GPU_TRACE_SITUATION",1,1,int.MaxValue);
        internal void RunSituation(int first,int count)
        {
            Guard(first,count);
            long activeFresh=ActiveFreshCount(first,count);
            long sentBefore=BytesSent, receivedBefore=BytesReceived;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            var step=System.Diagnostics.Stopwatch.StartNew();
            var extra=new Dictionary<string,object>(receptors){{"RnGSeed1",Program.RnGSeed.Seed1},{"RnGSeed2",Program.RnGSeed.Seed2},{"SourceGroups",Program.SourceGroups.ToArray()}};
            long receptorSavedBytes=receptorsCommitted?4L*((long)((int[])receptors["GridRecStart"]).Length+((int[])receptors["GridRecList"]).Length):0;
            // CSR arrays are constructed once for this server; keep the scalar in every init frame.
            if(receptorsCommitted) { extra.Remove("GridRecStart");extra.Remove("GridRecList"); }
            bool traceSituation=TraceThisSituation();
            string tracePath=traceSituation?Environment.GetEnvironmentVariable("INU_GPU_DEVICE_TRACE"):null;
            string residualTracePath=traceSituation?Environment.GetEnvironmentVariable("INU_GPU_DEVICE_RESIDUAL_TRACE"):null;
            extra.Add("TraceEnabled",tracePath!=null || residualTracePath!=null);
            if(tracePath!=null)extra.Add("Trace",new double[checked(first+count),8]);
            if(residualTracePath!=null)extra.Add("ResidualTrace",new double[Setting("INU_GPU_RESIDUAL_TRACE_COUNT",64,1,65536),16]);
            string stepPath=traceSituation?Environment.GetEnvironmentVariable("INU_GPU_DEVICE_STEP_TRACE"):null;
            if(stepPath!=null)
            {
                if(tracePath==null)throw new InvalidOperationException("Step trace requires final trace path");
                extra.Add("TraceIds",new int[]{2,636,5618,10215,12071,19036,25684,28760,30955});
                extra.Add("StepTrace",new double[9,4000,16]);
            }
            var hashClock=System.Diagnostics.Stopwatch.StartNew();
            var plan=inputCache.Prepare(fields);
            double cacheHashSeconds=hashClock.Elapsed.TotalSeconds;
            server.Init(plan.Fields,extra);
            using var initAck=server.ReadHeader();
            plan.Commit(initAck.RootElement);
            receptorsCommitted=true;
            JsonElement initDetails=initAck.RootElement.Clone();
            double initRoundTrip=step.Elapsed.TotalSeconds, residualSplit=0, residualRoundTrip=0, residualKernel=0;int batches=0;long residualParticles=0;
            using(var iter=GpuResidualBatch.Enumerate().GetEnumerator())
            while(true)
            {
                step.Restart();bool more=iter.MoveNext();residualSplit+=step.Elapsed.TotalSeconds;if(!more)break;
                var batch=iter.Current;int n=((int[])batch["_resI"]).Length;batches++;residualParticles=checked(residualParticles+n);
                step.Restart();server.Execute("residual",0,n,Array.Empty<string>(),batch,chunk,block);using(var ack=server.ReadHeader())
                {if(ack.RootElement.GetProperty("payload_bytes").GetInt64()!=0)throw new InvalidDataException("Unexpected residual payload");residualKernel+=ack.RootElement.GetProperty("kernel_ms").GetDouble();}
                residualRoundTrip+=step.Elapsed.TotalSeconds;
            }
            double freshRoundTrip=0,freshKernel=0;
            if(count>0)
            {
                step.Restart();server.Execute("fresh",first,count,Array.Empty<string>(),null,chunk,block);using(var ack=server.ReadHeader())
                {if(ack.RootElement.GetProperty("payload_bytes").GetInt64()!=0)throw new InvalidDataException("Unexpected fresh payload");freshKernel=ack.RootElement.GetProperty("kernel_ms").GetDouble();}
                freshRoundTrip=step.Elapsed.TotalSeconds;
            }
            step.Restart();server.SendControl("result");using var result=server.ReadHeader();double resultHeader=step.Elapsed.TotalSeconds;
            step.Restart();server.ApplyResult(result);double resultTransferApply=step.Elapsed.TotalSeconds;
            var metrics=new {situation=Program.IWET,rng_seed1=Program.RnGSeed.Seed1,rng_seed2=Program.RnGSeed.Seed2,fresh_particles=count,fresh_active_particles=activeFresh,fresh_active_particles_definition="positive finite mass after upstream time-series modulation, before geometry rejection",residual_particles=residualParticles,chunk,block,elapsed_s=clock.Elapsed.TotalSeconds,init_roundtrip_s=initRoundTrip,cache_hash_s=cacheHashSeconds,omitted_field_names=plan.Omitted,saved_payload_bytes=checked(plan.SavedPayloadBytes+receptorSavedBytes),saved_receptor_payload_bytes=receptorSavedBytes,residual_split_s=residualSplit,residual_roundtrip_s=residualRoundTrip,
                residual_kernel_ms=residualKernel,residual_batches=batches,fresh_roundtrip_s=freshRoundTrip,fresh_kernel_ms=freshKernel,
                result_request_to_header_s=resultHeader,result_transfer_apply_s=resultTransferApply,bytes_sent=BytesSent-sentBefore,bytes_received=BytesReceived-receivedBefore,
                kernel_total_ms=result.RootElement.GetProperty("kernel_total_ms").GetDouble(),phase_counts=result.RootElement.GetProperty("phase_counts").Clone(),
                init_server=initDetails,result_server_metrics=result.RootElement.GetProperty("metrics_cumulative").Clone()};
            File.AppendAllText(metricsPath,JsonSerializer.Serialize(metrics)+Environment.NewLine);
        }
        public void Dispose()
        {
            Exception closeError=null;
            try { if(!Program.CancelToken.IsCancellationRequested)server.SendControl("close"); } catch(Exception error) { closeError=error; }
            try { server.Dispose(); }
            catch(Exception error) when(closeError!=null) { throw new AggregateException("GPU close and cleanup failed",closeError,error); }
            if(closeError!=null)throw closeError;
        }
    }
}
