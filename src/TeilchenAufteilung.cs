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
        /// Calculate the Sum of all Emission Rates 
        /// Assign the particles to the sources
        /// </summary>
        public static int Calculate(bool activeOnly = false)
        {
            //total emissions (all sources)
            Array.Clear(Program.PS_PartNumb); Program.PS_PartSum = 0;
            Array.Clear(Program.LS_PartNumb); Program.LS_PartSum = 0;
            Array.Clear(Program.AS_PartNumb); Program.AS_PartSum = 0;
            Array.Clear(Program.TS_PartNumb); Program.TS_PartSum = 0;

            double sum_emission = 0.0; // Sum for all sources
            int Sum_of_Particles = 0; // sum for alternative approach

            // Calculate sum of all emissions
            for (int i = 1; i <= Program.PS_Count; i++) // Point sources
            {
                if (IsActive(Program.PS_ER[i], Program.PS_ER_Dep[i], Program.PS_Mode[i], Program.PS_SG[i], activeOnly))
                    sum_emission += Program.PS_ER[i];
            }

            for (int i = 1; i <= Program.LS_Count; i++) // Line sources
            {
                if (IsActive(Program.LS_ER[i], Program.LS_ER_Dep[i], Program.LS_Mode[i], Program.LS_SG[i], activeOnly))
                    sum_emission += Program.LS_ER[i];
            }

            for (int i = 1; i <= Program.TS_Count; i++) // Portals
            {
                if (IsActive(Program.TS_ER[i], Program.TS_ER_Dep[i], Program.TS_Mode[i], Program.TS_SG[i], activeOnly))
                    sum_emission += Program.TS_ER[i];
            }

            for (int i = 1; i <= Program.AS_Count; i++) // Area sources
            {
                if (IsActive(Program.AS_ER[i], Program.AS_ER_Dep[i], Program.AS_Mode[i], Program.AS_SG[i], activeOnly))
                    sum_emission += Program.AS_ER[i];
            }

            int PS_Min_Particles = Set_Min_Particles_PS_TS(Program.PS_Count);
            int LS_Min_Particles = Set_Min_Particles_LS(Program.LS_Count);
            int TS_Min_Particles = Set_Min_Particles_PS_TS(Program.TS_Count);
            int AS_Min_Particles = Set_Min_Particles_AS(Program.AS_Count);

            //assignment of particles for each source
            double unit = sum_emission > 0 ? (float)(activeOnly ? (int)(Program.TAUS * Program.TPS) : Program.NTEILMAX) / sum_emission : 0;

            int sum = 0;
            for (int i = 1; i <= Program.PS_Count; i++)
            {
                if (!IsActive(Program.PS_ER[i], Program.PS_ER_Dep[i], Program.PS_Mode[i], Program.PS_SG[i], activeOnly)) continue;
                Program.PS_PartNumb[i] = Convert.ToInt32(unit * Program.PS_ER[i]);

                if (Program.PS_PartNumb[i] < PS_Min_Particles)
                {
                    Program.PS_PartNumb[i] = PS_Min_Particles;
                }

                Program.PS_PartSum += Program.PS_PartNumb[i];
                sum += Program.PS_PartNumb[i];
                Sum_of_Particles += Program.PS_PartNumb[i];

                if (Program.LogLevel == Consts.LogLevelRefPart) // Show all particle-numbers in the case of Log-Level 02
                {
                    Console.WriteLine("PS " + i.ToString() + " : " + Math.Abs(Program.PS_PartNumb[i]).ToString());
                }
            }

            for (int i = 1; i <= Program.LS_Count; i++)
            {
                if (!IsActive(Program.LS_ER[i], Program.LS_ER_Dep[i], Program.LS_Mode[i], Program.LS_SG[i], activeOnly)) continue;
                Program.LS_PartNumb[i] = Convert.ToInt32(unit * Program.LS_ER[i]);

                if (Program.LS_PartNumb[i] < LS_Min_Particles) // avoid Null particle sources
                {
                    Program.LS_PartNumb[i] = LS_Min_Particles;
                }

                Program.LS_PartSum += Program.LS_PartNumb[i];
                sum += Program.LS_PartNumb[i];
                Sum_of_Particles += Program.LS_PartNumb[i];

                if (Program.LogLevel == Consts.LogLevelRefPart) // Show all particle-numbers in the case of Log-Level 02
                {
                    Console.WriteLine("LS " + i.ToString() + " : " + Math.Abs(Program.LS_PartNumb[i]).ToString());
                }
            }

            for (int i = 1; i <= Program.TS_Count; i++)
            {
                if (!IsActive(Program.TS_ER[i], Program.TS_ER_Dep[i], Program.TS_Mode[i], Program.TS_SG[i], activeOnly)) continue;
                Program.TS_PartNumb[i] = Convert.ToInt32(unit * Program.TS_ER[i]);

                if (Program.TS_PartNumb[i] < TS_Min_Particles)
                {
                    Program.TS_PartNumb[i] = TS_Min_Particles;
                }

                Program.TS_PartSum += Program.TS_PartNumb[i];
                sum += Program.TS_PartNumb[i];
                Sum_of_Particles += Program.TS_PartNumb[i];

                if (Program.LogLevel == Consts.LogLevelRefPart) // Show all particle-numbers in the case of Log-Level 02
                {
                    Console.WriteLine("TS " + i.ToString() + " : " + Math.Abs(Program.TS_PartNumb[i]).ToString());
                }
            }

            for (int i = 1; i <= Program.AS_Count; i++)
            {
                if (!IsActive(Program.AS_ER[i], Program.AS_ER_Dep[i], Program.AS_Mode[i], Program.AS_SG[i], activeOnly)) continue;
                Program.AS_PartNumb[i] = Convert.ToInt32(unit * Program.AS_ER[i]);

                if (Program.AS_PartNumb[i] < AS_Min_Particles) // avoid Null particle sources
                {
                    Program.AS_PartNumb[i] = AS_Min_Particles;
                }

                Program.AS_PartSum += Program.AS_PartNumb[i];
                sum += Program.AS_PartNumb[i];
                Sum_of_Particles += Program.AS_PartNumb[i];

                if (Program.LogLevel == Consts.LogLevelRefPart) // Show all particle-numbers in the case of Log-Level 02
                {
                    Console.WriteLine("AS " + i.ToString() + " : " + Math.Abs(Program.AS_PartNumb[i]).ToString());
                }
            }

            if (!activeOnly)
                Program.ParticleMassMean = sum_emission / 3600 / Program.TPS * 1000000000 / Program.GridVolume / Program.TAUS;

            if (!activeOnly && Program.ISTATIONAER == Consts.TransientMode && Program.TransientDepo == null)
            {
                Program.TransientDepo = new TransientDeposition[Program.SourceGroups.Count];
                int[] counts = new int[Program.SourceGroups.Count];
                for (int i = 0; i < Program.TransientDepo.Length; i++)
                    Program.TransientDepo[i] = new TransientDeposition();
                AddDeposition(Program.PS_Count, Program.PS_PartNumb, Program.PS_SG, Program.PS_Mode, Program.PS_V_Dep, Program.PS_V_sed, counts);
                AddDeposition(Program.TS_Count, Program.TS_PartNumb, Program.TS_SG, Program.TS_Mode, Program.TS_V_Dep, Program.TS_V_sed, counts);
                AddDeposition(Program.LS_Count, Program.LS_PartNumb, Program.LS_SG, Program.LS_Mode, Program.LS_V_Dep, Program.LS_V_sed, counts);
                AddDeposition(Program.AS_Count, Program.AS_PartNumb, Program.AS_SG, Program.AS_Mode, Program.AS_V_Dep, Program.AS_V_sed, counts);
                for (int i = 0; i < counts.Length; i++)
                {
                    if (counts[i] == 0) continue;
                    Program.TransientDepo[i].Vdep /= counts[i];
                    Program.TransientDepo[i].Vsed /= counts[i];
                }
            }

            string err = "Using a total of " + Sum_of_Particles.ToString() + " particles (" + Math.Round((Sum_of_Particles / Program.TAUS), 0).ToString() + " part./s) within the model domain";
            Console.WriteLine(err);
            ProgramWriters.LogfileGralCoreWrite(err);
            ProgramWriters.LogfileGralCoreWrite("");

            // Resize Arrays 
            if (Program.ParticleSource == null || Program.ParticleSource.Length < Sum_of_Particles + 1)
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

        private static void AddDeposition(int sourceCount, int[] particles, byte[] groups, byte[] modes, float[] vdep, float[] vsed, int[] counts)
        {
            for (int i = 1; i <= sourceCount; i++)
            {
                if (modes[i] >= Consts.DepoOnly || particles[i] == 0) continue;
                int group = Program.Get_Internal_SG_Number(groups[i]);
                TransientDeposition deposition = Program.TransientDepo[group];
                deposition.DepositionMode = Math.Max(deposition.DepositionMode, modes[i]);
                counts[group] += particles[i];
                if (modes[i] == Consts.DepoAndConc)
                {
                    deposition.Vdep += (double)vdep[i] * particles[i];
                    deposition.Vsed += (double)vsed[i] * particles[i];
                }
            }
        }

        private static bool IsActive(double emission, float depositionEmission, byte mode, byte sourceGroup, bool activeOnly)
        {
            if (!activeOnly) return true;
            if ((mode == Consts.DepoOnly ? depositionEmission : emission) <= 0) return false;
            int group = Program.Get_Internal_SG_Number(sourceGroup);
            return !(Program.ISTATIONAER == Consts.TransientMode && Program.EmissionTimeseriesExist &&
                     Program.IWET <= Program.EmFacTimeSeries.GetUpperBound(0) && group >= 0 &&
                     group <= Program.EmFacTimeSeries.GetUpperBound(1)) ||
                   Program.EmFacTimeSeries[Program.IWET - 1, group] > 0;
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
