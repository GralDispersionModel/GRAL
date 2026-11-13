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
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace GRAL_2001
{
    class DiagnosticFlowfield
    {
        /// <summary>
        ///Solving the pressure equation using the TDMA for diagnostic wind fields
        /// </summary>
        public static void Calculate()
        {
            float DXK = Program.DXK;
            float DYK = Program.DYK;

            int IterationLoops = 1;
            float DTIME = (float)(0.5 * Math.Min(DXK, DYK) / (Math.Sqrt(Program.Pow2(Program.UK[1][1][Program.KADVMAX]) + (Program.Pow2(Program.VK[1][1][Program.KADVMAX])))));
            DTIME = Math.Min(DTIME, 5);
            int MAXHOEH = Program.VertCellsFF;
            int Iterations = 5;
            if (Program.FlowFieldLevel == Consts.FlowFieldProg)
            {
                Iterations = 10;
            }

            //set non-hydrostatic pressure equal to zero
            Parallel.For(1, Program.NII + 1, Program.pOptions, i =>
            {
                for (int j = 1; j <= Program.NJJ; j++)
                {
                    for (int k = 1; k <= Program.NKK + 1; k++)
                    {
                        Program.DPM[i][j][k] = 0;
                    }
                }
            });

            //main loop
            while (IterationLoops <= Iterations)
            {
                //mass divergence in each cell
                Parallel.For(2, Program.NII, Program.pOptions, i =>
                {
                    for (int j = 2; j < Program.NJJ; j++)
                    {
                        float[] DIV_L = Program.DIV[i][j];
                        float[] UK_L = Program.UK[i][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] WK_L = Program.WK[i][j];
                        float[] UKip_L = Program.UK[i + 1][j];
                        float[] VKjp_L = Program.VK[i][j + 1];
                        int KKART = Program.KKART[i][j];

                        for (int k = 1; k <= Program.NKK - 1; k++)
                        {
                            if (KKART < k)
                            {
                                if (k > KKART + 1)
                                {
                                    DIV_L[k] = (UK_L[k] - UKip_L[k]) * DYK * Program.DZK[k] + (VK_L[k] - VKjp_L[k]) * DXK * Program.DZK[k] + (WK_L[k] - WK_L[k + 1]) * DXK * DYK;
                                }
                                else
                                {
                                    DIV_L[k] = (UK_L[k] - UKip_L[k]) * DYK * Program.DZK[k] + (VK_L[k] - VKjp_L[k]) * DXK * Program.DZK[k] - WK_L[k + 1] * DXK * DYK;
                                }
                            }
                        }
                    }
                });

                /*PROCEDURE CALCULATING THE PRESSURE CORRECTION
                EQUATION TDMA (PATANKAR 1980, S125)*/

                //switch the loop to improve convergence
                int maxTasks = 1;
                int cores = 1;
                if (Program.UseFixedRndSeedVal)
                {
                    cores = Program.pOptions.MaxDegreeOfParallelism;
                    Program.pOptions.MaxDegreeOfParallelism = 1;
                }
                //sweep direction: i ascending in the loops 0 and 1, j ascending in the loops 0 and 3
                int IS = (IterationLoops % 4 == 0 || IterationLoops % 4 == 1) ? 1 : -1;
                int JS = (IterationLoops % 4 == 0 || IterationLoops % 4 == 3) ? 1 : -1;
                //columns i1First..i1Last x j1First..j1Last of the sweep
                OrderedSweep.Tile tile = (i1First, i1Last, j1First, j1Last) =>
                {
                    Span<float> PIMP = stackalloc float[Program.NKK + 2];
                    Span<float> QIMP = stackalloc float[Program.NKK + 2];
                    Span<float> APP = stackalloc float[Program.NKK + 1];
                    Span<float> ABP = stackalloc float[Program.NKK + 1];
                    Span<float> ATP = stackalloc float[Program.NKK + 1];

                    for (int j1 = j1First; j1 <= j1Last; j1++)
                    {
                        int j = j1;
                        if (JS == -1)
                        {
                            j = Program.NJJ - j1 + 1;
                        }

                        for (int i1 = i1First; i1 <= i1Last; i1++)
                        {
                            int i = i1;
                            if (IS == -1)
                            {
                                i = Program.NII - i1 + 1;
                            }

                            float[] DIV_L = Program.DIV[i][j];
                            float[] DPM_L = Program.DPM[i][j];
                            float[] DPMim_L = Program.DPM[i - 1][j];
                            float[] DPMip_L = Program.DPM[i + 1][j];
                            float[] DPMjm_L = Program.DPM[i][j - 1];
                            float[] DPMjp_L = Program.DPM[i][j + 1];
                            int KKART = Program.KKART[i][j];

                            for (int k = Program.NKK - 1; k >= 1; k--)
                            {
                                if (KKART < k)
                                {
                                    float DIM = DIV_L[k] / DTIME + (DPMim_L[k] + DPMip_L[k]) / DXK * DYK * Program.DZK[k] +
                                        (DPMjm_L[k] - DPMjp_L[k]) / DYK * DXK * Program.DZK[k];
                                    APP[k] = 2 * (DYK * Program.DZK[k] / DXK + DXK * Program.DZK[k] / DYK + DXK * DYK / Program.DZK[k]);

                                    if (k > KKART + 1)
                                    {
                                        ABP[k] = DXK * DYK / (0.5F * (Program.DZK[k - 1] + Program.DZK[k]));
                                    }
                                    else
                                    {
                                        ABP[k] = DXK * DYK / Program.DZK[k];
                                    }

                                    ATP[k] = (DXK * DYK) / (0.5F * (Program.DZK[k + 1] + Program.DZK[k]));
                                    float TERMP = 1 / (APP[k] - ATP[k] * PIMP[k + 1]);
                                    PIMP[k] = ABP[k] * TERMP;
                                    QIMP[k] = (ATP[k] * QIMP[k + 1] + DIM) * TERMP;
                                }
                            }

                            //Obtain new P-components
                            DPM_L[0] = 0;
                            for (int k = 1; k <= Program.NKK - 1; k++)
                            {
                                if (KKART < k)
                                {
                                    DPM_L[k] = PIMP[k] * DPM_L[k - 1] + QIMP[k];
                                }
                                else
                                {
                                    DPM_L[k] = 0;
                                }
                            }
                        }
                    }
                };
                if (OrderedSweep.Enabled)
                {
                    //reproducible results on several threads: all columns are solved in the order of the sweep on one thread
                    OrderedSweep.Prepare(null);
                    OrderedSweep.Run(2, Program.NII - 1, 2, Program.NJJ - 1, IS, JS, tile);
                }
                else
                {
                    maxTasks = Math.Max(1, Program.IPROC);
                    Parallel.ForEach(Partitioner.Create(2, Program.NJJ, Math.Max(4, (int)(Program.NJJ / maxTasks))), Program.pOptions, range =>
                    //Parallel.For(2, Program.NJJ, Program.pOptions, j1 =>
                    {
                        tile(2, Program.NII - 1, range.Item1, range.Item2 - 1);
                    });
                }
                if (Program.UseFixedRndSeedVal)
                {
                    Program.pOptions.MaxDegreeOfParallelism = cores;
                }

                //correcting velocities
                maxTasks = Math.Max(1, Program.IPROC);
                Parallel.ForEach(Partitioner.Create(2, Program.NII, Math.Max(4, (int)(Program.NII / maxTasks))), Program.pOptions, range =>
                //Parallel.For(2, Program.NII, Program.pOptions, i =>
                {
                    float DDPX = 0;
                    float DDPY = 0;
                    float DDPZ = 0;
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        for (int j = 2; j < Program.NJJ; j++)
                        {
                            float[] DPM_L = Program.DPM[i][j];
                            float[] DPMim_L = Program.DPM[i - 1][j];
                            float[] DPMjm_L = Program.DPM[i][j - 1];
                            float[] UK_L = Program.UK[i][j];
                            float[] VK_L = Program.VK[i][j];
                            float[] WK_L = Program.WK[i][j];
                            int KKART = Program.KKART[i][j];

                            for (int k = 1; k < Program.NKK; k++)
                            {
                                DDPX = DPMim_L[k] - DPM_L[k];
                                DDPY = DPMjm_L[k] - DPM_L[k];
                                if (k > KKART + 1)
                                {
                                    DDPZ = DPM_L[k - 1] - DPM_L[k];
                                }
                                else
                                {
                                    DDPZ = 0;
                                }

                                if ((KKART < k) && (Program.KKART[i - 1][j] < k))
                                {
                                    UK_L[k] += (DDPX / DXK * DTIME);
                                }
                                else
                                {
                                    UK_L[k] = 0;
                                }

                                if ((KKART < k) && (Program.KKART[i][j - 1] < k))
                                {
                                    VK_L[k] += (DDPY / DYK * DTIME);
                                }
                                else
                                {
                                    VK_L[k] = 0;
                                }

                                if (KKART + 1 < k)
                                {
                                    WK_L[k] += (DDPZ * 2 / (Program.DZK[k] + Program.DZK[k - 1]) * DTIME);
                                }
                                else
                                {
                                    WK_L[k] = 0;
                                }
                            }
                        }
                    }
                });

                IterationLoops++;
            }
            
            //online output of simulated GRAL flow fields
            if (Program.GRALOnlineFunctions)
            {
                GRALONLINE.Output(Program.NII, Program.NJJ, Program.NKK);
            }

        }
    }
}
