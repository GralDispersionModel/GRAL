using System;
using System.Collections.Generic;
namespace GRAL_2001
{
    internal static class GpuResidualBatch
    {
        internal static IEnumerable<Dictionary<string,object>> Enumerate(int capacity=65536)
        {
            if(capacity<1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if(!Program.UseFixedRndSeedVal || Program.SourceType==null || Program.SourceType.Length==0)
                throw new InvalidOperationException("Unsupported residual configuration");
            var vi=new List<int>(capacity);var vj=new List<int>(capacity);var vk=new List<int>(capacity);var vg=new List<int>(capacity);var vs=new List<int>(capacity);var vc=new List<float>(capacity);
            for(int index=0;index<Program.NII*Program.NJJ;index++)
            {
                int i=1+index%Program.NII,j=1+index/Program.NII;
                for(int k=Program.NKK_Transient;k>0;k--)
                for(int sg=0;sg<Program.SourceGroups.Count;sg++)
                {
                    float conc=Program.Conz4d[i][j][k][sg];
                    if(!(conc>=Program.TransConcThreshold)) continue;
                    double volume=Program.DXK*Program.DYK*Program.DZK_Trans[k]/Program.GridVolume;
                    double mass=conc*volume/Program.TAUS;
                    // Preserve the bounded double-floor rule in V2701 Zeitschleife_nonsteadystate.Calculate.
                    int splits=(int)Math.Min(100000,Math.Floor(Math.Floor(mass/Program.ParticleMassMean)/10)+1);
                    float each=splits<=1?conc:conc/splits;
                    for(int split=0;split<Math.Max(1,splits);split++)
                    {
                        vi.Add(i);vj.Add(j);vk.Add(k);vg.Add(sg);vc.Add(each);vs.Add(split);
                        if(vi.Count==capacity)
                        {
                            yield return Make(vi,vj,vk,vg,vc,vs);
                            vi.Clear();vj.Clear();vk.Clear();vg.Clear();vc.Clear();vs.Clear();
                        }
                    }
                }
            }
            if(vi.Count>0) yield return Make(vi,vj,vk,vg,vc,vs);
        }
        private static Dictionary<string,object> Make(List<int> i,List<int> j,List<int> k,List<int> sg,List<float> conc,List<int> split)
            => new Dictionary<string,object>{{"_resI",i.ToArray()},{"_resJ",j.ToArray()},{"_resK",k.ToArray()},{"_resSG",sg.ToArray()},{"_resConc",conc.ToArray()},{"_resSplit",split.ToArray()}};
    }
}

