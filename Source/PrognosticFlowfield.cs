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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            //some array declarations
            if (Program.UKS.Length < 5) // Arrays already created?
            {
                // create jagged arrays manually to keep memory areas of similar indices togehter -> reduce false sharing & 
                // save memory because of the unused index 0  
                Program.UKS = new float[NII + 2][][];
                Program.VKS = new float[NII + 2][][];
                Program.WKS = new float[NII + 2][][];
                Program.DPMNEW = new float[NII + 1][][];
                for (int i = 1; i < NII + 2; ++i)
                {
                    Program.UKS[i] = new float[NJJ + 2][];
                    Program.VKS[i] = new float[NJJ + 2][];
                    Program.WKS[i] = new float[NJJ + 2][];
                    if (i < NII + 1)
                        Program.DPMNEW[i] = new float[NJJ + 1][];

                    for (int j = 1; j < NJJ + 2; ++j)
                    {
                        Program.UKS[i][j] = new float[NKK + 1];
                        Program.VKS[i][j] = new float[NKK + 1];
                        Program.WKS[i][j] = new float[NKK + 2];
                        if (i < NII + 1 && j < NJJ + 1)
                            Program.DPMNEW[i][j] = new float[Program.KADVMAX + 2];
                    }
                }

                Program.VerticalIndex = new Int16[NII + 1][];
                Program.UsternObstaclesHelpterm = new float[NII + 1][];
                Program.UsternTerrainHelpterm = new float[NII + 1][];
                for (int i = 1; i < NII + 1; ++i)
                {
                    Program.VerticalIndex[i] = new Int16[NJJ + 1];
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
                });
            }

            //wind velocity at the top of the model domain, needed for scaling the pressure changes for stationarity condition
            float topwind = (float)Math.Sqrt(Program.Pow2(Program.UK[1][1][NKK - 1]) + Program.Pow2(Program.VK[1][1][NKK - 1]));
            topwind = Math.Max(topwind, 0.01F);

            //some variable declarations
            int IV = 1;
            float DTIME = (float)(0.5 * Math.Min(DXK, Program.DZK[1]) /
                Math.Sqrt(Program.Pow2(Program.UK[1][1][Program.KADVMAX]) + Program.Pow2(Program.VK[1][1][Program.KADVMAX])));
            DTIME = Math.Min(DTIME, 5);
            float DELTAUMAX = 0;
            float DELTALAST = 0;
            float DELTAFIRST = 0;

            float AREAxy = DXK * DYK;
            for (int k = 1; k <= Program.KADVMAX; ++k)
            {
                AP0[k] = AREAxy * Program.DZK[k] / DTIME;
            }

            int MAXHOEH = Program.VertCellsFF;
            float building_Z0 = 0.001F;
            double DELTAMEAN = 0;

            //constants in the k-eps turbulence model
            float Cmueh = 0.09F;
            float Ceps = 1;
            float Ceps1 = 1.44F;
            float Ceps2 = 1.92F;

            //set turbulence model
            int TURB_MODEL = 1;
            if (File.Exists("turbulence_model.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("turbulence_model.txt"))
                    {
                        TURB_MODEL = Convert.ToInt32(sr.ReadLine());
                    }
                }
                catch
                { }
            }

            if (TURB_MODEL != 1)
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

            if (TURB_MODEL != 1)
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
                            TURB_L[k] = (float)0.001;
                            TDISS_L[k] = (float)0.0000001;
                        }
                    }
                });
            }

            //term used to round numbers -> improves steady-state condition
            double Frund = 8000 / topwind;  //no-diffusion
            if (TURB_MODEL == 1)               //algebraic mixing-length model
                Frund = 8000 / topwind;
            if (TURB_MODEL == 2)            //k-eps model
                Frund = 10000 / topwind;

            //definition of the minimum and maximum iterations
            int INTEGRATIONSSCHRITTE_MIN = 100;
            int INTEGRATIONSSCHRITTE_MAX = 500;
            if (File.Exists("Integrationtime.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("Integrationtime.txt"))
                    {
                        INTEGRATIONSSCHRITTE_MIN = Convert.ToInt32(sr.ReadLine());
                        INTEGRATIONSSCHRITTE_MAX = Convert.ToInt32(sr.ReadLine());
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
            Parallel.ForEach(Partitioner.Create(1, NII + 1, Math.Max(4, (int)((NII + 1) / Program.pOptions.MaxDegreeOfParallelism))), range =>
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

                        for (int k = 1; k <= NKK - 1; ++k)
                        {
                            UKS_L[k] = 0.5F * (UK_L[k] + Program.UK[i + 1][j][k]);
                            VKS_L[k] = 0.5F * (VK_L[k] + Program.VK[i][j + 1][k]);
                            WKS_L[k] = 0.5F * (WK_L[k] + WK_L[k + 1]);
                            Geschw_max1 = Math.Max(Geschw_max1, Math.Sqrt(Program.Pow2(UKS_L[k]) + Program.Pow2(VKS_L[k])));
                        }
                    }
                }

                lock (obj) { GESCHW_MAX = Math.Max(Geschw_max1, GESCHW_MAX); }
            });

            //Minimum vertical cell index up to which calculations are performed
            //Parallel.For(1, NII + 1, Program.pOptions, i =>
            Parallel.ForEach(Partitioner.Create(1, NII + 1, Math.Max(4, (int)((NII + 1) / Program.pOptions.MaxDegreeOfParallelism))), range =>
            {
                for (int i = range.Item1; i < range.Item2; ++i)
                {
                    for (int j = 1; j <= NJJ; ++j)
                    {
                        Program.VerticalIndex[i][j] = (Int16)(Math.Min(NKK - 1, Program.KKART[i][j] + MAXHOEH));
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
                        KSTART = Program.KKART[i][j] + 1;

                    int Vert_index = Program.VerticalIndex[i][j];
                    int KKART_L = Program.KKART[i][j];
                    float CUTK_L = Program.CUTK[i][j];

                    for (int k = KSTART; k <= Vert_index; ++k)
                    {
                        if ((k == KKART_L + 1) && (CUTK_L == 0))
                        {
                            float xhilf = (float)i * (DXK - DXK * 0.5F);
                            float yhilf = (float)j * (DYK - DYK * 0.5F);
                            int IUstern = (int)(xhilf / Program.DDX[1]) + 1;
                            int JUstern = (int)(yhilf / Program.DDY[1]) + 1;
                            Program.UsternTerrainHelpterm[i][j] = 0.4F / MathF.Log(Program.DZK[k] * 0.5F / Program.Z0Gramm[IUstern][JUstern]);

                            if (Program.Z0Gramm[IUstern][JUstern] >= Program.DZK[k] * 0.1)
                                Program.UsternTerrainHelpterm[i][j] = 0.4F / MathF.Log((Program.DZK[k] + Program.DZK[k + 1] * 0.5F) / Program.DZK[k] * 0.1F);
                        }
                        else if ((k == KKART_L + 1) && (CUTK_L == 1))
                        {
                            Program.UsternObstaclesHelpterm[i][j] = 0.4F / MathF.Log(Program.DZK[k] * 0.5F / building_Z0);
                        }
                    }
                }
            });

            //geostrophic wind
            float UG = Program.UKS[1][1][NKK - 1];
            float VG = Program.VKS[1][1][NKK - 1];

            //initialization of turbulent kinetic energy, dissipation, and potential temperature
            Parallel.For(1, NII + 1, Program.pOptions, i =>
            {
                for (int j = 1; j <= NJJ; ++j)
                {
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
                        double xhilf = (float)i * (DXK - DXK * 0.5F);
                        double yhilf = (float)j * (DYK - DYK * 0.5F);
                        int IUstern = (int)(xhilf / Program.DDX[1]) + 1;
                        int JUstern = (int)(yhilf / Program.DDY[1]) + 1;
                        float U0int = 0;
                        float varw = 0;
                        if ((Program.IStatistics == 0) || (Program.IStatistics == 3))
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
                                        ipo = iprof + 1;
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
                            U0int = (float)(windhilf * (0.2 * Math.Pow(windhilf, -0.9) + 0.32 * Program.Z0Gramm[IUstern][JUstern] + 0.18));
                            U0int = (float)Math.Max(U0int, 0.3) * Program.StdDeviationV;
                        }

                        if (Program.Ob[IUstern][JUstern] < 0)
                            varw = (Program.Pow2(Program.Ustern[IUstern][JUstern]) * Program.Pow2(1.15F + 0.1F * MathF.Pow(Program.BdLayHeight / (-Program.Ob[IUstern][JUstern]), 0.67F)) * Program.Pow2(Program.StdDeviationW));
                        else
                            varw = Program.Pow2(Program.Ustern[IUstern][JUstern] * Program.StdDeviationW * 1.25F);
                        if (TURB_MODEL != 1)
                        {
                            Program.TURB[i][j][k] = (float)(0.5 * (Program.Pow2(U0int) + Program.Pow2(V0int) + varw));
                        }
                        /*
                        if (Program.IEPS == 1)
                            Program.TDISS[i][j][k] = (float)MyPow(Program.Ustern[IUstern][JUstern], 3) / zhilf / 0.4 * MyPow(1 + 0.5 * MyPow(zhilf / Math.Abs(Program.Ob[IUstern][JUstern]), 0.67), 1.5);
                         */
                    }
                }
            });

            //Minimum vertical and horizontal turbulent exchange coefficients in dependence on atmospheric stability
            float VISHMIN = 0.00F;
            float VISVMIN = 0.00F;
            if (TURB_MODEL == 1)
            {
                VISHMIN = 0.0F;
                VISVMIN = 0.0F;
            }
            if (TURB_MODEL == 2)
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
                                if (i == NII) Program.UK[i + 1][j][k] = 0;
                                VK_L[k] = 0;
                                if (j == NJJ) Program.VK[i][j + 1][k] = 0;
                                WK_L[k] = 0;
                                if (k == Vert_index) WK_L[k + 1] = 0;
                                if (TURB_MODEL != 1)
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

            int COUNT = 1;
            int COUNT100 = 1;

            while ((IV <= INTEGRATIONSSCHRITTE_MAX) && (Program.FlowFieldLevel > 1))
            {
                if (IV == 1)
                {
                    Console.WriteLine();
                    Console.WriteLine("ADVECTION");
                }

                //double Startzeit = Environment.TickCount;

                //lateral boundary conditions at the western and eastern boundaries
                Parallel.For(2, NJJ + 1, Program.pOptions, j =>
                {
                    for (int i = 3; i < NII; ++i)
                    {
                        if (Program.ADVDOM[i][j] == 1)
                        {
                            int iM1 = i - 1;
                            int iM2 = i - 2;
                            int iP1 = i + 1;
                            float[] VK_L = Program.VK[i][j];
                            float[] WK_L = Program.WK[i][j];
                            int Vert_index = Program.VerticalIndex[i][j];

                            bool ADVDOMiM = (Program.ADVDOM[iM1][j] < 1) || (i == 3);
                            bool ADVDOMiP = (Program.ADVDOM[iP1][j] < 1) || (i == NII - 1);

                            if (ADVDOMiM || ADVDOMiP)
                            {
                                int iP2 = i + 2;
                                for (int k = 1; k <= Vert_index; ++k)
                                {
                                    if (ADVDOMiM)
                                    {
                                        float UK_L = Program.UK[iM2][j][k];
                                        if (UK_L >= 0)
                                            Program.UK[iM1][j][k] = UK_L;

                                        Program.VK[iM2][j][k] = Program.VK[iM1][j][k];
                                        Program.WK[iM2][j][k] = Program.WK[iM1][j][k];
                                    }

                                    if (ADVDOMiP)
                                    {
                                        float UK_L = Program.UK[iP2][j][k];
                                        if (UK_L <= 0)
                                            Program.UK[iP1][j][k] = UK_L;

                                        Program.VK[iP1][j][k] = VK_L[k];
                                        Program.WK[iP1][j][k] = WK_L[k];
                                    }
                                }
                            }
                        }
                    }
                });

                //lateral boundary conditions at the northern and southern boundaries
                Parallel.For(2, NII + 1, Program.pOptions, i =>
                {
                    for (int j = 3; j <= NJJ - 1; ++j)
                    {
                        if (Program.ADVDOM[i][j] == 1)
                        {
                            int jM1 = j - 1;
                            int jM2 = j - 2;
                            int jP1 = j + 1;

                            float[] UK_L = Program.UK[i][j];
                            float[] WK_L = Program.WK[i][j];
                            int Vert_index = Program.VerticalIndex[i][j];
                            bool ADVDOMjM = (Program.ADVDOM[i][jM1] < 1) || (j == 3);
                            bool ADVDOMjP = (Program.ADVDOM[i][jP1] < 1) || (j == NJJ - 1);

                            if (ADVDOMjM || ADVDOMjP)
                            {
                                int jP2 = j + 2;
                                for (int k = 1; k <= Vert_index; ++k)
                                {
                                    if (ADVDOMjM)
                                    {
                                        float VK_L = Program.VK[i][jM2][k];
                                        if (VK_L >= 0)
                                            Program.VK[i][jM1][k] = VK_L;

                                        Program.UK[i][jM2][k] = Program.UK[i][jM1][k];
                                        Program.WK[i][jM2][k] = Program.WK[i][jM1][k];
                                    }

                                    if (ADVDOMjP)
                                    {
                                        float VK_L = Program.VK[i][jP2][k];
                                        if (VK_L <= 0)
                                            Program.VK[i][jP1][k] = VK_L;

                                        Program.UK[i][jP1][k] = UK_L[k];
                                        Program.WK[i][jP1][k] = WK_L[k];
                                    }
                                }
                            }
                        }
                    }
                });

                //boundary condition at the top of the domain
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

                            for (int k = 1; k <= Vert_index; ++k)
                            {
                                if (KKART_LL < k)
                                {
                                    if (k > KKART_LL + 1)
                                        DIV_L[k] = (UK_L[k] - UKip_L[k]) * DYK * Program.DZK[k] + (VK_L[k] - VKjp_L[k]) * DXK * Program.DZK[k] + (WK_L[k] - WK_L[k + 1]) * AREAxy;
                                    else
                                        DIV_L[k] = (UK_L[k] - UKip_L[k]) * DYK * Program.DZK[k] + (VK_L[k] - VKjp_L[k]) * DXK * Program.DZK[k] - WK_L[k + 1] * AREAxy;

                                    DPM_L[k] = 0;
                                }
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

                //switch the loop to improve convergence
                int IA = 2;
                int IE = NII - 1;
                int JA = 2;
                int JE = NJJ - 1;
                int IS = 1;
                int JS = 1;
                if (COUNT == 4)
                {
                    IA = NII - 1;
                    IE = 2;
                    JA = 2;
                    JE = NJJ - 1;
                    IS = -1;
                    JS = 1;
                }
                else if (COUNT == 3)
                {
                    IA = NII - 1;
                    IE = 2;
                    JA = NJJ - 1;
                    JE = 2;
                    IS = -1;
                    JS = -1;
                }
                else if (COUNT == 2)
                {
                    IA = 2;
                    IE = NII - 1;
                    JA = NJJ - 1;
                    JE = 2;
                    IS = 1;
                    JS = -1;
                }

                DELTAUMAX = 0;
                Parallel.For(2, NJJ, Program.pOptions, j1 =>
                {
                    float DELTAUMAX1 = 0;
                    Span<float> PIMP = stackalloc float[NKK + 2];
                    Span<float> QIMP = stackalloc float[NKK + 2];
                    float APP;
                    float ABP;
                    float ATP;
                    float DIM;
                    float TERMP;

                    int j = j1;
                    if (JS == -1) j = NJJ - j1 + 1;

                    for (int i1 = 2; i1 <= NII - 1; ++i1)
                    {
                        int i = i1;
                        if (IS == -1) i = NII - i1 + 1;

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

                            for (int k = Vert_index; k >= 1; --k)
                            {
                                if (KKART_LL < k)
                                {
                                    DIM = DIV_L[k] / DTIME + (DPMim_L[k] + DPMip_L[k]) / DXK * DYK * Program.DZK[k] +
                                                  (DPMjm_L[k] - DPMjp_L[k]) / DYK * DXK * Program.DZK[k];
                                    APP = 2F * (DYK * Program.DZK[k] / DXK + DXK * Program.DZK[k] / DYK + AREAxy / Program.DZK[k]);

                                    if (k > 1)
                                        ABP = AREAxy / (0.5F * (Program.DZK[k - 1] + Program.DZK[k]));
                                    else
                                        ABP = AREAxy / Program.DZK[k];

                                    ATP = AREAxy / (0.5F * (Program.DZK[k + 1] + Program.DZK[k]));
                                    TERMP = 1 / (APP - ATP * PIMP[k + 1]);
                                    PIMP[k] = ABP * TERMP;
                                    QIMP[k] = (ATP * QIMP[k + 1] + DIM) * TERMP;
                                }
                            }

                            //Obtain new P-components
                            //DPM_L[0] = 0;
                            for (int k = 1; k <= Vert_index; ++k)
                            {
                                if (KKART_LL < k)
                                {
                                    float temp = PIMP[k] * DPM_L[k - 1] + QIMP[k];
                                    DELTAUMAX1 = (float)Math.Max(DELTAUMAX1, Math.Abs(DPM_L[k] - Program.ConvToInt(temp * Frund) / Frund));
                                    DPM_L[k] = temp;

                                    //Pressure field to compute pressure gradients in the momentum equations
                                    DPMNEW_L[k] += relaxp * DPM_L[k];
                                }
                            }
                        }
                    }
                    lock (obj)
                    {
                        if (DELTAUMAX1 > DELTAUMAX)
                            Interlocked.Exchange(ref DELTAUMAX, DELTAUMAX1);
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
                                    UK_L[k] = (float)(Program.ConvToInt((UK_L[k] + (DPMim_L[k] - DPM_L_K) / DXK * DTIME) * Frund) / Frund);

                                if (j > 2 && (KKART_LL < k) && (KKARTjM < k))
                                    VK_L[k] = (float)(Program.ConvToInt((VK_L[k] + (DPMjm_L[k] - DPM_L_K) / DYK * DTIME) * Frund) / Frund);

                                if (KKART_LL_P1 < k)
                                    WK_L[k] = (float)(Program.ConvToInt((WK_L[k] + (DPM_L[k - 1] - DPM_L_K) / (Program.DZK[k] + Program.DZK[k - 1]) * 2 * DTIME) * Frund) / Frund);
                                else
                                    WK_L[k] = 0;
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
                                KSTART = KKART_LL_P1;

                            for (int k = KSTART; k <= Vert_index; ++k)
                            {
                                if (KKART_LL < k)
                                {
                                    UKS_L[k] = 0.5F * (UK_L[k] + UKip_L[k]);
                                    VKS_L[k] = 0.5F * (VK_L[k] + VKjp_L[k]);
                                }
                                if (KKART_LL_P1 < k)
                                    WKS_L[k] = 0.5F * (WK_L[k] + WK_L[k + 1]);

                                if (j == 2) Program.UKS[i][1][k] = Program.UKS[i][2][k];
                                if (j == NJJ - 1) Program.UKS[i][NJJ][k] = Program.UKS[i][NJJ - 1][k];
                            }
                        }
                    }
                });

                for (int j = 2; j < NJJ; ++j) // AVOID False Sharing! No Parallelization here
                {
                    for (int k = 0; k <= NKK; ++k)
                    {
                        Program.VKS[1][j][k] = Program.VKS[2][j][k];
                        Program.VKS[NII][j][k] = Program.VKS[NII - 1][j][k];
                    }
                }

                //TIME_dispersion = (Environment.TickCount - Startzeit) * 0.001;
                //Console.WriteLine("Part 5: " + TIME_dispersion.ToString("0.000"));
                //Startzeit = Environment.TickCount;

                //MOMENTUM EQUATIONS FOR THE THREE WIND-COMPONENTS AND
                //MOMENTUM EQUATION FOR THE TURBULENT KINETIC ENERGY AND DISSIPATION RATE

                //no-diffusion
                if (TURB_MODEL == 0)
                {
                    Parallel.Invoke(Program.pOptions,
                        () => U_PrognosticMicroscaleV0.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, UG, building_Z0, relax),
                        () => V_PrognosticMicroscaleV0.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, VG, building_Z0, relax));
                    W_PrognosticMicroscaleV0.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, building_Z0, relax);
                }

                //algebraic mixing length model
                if (TURB_MODEL == 1)
                {
                    Parallel.Invoke(Program.pOptions,
                        () => U_PrognosticMicroscaleV1.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, UG, building_Z0, relax),
                        () => V_PrognosticMicroscaleV1.Calculate(-IS, -JS, Cmueh, VISHMIN, AREAxy, VG, building_Z0, relax));
                    W_PrognosticMicroscaleV1.Calculate(IS, JS, Cmueh, VISHMIN, AREAxy, building_Z0, relax);
                }

                //k-eps model
                if (TURB_MODEL == 2)
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
                if (IV == INTEGRATIONSSCHRITTE_MAX) DELTALAST = (float)(DELTAMEAN / Program.Pow2(topwind));
                if ((COUNT100 == 0) && (IV > 1) && (Program.FlowFieldLevel > 1))
                {
                    if (IV == 100) DELTAFIRST = (float)(DELTAMEAN / Program.Pow2(topwind));
                    Console.WriteLine("ITERATION " + IV.ToString() + ": " + (DELTAMEAN / Program.Pow2(topwind)).ToString("0.000"));
                    if ((DELTAMEAN / Program.Pow2(topwind) < 0.012) && (IV >= INTEGRATIONSSCHRITTE_MIN))
                        break;
                    DELTAMEAN = 0;
                }
                else
                {
                    DELTAMEAN += DELTAUMAX;
                }

                //ONLINE OUTPUT OF GRAL FLOW FIELDS
                if ((COUNT100 == 0) && (Program.FlowFieldLevel > 1))
                {
                    GRALONLINE.Output(NII, NJJ, NKK);
                }

                IV++;
                COUNT++;
                COUNT100++;
                if (COUNT > 4) COUNT = 1;
                if (COUNT100 >= 100) COUNT100 = 0;

                //double TIME_dispersion = (Environment.TickCount - Startzeit) * 0.001;
                //Console.WriteLine("Total time: " + TIME_dispersion.ToString("0.000"));
            }

            //OUTPUT OF ENCOUNTERED PROBLEMS DURING THE SIMULATION
            if (DELTALAST > DELTAFIRST)
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
                        for (int k = 1; k <= Math.Min(NKK - 1, Program.KKART[i][j] + MAXHOEH); ++k)
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

            Console.WriteLine("Total number of iterations: " + IV.ToString());

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


                IV = 1;
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
                            varw = Program.Pow2(Program.Ustern[1][1]) * Program.Pow2(1.15 + 0.1 * Math.Pow(Program.BdLayHeight / (-Program.Ob[1][1]), 0.67)) * Program.Pow2(Program.StdDeviationW);
                        if (TURB_MODEL != 1)
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
