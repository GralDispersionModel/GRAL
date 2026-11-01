using System;
using System.Collections.Generic;

namespace GRAL_2001
{
    // Only state consumed by the real allocation component is supplied here.
    class Program
    {
        public static int PS_Count = 2, LS_Count = 2, TS_Count = 2, AS_Count = 2;
        public static int[] PS_PartNumb = new int[3], LS_PartNumb = new int[3], TS_PartNumb = new int[3], AS_PartNumb = new int[3];
        public static int PS_PartSum, LS_PartSum, TS_PartSum, AS_PartSum, NTEILMAX = 1000, IWET = 1, ISTATIONAER = 0, LogLevel = 0;
        public static float TPS = 10, TAUS = 100, GridVolume = 1;
        public static double ParticleMassMean;
        public static double[] PS_ER = { 0, 1, 1 }, LS_ER = { 0, 1, 1 }, TS_ER = { 0, 1, 1 }, AS_ER = { 0, 1, 1 };
        public static float[] PS_ER_Dep = new float[3], LS_ER_Dep = new float[3], TS_ER_Dep = new float[3], AS_ER_Dep = new float[3];
        public static byte[] PS_SG = { 0, 1, 2 }, LS_SG = { 0, 1, 2 }, TS_SG = { 0, 1, 2 }, AS_SG = { 0, 1, 2 };
        public static byte[] PS_Mode = new byte[3], LS_Mode = new byte[3], TS_Mode = new byte[3], AS_Mode = new byte[3];
        public static float[] PS_V_Dep = { 0, 0.25f, 0.5f }, TS_V_Dep = { 0, 0.25f, 0.5f }, LS_V_Dep = { 0, 0.25f, 0.5f }, AS_V_Dep = { 0, 9, 0.5f };
        public static float[] PS_V_sed = { 0, 0.25f, 0.5f }, TS_V_sed = { 0, 0.25f, 0.5f }, LS_V_sed = { 0, 0.25f, 0.5f }, AS_V_sed = { 0, 9, 0.5f };
        public static TransientDeposition[] TransientDepo;
        public static bool EmissionTimeseriesExist = true;
        public static float[,] EmFacTimeSeries = { { 1, 0 }, { 0, 1 }, { 0, 0 }, { 0, 0 } };
        public static List<int> SourceGroups = new List<int> { 1, 2 };
        public static int Get_Internal_SG_Number(int sg) => SourceGroups.IndexOf(sg);
        public static int[] ParticleSource;
        public static byte[] ParticleSG, SourceType, ParticleMode;
        public static double[] Xcoord, YCoord, ParticleMass;
        public static float[] ZCoord, ParticleVsed, ParticleVdep;

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "deposition")
            {
                PS_Mode[1] = PS_Mode[2] = TS_Mode[1] = TS_Mode[2] = LS_Mode[1] = LS_Mode[2] = 1;
                AS_Mode[2] = 2;
                ParticleManagement.Calculate();
                Check(TransientDepo != null, "reference deposition must exist before active filtering");
                Check(TransientDepo[0].Vdep == 0.1875 && TransientDepo[0].Vsed == 0.1875, "deposition-off sources must contribute zero velocity");
                Check(TransientDepo[1].Vdep == 0.5 && TransientDepo[1].Vsed == 0.5, "inactive groups must retain reference deposition; deposition-only sources are excluded");
                Console.WriteLine("PASS reference deposition");
                return;
            }
            if (args.Length > 0 && args[0] == "deposition-source")
            {
                foreach (byte[] modes in new[] { PS_Mode, TS_Mode, LS_Mode, AS_Mode }) modes[1] = modes[2] = 2;
                foreach (double[] rates in new[] { PS_ER, TS_ER, LS_ER, AS_ER }) rates[1] = 0;
                foreach (float[] rates in new[] { PS_ER_Dep, TS_ER_Dep, LS_ER_Dep, AS_ER_Dep }) rates[1] = 1;
                EmFacTimeSeries[0, 1] = 1;
                ParticleManagement.Calculate(true);
                foreach (int[] counts in new[] { PS_PartNumb, TS_PartNumb, LS_PartNumb, AS_PartNumb })
                    Check(counts[1] > 0 && counts[2] == 0, "deposition sources use actual deposition mass for activity");
                Console.WriteLine("PASS deposition source activity");
                return;
            }
            if (args.Length > 0 && args[0] == "capacity")
            {
                PS_Count = 3; LS_Count = TS_Count = AS_Count = 0;
                PS_PartNumb = new int[4]; PS_ER = new double[] { 0, 1, 1, 1 };
                PS_ER_Dep = new float[4]; PS_Mode = new byte[4]; PS_SG = new byte[] { 0, 2, 1, 2 };
                PS_V_Dep = new float[4]; PS_V_sed = new float[4];
                SourceGroups = new List<int> { 2, 1 }; TPS = 10.03f; NTEILMAX = 1003;
                ParticleManagement.Calculate();
                int[] initialArray = ParticleSource;
                Check(initialArray.Length == 1003, "baseline rounds to 1002 particles");
                Check(ParticleManagement.Calculate(true) == 1004, "two active sources should round to 1004 particles");
                Check(!ReferenceEquals(initialArray, ParticleSource) && ParticleSource.Length == 1005, "larger allocation must grow coupled arrays");
                Check(PS_PartNumb[1] == 502 && PS_PartNumb[2] == 0 && PS_PartNumb[3] == 502, "source groups must use their internal order");
                int[] grownArray = ParticleSource; IWET = 3;
                Check(ParticleManagement.Calculate(true) == 0, "all sources should be inactive");
                IWET = 1; ParticleManagement.Calculate(true);
                Check(ReferenceEquals(grownArray, ParticleSource), "reactivation should reuse sufficient capacity");
                Console.WriteLine("PASS capacity and source group mapping");
                return;
            }
            if (args.Length > 0 && args[0] == "budget")
            {
                LS_Count = TS_Count = AS_Count = 0;
                PS_ER[2] = 0.001;
                EmFacTimeSeries[0, 1] = 1;
                NTEILMAX = ParticleManagement.Calculate(true);
                Check(NTEILMAX > 1000, "the first situation should exceed the budget due to the minimum");
                IWET = 2;
                Check(ParticleManagement.Calculate(true) == 1000, "the next situation should use the original budget");
                Console.WriteLine("PASS budget reset");
                return;
            }
            if (args.Length > 0 && args[0] == "off")
            {
                ParticleManagement.Calculate();
                double referenceMass = ParticleMassMean;
                TransientDeposition[] referenceDeposition = TransientDepo;
                IWET = 3;
                Check(ParticleManagement.Calculate(true) == 0, "all-off situation should release no new particles");
                Check(ParticleMassMean == referenceMass && ReferenceEquals(TransientDepo, referenceDeposition), "all-off must preserve residual particle settings");
                Console.WriteLine("PASS all-off situation");
                return;
            }
            EmissionTimeseriesExist = false;
            int baseline = ParticleManagement.Calculate();
            Check(baseline == 1000 && PS_PartNumb[1] == 125 && PS_PartNumb[2] == 125, "all-positive allocation should remain unchanged");
            EmissionTimeseriesExist = true;
            EmFacTimeSeries[0, 1] = 3;
            ParticleManagement.Calculate(true);
            Check(PS_PartNumb[1] == 125 && PS_PartNumb[2] == 125, "positive factors must preserve base weighting");
            EmFacTimeSeries[0, 1] = 0;
            int total = ParticleManagement.Calculate(true);
            Check(total == 1000, "first situation should allocate the configured budget");
            foreach (int[] counts in new[] { PS_PartNumb, TS_PartNumb, LS_PartNumb, AS_PartNumb })
                Check(counts[1] == 250 && counts[2] == 0, "only the active group should receive particles");
            NTEILMAX = total;
            IWET = 2;
            total = ParticleManagement.Calculate(true);
            Check(total == 1000, "next situation should use the configured budget");
            foreach (int[] counts in new[] { PS_PartNumb, TS_PartNumb, LS_PartNumb, AS_PartNumb })
                Check(counts[1] == 0 && counts[2] == 250, "the newly active group should receive particles");
            IWET = 5;
            ParticleManagement.Calculate(true);
            Check(PS_PartNumb[1] == 125 && PS_PartNumb[2] == 125, "outside the time series should use the existing fallback");
            Console.WriteLine("PASS active source allocation");
        }
    }
    static class Consts { public const int LogLevelRefPart = 2, TransientMode = 0, DepoAndConc = 1, DepoOnly = 2; }
    static class ProgramWriters { public static void LogfileGralCoreWrite(string text) { } }
}
