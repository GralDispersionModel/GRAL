#region Copyright
///<remarks>
/// <Graz Lagrangian Particle Dispersion Model>
/// Copyright (C) [2019]  [Dietmar Oettl, Markus Kuntner]
/// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
/// the Free Software Foundation version 3 of the License
/// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
/// You should have received a copy of the GNU General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.
///</remarks>
#endregion

using System;

namespace GRAL_2001
{
    class ParticleManagement
    {
        /// <summary>
        /// Calculate the source particle allocation for the current weather situation
        /// Assign new particles only to sources with nonzero current emissions
        /// </summary>
        public static int Calculate()
        {
            //total emissions (all sources)
            Array.Clear(Program.PS_PartNumb); Program.PS_PartSum = 0;
            Array.Clear(Program.LS_PartNumb); Program.LS_PartSum = 0;
            Array.Clear(Program.AS_PartNumb); Program.AS_PartSum = 0;
            Array.Clear(Program.TS_PartNumb); Program.TS_PartSum = 0;

            double sum_emission = 0.0;
            double reference_emission = 0.0;
            int Sum_of_Particles = 0;
            // NTEILMAX is the previous allocation, including per-source minima.
            // Always distribute the configured budget, not that previous result.
            int particleBudget = (int)(Program.TAUS * Program.TPS);
            int PS_active = 0, PS_reference = 0;
            for (int i = 1; i <= Program.PS_Count; i++)
            {
                if (HasEmission(Program.PS_ER[i], Program.PS_ER_Dep[i], Program.PS_Mode[i]))
                {
                    reference_emission += Program.PS_ER[i];
                    PS_reference++;
                    if (SourceGroupIsActive(Program.PS_SG[i]))
                    {
                        sum_emission += Program.PS_ER[i];
                        PS_active++;
                    }
                }
            }

            int LS_active = 0, LS_reference = 0;
            for (int i = 1; i <= Program.LS_Count; i++)
            {
                if (HasEmission(Program.LS_ER[i], Program.LS_ER_Dep[i], Program.LS_Mode[i]))
                {
                    reference_emission += Program.LS_ER[i];
                    LS_reference++;
                    if (SourceGroupIsActive(Program.LS_SG[i]))
                    {
                        sum_emission += Program.LS_ER[i];
                        LS_active++;
                    }
                }
            }

            int TS_active = 0, TS_reference = 0;
            for (int i = 1; i <= Program.TS_Count; i++)
            {
                if (HasEmission(Program.TS_ER[i], Program.TS_ER_Dep[i], Program.TS_Mode[i]))
                {
                    reference_emission += Program.TS_ER[i];
                    TS_reference++;
                    if (SourceGroupIsActive(Program.TS_SG[i]))
                    {
                        sum_emission += Program.TS_ER[i];
                        TS_active++;
                    }
                }
            }

            int AS_active = 0, AS_reference = 0;
            for (int i = 1; i <= Program.AS_Count; i++)
            {
                if (HasEmission(Program.AS_ER[i], Program.AS_ER_Dep[i], Program.AS_Mode[i]))
                {
                    reference_emission += Program.AS_ER[i];
                    AS_reference++;
                    if (SourceGroupIsActive(Program.AS_SG[i]))
                    {
                        sum_emission += Program.AS_ER[i];
                        AS_active++;
                    }
                }
            }

            int PS_Min_Particles = Set_Min_Particles_PS_TS(PS_active);
            int LS_Min_Particles = Set_Min_Particles_LS(LS_active);
            int TS_Min_Particles = Set_Min_Particles_PS_TS(TS_active);
            int AS_Min_Particles = Set_Min_Particles_AS(AS_active);

            // Positive temporal factors still scale particle mass in Zeitschleife.
            // Retain the original sampling weights among the active sources.
            double unit = sum_emission > 0 ? (float)particleBudget / sum_emission : 0;

            for (int i = 1; i <= Program.PS_Count; i++)
            {
                if (HasEmission(Program.PS_ER[i], Program.PS_ER_Dep[i], Program.PS_Mode[i]) &&
                    SourceGroupIsActive(Program.PS_SG[i]))
                {
                    Program.PS_PartNumb[i] = Math.Max(PS_Min_Particles,
                        Convert.ToInt32(unit * Program.PS_ER[i]));
                }

                Program.PS_PartSum += Program.PS_PartNumb[i];
                Sum_of_Particles += Program.PS_PartNumb[i];

                if (Program.LogLevel == Consts.LogLevelRefPart) // Show all particle-numbers in the case of Log-Level 02
                {
                    Console.WriteLine("PS " + i.ToString() + " : " + Math.Abs(Program.PS_PartNumb[i]).ToString());
                }
            }

            for (int i = 1; i <= Program.LS_Count; i++)
            {
                if (HasEmission(Program.LS_ER[i], Program.LS_ER_Dep[i], Program.LS_Mode[i]) &&
                    SourceGroupIsActive(Program.LS_SG[i]))
                {
                    Program.LS_PartNumb[i] = Math.Max(LS_Min_Particles,
                        Convert.ToInt32(unit * Program.LS_ER[i]));
                }

                Program.LS_PartSum += Program.LS_PartNumb[i];
                Sum_of_Particles += Program.LS_PartNumb[i];

                if (Program.LogLevel == Consts.LogLevelRefPart) // Show all particle-numbers in the case of Log-Level 02
                {
                    Console.WriteLine("LS " + i.ToString() + " : " + Math.Abs(Program.LS_PartNumb[i]).ToString());
                }
            }

            for (int i = 1; i <= Program.TS_Count; i++)
            {
                if (HasEmission(Program.TS_ER[i], Program.TS_ER_Dep[i], Program.TS_Mode[i]) &&
                    SourceGroupIsActive(Program.TS_SG[i]))
                {
                    Program.TS_PartNumb[i] = Math.Max(TS_Min_Particles,
                        Convert.ToInt32(unit * Program.TS_ER[i]));
                }

                Program.TS_PartSum += Program.TS_PartNumb[i];
                Sum_of_Particles += Program.TS_PartNumb[i];

                if (Program.LogLevel == Consts.LogLevelRefPart) // Show all particle-numbers in the case of Log-Level 02
                {
                    Console.WriteLine("TS " + i.ToString() + " : " + Math.Abs(Program.TS_PartNumb[i]).ToString());
                }
            }

            for (int i = 1; i <= Program.AS_Count; i++)
            {
                if (HasEmission(Program.AS_ER[i], Program.AS_ER_Dep[i], Program.AS_Mode[i]) &&
                    SourceGroupIsActive(Program.AS_SG[i]))
                {
                    Program.AS_PartNumb[i] = Math.Max(AS_Min_Particles,
                        Convert.ToInt32(unit * Program.AS_ER[i]));
                }

                Program.AS_PartSum += Program.AS_PartNumb[i];
                Sum_of_Particles += Program.AS_PartNumb[i];

                if (Program.LogLevel == Consts.LogLevelRefPart) // Show all particle-numbers in the case of Log-Level 02
                {
                    Console.WriteLine("AS " + i.ToString() + " : " + Math.Abs(Program.AS_PartNumb[i]).ToString());
                }
            }

            // Keep a time-independent reference for splitting the stored plume.
            // An all-off hour must not erase it or divide by zero on carryover.
            Program.ParticleMassMean = reference_emission / 3600 / Program.TPS * 1000000000 / Program.GridVolume / Program.TAUS;

            if (Program.ISTATIONAER == Consts.TransientMode && Program.TransientDepo == null)
            {
                InitializeTransientDeposition(reference_emission > 0 ? (float)particleBudget / reference_emission : 0,
                    PS_reference, LS_reference, TS_reference, AS_reference);
            }

            string err = "Using a total of " + Sum_of_Particles.ToString() + " particles (" + Math.Round((Sum_of_Particles / Program.TAUS), 0).ToString() + " part./s) within the model domain";
            Console.WriteLine(err);
            ProgramWriters.LogfileGralCoreWrite(err);
            ProgramWriters.LogfileGralCoreWrite("");

            // Reuse storage when the current allocation has the same size.
            if (Program.ParticleSource.Length != Sum_of_Particles + 1)
            {
                Program.ParticleSource = GC.AllocateUninitializedArray<Int32>(Sum_of_Particles + 1);
                Program.ParticleSG = GC.AllocateUninitializedArray<byte>(Sum_of_Particles + 1);
                Program.Xcoord = GC.AllocateUninitializedArray<double>(Sum_of_Particles + 1);
                Program.YCoord = GC.AllocateUninitializedArray<double>(Sum_of_Particles + 1);
                Program.ZCoord = GC.AllocateUninitializedArray<float>(Sum_of_Particles + 1);
                Program.ParticleMass = GC.AllocateUninitializedArray<double>(Sum_of_Particles + 1);
                Program.SourceType = GC.AllocateUninitializedArray<byte>(Sum_of_Particles + 1);
                Program.ParticleVsed = GC.AllocateUninitializedArray<float>(Sum_of_Particles + 1);         // sedimentation velocity of one lagrangian particle
                Program.ParticleVdep = GC.AllocateUninitializedArray<float>(Sum_of_Particles + 1);         // deposition velocity of one lagrangian particle
                Program.ParticleMode = GC.AllocateUninitializedArray<byte>(Sum_of_Particles + 1);
            }

            Console.WriteLine();

            return Sum_of_Particles;
        }

        private static bool HasEmission(double rate, float depositionRate, byte mode)
        {
            return rate > 0 && (mode != Consts.DepoOnly || depositionRate > 0);
        }

        private static bool SourceGroupIsActive(int sourceGroup)
        {
            if (Program.ISTATIONAER == Consts.TransientMode && Program.EmissionTimeseriesExist &&
                Program.IWET > 0 && Program.IWET <= Program.EmFacTimeSeries.GetUpperBound(0))
            {
                int group = Program.Get_Internal_SG_Number(sourceGroup);
                if (group >= 0 && group <= Program.EmFacTimeSeries.GetUpperBound(1))
                {
                    return Program.EmFacTimeSeries[Program.IWET - 1, group] > 0;
                }
            }
            return true;
        }

        // Initialize all groups from their unmodulated source allocation, including
        // groups that first emit later or have a plume loaded from a checkpoint.
        // This is the former particle-weighted average without creating inactive
        // particles just to recover the source deposition parameters.
        private static void InitializeTransientDeposition(double unit, int pointCount, int lineCount,
            int portalCount, int areaCount)
        {
            int count = Program.SourceGroups.Count;
            Program.TransientDepo = new TransientDeposition[count];
            double[] weights = new double[count];
            for (int i = 0; i < count; i++)
            {
                Program.TransientDepo[i] = new TransientDeposition();
            }
            for (int i = 1; i <= Program.PS_Count; i++)
            {
                AddTransientDeposition(Program.PS_SG[i], Program.PS_ER[i], Program.PS_Mode[i],
                    Program.PS_V_Dep[i], Program.PS_V_sed[i], unit, Set_Min_Particles_PS_TS(pointCount), weights);
            }
            for (int i = 1; i <= Program.TS_Count; i++)
            {
                AddTransientDeposition(Program.TS_SG[i], Program.TS_ER[i], Program.TS_Mode[i],
                    Program.TS_V_Dep[i], Program.TS_V_sed[i], unit, Set_Min_Particles_PS_TS(portalCount), weights);
            }
            for (int i = 1; i <= Program.LS_Count; i++)
            {
                AddTransientDeposition(Program.LS_SG[i], Program.LS_ER[i], Program.LS_Mode[i],
                    Program.LS_V_Dep[i], Program.LS_V_sed[i], unit, Set_Min_Particles_LS(lineCount), weights);
            }
            for (int i = 1; i <= Program.AS_Count; i++)
            {
                AddTransientDeposition(Program.AS_SG[i], Program.AS_ER[i], Program.AS_Mode[i],
                    Program.AS_V_Dep[i], Program.AS_V_sed[i], unit, Set_Min_Particles_AS(areaCount), weights);
            }
            for (int i = 0; i < count; i++)
            {
                if (weights[i] > 0)
                {
                    Program.TransientDepo[i].Vdep /= weights[i];
                    Program.TransientDepo[i].Vsed /= weights[i];
                }
            }
        }

        private static void AddTransientDeposition(int group, double rate, byte mode, float vdep,
            float vsed, double unit, int minimum, double[] weights)
        {
            if (rate <= 0 || mode >= Consts.DepoOnly)
            {
                return;
            }
            int index = Program.Get_Internal_SG_Number(group);
            int particles = Math.Max(minimum, Convert.ToInt32(unit * rate));
            Program.TransientDepo[index].DepositionMode = Math.Max(Program.TransientDepo[index].DepositionMode, mode);
            Program.TransientDepo[index].Vdep += (double)vdep * particles;
            Program.TransientDepo[index].Vsed += (double)vsed * particles;
            weights[index] += particles;
        }

        private static int Set_Min_Particles_PS_TS(int source_count)
        {
            int Min_Particles = 10;
            if (source_count < 2000)
            {
                Min_Particles = 20;
            }
            else if (source_count > 30000)
            {
                Min_Particles = 5;
            }

            return Min_Particles;
        }

        private static int Set_Min_Particles_LS(int source_count)
        {
            int Min_Particles = 5;
            if (source_count < 2000)
            {
                Min_Particles = 16;
            }
            else if (source_count < 10000)
            {
                Min_Particles = 8;
            }
            return Min_Particles;
        }

        private static int Set_Min_Particles_AS(int source_count)
        {
            int Min_Particles = 2;
            if (source_count < 2000)
            {
                Min_Particles = 6;
            }
            else if (source_count < 10000)
            {
                Min_Particles = 4;
            }

            return Min_Particles;
        }

    }
}
