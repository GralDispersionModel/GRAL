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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRAL_2001
{
    partial class Program
    {
        // Set GRAL_WORKER_CONCENTRATION_MB to 1..256 to enable worker buffers.
        private static readonly long WorkerConcentrationBudget = ReadWorkerConcentrationBudget();
        private static double[][] WorkerConcentrations;
        internal static int[] WorkerConcentrationSlots;
        private static int[] WorkerConcentrationGroups;
        internal static int WorkerConcentrationNy, WorkerConcentrationNs, WorkerConcentrationSlotCount;

        private static long ReadWorkerConcentrationBudget()
        {
            return int.TryParse(Environment.GetEnvironmentVariable("GRAL_WORKER_CONCENTRATION_MB"), out int mb) && mb > 0
                ? Math.Min(mb, 256) * 1024L * 1024 : 0;
        }

        private static bool PrepareWorkerConcentrations()
        {
            if (WorkerConcentrationBudget == 0)
            {
                return false;
            }
            int workers = pOptions.MaxDegreeOfParallelism;
            if (!UseFixedRndSeedVal || workers <= 1)
            {
                ReportWorkerConcentrations("shared (driver)", workers, 0, 0);
                return false;
            }
            bool[] active = new bool[byte.MaxValue + 1];
            void Mark(int[] particles, byte[] groups, int count)
            {
                for (int i = 1; i <= count; i++)
                {
                    if (particles[i] > 0)
                    {
                        active[groups[i]] = true;
                    }
                }
            }
            Mark(PS_PartNumb, PS_SG, PS_Count);
            Mark(LS_PartNumb, LS_SG, LS_Count);
            Mark(TS_PartNumb, TS_SG, TS_Count);
            Mark(AS_PartNumb, AS_SG, AS_Count);
            int[] slots = new int[SourceGroups.Count];
            Array.Fill(slots, -1);
            var groups = new List<int>();
            for (int q = 0; q < SourceGroups.Count; q++)
            {
                if ((uint)SourceGroups[q] < active.Length && active[SourceGroups[q]])
                {
                    slots[q] = groups.Count;
                    groups.Add(q);
                }
            }
            int ny = Conz3dDouble[0].Length, ns = Conz3dDouble[0][0].Length;
            long length = (long)Conz3dDouble.Length * ny * ns * groups.Count;
            long bytes = length * sizeof(double) * workers;
            if (groups.Count == 0 || bytes > WorkerConcentrationBudget)
            {
                ReportWorkerConcentrations("shared (budget or no groups)", workers, groups.Count, bytes);
                return false;
            }
            // Reuse the first allocation. Falling back avoids overlapping old and new buffers.
            if (WorkerConcentrations != null &&
                (WorkerConcentrations.Length < workers || WorkerConcentrations[0].LongLength < length))
            {
                ReportWorkerConcentrations("shared (capacity)", workers, groups.Count, bytes);
                return false;
            }
            if (WorkerConcentrations == null)
            {
                WorkerConcentrations = CreateArray<double[]>(workers, () => new double[length]);
            }
            WorkerConcentrationSlots = slots;
            WorkerConcentrationGroups = groups.ToArray();
            WorkerConcentrationNy = ny;
            WorkerConcentrationNs = ns;
            WorkerConcentrationSlotCount = groups.Count;
            ReportWorkerConcentrations("enabled", workers, groups.Count, bytes);
            return true;
        }

        private static void MergeWorkerConcentrations()
        {
            int workers = pOptions.MaxDegreeOfParallelism;
            int ny = WorkerConcentrationNy, ns = WorkerConcentrationNs, count = WorkerConcentrationSlotCount;
            Parallel.For(0, Conz3dDouble.Length, pOptions, i =>
            {
                for (int j = 0; j < ny; j++)
                {
                    for (int slice = 0; slice < ns; slice++)
                    {
                        double[] target = Conz3dDouble[i][j][slice];
                        int start = ((i * ny + j) * ns + slice) * count;
                        for (int slot = 0; slot < count; slot++)
                        {
                            int q = WorkerConcentrationGroups[slot];
                            for (int worker = 0; worker < workers; worker++)
                            {
                                double value = WorkerConcentrations[worker][start + slot];
                                if (value != 0)
                                {
                                    target[q] += value;
                                    WorkerConcentrations[worker][start + slot] = 0;
                                }
                            }
                        }
                    }
                }
            });
        }

        private static void ReportWorkerConcentrations(string status, int workers, int groups, long required)
        {
            long reserved = WorkerConcentrations == null ? 0 :
                WorkerConcentrations.LongLength * WorkerConcentrations[0].LongLength * sizeof(double);
            string message = $"Worker concentration buffers: {status}; workers={workers}; groups={groups}; required={required}; reserved={reserved}; limit={WorkerConcentrationBudget} bytes";
            Console.WriteLine(message);
            ProgramWriters.LogfileGralCoreWrite(message);
        }
    }
}
