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
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Threading;
using System.Threading.Tasks;

namespace GRAL_2001
{
    public class PrognosticFlowfield
    {
        public static float[] AP0 = new float[Program.KADVMAX + 2];

        /// <summary>
        ///Start the calculation of microscale prognostic flow fields
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Calculate()
        {
            CultureInfo ic = CultureInfo.InvariantCulture; //19.05.25 Ku: use ic for the file output
            int NJJ = Program.NJJ;
            int NII = Program.NII;
            int NKK = Program.NKK;
            float DXK = Program.DXK;
            float DYK = Program.DYK;
            int IKOOAGRAL = Program.IKOOAGRAL;
            int JKOOAGRAL = Program.JKOOAGRAL;
            bool topoZ0Available = (Program.Topo == Consts.TerrainAvailable) && (Program.LandUseAvailable == true);

            //some array declarations
            if (Program.UKS.Length < 5) // Arrays already created?
            {
                // create jagged arrays manually to keep memory areas of similar indices togehter -> reduce false sharing & 
                // save memory because of the unused index 0  
                Program.UKS = new float[NII + 2][][];
                Program.VKS = new float[NII + 2][][];
                Program.WKS = new float[NII + 2][][];
                Program.DPMNEW = new float[NII + 1][][];
                long MemRed=0;
                for (int i = 1; i < NII + 2; ++i)
                {
                    Program.UKS[i] = new float[NJJ + 2][];
                    Program.VKS[i] = new float[NJJ + 2][];
                    Program.WKS[i] = new float[NJJ + 2][];
                    if (i < NII + 1)
                    {
                        Program.DPMNEW[i] = new float[NJJ + 1][];
                    }

                    for (int j = 1; j < NJJ + 2; ++j)
                    {
                        int ip = Math.Min(i + 1, NII + 1);
                        int jp = Math.Min(j + 1, NJJ + 1);
                        
                        // Check for prognostic sub domains and reduce memory consumption of the jagged arrays
                        if (Program.ADVDOM[i][j] == 1 || Program.ADVDOM[i - 1][j] == 1 || Program.ADVDOM[ip][j] == 1 ||
                            Program.ADVDOM[i][j - 1] == 1 || Program.ADVDOM[i - 1][j - 1] == 1 || Program.ADVDOM[ip][j - 1] == 1 ||
                            Program.ADVDOM[i][jp] == 1 || Program.ADVDOM[i - 1][jp] == 1 || Program.ADVDOM[ip][jp] == 1)
                        {
                            Program.UKS[i][j] = new float[NKK + 1];
                            Program.VKS[i][j] = new float[NKK + 1];
                            Program.WKS[i][j] = new float[NKK + 2];
                            if (i < NII + 1 && j < NJJ + 1)
                            {
                                Program.DPMNEW[i][j] = new float[Program.KADVMAX + 2];
                            }
                        }
                        else
                        {
                            MemRed++;
                        }
                    }
                }

                if (MemRed > 500)
                {
                    string Info = "Reduce Memory Usage (Staggered Prognostic Grid): " + MemRed.ToString() + " x " + NKK.ToString() + " Cells: " + Math.Round(MemRed / 1000000D * 16D * (NKK + 2), 1) + " MByte";
                    Console.WriteLine(Info);
                    ProgramWriters.LogfileGralCoreWrite(Info);
                }

                Program.VerticalIndex = new int[NII + 1][];
                Program.UsternObstaclesHelpterm = new float[NII + 1][];
                Program.UsternTerrainHelpterm = new float[NII + 1][];
                for (int i = 1; i < NII + 1; ++i)
                {
                    Program.VerticalIndex[i] = new int[NJJ + 1];
                    Program.UsternObstaclesHelpterm[i] = new float[NJJ + 1];
                    Program.UsternTerrainHelpterm[i] = new float[NJJ + 1];
                }
            }
            else
            {
                Parallel.For(1, NII + 2, Program.pOptions, i =>
                {
                    for (int j = 1; j <= NJJ + 1; ++j)
                    {
                        if ((i < NII + 1) && (j < NJJ + 1))
                        {
                            Program.VerticalIndex[i][j] = 0;
                            Program.UsternObstaclesHelpterm[i][j] = 0;
                            Program.UsternTerrainHelpterm[i][j] = 0;
                        }

                        int ip = Math.Min(i + 1, NII + 1);
                        int jp = Math.Min(j + 1, NJJ + 1);
                        // Check for prognostic sub domains and reduce memory consumption of the jagged arrays
                        if (Program.ADVDOM[i][j] == 1 || Program.ADVDOM[i - 1][j] == 1 || Program.ADVDOM[ip][j] == 1 ||
                            Program.ADVDOM[i][j - 1] == 1 || Program.ADVDOM[i - 1][j - 1] == 1 || Program.ADVDOM[ip][j - 1] == 1 ||
                            Program.ADVDOM[i][jp] == 1 || Program.ADVDOM[i - 1][jp] == 1 || Program.ADVDOM[ip][jp] == 1)
                        {
                            for (int k = 0; k <= NKK + 1; ++k)
                            {
                                if (k < NKK + 1)
                                {
                                    Program.UKS[i][j][k] = 0;
                                    Program.VKS[i][j][k] = 0;
                                }
                                Program.WKS[i][j][k] = 0;
                                if ((k < Program.KADVMAX + 2) && (i < NII + 1) && (j < NJJ + 1))
                                {
                                    Program.DPMNEW[i][j][k] = 0;
                                }
                            }
                        }
                    }
                });
            }

            //wind velocity at the top of the model domain, needed for scaling the pressure changes for stationarity condition
            IntPoint refP = Program.SubDomainRefPos;
            float topwind = (float)Math.Sqrt(Program.Pow2(Program.UK[refP.X][refP.Y][NKK - 1]) +
                                             Program.Pow2(Program.VK[refP.X][refP.Y][NKK - 1]));
            topwind = Math.Max(topwind, 0.01F);

            //some variable declarations
            int IterationLoops = 1;
            float DTIME = 0.5F * MathF.Min(DXK, Program.DZK[1]) / MathF.Max(0.01f, MathF.Sqrt(Program.Pow2(Program.UK[refP.X][refP.Y][Program.KADVMAX]) +
                                                                                              Program.Pow2(Program.VK[refP.X][refP.Y][Program.KADVMAX])));
            DTIME = MathF.Min(DTIME, 5);
            float DeltaUMax = 0;
            float DeltaFinish = 0;
            float DeltaFirst = 0;

            float AREAxy = DXK * DYK;
            for (int k = 1; k <= Program.KADVMAX; ++k)
            {
                AP0[k] = AREAxy * Program.DZK[k] / DTIME;
            }

            int MaxProgCellCount = Program.VertCellsFF;
            float building_Z0 = 0.001F;
            double DeltaMean = 0;

            //constants in the k-eps turbulence model
            float Cmueh = 0.09F;
            float Ceps = 1;
            float Ceps1 = 1.44F;
            float Ceps2 = 1.92F;

            //set turbulence model
            int TurbulenceModel = 1;
            if (File.Exists("turbulence_model.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("turbulence_model.txt"))
                    {
                        TurbulenceModel = Convert.ToInt32(sr.ReadLine());
                    }
                }
                catch
                { }
            }

            if (TurbulenceModel != 1)
            {
                if (Program.TURB.Length < 5) // Arrays already created?
                {
                    Program.TURB = Program.CreateArray<Single[][]>(NII + 2, () => Program.CreateArray<Single[]>(NJJ + 2, () => new Single[NKK + 2]));
                    Program.TDISS = Program.CreateArray<Single[][]>(NII + 2, () => Program.CreateArray<Single[]>(NJJ + 2, () => new Single[NKK + 2]));
                }
            }

            Parallel.For(1, NII + 1, Program.pOptions, i =>
            {
                for (int j = 1; j <= NJJ; ++j)
                {
                    float[] DPM_L = Program.DPM[i][j];
                    for (int k = 1; k <= NKK + 1; ++k)
                    {
                        DPM_L[k] = 0;
                    }
                }
            });

            if (TurbulenceModel != 1)
            {
                Parallel.For(1, NII + 1, Program.pOptions, i =>
                {
                    for (int j = 1; j <= NJJ; ++j)
                    {
                        float[] TURB_L;
                        float[] TDISS_L;
                        TURB_L = Program.TURB[i][j];
                        TDISS_L = Program.TDISS[i][j];
                        for (int k = 1; k <= NKK + 1; ++k)
                        {
                            TURB_L[k] = 0.001F;
                            TDISS_L[k] = 0.0000001F;
                        }
                    }
                });
            }

            //term used to round numbers -> improves steady-state condition
            double Frund = 8000 / topwind;  //no-diffusion
            if (TurbulenceModel == 1)       //algebraic mixing-length model
            {
                Frund = 8000 / topwind;
            }

            if (TurbulenceModel == 2)       //k-eps model
            {
                Frund = 10000 / topwind;
            }

            //definition of the minimum and maximum iterations
            int IterationStepsMin = 100;
            int IterationStepsMax = 500;
            if (File.Exists("Integrationtime.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("Integrationtime.txt"))
                    {
                        IterationStepsMin = Convert.ToInt32(sr.ReadLine());
                        IterationStepsMax = Convert.ToInt32(sr.ReadLine());
                    }
                }
                catch
                { }
            }

            //roughness length for building walls
            if (File.Exists("building_roughness.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("building_roughness.txt"))
                    {
                        building_Z0 = Convert.ToSingle(sr.ReadLine().Replace(".", Program.Decsep));
                    }
                }
                catch
                { }
            }

            //relaxation factors for pressure gradient and velocity
            float relax = 0.1F;
            float relaxp = 1;
            if (File.Exists("relaxation_factors.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("relaxation_factors.txt"))
                    {
                        relax = Convert.ToSingle(sr.ReadLine().Replace(".", Program.Decsep));
                        relaxp = Convert.ToSingle(sr.ReadLine().Replace(".", Program.Decsep));
                    }
                }
                catch
                { }

                Console.WriteLine("Relaxation Factor Velocities:   " + relax.ToString());
                Console.WriteLine("Relaxation Factor Pressure:     " + relaxp.ToString());
            }

            //INITIAL VALUES FOR STAGGERED GRID VELOCITIES VALUES AND COMPUTATION OF INITIAL MAXIMUM VELOCITIES
            double GESCHW_MAX = 0;
            object obj = new object();
            //Parallel.For(1, NII + 1, Program.pOptions, i =>
            Parallel.ForEach(Partitioner.Create(1, NII + 1, Math.Max(4, (int)((NII + 1) / Program.IPROC))), Program.pOptions, range =>
            {
                double Geschw_max1 = 0;
                for (int i = range.Item1; i < range.Item2; ++i)
                {
                    for (int j = 1; j <= NJJ; ++j)
                    {
                        float[] UKS_L = Program.UKS[i][j];
                        float[] VKS_L = Program.VKS[i][j];
                        float[] WKS_L = Program.WKS[i][j];
                        float[] UK_L = Program.UK[i][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] WK_L = Program.WK[i][j];

                        int ip = Math.Min(i + 1, NII + 1);
                        int jp = Math.Min(j + 1, NJJ + 1);
                        // Check for prognostic sub domains and reduce memory consumption of the jagged arrays
                        if (Program.ADVDOM[i][j] == 1 || Program.ADVDOM[i - 1][j] == 1 || Program.ADVDOM[ip][j] == 1 ||
                            Program.ADVDOM[i][j - 1] == 1 || Program.ADVDOM[i - 1][j - 1] == 1 || Program.ADVDOM[ip][j - 1] == 1 ||
                            Program.ADVDOM[i][jp] == 1 || Program.ADVDOM[i - 1][jp] == 1 || Program.ADVDOM[ip][jp] == 1)
                        {
                            for (int k = 1; k <= NKK - 1; ++k)
                            {
                                UKS_L[k] = 0.5F * (UK_L[k] + Program.UK[i + 1][j][k]);
                                VKS_L[k] = 0.5F * (VK_L[k] + Program.VK[i][j + 1][k]);
                                WKS_L[k] = 0.5F * (WK_L[k] + WK_L[k + 1]);
                                Geschw_max1 = Math.Max(Geschw_max1, Math.Sqrt(Program.Pow2(UKS_L[k]) + Program.Pow2(VKS_L[k])));
                            }
                        }
                    }
                }
                lock (obj) { GESCHW_MAX = Math.Max(Geschw_max1, GESCHW_MAX); }
            });

            //Minimum vertical cell index up to which calculations are performed
            //Parallel.For(1, NII + 1, Program.pOptions, i =>
            Parallel.ForEach(Partitioner.Create(1, NII + 1, Math.Max(4, (int)((NII + 1) / Program.IPROC))), range =>
            {
                for (int i = range.Item1; i < range.Item2; ++i)
                {
                    for (int j = 1; j <= NJJ; ++j)
                    {
                        Program.VerticalIndex[i][j] = Math.Min(NKK - 1, Program.KKART[i][j] + MaxProgCellCount);
                    }
                }
            });

            //compute terms for the fricition velocity (just at the very beginning needed)
            Parallel.For(1, NII + 1, Program.pOptions, i =>
            {
                for (int j = 1; j <= NJJ; ++j)
                {
                    int KSTART = 1;
                    if (Program.CUTK[i][j] == 0)
                    {
                        KSTART = Program.KKART[i][j] + 1;
                    }

                    int Vert_index = Program.VerticalIndex[i][j];
                    int KKART_L = Program.KKART[i][j];
                    float CUTK_L = Program.CUTK[i][j];

                    float rough = 0;
                    if (Program.AdaptiveRoughnessMax > 0)
                    {
                        rough = Program.Z0Gral[i][j];
                    }
                    else if (topoZ0Available)
                    {
                        double x = i * Program.DXK + Program.GralWest;
                        double y = j * Program.DYK + Program.GralSouth;
                        double xsi1 = x - Program.GrammWest;
                        double eta1 = y - Program.GrammSouth;
                        int IUstern = Math.Clamp((int)(xsi1 / Program.DDX[1]) + 1, 1, Program.NX);
                        int JUstern = Math.Clamp((int)(eta1 / Program.DDY[1]) + 1, 1, Program.NY);
                        rough = Program.Z0Gramm[IUstern][JUstern];
                    }
                    else
                    {
                        rough = Program.Z0Gramm[1][1];
                    }

                    for (int k = KSTART; k <= Vert_index; ++k)
                    {
                        if (k == KKART_L + 1)
                        {
                            if (CUTK_L < 1) // building heigth < 1 m
                            {
                                //above terrain
                                if (rough >= Program.DZK[k] * 0.1)
                                {
                                    Program.UsternTerrainHelpterm[i][j] = 0.4F / MathF.Log((Program.DZK[k] + Program.DZK[k + 1] * 0.5F) / Program.DZK[k] * 0.1F);
                                }
                                else
                                {
                                    Program.UsternTerrainHelpterm[i][j] = 0.4F / MathF.Log(Program.DZK[k] * 0.5F / rough);
                                }
                            }
                            else 
                            {
                                //above building
                                Program.UsternObstaclesHelpterm[i][j] = 0.4F / MathF.Log(Program.DZK[k] * 0.5F / building_Z0);
                            }
                        }
                    }
                }
            });

            //geostrophic wind at the reference point
            float UG = Program.UKS[refP.X][refP.Y][NKK - 1];
            float VG = Program.VKS[refP.X][refP.Y][NKK - 1];
            
            //initialization of turbulent kinetic energy, dissipation, and potential temperature
            Parallel.For(1, NII + 1, Program.pOptions, i =>
            {
                for (int j = 1; j <= NJJ; ++j)
                {
                    float ObLength, ust, rough;
                    if (Program.AdaptiveRoughnessMax > 0)
                    {
                        ObLength = Program.OLGral[i][j];
                        ust = Program.USternGral[i][j];
                        rough = Program.Z0Gral[i][j];
                    }
                    else if (topoZ0Available)
                    {
                        double xhilf = (float)i * (DXK - DXK * 0.5F);
                        double yhilf = (float)j * (DYK - DYK * 0.5F);
                        int IUstern = (int)(xhilf / Program.DDX[1]) + 1;
                        int JUstern = (int)(yhilf / Program.DDY[1]) + 1;
                        ObLength = Program.Ob[IUstern][JUstern];
                        ust = Program.Ustern[IUstern][JUstern];
                        rough = Program.Z0Gramm[IUstern][JUstern];
                    }
                    else
                    {
                        ObLength = Program.Ob[1][1];
                        ust = Program.Ustern[1][1];
                        rough = Program.Z0Gramm[1][1];
                    }

                    for (int k = 1; k <= NKK - 1; ++k)
                    {
                        int ipo = 1;
                        float V0int = Program.V0[1];

                        if (i == 1)  //avoiding dead locks
                        {
                            Program.UK[NII + 1][j][k] = Program.UK[NII][j][k];
                        }
                        if (j == 1)  //avoiding dead locks
                        {
                            Program.VK[i][NJJ + 1][k] = Program.VK[i][NJJ][k];
                        }

                        float zhilf = Program.HOKART[k] - (Program.AHK[i][j] - Program.HOKART[Program.KKART[i][j]]);

                        float U0int = 0;
                        float varw = 0;
                        if ((Program.IStatistics == Consts.MeteoZR) || (Program.IStatistics == Consts.MeteoSonic))
                        {
                            //interpolation of observed standard deviations of horizontal wind fluctuations
                            if (zhilf <= Program.MeasurementHeight[1])
                            {
                                U0int = Program.U0[1];
                                V0int = Program.V0[1];
                            }
                            else if (zhilf >= Program.MeasurementHeight[Program.MetProfileNumb])
                            {
                                U0int = Program.U0[Program.MetProfileNumb];
                                V0int = Program.V0[Program.MetProfileNumb];
                            }
                            else
                            {
                                for (int iprof = 1; iprof <= Program.MetProfileNumb; iprof++)
                                {
                                    if (zhilf > Program.MeasurementHeight[iprof])
                                    {
                                        ipo = iprof + 1;
                                    }
                                }
                                U0int = Program.U0[ipo - 1] + (Program.U0[ipo] - Program.U0[ipo - 1]) / (Program.MeasurementHeight[ipo] - Program.MeasurementHeight[ipo - 1]) *
                                    (zhilf - Program.MeasurementHeight[ipo - 1]);
                                V0int = Program.V0[ipo - 1] + (Program.U0[ipo] - Program.U0[ipo - 1]) / (Program.MeasurementHeight[ipo] - Program.MeasurementHeight[ipo - 1]) *
                                    (zhilf - Program.MeasurementHeight[ipo - 1]);
                            }
                        }
                        else
                        {
                            double windhilf = Math.Max(Math.Sqrt(Program.Pow2(Program.UK[i][j][k]) + Program.Pow2(Program.VK[i][j][k])), 0.01);
                            U0int = (float)(windhilf * (0.2 * Math.Pow(windhilf, -0.9) + 0.32 * rough + 0.18));
                            U0int = (float)Math.Max(U0int, 0.3) * Program.StdDeviationV;
                        }

                        if (ObLength < 0)
                        {
                            varw = (Program.Pow2(ust) * Program.Pow2(1.15F + 0.1F * MathF.Pow(Program.BdLayHeight / (-ObLength), 0.67F)) * Program.Pow2(Program.StdDeviationW));
                        }
                        else
                        {
                            varw = Program.Pow2(ust * Program.StdDeviationW * 1.25F);
                        }

                        if (TurbulenceModel != 1)
                        {
                            Program.TURB[i][j][k] = (float)(0.5 * (Program.Pow2(U0int) + Program.Pow2(V0int) + varw));
                        }
                        /*
                        if (Program.IEPS == 1)
                            Program.TDISS[i][j][k] = (float)MyPow(ust, 3) / zhilf / 0.4 * MyPow(1 + 0.5 * MyPow(zhilf / Math.Abs(Program.Ob[IUstern][JUstern]), 0.67), 1.5);
                         */
                    }
                }
            });

            //Minimum vertical and horizontal turbulent exchange coefficients in dependence on atmospheric stability
            float VISHMIN = 0.00F;
            float VISVMIN = 0.00F;
            if (TurbulenceModel == 1)
            {
                VISHMIN = 0.0F;
                VISVMIN = 0.0F;
            }
            if (TurbulenceModel == 2)
            {
                VISHMIN = 0.01F;
                VISVMIN = 0.01F;
            }

            //INIITAL USTERN GEOMETRY FACTORS IN HORIZONTAL AND VERTICAL DIRECTIONS, ASSUMING NEUTRAL STABILITY AND A ROUGHNESS LENGTH OF 0.01m 
            float Ustern_factorX = (float)(0.4 / Math.Log(DXK * 0.5 / building_Z0));
            
            //SET VELOCITIES AND TKE/DISSIPATION INSIDE BUILDINGS TO ZERO
            Parallel.For(1, NII + 1, Program.pOptions, i =>
            {
                for (int j = 1; j <= NJJ; ++j)
                {
                    if (Program.ADVDOM[i][j] == 1)
                    {
                        float[] UKS_L = Program.UKS[i][j];
                        float[] VKS_L = Program.VKS[i][j];
                        float[] WKS_L = Program.WKS[i][j];
                        float[] UK_L = Program.UK[i][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] WK_L = Program.WK[i][j];
                        int Vert_index = Program.VerticalIndex[i][j];
                        int KKART = Program.KKART[i][j];

                        for (int k = 1; k <= Vert_index; ++k)
                        {
                            if (KKART >= k)
                            {
                                UK_L[k] = 0;
                                if (i == NII)
                                {
                                    Program.UK[i + 1][j][k] = 0;
                                }

                                VK_L[k] = 0;
                                if (j == NJJ)
                                {
                                    Program.VK[i][j + 1][k] = 0;
                                }

                                WK_L[k] = 0;
                                if (k == Vert_index)
                                {
                                    WK_L[k + 1] = 0;
                                }

                                if (TurbulenceModel != 1)
                                {
                                    Program.TURB[i][j][k] = 0;
                                    Program.TDISS[i][j][k] = 0;
                                }
                                UKS_L[k] = 0;
                                VKS_L[k] = 0;
                                WKS_L[k] = 0;
                            }
                        }
                    }
                }
            });

        //START OF THE ITERATIVE LOOP TO SOLVE THE PRESSURE AND ADVECTION-DIFFUSION EQUATIONS 
        CONTINUE_SIMULATION:
            while ((IterationLoops <= IterationStepsMax) && (Program.FlowFieldLevel == Consts.FlowFieldProg))
            {
                if (IterationLoops == 1)
                {
                    Console.WriteLine();
                    Console.WriteLine("ADVECTION");
                }

                //double Startzeit = Environment.TickCount;

                //lateral boundary conditions at the western and eastern boundaries of sub domains
                Parallel.For(2, NJJ + 1, Program.pOptions, j =>
                {
                    for (int i = 3; i < NII; ++i)
                    {
                        if (Program.ADVDOM[i][j] == 1)
                        {
                            int iM1 = i - 1;
                            int iM2 = i - 2;
                            int iP1 = i + 1;
                            int iP2 = i + 2;

                            float[] VK_L = Program.VK[i][j];
                            float[] WK_L = Program.WK[i][j];
                            int Vert_index = Program.VerticalIndex[i][j];

                            bool ADVDOMiM = (Program.ADVDOM[iM1][j] < 1) || (i == 3);       // sub domain or western border
                            bool ADVDOMiP = (Program.ADVDOM[iP1][j] < 1) || (i == NII - 1); // sub domain or eastern border

                            if (ADVDOMiM)
                            {
                                // set inflow to values outside the sub domain
                                for (int k = 1; k <= Vert_index; ++k)
                                {
                                    float UK_L = Program.UK[iM2][j][k];
                                    if (UK_L >= 0)
                                    {
                                        Program.UK[iM1][j][k] = UK_L;
                                    }
                                    Program.VK[iM2][j][k] = Program.VK[iM1][j][k];
                                    Program.WK[iM2][j][k] = Program.WK[iM1][j][k];
                                }
                            }

                            if (ADVDOMiP)
                            {
                                // set inflow to values outside the sub domain
                                for (int k = 1; k <= Vert_index; ++k)
                                {
                                    float UK_L = Program.UK[iP2][j][k];
                                    if (UK_L <= 0)
                                    {
                                        Program.UK[iP1][j][k] = UK_L;
                                    }
                                    Program.VK[iP1][j][k] = VK_L[k];
                                    Program.WK[iP1][j][k] = WK_L[k];
                                }
                            }
                        }
                    }
                });

                //lateral boundary conditions at the northern and southern boundaries of sub domains
                Parallel.For(2, NII + 1, Program.pOptions, i =>
                {
                    for (int j = 3; j <= NJJ - 1; ++j)
                    {
                        if (Program.ADVDOM[i][j] == 1)
                        {
                            int jM1 = j - 1;
                            int jM2 = j - 2;
                            int jP1 = j + 1;
                            int jP2 = j + 2;

                            float[] UK_L = Program.UK[i][j];
                            float[] WK_L = Program.WK[i][j];
                            int Vert_index = Program.VerticalIndex[i][j];

                            bool ADVDOMjM = (Program.ADVDOM[i][jM1] < 1) || (j == 3);       // sub domain or southern border
                            bool ADVDOMjP = (Program.ADVDOM[i][jP1] < 1) || (j == NJJ - 1); // sub domain or northern border

                            if (ADVDOMjM)
                            {
                                // set inflow to values outside the sub domain
                                for (int k = 1; k <= Vert_index; ++k)
                                {
                                    float VK_L = Program.VK[i][jM2][k];
                                    if (VK_L >= 0)
                                    {
                                        Program.VK[i][jM1][k] = VK_L;
                                    }
                                    Program.UK[i][jM2][k] = Program.UK[i][jM1][k];
                                    Program.WK[i][jM2][k] = Program.WK[i][jM1][k];
                                }
                            }

                            if (ADVDOMjP)
                            {
                                // set inflow to values outside the sub domain
                                for (int k = 1; k <= Vert_index; ++k)
                                {
                                    float VK_L = Program.VK[i][jP2][k];
                                    if (VK_L <= 0)
                                    {
                                        Program.VK[i][jP1][k] = VK_L;
                                    }
                                    Program.UK[i][jP1][k] = UK_L[k];
                                    Program.WK[i][jP1][k] = WK_L[k];
                                }
                            }
                        }
                    }
                });

                //boundary condition at the top of the domain for all sub domains
                Parallel.For(2, NII, Program.pOptions, i =>
                {
                    for (int j = 2; j <= NJJ - 1; ++j)
                    {
                        if (Program.ADVDOM[i][j] == 1)
                        {
                            float[] UK_L = Program.UK[i][j];
                            float[] VK_L = Program.VK[i][j];
                            float[] WK_L = Program.WK[i][j];
                            int k = Program.VerticalIndex[i][j];
                            WK_L[k + 1] = WK_L[k];
                            UK_L[k + 1] = UK_L[k];
                            VK_L[k + 1] = VK_L[k];
                        }
                    }
                });

                /*
                double TIME_dispersion = (Environment.TickCount - Startzeit) * 0.001;
                Console.WriteLine("Part 1: " + TIME_dispersion.ToString("0.000"));
                Startzeit = Environment.TickCount;
                 */

                //mass divergence in each cell
                Parallel.For(2, NII, Program.pOptions, i =>
                {
                    int iP1 = i + 1;
                    for (int j = 2; j < NJJ; ++j)
                    {
                        if (Program.ADVDOM[i][j] == 1)
                        {
                            float[] DIV_L = Program.DIV[i][j];
                            float[] UK_L = Program.UK[i][j];
                            float[] VK_L = Program.VK[i][j];
                            float[] WK_L = Program.WK[i][j];
                            float[] DPM_L = Program.DPM[i][j];
                            float[] UKip_L = Program.UK[iP1][j];
                            float[] VKjp_L = Program.VK[i][j + 1];
                            int KKART_LL = Program.KKART[i][j];
                            int Vert_index = Program.VerticalIndex[i][j];

                            for (int k = KKART_LL + 1; k <= Vert_index; ++k)
                            {
                                //if (KKART_LL < k)
                                //{
                                if (k > KKART_LL + 1)
                                {
                                    DIV_L[k] = (UK_L[k] - UKip_L[k]) * DYK * Program.DZK[k] + (VK_L[k] - VKjp_L[k]) * DXK * Program.DZK[k] + (WK_L[k] - WK_L[k + 1]) * AREAxy;
                                }
                                else
                                {
                                    DIV_L[k] = (UK_L[k] - UKip_L[k]) * DYK * Program.DZK[k] + (VK_L[k] - VKjp_L[k]) * DXK * Program.DZK[k] - WK_L[k + 1] * AREAxy;
                                }

                                DPM_L[k] = 0;
                                //}
                            }
                        }
                    }
                });

                /*
                TIME_dispersion = (Environment.TickCount - Startzeit) * 0.001;
                Console.WriteLine("Part 2: " + TIME_dispersion.ToString("0.000"));
                Startzeit = Environment.TickCount;
                 */

                /*PROCEDURE CALCULATING THE PRESSURE CORRECTION
                EQUATION TDMA (PATANKAR 1980, S125)*/

                //switch the loop to improve convergence - Alternating Direction Implicit (ADI) method 
                int IA = 2;
                int IE = NII - 1;
                int JA = 2;
                int JE = NJJ - 1;
                int IS = 1;
                int JS = 1;
                if (IterationLoops % 4 == 0)
                {
                    IA = NII - 1;
                    IE = 2;
                    JA = 2;
                    JE = NJJ - 1;
                    IS = -1;
                    JS = 1;
                }
                else if (IterationLoops % 4 == 1)
                {
                    IA = NII - 1;
                    IE = 2;
                    JA = NJJ - 1;
                    JE = 2;
                    IS = -1;
                    JS = -1;
                }
                else if (IterationLoops % 4 == 2)
                {
                    IA = 2;
                    IE = NII - 1;
                    JA = NJJ - 1;
                    JE = 2;
                    IS = 1;
                    JS = -1;
                }

                DeltaUMax = 0;
                Parallel.For(2, NJJ, Program.pOptions, j1 =>
                {
                    float DeltaUMaxIntern = 0;
                    Span<float> PIMP = stackalloc float[NKK + 2];
                    Span<float> QIMP = stackalloc float[NKK + 2];
                    float APP;
                    float ABP;
                    float ATP;
                    float DIM;
                    float TERMP;

                    int j = j1;
                    if (JS == -1)
                    {
                        j = NJJ - j1 + 1;
                    }

                    for (int i1 = 2; i1 <= NII - 1; ++i1)
                    {
                        int i = i1;
                        if (IS == -1)
                        {
                            i = NII - i1 + 1;
                        }

                        if (Program.ADVDOM[i][j] == 1)
                        {
                            float[] DIV_L = Program.DIV[i][j];
                            float[] DPM_L = Program.DPM[i][j];
                            float[] DPMim_L = Program.DPM[i - 1][j];
                            float[] DPMip_L = Program.DPM[i + 1][j];
                            float[] DPMjm_L = Program.DPM[i][j - 1];
                            float[] DPMjp_L = Program.DPM[i][j + 1];
                            float[] DPMNEW_L = Program.DPMNEW[i][j];
                            int KKART_LL = Program.KKART[i][j];
                            int Vert_index = Program.VerticalIndex[i][j];

                            for (int k = Vert_index; k > KKART_LL; --k)
                            {
                                //if (KKART_LL < k)
                                //{
                                DIM = DIV_L[k] / DTIME + (DPMim_L[k] + DPMip_L[k]) / DXK * DYK * Program.DZK[k] +
                                              (DPMjm_L[k] - DPMjp_L[k]) / DYK * DXK * Program.DZK[k];
                                APP = 2F * (DYK * Program.DZK[k] / DXK + DXK * Program.DZK[k] / DYK + AREAxy / Program.DZK[k]);

                                if (k > 1)
                                {
                                    ABP = AREAxy / (0.5F * (Program.DZK[k - 1] + Program.DZK[k]));
                                }
                                else
                                {
                                    ABP = AREAxy / Program.DZK[k];
                                }

                                ATP = AREAxy / (0.5F * (Program.DZK[k + 1] + Program.DZK[k]));
                                TERMP = 1 / (APP - ATP * PIMP[k + 1]);
                                PIMP[k] = ABP * TERMP;
                                QIMP[k] = (ATP * QIMP[k + 1] + DIM) * TERMP;
                                //}
                            }

                            //Obtain new P-components
                            //DPM_L[0] = 0;
                            for (int k = KKART_LL + 1; k <= Vert_index; ++k)
                            {
                                //if (KKART_LL < k)
                                //{
                                float temp = PIMP[k] * DPM_L[k - 1] + QIMP[k];
                                DeltaUMaxIntern = (float)Math.Max(DeltaUMaxIntern, Math.Abs(DPM_L[k] - Program.ConvToInt(temp * Frund) / Frund));
                                DPM_L[k] = temp;

                                //Pressure field to compute pressure gradients in the momentum equations
                                DPMNEW_L[k] += relaxp * DPM_L[k];
                                //}
                            }
                        }
                    }
                    lock (obj)
                    {
                        if (DeltaUMaxIntern > DeltaUMax)
                        {
                            Interlocked.Exchange(ref DeltaUMax, DeltaUMaxIntern);
                        }
                    }
                });

                //pressure-correction of u-component
                Parallel.For(2, NII, Program.pOptions, i =>
                {
                    for (int j = 2; j <= NJJ - 1; ++j)
                    {
                        int iM1 = i - 1;
                        int jM1 = j - 1;
                        if ((Program.ADVDOM[i][j] == 1) || (Program.ADVDOM[iM1][j] == 1) || (Program.ADVDOM[i][jM1] == 1))
                        {
                            float[] DPMim_L = Program.DPM[iM1][j];
                            float[] DPMjm_L = Program.DPM[i][jM1];
                            float[] DPM_L = Program.DPM[i][j];
                            float[] UK_L = Program.UK[i][j];
                            float[] VK_L = Program.VK[i][j];
                            float[] WK_L = Program.WK[i][j];
                            int KKART_LL = Program.KKART[i][j];
                            int KKART_LL_P1 = KKART_LL + 1;
                            int Vert_index = Program.VerticalIndex[i][j];
                            int KKARTiM = Program.KKART[iM1][j];
                            int KKARTjM = Program.KKART[i][jM1];

                            for (int k = 1; k <= Vert_index; ++k)
                            {
                                float DPM_L_K = DPM_L[k];

                                if (i > 2 && (KKART_LL < k) && (KKARTiM < k))
                                {
                                    UK_L[k] = (float)(Program.ConvToInt((UK_L[k] + (DPMim_L[k] - DPM_L_K) / DXK * DTIME) * Frund) / Frund);
                                }

                                if (j > 2 && (KKART_LL < k) && (KKARTjM < k))
                                {
                                    VK_L[k] = (float)(Program.ConvToInt((VK_L[k] + (DPMjm_L[k] - DPM_L_K) / DYK * DTIME) * Frund) / Frund);
                                }

                                if (KKART_LL_P1 < k)
                                {
                                    WK_L[k] = (float)(Program.ConvToInt((WK_L[k] + (DPM_L[k - 1] - DPM_L_K) / (Program.DZK[k] + Program.DZK[k - 1]) * 2 * DTIME) * Frund) / Frund);
                                }
                                else
                                {
                                    WK_L[k] = 0;
                                }
                            }
                        }
                    }
                });

                /*
                TIME_dispersion = (Environment.TickCount - Startzeit) * 0.001;
                Console.WriteLine("Part 4: " + TIME_dispersion.ToString("0.000"));
                Startzeit = Environment.TickCount;
                 */

                //COMPUTE NEW U, V, AND W-WIND FIELDS
                Parallel.For(2, NII, Program.pOptions, i =>
                {
                    int iM1 = i - 1;
                    int iP1 = i + 1;
                    for (int j = 2; j <= NJJ - 1; ++j)
                    {
                        int jM1 = j - 1;
                        if ((Program.ADVDOM[i][j] == 1) || (Program.ADVDOM[iM1][j] == 1) || (Program.ADVDOM[i][jM1] == 1))
                        {
                            float[] UK_L = Program.UK[i][j];
                            float[] UKip_L = Program.UK[iP1][j];
                            float[] UKS_L = Program.UKS[i][j];
                            float[] VK_L = Program.VK[i][j];
                            float[] VKjp_L = Program.VK[i][j + 1];
                            float[] VKS_L = Program.VKS[i][j];
                            float[] WK_L = Program.WK[i][j];
                            float[] WKS_L = Program.WKS[i][j];
                            int KKART_LL = Program.KKART[i][j];
                            int KKART_LL_P1 = KKART_LL + 1;
                            int Vert_index = Program.VerticalIndex[i][j];

                            int KSTART = 1;
                            if (Program.CUTK[i][j] == 0)
                            {
                                KSTART = KKART_LL_P1;
                            }

                            for (int k = KSTART; k <= Vert_index; ++k)
                            {
                                if (KKART_LL < k)
                                {
                                    UKS_L[k] = 0.5F * (UK_L[k] + UKip_L[k]);
                                    VKS_L[k] = 0.5F * (VK_L[k] + VKjp_L[k]);
                                }
                                if (KKART_LL_P1 < k)
                                {
                                    WKS_L[k] = 0.5F * (WK_L[k] + WK_L[k + 1]);
                                }   
                            }

                            if (j == 2)
                            {
                                float[] UKS1_L = Program.UKS[i][1];
                                for (int k = KSTART; k <= Vert_index; ++k)
                                {
                                    UKS1_L[k] = UKS_L[k];
                                }
                            }

                            if (j == NJJ - 1 && Program.ADVDOM[i][NJJ] == 1)
                            {
                                float[] UKSNJJ_L = Program.UKS[i][NJJ];
                                for (int k = KSTART; k <= Vert_index; ++k)
                                {
                                    UKSNJJ_L[k] = UKS_L[k];
                                }
                            }
                        }
                    }
                });

                for (int j = 2; j < NJJ; ++j) // AVOID False Sharing! No Parallelization here
                {
                    if (Program.ADVDOM[1][j] == 1)
                    {
                        float[] VKS1_L = Program.VKS[1][j];
                        float[] VKS2_L = Program.VKS[2][j];
                        for (int k = 0; k <= NKK; ++k)
                        {
                            VKS1_L[k] = VKS2_L[k];
                        }
                    }
                    if (Program.ADVDOM[NII - 1][j] == 1)
                    {
                        float[] VKSNII_L = Program.VKS[NII][j];
                        float[] VKSNII1_L = Program.VKS[NII - 1][j];
                        for (int k = 0; k <= NKK; ++k)
                        {
                           VKSNII_L[k] = VKSNII1_L[k];
                        }
                    }
                }

                //TIME_dispersion = (Environment.TickCount - Startzeit) * 0.001;
                //Console.WriteLine("Part 5: " + TIME_dispersion.ToString("0.000"));
                //Startzeit = Environment.TickCount;

                //MOMENTUM EQUATIONS FOR THE THREE WIND-COMPONENTS AND
                //MOMENTUM EQUATION FOR THE TURBULENT KINETIC ENERGY AND DISSIPATION RATE

                //no-diffusion
                if (TurbulenceModel == 0)
                {
                    Parallel.Invoke(Program.pOptions,
                        () => U_PrognosticMicroscaleV0.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, UG, building_Z0, relax),
                        () => V_PrognosticMicroscaleV0.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, VG, building_Z0, relax));
                    W_PrognosticMicroscaleV0.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, building_Z0, relax);
                }

                //algebraic mixing length model
                if (TurbulenceModel == 1)
                {
                    if (Program.UseVector512Class)
                    {
                        if (!Program.UseFixedRndSeedVal)
                        {
                            Program.pOptions.MaxDegreeOfParallelism += 2;
                            Parallel.Invoke(Program.pOptions,
                            () => U_PrognosticMicroscaleV1_Vec512.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, UG, relax),
                            () => V_PrognosticMicroscaleV1_Vec512.Calculate(-IS, -JS, Cmueh, VISHMIN, AREAxy, VG, relax));
                            Program.pOptions.MaxDegreeOfParallelism -= 2;
                        }
                        else
                        {
                            U_PrognosticMicroscaleV1_Vec512.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, UG, relax);
                            V_PrognosticMicroscaleV1_Vec512.Calculate(-IS, -JS, Cmueh, VISHMIN, AREAxy, VG, relax);
                        }
                        W_PrognosticMicroscaleV1_Vec512.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, relax);
                    }
                    else
                    {
                        if (!Program.UseFixedRndSeedVal)
                        {
                            Program.pOptions.MaxDegreeOfParallelism += 2;
                            Parallel.Invoke(Program.pOptions,
                            () => U_PrognosticMicroscaleV1.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, UG, relax),
                            () => V_PrognosticMicroscaleV1.Calculate(-IS, -JS, Cmueh, VISHMIN, AREAxy, VG, relax));
                            Program.pOptions.MaxDegreeOfParallelism -= 2;
                        }
                        else
                        {
                            U_PrognosticMicroscaleV1.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, UG, relax);
                            V_PrognosticMicroscaleV1.Calculate(-IS, -JS, Cmueh, VISHMIN, AREAxy, VG, relax);
                        }
                        W_PrognosticMicroscaleV1.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, relax);
                    }
                }

                //k-eps model
                if (TurbulenceModel == 2)
                {
                    Parallel.Invoke(Program.pOptions,
                           () => U_PrognosticMicroscaleV2.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, UG, building_Z0, relax),
                           () => V_PrognosticMicroscaleV2.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, VG, building_Z0, relax));
                    W_PrognosticMicroscaleV2.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, building_Z0, relax);
                    TKE_PrognosticMicroscale.Calculate(IS, JS, Cmueh, Ceps, Ceps1, Ceps2, VISHMIN, VISVMIN, AREAxy, building_Z0, relax, Ustern_factorX);
                }

                //double TIME_dispersion = (Environment.TickCount - Startzeit) * 0.001;
                //Console.WriteLine("Part 8: " + TIME_dispersion.ToString("0.000"));
                //Startzeit = Environment.TickCount;

                //Average maximum deviation of wind components between two time steps
                if (IterationLoops >= IterationStepsMax)
                {
                    DeltaFinish = (float)(DeltaMean / Program.Pow2(topwind));
                }

                if ((IterationLoops % 100 == 0) && (IterationLoops > 1) && (Program.FlowFieldLevel == Consts.FlowFieldProg))
                {
                    double _delta = DeltaMean / Program.Pow2(topwind);

                    if (IterationLoops == 100)
                    {
                        DeltaFirst = (float)_delta;
                    }

                    Console.WriteLine("ITERATION " + IterationLoops.ToString() + ": " + _delta.ToString("0.000"));

                    if (_delta < 0.012 && (IterationLoops >= IterationStepsMin))
                    {
                        break;
                    }

                    DeltaMean = 0;
                }
                else
                {
                    DeltaMean += DeltaUMax;
                }

                //ONLINE OUTPUT OF GRAL FLOW FIELDS
                if (Program.GRALOnlineFunctions && (IterationLoops % 100 == 0) && (Program.FlowFieldLevel == Consts.FlowFieldProg))
                {
                    GRALONLINE.Output(NII, NJJ, NKK);
                }

                IterationLoops++;

                //double TIME_dispersion = (Environment.TickCount - Startzeit) * 0.001;
                //Console.WriteLine("Total time: " + TIME_dispersion.ToString("0.000"));
            }

            //OUTPUT OF ENCOUNTERED PROBLEMS DURING THE SIMULATION
            if (DeltaFinish > DeltaFirst)
            {
                ProgramWriters.LogfileProblemreportWrite("Convergence not fullfilled for flow field: " + Program.IWET.ToString());
            }

            //CHECK FOR UNREALISTIC WIND SPEED VALUES
            int UKunrealistic = 0;
            int VKunrealistic = 0;
            int WKunrealistic = 0;
            double Xunrealistic = 0;
            double Yunrealistic = 0;
            double Zunrealistic = 0;
            for (int i = 1; i <= NII; ++i)
            {
                for (int j = 1; j <= NJJ; ++j)
                {
                    if (Program.ADVDOM[i][j] == 1)
                    {
                        float[] UK_L = Program.UK[i][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] WK_L = Program.WK[i][j];
                        float limit = (float)(10 * GESCHW_MAX);
                        for (int k = 1; k <= Math.Min(NKK - 1, Program.KKART[i][j] + MaxProgCellCount); ++k)
                        {
                            if ((Math.Abs(UK_L[k]) > 55) || (Math.Abs(UK_L[k]) > limit))
                            {
                                UKunrealistic++;
                                Xunrealistic = IKOOAGRAL + (float)i * DXK - DXK * 0.5;
                                Yunrealistic = JKOOAGRAL + (float)j * DYK - DYK * 0.5;
                                Zunrealistic = Program.HOKART[k] - Program.DZK[k] * 0.5;
                            }
                            if ((Math.Abs(VK_L[k]) > 55) || (Math.Abs(VK_L[k]) > limit))
                            {
                                VKunrealistic++;
                                Xunrealistic = IKOOAGRAL + (float)i * DXK - DXK * 0.5;
                                Yunrealistic = JKOOAGRAL + (float)j * DYK - DYK * 0.5;
                                Zunrealistic = Program.HOKART[k] - Program.DZK[k] * 0.5;
                            }
                            if ((Math.Abs(WK_L[k]) > 20) || (Math.Abs(WK_L[k]) > limit))
                            {
                                WKunrealistic++;
                                Xunrealistic = IKOOAGRAL + (float)i * DXK - DXK * 0.5;
                                Yunrealistic = JKOOAGRAL + (float)j * DYK - DYK * 0.5;
                                Zunrealistic = Program.HOKART[k] - Program.DZK[k] * 0.5;
                            }
                        }
                    }
                }
            }

            if ((UKunrealistic >= 1) || (VKunrealistic >= 1) || (WKunrealistic >= 1))
            {
                ProgramWriters.LogfileProblemreportWrite("Unrealistic wind speeds encountered for flow field: " + Program.IWET.ToString() + " x,y,z-coordinates (total): "
                                            + Xunrealistic.ToString("0.0", ic) + "(" + UKunrealistic.ToString() + ") ,"
                                            + Yunrealistic.ToString("0.0", ic) + "(" + VKunrealistic.ToString() + ") ,"
                                            + Zunrealistic.ToString("0.0", ic) + "(" + VKunrealistic.ToString() + ")");
            }

            Console.WriteLine("Total number of iterations: " + IterationLoops.ToString());

            //OUTPUT FOR VDI TEST CASES
            if (File.Exists("VDI-A1-1.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-A1-1.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 3; j <= NJJ - 2; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -200) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 250) && (Program.HOKART[k] - Program.DZK[k] * 0.5 < 250))
                                    {
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Program.UKS[i][j][k].ToString("0.000", ic) + " " + Program.VKS[i][j][k].ToString("0.000", ic) + " " + Program.WKS[i][j][k].ToString("0.000", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-A1-2.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-A1-2.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -90) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 210) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -5) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 5) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 85))
                                    {
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Program.UKS[i][j][k].ToString("0.000", ic) + " " + Program.VKS[i][j][k].ToString("0.000", ic) + " " + Program.WKS[i][j][k].ToString("0.000", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }


                IterationLoops = 1;
                goto CONTINUE_SIMULATION;
            }

            if (File.Exists("VDI-A3.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-A3.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -37.5) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 62.5) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -25) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 25) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 37.5))
                                    {
                                        float Uhilf = (float)(0.5 * (Program.UK[i][j][k] + Program.UK[i + 1][j][k]));
                                        float Vhilf = (float)(0.5 * (Program.VK[i][j][k] + Program.VK[i][j + 1][k]));
                                        float Whilf = (float)(0.5 * (Program.WK[i][j][k] + Program.WK[i][j][k + 1]));
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Uhilf.ToString("0.00", ic) + " " + Vhilf.ToString("0.00", ic) + " " + Whilf.ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-A3-1.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-A3-1.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -65) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 125) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -38) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 38) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 100))
                                    {
                                        float Uhilf = (float)(0.5 * (Program.UK[i][j][k] + Program.UK[i + 1][j][k]));
                                        float Vhilf = (float)(0.5 * (Program.VK[i][j][k] + Program.VK[i][j + 1][k]));
                                        float Whilf = (float)(0.5 * (Program.WK[i][j][k] + Program.WK[i][j][k + 1]));
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Uhilf.ToString("0.00", ic) + " " + Vhilf.ToString("0.00", ic) + " " + Whilf.ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if ((File.Exists("VDI-A4-1.txt") == true) && (File.Exists("VDI-A4-2.txt") == false))
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-A4-1.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -50) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 75) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -50) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 75) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 100))
                                    {
                                        float Uhilf = (float)(0.5 * (Program.UK[i][j][k] + Program.UK[i + 1][j][k]));
                                        float Vhilf = (float)(0.5 * (Program.VK[i][j][k] + Program.VK[i][j + 1][k]));
                                        float Whilf = (float)(0.5 * (Program.WK[i][j][k] + Program.WK[i][j][k + 1]));
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Uhilf.ToString("0.00", ic) + " " + Vhilf.ToString("0.00", ic) + " " + Whilf.ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-A4-2.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-A4-2.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -65) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 75) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -50) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 75) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 100))
                                    {
                                        float Uhilf = (float)(0.5 * (Program.UK[i][j][k] + Program.UK[i + 1][j][k]));
                                        float Vhilf = (float)(0.5 * (Program.VK[i][j][k] + Program.VK[i][j + 1][k]));
                                        float Whilf = (float)(0.5 * (Program.WK[i][j][k] + Program.WK[i][j][k + 1]));
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Uhilf.ToString("0.00", ic) + " " + Vhilf.ToString("0.00", ic) + " " + Whilf.ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-B1-x.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-B1-x.txt"))
                    {
                        for (int i = 1; i <= NII; ++i)
                        {
                            for (int j = 1; j <= NJJ; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if (Program.HOKART[k] - Program.DZK[k] * 0.5 < 250)
                                    {
                                        float Uhilf = (float)(0.5 * (Program.UK[i][j][k] + Program.UK[i + 1][j][k]));
                                        float Vhilf = (float)(0.5 * (Program.VK[i][j][k] + Program.VK[i][j + 1][k]));
                                        float Whilf = (float)(0.5 * (Program.WK[i][j][k] + Program.WK[i][j][k + 1]));
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Uhilf.ToString("0.00", ic) + " " + Vhilf.ToString("0.00", ic) + " " + Whilf.ToString("0.00", ic) + " " + Program.Ustern[1][1].ToString("0.000", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if ((File.Exists("VDI-B1-x.txt") == false) && (File.Exists("VDI-B1-1.txt") == true))
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-B1-1.txt"))
                    {
                        for (int i = 1; i <= NII; ++i)
                        {
                            for (int j = 1; j <= NJJ; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if (Program.HOKART[k] - Program.DZK[k] * 0.5 < 250)
                                    {
                                        float Uhilf = (float)(0.5 * (Program.UK[i][j][k] + Program.UK[i + 1][j][k]));
                                        float Vhilf = (float)(0.5 * (Program.VK[i][j][k] + Program.VK[i][j + 1][k]));
                                        float Whilf = (float)(0.5 * (Program.WK[i][j][k] + Program.WK[i][j][k + 1]));
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Uhilf.ToString("0.00", ic) + " " + Vhilf.ToString("0.00", ic) + " " + Whilf.ToString("0.00", ic) + " " + Program.Ustern[1][1].ToString("0.000", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-C3.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-C3.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -70) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 140) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -70) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 10) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 80))
                                    {
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Program.UKS[i][j][k].ToString("0.00", ic) + " " + Program.VKS[i][j][k].ToString("0.00", ic) + " " + Program.WKS[i][j][k].ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-C1.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-C1.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -90) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 210) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -5) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 5) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 85))
                                    {
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Program.UKS[i][j][k].ToString("0.00", ic) + " " + Program.VKS[i][j][k].ToString("0.00", ic) + " " + Program.WKS[i][j][k].ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-C4.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-C4.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -50) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 130) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -80) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 100) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 80))
                                    {
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Program.UKS[i][j][k].ToString("0.00", ic) + " " + Program.VKS[i][j][k].ToString("0.00", ic) + " " + Program.WKS[i][j][k].ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-C5.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-C5.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -50) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 90) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -40) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 5) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 50))
                                    {
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Program.UKS[i][j][k].ToString("0.00", ic) + " " + Program.VKS[i][j][k].ToString("0.00", ic) + " " + Program.WKS[i][j][k].ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-C6.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-C6.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -15) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 50) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -30) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 40) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 45))
                                    {
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Program.UKS[i][j][k].ToString("0.00", ic) + " " + Program.VKS[i][j][k].ToString("0.00", ic) + " " + Program.WKS[i][j][k].ToString("0.00", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("VDI-Michelstadt.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("VDI-Michelstadt.txt"))
                    {
                        for (int i = 2; i <= NII - 1; ++i)
                        {
                            for (int j = 2; j <= NJJ - 1; ++j)
                            {
                                for (int k = 1; k <= NKK - 1; ++k)
                                {
                                    if ((((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -460) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < -440)) ||
                                         ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -340) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 170)) ||
                                         ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > 220) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 230)) ||
                                         ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > 220) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 230)) ||
                                         ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > 330) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < 345)) ||
                                         ((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) > -660) && (IKOOAGRAL + DXK * 0.5 + DXK * (i - 1) < -650))) &&
                                        (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) > -230) && (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1) < 230) &&
                                        (Program.HOKART[k] - Program.DZK[k] * 0.5 < 120))
                                    {
                                        sw.WriteLine((IKOOAGRAL + DXK * 0.5 + DXK * (i - 1)).ToString("0.0", ic) + " " + (JKOOAGRAL + DYK * 0.5 + DYK * (j - 1)).ToString("0.0", ic) + " " + (Program.HOKART[k] - Program.DZK[k] * 0.5).ToString("0.0", ic) +
                                                     " " + Program.UKS[i][j][k].ToString("0.000", ic) + " " + Program.VKS[i][j][k].ToString("0.000", ic) + " " + Program.WKS[i][j][k].ToString("0.000", ic));
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (File.Exists("IDAHO.txt") == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter("IDAHO.txt", true))
                    {
                        int iA = (int)((-9.6 - IKOOAGRAL) / DXK) + 1;
                        int jA = (int)((0 - JKOOAGRAL) / DYK) + 1;
                        int kA = (int)((3 - 0) / 1) + 1;
                        int iB = (int)((24 - IKOOAGRAL) / DXK) + 1;
                        int jB = (int)((0 - JKOOAGRAL) / DYK) + 1;
                        int kB = (int)((3 - 0) / 1) + 1;
                        int iC = (int)((24 - IKOOAGRAL) / DXK) + 1;
                        int jC = (int)((0 - JKOOAGRAL) / DYK) + 1;
                        int kC = (int)((6 - 0) / 1) + 1;
                        int iG = (int)((24 - IKOOAGRAL) / DXK) + 1;
                        int jG = (int)((0 - JKOOAGRAL) / DYK) + 1;
                        int kG = (int)((9 - 0) / 1) + 1;
                        int iD = (int)((66 - IKOOAGRAL) / DXK) + 1;
                        int jD = (int)((0 - JKOOAGRAL) / DYK) + 1;
                        int kD = (int)((3 - 0) / 1) + 1;

                        double windhilf1 = Math.Max(Math.Sqrt(Program.Pow2(Program.UKS[iA][jA][kA]) + Program.Pow2(Program.VKS[iA][jA][kA])), 0.01);
                        double U0int1 = windhilf1 * (0.2 * Math.Pow(windhilf1, -0.9) + 0.32 * Program.Z0Gramm[1][1] + 0.18);
                        U0int1 = Math.Max(U0int1, 0.3);
                        double windhilf2 = Math.Max(Math.Sqrt(Program.Pow2(Program.UKS[iB][jB][kB]) + Program.Pow2(Program.VKS[iB][jB][kB])), 0.01);
                        double U0int2 = windhilf2 * (0.2 * Math.Pow(windhilf2, -0.9) + 0.32 * Program.Z0Gramm[1][1] + 0.18);
                        U0int2 = Math.Max(U0int2, 0.3);
                        double windhilf3 = Math.Max(Math.Sqrt(Program.Pow2(Program.UKS[iC][jC][kC]) + Program.Pow2(Program.VKS[iC][jC][kC])), 0.01);
                        double U0int3 = windhilf3 * (0.2 * Math.Pow(windhilf3, -0.9) + 0.32 * Program.Z0Gramm[1][1] + 0.18);
                        U0int3 = Math.Max(U0int3, 0.3);
                        double windhilf4 = Math.Max(Math.Sqrt(Program.Pow2(Program.UKS[iG][jG][kG]) + Program.Pow2(Program.VKS[iG][jG][kG])), 0.01);
                        double U0int4 = windhilf4 * (0.2 * Math.Pow(windhilf4, -0.9) + 0.32 * Program.Z0Gramm[1][1] + 0.18);
                        U0int4 = Math.Max(U0int4, 0.3);
                        double windhilf5 = Math.Max(Math.Sqrt(Program.Pow2(Program.UKS[iD][jD][kD]) + Program.Pow2(Program.VKS[iD][jD][kD])), 0.01);
                        double U0int5 = windhilf5 * (0.2 * Math.Pow(windhilf5, -0.9) + 0.32 * Program.Z0Gramm[1][1] + 0.18);
                        U0int5 = Math.Max(U0int5, 0.3);
                        double varw = Program.Pow2(Program.Ustern[1][1] * Program.StdDeviationW * 1.25);
                        if (Program.Ob[1][1] < 0)
                        {
                            varw = Program.Pow2(Program.Ustern[1][1]) * Program.Pow2(1.15 + 0.1 * Math.Pow(Program.BdLayHeight / (-Program.Ob[1][1]), 0.67)) * Program.Pow2(Program.StdDeviationW);
                        }

                        if (TurbulenceModel != 1)
                        {
                            sw.WriteLine(Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iA][jA][kA].ToString("0.00") + "," + Program.VKS[iA][jA][kA].ToString("0.00") + "," + Program.TURB[iA][jA][kA].ToString("0.00") + "," +
                                         Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iB][jB][kB].ToString("0.00") + "," + Program.VKS[iB][jB][kB].ToString("0.00") + "," + Program.TURB[iB][jB][kB].ToString("0.00") + "," +
                                         Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iC][jC][kC].ToString("0.00") + "," + Program.VKS[iC][jC][kC].ToString("0.00") + "," + Program.TURB[iC][jC][kC].ToString("0.00") + "," +
                                         Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iG][jG][kG].ToString("0.00") + "," + Program.VKS[iG][jG][kG].ToString("0.00") + "," + Program.TURB[iG][jG][kG].ToString("0.00") + "," +
                                         Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iD][jD][kD].ToString("0.00") + "," + Program.VKS[iD][jD][kD].ToString("0.00") + "," + Program.TURB[iD][jD][kD].ToString("0.00") + "," +
                                         Program.Pow2(U0int1).ToString("0.000") + "," + Program.Pow2(U0int2).ToString("0.000") + "," + Program.Pow2(U0int3).ToString("0.000") + "," + Program.Pow2(U0int4).ToString("0.000") + "," + Program.Pow2(U0int5).ToString("0.000") + "," + varw.ToString("0.000"));
                        }
                        else
                        {
                            sw.WriteLine(Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iA][jA][kA].ToString("0.00") + "," + Program.VKS[iA][jA][kA].ToString("0.00") + "," + 0 + "," +
                                         Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iB][jB][kB].ToString("0.00") + "," + Program.VKS[iB][jB][kB].ToString("0.00") + "," + 0 + "," +
                                         Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iC][jC][kC].ToString("0.00") + "," + Program.VKS[iC][jC][kC].ToString("0.00") + "," + 0 + "," +
                                         Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iG][jG][kG].ToString("0.00") + "," + Program.VKS[iG][jG][kG].ToString("0.00") + "," + 0 + "," +
                                         Program.Ustern[1][1].ToString("0.00") + "," + Program.UKS[iD][jD][kD].ToString("0.00") + "," + Program.VKS[iD][jD][kD].ToString("0.00") + "," + 0 + "," +
                                         Program.Pow2(U0int1).ToString("0.000") + "," + Program.Pow2(U0int2).ToString("0.000") + "," + Program.Pow2(U0int3).ToString("0.000") + "," + Program.Pow2(U0int4).ToString("0.000") + "," + Program.Pow2(U0int5).ToString("0.000") + "," + varw.ToString("0.000"));
                        }
                    }
                }
                catch { }
            }
        }
    }
}
