using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            return IntValue(Environment.GetEnvironmentVariable(name),name,fallback,min,max);
        }
        internal static int IntValue(string raw,string label,int fallback,int min,int max)
        {
            if(raw==null)return fallback;
            if(!int.TryParse(raw,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int value) || value<min || value>max)
                throw new ArgumentException("Invalid "+label+": '"+raw+"' (use an integer from "+min.ToString(System.Globalization.CultureInfo.InvariantCulture)+" to "+max.ToString(System.Globalization.CultureInfo.InvariantCulture)+")");
            return value;
        }
        // Threads per block: 128 (default) or 256
        internal static int BlockValue(string raw,string label)
        {
            int value=IntValue(raw,label,128,128,256);
            if(value!=128 && value!=256)throw new ArgumentException("Invalid "+label+": '"+raw+"' (use 128 or 256)");
            return value;
        }
        private readonly List<string> fields=new List<string>();
        internal long BytesSent => server.BytesSent;
        internal long BytesReceived => server.BytesReceived;
        internal GpuSituationDriver(string manifest,string metricsPath,string stderrPath)
        {
            using var json=JsonDocument.Parse(File.ReadAllText(manifest));
            var omit=new HashSet<string>{"PS_TimeSerTempValues","PS_TimeSerVelValues","TS_TimeSerTempValues","TS_TimeSerVelValues","TransientDepo","TransientDepoMode","TransientDepoVdep","TransientDepoVsed","VegetationDepoVelFactors","VegetationDepoGasFactor","VegetationDepoPMFactor","Conz4d","SourceGroups"};
            if(Program.ISTATIONAER==Consts.StationaryMode) { omit.Add("Conz5d");omit.Add("EmissionPerSG"); }
            foreach(var field in json.RootElement.GetProperty("fields").EnumerateArray())
            {
                string name=field.GetProperty("name").GetString();
                if(!field.GetProperty("constant").GetBoolean() && !omit.Contains(name)) fields.Add(name);
            }
            this.metricsPath=metricsPath;
            chunk=IntValue(GpuSettings.Value("chunk"),GpuSettings.Label("chunk"),65536,1,65536);
            block=BlockValue(GpuSettings.Value("block"),GpuSettings.Label("block"));
            receptors=ReceptorCsr();
            server=new GpuServerConnection(manifest,stderrPath);
        }
        // scanAccumulated=false skips the non-finite output scan; Guard uses it after CanRun has already scanned this situation.
        internal static string GetUnsupportedReason(int first,int count,bool scanAccumulated=true)
        {
            Program.CancelToken.Token.ThrowIfCancellationRequested();
            if(first<1 || count<0) throw new InvalidOperationException("Invalid particle range");
            if((Program.ISTATIONAER!=Consts.TransientMode && Program.ISTATIONAER!=Consts.StationaryMode) ||
               (Program.Topo!=Consts.TerrainAvailable && Program.Topo!=Consts.TerrainFlat) ||
               (Program.IStatistics!=Consts.MeteoZR && Program.IStatistics!=Consts.MeteoEKI && Program.IStatistics!=Consts.MeteoSonic && Program.IStatistics!=Consts.MeteoPgtAll) ||
               Program.NS<1 || Program.ReceptorNumber<0)
                throw new InvalidOperationException("Invalid GPU configuration");
            // Input-dependent conditions the GPU server would reject after the run has started: fall back instead.
            string input=InputReason();
            if(input!=null)return input;
            if(Program.LogLevel<0 || Program.LogLevel>3)throw new InvalidOperationException("Invalid GPU diagnostic log level");
            if(Program.PS_Count<0 || Program.TS_Count<0 || Program.AS_Count<0 || Program.LS_Count<0 ||
               (Program.PS_Count>0 && (Program.PS_Mode==null || Program.PS_Mode.Length<=Program.PS_Count)) ||
               (Program.TS_Count>0 && (Program.TS_Mode==null || Program.TS_Mode.Length<=Program.TS_Count)) ||
               (Program.AS_Count>0 && (Program.AS_Mode==null || Program.AS_Mode.Length<=Program.AS_Count)) ||
               (Program.LS_Count>0 && (Program.LS_Mode==null || Program.LS_Mode.Length<=Program.LS_Count)) ||
               Program.DecayRate==null || Program.ReceptorNearbyBuilding==null || Program.SourceGroups==null)
                throw new InvalidOperationException("Invalid GPU configuration array length");
            if(Program.SourceGroups.Any(group=>group<0 || group>=Program.DecayRate.Length))
                throw new InvalidOperationException("Invalid GPU source group");
            if((Program.PS_Count>0 && Program.PS_Mode.Skip(1).Take(Program.PS_Count).Any(x=>x>Consts.DepoOnly)) ||
               (Program.TS_Count>0 && Program.TS_Mode.Skip(1).Take(Program.TS_Count).Any(x=>x>Consts.DepoOnly)) ||
               (Program.AS_Count>0 && Program.AS_Mode.Skip(1).Take(Program.AS_Count).Any(x=>x>Consts.DepoOnly)) ||
               (Program.LS_Count>0 && Program.LS_Mode.Skip(1).Take(Program.LS_Count).Any(x=>x>Consts.DepoOnly)) ||
               Program.DecayRate.Any(x=>!double.IsFinite(x)))
                throw new InvalidOperationException("Invalid GPU source mode or decay rate");
            if(!Program.DepositionExist &&
               ((Program.PS_Count>0 && Program.PS_Mode.Skip(1).Take(Program.PS_Count).Any(x=>x>0)) ||
                (Program.TS_Count>0 && Program.TS_Mode.Skip(1).Take(Program.TS_Count).Any(x=>x>0)) ||
                (Program.AS_Count>0 && Program.AS_Mode.Skip(1).Take(Program.AS_Count).Any(x=>x>0)) ||
                (Program.LS_Count>0 && Program.LS_Mode.Skip(1).Take(Program.LS_Count).Any(x=>x>0))))
                throw new InvalidOperationException("Deposition mode requires deposition output");
            if(Program.ReceptorNumber>0 && Program.GridCellReceptors==null)
                throw new InvalidOperationException("Invalid GPU receptor configuration");
            if(Program.SourceType==null || (long)first+count>Program.SourceType.Length ||
               Program.ParticleMode==null || (long)first+count>Program.ParticleMode.Length ||
               Program.ParticleSource==null || (long)first+count>Program.ParticleSource.Length ||
               Program.ParticleMass==null || (long)first+count>Program.ParticleMass.Length ||
               Program.ParticleSG==null || (long)first+count>Program.ParticleSG.Length ||
               Program.ParticleVdep==null || (long)first+count>Program.ParticleVdep.Length ||
               Program.ParticleVsed==null || (long)first+count>Program.ParticleVsed.Length)
                throw new InvalidOperationException("Invalid particle range");
            for(int i=first;i<first+count;i++)
            {
                if(Program.ParticleSource[i]==0)continue;
                int limit=Program.SourceType[i] switch { Consts.SourceTypePoint => Program.PS_Count, Consts.SourceTypePortal => Program.TS_Count, Consts.SourceTypeLine => Program.LS_Count, Consts.SourceTypeArea => Program.AS_Count, _ => -1 };
                if(limit<0) throw new InvalidOperationException("Invalid GPU particle source type");
                if(Program.ParticleSource[i]<1 || Program.ParticleSource[i]>limit) throw new InvalidOperationException("Invalid particle source index");
                if(Program.ParticleMode[i]>Consts.DepoOnly)throw new InvalidOperationException("Invalid particle deposition mode");
                if(Program.ParticleMode[i]>0 && !Program.DepositionExist)throw new InvalidOperationException("Particle deposition requires deposition output");
                if(!double.IsFinite(Program.ParticleMass[i]) || !float.IsFinite(Program.ParticleVdep[i]) || !float.IsFinite(Program.ParticleVsed[i]))
                    throw new InvalidOperationException("Non-finite particle value");
            }
            if(Program.IWET<1) throw new InvalidOperationException("Invalid situation index");
            ValidateTimeSeries(Program.PS_TimeSerTempValues,Program.PS_TimeSeriesTemperature,Program.PS_Count);
            ValidateTimeSeries(Program.PS_TimeSerVelValues,Program.PS_TimeSeriesVelocity,Program.PS_Count);
            ValidateTimeSeries(Program.TS_TimeSerTempValues,Program.TS_TimeSeriesTemperature,Program.TS_Count);
            ValidateTimeSeries(Program.TS_TimeSerVelValues,Program.TS_TimeSeriesVelocity,Program.TS_Count);
            ValidateSourceArrays(Program.PS_Count, new[]{"PS_D","PS_HorExitDir","PS_HorExitVel","PS_T","PS_V"});
            ValidateSourceArrays(Program.TS_Count, new[]{"TS_Area","TS_Height","TS_Width","TS_T","TS_V","TS_X1","TS_X2","TS_Y1","TS_Y2"});
            for(int i=1;i<=Program.TS_Count;i++)
                if(Program.TS_Width[i]<=0 || Program.TS_Height[i]<=0) throw new InvalidOperationException("Invalid portal dimensions");
            // Last, because it is the only check that costs a pass over output arrays.
            if(scanAccumulated && NonFiniteAccumulatedOutput())return "non-finite accumulated output";
            return null;
        }
        private static string InputReason()
        {
            if(Program.WriteVerticalConcentration && Program.ISTATIONAER!=Consts.TransientMode)
                return "vertical concentration output requires transient mode";
            // The CPU reads mettimeseries.dat and meteopgt.all only for this combination (Program.cs, transient branch).
            if(Program.ISTATIONAER==Consts.TransientMode && Program.IStatistics!=Consts.MeteoPgtAll)
                return "transient mode requires meteopgt.all";
            // Same condition as the server's profile requirement; the profile arrays hold 50 levels.
            bool needsProfile=(Program.Topo==Consts.TerrainFlat && !Program.BuildingsExist) ||
                              Program.IStatistics==Consts.MeteoZR || Program.IStatistics==Consts.MeteoSonic;
            if(needsProfile && (Program.MetProfileNumb<1 || Program.MetProfileNumb>50))
                return "meteorology profile count out of range";
            return null;
        }
        // The server rejects non-finite values in uploaded output fields; a CPU-fallback situation or a .tmp restart can contain them.
        private static bool NonFiniteAccumulatedOutput()
        {
            if(Program.WriteVerticalConcentration && NonFinite(Program.ConzSsum))return true;
            if(Program.Odour && (NonFinite(Program.Conz3dp) || NonFinite(Program.Conz3dm)))return true;
            if(NonFinite(Program.Conz3dDouble))return true;
            if((Program.DepositionExist || Program.WetDeposition) && NonFinite(Program.Depo_conz))return true;
            return false;
        }
        private static bool NonFinite(Array outer)
        {
            if(outer==null)return false;
            bool found=false;
            System.Threading.Tasks.Parallel.For(0,outer.Length,(i,loop)=>{ if(NonFiniteNested(outer.GetValue(i))){found=true;loop.Stop();} });
            return found;
        }
        private static bool NonFiniteNested(object value)
        {
            switch(value)
            {
                case null: return false;
                case float[] f: foreach(float x in f) if(!float.IsFinite(x)) return true; return false;
                case double[] d: foreach(double x in d) if(!double.IsFinite(x)) return true; return false;
                case Array a: foreach(object child in a) if(NonFiniteNested(child)) return true; return false;
                default: throw new InvalidOperationException("Unsupported accumulated output type");
            }
        }
        private static void ValidateSourceArrays(int count,string[] names)
        {
            foreach(string name in names)
            {
                var array=(Array)typeof(Program).GetField(name).GetValue(null);
                if(count>0 && (array==null || array.Length<=count)) throw new InvalidOperationException("Invalid source array " + name);
                for(int i=1;i<=count;i++) if(!double.IsFinite(Convert.ToDouble(array.GetValue(i)))) throw new InvalidOperationException("Non-finite source value " + name);
            }
        }
        internal static void Guard(int first,int count)
        {
            string reason=GetUnsupportedReason(first,count,scanAccumulated:false);
            if(reason!=null)throw new InvalidOperationException(reason);
        }
        private static Dictionary<string,object> ReceptorCsr()
        {
            if(Program.GridCellReceptors==null)
                return new Dictionary<string,object>{{"GridRecRow",0},{"GridRecStart",Array.Empty<int>()},{"GridRecList",Array.Empty<int>()}};
            int needed=checked(Program.ReceptorNumber+1);
            if(Program.ReceptorNearbyBuilding==null || Program.ReceptorNearbyBuilding.Length<needed ||
               Program.ReceptorX==null || Program.ReceptorX.Length<needed || Program.ReceptorY==null || Program.ReceptorY.Length<needed ||
               Program.ReceptorZ==null || Program.ReceptorZ.Length<needed || Program.ReceptorIInd==null || Program.ReceptorIInd.Length<needed ||
               Program.ReceptorJInd==null || Program.ReceptorJInd.Length<needed)
                throw new InvalidOperationException("Invalid GPU receptor array length");
            int row=checked(Program.NYL+1), cells=checked((Program.NXL+1)*row);
            var start=new int[checked(cells+1)];var list=new List<int>();
            foreach(var entry in Program.GridCellReceptors)
            {
                if(entry.Key.i<0 || entry.Key.i>Program.NXL || entry.Key.j<0 || entry.Key.j>Program.NYL || entry.Value==null ||
                   entry.Value.Any(index=>index<1 || index>Program.ReceptorNumber))
                    throw new InvalidOperationException("Invalid GPU receptor CSR mapping");
            }
            for(int i=1;i<=Program.ReceptorNumber;i++)
                if(!double.IsFinite(Program.ReceptorX[i]) || !double.IsFinite(Program.ReceptorY[i]) || !float.IsFinite(Program.ReceptorZ[i]))
                    throw new InvalidOperationException("Non-finite GPU receptor coordinate");
            int groups=Program.SourceGroups?.Count ?? throw new InvalidOperationException("Missing source groups");
            if(Program.ReceptorNumber>0 && (Program.ReceptorConc==null || Program.ReceptorConc.Length<needed ||
               Program.ReceptorParticleMaxConc==null || Program.ReceptorParticleMaxConc.Length<needed))
                throw new InvalidOperationException("Invalid GPU receptor output length");
            for(int i=1;i<=Program.ReceptorNumber;i++)
                if(Program.ReceptorConc[i]==null || Program.ReceptorConc[i].Length<groups ||
                   Program.ReceptorParticleMaxConc[i]==null || Program.ReceptorParticleMaxConc[i].Length<groups)
                    throw new InvalidOperationException("Invalid GPU receptor output shape");
            for(int i=0;i<=Program.NXL;i++)
            for(int j=0;j<=Program.NYL;j++)
            {
                int cell=checked(i*row+j);start[cell]=list.Count;
                if(Program.GridCellReceptors.TryGetValue((i,j),out var values)) list.AddRange(values);
            }
            start[cells]=list.Count;
            return new Dictionary<string,object>{{"GridRecRow",row},{"GridRecStart",start},{"GridRecList",list.ToArray()}};
        }
        private static (uint,uint) ExecutionSeeds()
        {
            if(Program.UseFixedRndSeedVal)return (Program.RnGSeed.Seed1,Program.RnGSeed.Seed2);
            static uint Next()
            {
                uint value;
                do value=BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint))); while(value==0);
                return value;
            }
            return (Next(),Next());
        }
        private static void AddExecutionSeeds(Dictionary<string,object> frame,List<object> receipts,string phase,int count)
        {
            if(Program.UseFixedRndSeedVal)return;
            var seeds=ExecutionSeeds();frame.Add("RnGSeed1",seeds.Item1);frame.Add("RnGSeed2",seeds.Item2);
            receipts.Add(new {phase,count,seed1=seeds.Item1,seed2=seeds.Item2});
        }
        private static void AddPrimitiveSnapshots(Dictionary<string,object> extra)
        {
            int count=Program.SourceGroups?.Count ?? throw new InvalidOperationException("Missing source groups");
            var mode=new int[count];var vdep=new double[count];var vsed=new double[count];
            if(Program.TransientDepo!=null)
            {
                if(Program.TransientDepo.Length<count)throw new InvalidOperationException("Invalid transient deposition length");
                for(int i=0;i<count;i++)
                {
                    var item=Program.TransientDepo[i] ?? throw new InvalidOperationException("Missing transient deposition entry");
                    mode[i]=item.DepositionMode;vdep[i]=item.Vdep;vsed[i]=item.Vsed;
                    if(mode[i]<Consts.DepoOff || mode[i]>Consts.DepoOnly || !double.IsFinite(vdep[i]) || !double.IsFinite(vsed[i]))
                        throw new InvalidOperationException("Invalid transient deposition value");
                    if(mode[i]>0 && !Program.DepositionExist)throw new InvalidOperationException("Transient deposition requires deposition output");
                }
            }
            extra.Add("TransientDepoMode",mode);extra.Add("TransientDepoVdep",vdep);extra.Add("TransientDepoVsed",vsed);
            float gas=Program.VegetationDepoVelFactors.VelGasFact, pm=Program.VegetationDepoVelFactors.VelPMxxFact;
            if(!float.IsFinite(gas) || !float.IsFinite(pm))throw new InvalidOperationException("Non-finite vegetation deposition factor");
            extra.Add("VegetationDepoGasFactor",gas);extra.Add("VegetationDepoPMFactor",pm);
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
        private static int ValidateTimeSeries(List<TimeSeriesColumn> columns, int[] indexes, int count)
        {
            int n=columns?.Count ?? 0;
            int rows=n==0?0:columns[0]?.Value?.Length ?? throw new InvalidOperationException("Invalid time-series column");
            for(int col=0;col<n;col++)
            {
                if(columns[col]?.Value==null || columns[col].Value.Length!=rows) throw new InvalidOperationException("Invalid time-series column length");
                foreach(float value in columns[col].Value) if(!float.IsFinite(value)) throw new InvalidOperationException("Non-finite time-series value");
            }
            if(count>0 && (indexes==null || indexes.Length<=count)) throw new InvalidOperationException("Invalid time-series index array");
            for(int i=1;i<=count;i++) if(indexes[i]<-1 || indexes[i]>=n) throw new InvalidOperationException("Invalid time-series index");
            return rows;
        }
        private static float[,] TimeSeriesSnapshot(List<TimeSeriesColumn> columns, int[] indexes, int count)
        {
            int rows=ValidateTimeSeries(columns,indexes,count), n=columns?.Count ?? 0;
            var result=new float[n,rows];
            for(int col=0;col<n;col++) for(int row=0;row<rows;row++) result[col,row]=columns[col].Value[row];
            return result;
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
            AddPrimitiveSnapshots(extra);
            var executionSeeds=new List<object>();
            if(Program.IWET<1) throw new InvalidOperationException("Invalid situation index");
            extra.Add("PS_TimeSerTempValues",TimeSeriesSnapshot(Program.PS_TimeSerTempValues,Program.PS_TimeSeriesTemperature,Program.PS_Count));
            extra.Add("PS_TimeSerVelValues",TimeSeriesSnapshot(Program.PS_TimeSerVelValues,Program.PS_TimeSeriesVelocity,Program.PS_Count));
            extra.Add("TS_TimeSerTempValues",TimeSeriesSnapshot(Program.TS_TimeSerTempValues,Program.TS_TimeSeriesTemperature,Program.TS_Count));
            extra.Add("TS_TimeSerVelValues",TimeSeriesSnapshot(Program.TS_TimeSerVelValues,Program.TS_TimeSeriesVelocity,Program.TS_Count));
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
            if(Program.ISTATIONAER==Consts.TransientMode)
            using(var iter=GpuResidualBatch.Enumerate().GetEnumerator())
            while(true)
            {
                step.Restart();bool more=iter.MoveNext();residualSplit+=step.Elapsed.TotalSeconds;if(!more)break;
                var batch=iter.Current;int n=((int[])batch["_resI"]).Length;batches++;residualParticles=checked(residualParticles+n);
                AddExecutionSeeds(batch,executionSeeds,"residual",n);
                step.Restart();server.Execute("residual",0,n,Array.Empty<string>(),batch,chunk,block);using(var ack=server.ReadHeader())
                {if(ack.RootElement.GetProperty("payload_bytes").GetInt64()!=0)throw new InvalidDataException("Unexpected residual payload");residualKernel+=ack.RootElement.GetProperty("kernel_ms").GetDouble();}
                residualRoundTrip+=step.Elapsed.TotalSeconds;
            }
            double freshRoundTrip=0,freshKernel=0;
            if(count>0)
            {
                Dictionary<string,object> freshExtra=null;
                if(!Program.UseFixedRndSeedVal)
                {
                    freshExtra=new Dictionary<string,object>();
                    AddExecutionSeeds(freshExtra,executionSeeds,"fresh",count);
                }
                step.Restart();server.Execute("fresh",first,count,Array.Empty<string>(),freshExtra,chunk,block);using(var ack=server.ReadHeader())
                {if(ack.RootElement.GetProperty("payload_bytes").GetInt64()!=0)throw new InvalidDataException("Unexpected fresh payload");freshKernel=ack.RootElement.GetProperty("kernel_ms").GetDouble();}
                freshRoundTrip=step.Elapsed.TotalSeconds;
            }
            step.Restart();server.SendControl("result");using var result=server.ReadHeader();double resultHeader=step.Elapsed.TotalSeconds;
            step.Restart();server.ApplyResult(result);
            var serverMetrics=server.ReadResultMetrics(result);
            double resultTransferApply=step.Elapsed.TotalSeconds;
            var metrics=new {situation=Program.IWET,rng_seed1=Program.RnGSeed.Seed1,rng_seed2=Program.RnGSeed.Seed2,rng_seed_mode=Program.UseFixedRndSeedVal?"fixed":"entropy_master_hashed_per_particle",execution_seeds=executionSeeds,fresh_particles=count,fresh_active_particles=activeFresh,fresh_active_particles_definition="positive finite mass after upstream time-series modulation, before geometry rejection",residual_particles=residualParticles,chunk,block,elapsed_s=clock.Elapsed.TotalSeconds,init_roundtrip_s=initRoundTrip,cache_hash_s=cacheHashSeconds,omitted_field_names=plan.Omitted,saved_payload_bytes=checked(plan.SavedPayloadBytes+receptorSavedBytes),saved_receptor_payload_bytes=receptorSavedBytes,residual_split_s=residualSplit,residual_roundtrip_s=residualRoundTrip,
                residual_kernel_ms=residualKernel,residual_batches=batches,fresh_roundtrip_s=freshRoundTrip,fresh_kernel_ms=freshKernel,
                result_request_to_header_s=resultHeader,result_transfer_apply_s=resultTransferApply,bytes_sent=BytesSent-sentBefore,bytes_received=BytesReceived-receivedBefore,
                kernel_total_ms=result.RootElement.GetProperty("kernel_total_ms").GetDouble(),phase_counts=result.RootElement.GetProperty("phase_counts").Clone(),
                init_server=initDetails,result_server_metrics=serverMetrics};
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
