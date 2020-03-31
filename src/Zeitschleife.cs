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
using System.Runtime.CompilerServices;
using System.Threading;

namespace GRAL_2001
{
    partial class Zeitschleife
    {
        private const float sqrt2F = 1.4142135623731F;
        private const float sqrtPiF = 1.77245385090552F;

        /// <summary>
        ///Time loop for all released particles - the particles are tracked until they leave the domain area(steady state mode)
        ///or until the dispersion time has expired (transient mode) 
        /// </summary>
        /// <param name="nteil">Particle number</param>
        /// /// <remarks>
        /// This class is the particle driver for particles, released by a source in steady state and transient GRAL mode
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Calculate(int nteil)
        {
            Object thisLock = new Object();
            //random number generator seeds
            Random RND = new Random();
            uint m_w = (uint)(RND.Next() + 521288629);
            uint m_z = (uint)(RND.Next() + 2232121);
            RND = null;

            float zahl1 = 0;
            uint u_rg = 0;
            double u1_rg = 0;

            const float Pi2F = 2F * MathF.PI;

            const float RNG_Const = 2.328306435454494e-10F;

            double xcoord_nteil = Program.Xcoord[nteil];
            double ycoord_nteil = Program.YCoord[nteil];
            float zcoord_nteil = Program.ZCoord[nteil];
            double masse = Program.ParticleMass[nteil];

            //get index of internal source group number
            int SG_nteil = Program.ParticleSG[nteil];
            for (int i = 0; i < Program.SourceGroups.Count; i++)
            {
                if (SG_nteil == Program.SourceGroups[i])
                {
                    SG_nteil = i;
                    break;
                }
            }

            // deposition parameters
            int Deposition_type = Program.ParticleMode[nteil]; // 0 = no deposition, 1 = depo + conc, 2 = only deposition
            float vsed = Program.ParticleVsed[nteil];
            float vdep = Program.ParticleVdep[nteil];
            double area_rez_fac = Program.TAUS * Program.GridVolume * 24 / 1000; // conversion factor for particle mass from µg/m³/s to mg per day
            area_rez_fac = area_rez_fac / (Program.GralDx * Program.GralDy);  // conversion to mg/m²

            double xtun = 0;
            double ytun = 0;
            float meanhurly = 0;
            float Ghurly = 0;
            float Fhurly = 0;
            float Mhurly = 0;
            float Rhurly = 0;
            float wp = 0;
            float up = 0;
            float DXK = Program.DXK;
            float DYK = Program.DYK;
            float DXK_rez = 1 / Program.DXK;
            float DYK_rez = 1 / Program.DYK;
            float DDX1_rez = 1 / Program.DDX[1];
            float DDY1_rez = 1 / Program.DDY[1];
            float dx_rez = 1 / Program.GralDx;
            float dy_rez = 1 / Program.GralDy;
            float dz_rez = 1 / Program.GralDz;
            float dxHalf = Program.GralDx * 0.5F;
            float dyHalf = Program.GralDy * 0.5F;

            int IFQ = Program.TS_Count;
            int Kenn_NTeil = Program.ParticleSource[nteil];       // number of source
            if (Kenn_NTeil == 0) return;
            int SourceType = Program.SourceType[nteil]; // sourcetype
            float blh = Program.BdLayHeight;
            int IKOOAGRAL = Program.IKOOAGRAL;
            int JKOOAGRAL = Program.JKOOAGRAL;

            Span<int> kko = stackalloc int[Program.NS];
            double[] ReceptorConcentration = new double[Program.ReceptorNumber + 1];
            float a1 = 0.05F, a2 = 1.7F, a3 = 1.1F;

            int reflexion_flag = 0;
            int ISTATISTIK = Program.IStatistics;
            int ISTATIONAER = Program.ISTATIONAER;
            int topo = Program.Topo;

            int IndexI3d = 1;
            int IndexJ3d = 1;
            int IndexK3d = 1;
            Boolean emission_timeseries = Program.EmissionTimeseriesExist;
            int SG_MaxNumb = Program.SourceGroups.Count;

            double Area_cart = Program.DXK * Program.DYK;

            int reflexion_number = 0; 		  // counter to limit max. number of reflexions
            int timestep_number = 0;         // counter for the time-steps LOG-output

            double decay_rate = Program.DecayRate[Program.ParticleSG[nteil]]; // decay rate for real source group number of this source

            TransientConcentration trans_conz = new TransientConcentration();

            //for transient simulations dispersion time needs to be equally distributed from zero to TAUS
            float tgesamt = Program.DispTimeSum;
            if (ISTATIONAER == 0)
            {
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                uint u9 = (m_z << 16) + m_w;
                float zuff1 = (float)((u9 + 1.0) * 2.328306435454494e-10);
                tgesamt = zuff1 * tgesamt;

                //modulate emission
                if (emission_timeseries == true)
                {
                    if (Program.IWET <= Program.EmFacTimeSeries.GetUpperBound(0) && SG_nteil <= Program.EmFacTimeSeries.GetUpperBound(1))
                    {
                        masse *= Program.EmFacTimeSeries[Program.IWET - 1, SG_nteil];
                    }

                    if (masse <= 0)
                    {
                        goto REMOVE_PARTICLE;
                    }
                }
                //try
                {
                    lock (Program.EmissionPerSG)
                    {
                        Program.EmissionPerSG[SG_nteil] += masse;
                    }
                }
                //catch{}
            }

            /*
			 *   INITIIALIZING PARTICLE PROPERTIES -> DEPEND ON SOURCE CATEGORY
			 */
            int IUstern = 1, JUstern = 1;
            float AHint = 0;
            float AHintold = 0;
            float varw = 0, sigmauhurly = 0;
            double xturb = 0;  // entrainment turbulence for tunnel jet streams in x-direction
            double yturb = 0;  // entrainment turbulence for tunnel jet streams in y-direction

            //advection time step
            float idt = 0.1F;
            float auszeit = 0;

            //flag indicating if particle belongs to a tunnel portal
            int tunfak = 1;

            float DSIGUDX = 0, DSIGUDY = 0, DSIGVDX = 0, DSIGVDY = 0;
            float DUDX = 0, DVDX = 0, DUDY = 0, DVDY = 0;

            //cell-indices of particles moving in the microscale flow field
            double xsi = xcoord_nteil - IKOOAGRAL;
            double eta = ycoord_nteil - JKOOAGRAL;
            double xsi1 = xcoord_nteil - Program.IKOOA;
            double eta1 = ycoord_nteil - Program.JKOOA;
            if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
            {
                goto REMOVE_PARTICLE;
            }

            int Max_Loops = (int)(3E6 + Math.Min(9.7E7,
                Math.Max(Math.Abs(Program.EtaMaxGral - Program.EtaMinGral), Math.Abs(Program.XsiMaxGral - Program.XsiMinGral)) * 1000)); // max. Loops 1E6 (max. nr. of reflexions) + 2E6 Min + max(x,y)/0.001 

            int Max_Reflections = Math.Min(1000000, Program.NII * Program.NJJ * 10); // max. 10 reflections per cell

            int IndexI;
            int IndexJ;
            int IndexId;
            int IndexJd;

            //interpolated orography
            float differenz = 0;
            float diff_building = 0;

            if (topo == 1)
            {
                IndexI = (int)(xsi * DXK_rez) + 1;
                IndexJ = (int)(eta * DYK_rez) + 1;
                if ((IndexI < 1) || (IndexI > Program.NII) || (IndexJ < 1) || (IndexJ > Program.NJJ))
                    goto REMOVE_PARTICLE;

                IUstern = (int)(xsi1 * DDX1_rez) + 1;
                JUstern = (int)(eta1 * DDY1_rez) + 1;
                if ((IUstern > Program.NX) || (JUstern > Program.NY) || (IUstern < 1) || (JUstern < 1))
                    goto REMOVE_PARTICLE;

                IndexId = IndexI;
                IndexJd = IndexJ;

                //interpolated orography
                AHint = Program.AHK[IndexI][IndexJ];

                differenz = zcoord_nteil - AHint;
                diff_building = differenz;
            }
            else
            {
                IndexI = (int)(xsi1 * DDX1_rez) + 1;
                IndexJ = (int)(eta1 * DDY1_rez) + 1;
                if ((IndexI < 1) || (IndexI > Program.NI) || (IndexJ < 1) || (IndexJ > Program.NJ))
                    goto REMOVE_PARTICLE;

                IndexId = (int)(xsi * DXK_rez) + 1;
                IndexJd = (int)(eta * DYK_rez) + 1;
                if ((IndexId < 1) || (IndexId > Program.NII) || (IndexJd < 1) || (IndexJd > Program.NJJ))
                    goto REMOVE_PARTICLE;

                //interpolated orography
                differenz = zcoord_nteil;
                diff_building = differenz;
            }

            //wind-field interpolation
            float UXint = 0, UYint = 0, UZint = 0;
            int IndexK = 1;
            IntWindCalculate(IndexI, IndexJ, IndexId, IndexJd, AHint, ref UXint, ref UYint, ref UZint, ref IndexK, xcoord_nteil, ycoord_nteil, zcoord_nteil);
            float windge = MathF.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint));

            if (windge == 0) windge = 0.01F;

            //variables tunpa and aufhurly needs to be defined even if not used
            float tunpa = 0;
            float aufhurly = 0;

            //vertical interpolation of horizontal standard deviations of wind component fluctuations between observations
            float U0int = 0;
            float V0int = 0;
            IntStandCalculate(nteil, IUstern, JUstern, diff_building, windge, sigmauhurly, ref U0int, ref V0int);

            //remove particles above boundary-layer
            if ((diff_building > blh) && (Program.Ob[IUstern][JUstern] < 0))
                goto REMOVE_PARTICLE;

            //initial turbulent velocities
            float velxold = 0;
            float velyold = 0;
            float velzold = 0;

            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
            u_rg = (m_z << 16) + m_w;
            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
            u_rg = (m_z << 16) + m_w;
            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

            velxold = U0int * zahl1;

            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
            u_rg = (m_z << 16) + m_w;
            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
            u_rg = (m_z << 16) + m_w;
            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

            velyold = V0int * zahl1;

            //dispersion time of a particle
            auszeit = 0;

            //initial properties for particles stemming from point sources
            if (SourceType == 0)
            {
                float ExitVelocity = Program.PS_V[Kenn_NTeil];
                int velTimeSeriesIndex = Program.PS_TimeSeriesVelocity[Kenn_NTeil];
                if (velTimeSeriesIndex != -1) // Time series for exit velocity
                {
                    if ((Program.IWET - 1) < Program.PS_TimeSerVelValues[velTimeSeriesIndex].Value.Length)
                    {
                        ExitVelocity = Program.PS_TimeSerVelValues[velTimeSeriesIndex].Value[Program.IWET - 1];
                    }
                }

                float ExitTemperature = Program.PS_T[Kenn_NTeil];
                int tempTimeSeriesIndex = Program.PS_TimeSeriesTemperature[Kenn_NTeil];
                if (tempTimeSeriesIndex != -1) // Time series for exit temperature
                {
                    if ((Program.IWET - 1) < Program.PS_TimeSerTempValues[tempTimeSeriesIndex].Value.Length)
                    {
                        ExitTemperature = Program.PS_TimeSerTempValues[tempTimeSeriesIndex].Value[Program.IWET - 1] + 273;
                    }
                }

                Fhurly = (9.81F * ExitVelocity * Program.Pow2(Program.PS_D[Kenn_NTeil] * 0.5F) *
                                 (ExitTemperature - 273F) / ExitTemperature);
                Ghurly = (273 / ExitTemperature * ExitVelocity * Program.Pow2(Program.PS_D[Kenn_NTeil] * 0.5F));
                Mhurly = Ghurly * ExitVelocity;
                Rhurly = MathF.Sqrt(ExitVelocity / MathF.Sqrt(Program.Pow2(windge) + Program.Pow2(ExitVelocity)));
                wp = ExitVelocity;
                up = MathF.Sqrt(Program.Pow2(windge) + Program.Pow2(wp));
                meanhurly = 0;
                aufhurly = 0;
            }

            //initial properties for particles stemming from tunnel portals
            double vtunx = 0; double vtuny = 0; float tunbe = 0; double tuncos = 0; double tunsin = 0; double tunqu = 0;
            double tunx = 0; double tuny = 0; float tunbreitalt = 0; float tunbreit = 0; float yteilchen = 0;
            double yneu = 0; double xneuy = 0; double yneuy = 0; float zturb = 0; double yold = 0;
            float coruri = 0; float corqri = 0; float coswind = 0; float sinwind = 0; float UXneu = 0;
            float UYneu = 0; float xrikor = 0; float yrikor = 0; float buoy = 0; float B = 0;
            float HydrD = 0; float TLT0 = 0; float constA = 0.35F; float faktzeit = 1; float geschwalt = 0; float geschwneu = 0;

            float TS_ExitTemperature = 0;
            float TS_ExitVelocity = 0;
            if (SourceType == 1) // Tunnel portals
            {
                TS_ExitVelocity = Program.TS_V[Kenn_NTeil];
                int velTimeSeriesIndex = Program.TS_TimeSeriesVelocity[Kenn_NTeil];
                if (velTimeSeriesIndex != -1) // Time series for exit velocity
                {
                    if ((Program.IWET - 1) < Program.TS_TimeSerVelValues[velTimeSeriesIndex].Value.Length)
                    {
                        TS_ExitVelocity = Program.TS_TimeSerVelValues[velTimeSeriesIndex].Value[Program.IWET - 1];
                    }
                }

                TS_ExitTemperature = Program.TS_T[Kenn_NTeil];
                int tempTimeSeriesIndex = Program.TS_TimeSeriesTemperature[Kenn_NTeil];
                if (tempTimeSeriesIndex != -1) // Time series for exit temperature
                {
                    if ((Program.IWET - 1) < Program.TS_TimeSerTempValues[tempTimeSeriesIndex].Value.Length)
                    {
                        TS_ExitTemperature = Program.TS_TimeSerTempValues[tempTimeSeriesIndex].Value[Program.IWET - 1];
                    }
                }

                vtunx = TS_ExitVelocity * (Program.TS_Y2[Kenn_NTeil] - Program.TS_Y1[Kenn_NTeil]) / Program.TS_Width[Kenn_NTeil];
                vtuny = TS_ExitVelocity * (Program.TS_X1[Kenn_NTeil] - Program.TS_X2[Kenn_NTeil]) / Program.TS_Width[Kenn_NTeil];
                tunbe = (float)Math.Sqrt(Program.Pow2(vtunx) + Program.Pow2(vtuny));
                tuncos = vtunx / (tunbe + 0.01F);
                tunsin = vtuny / (tunbe + 0.01F);
                tunqu = 0;
                tunpa = tunbe;
                tunfak = 0;
                tunx = tunpa * tuncos - tunqu * tunsin;
                tuny = tunpa * tunsin + tunqu * tuncos;

                tunbreitalt = Program.FloatMax(Program.TS_Width[Kenn_NTeil], 0.00001F);
                tunbreit = tunbreitalt;
                xtun = 0.5F * (Program.TS_X1[Kenn_NTeil] + Program.TS_X2[Kenn_NTeil]);
                ytun = 0.5F * (Program.TS_Y1[Kenn_NTeil] + Program.TS_Y2[Kenn_NTeil]);

                //relative particle position to the jet stream centre-line
                yteilchen = (float)(-xcoord_nteil * tunsin + ycoord_nteil * tuncos + xtun * tunsin - ytun * tuncos);
                yold = yteilchen;
                yneu = yold;
                xneuy = xtun - yteilchen * tunsin;
                yneuy = ytun + yteilchen * tuncos;
                zturb = 0;

                //influence of changing wind directions on the position of the jet-stream centre-line
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                coruri = windge + U0int * zahl1;

                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                corqri = V0int * zahl1;

                coswind = UXint / windge;
                sinwind = UYint / windge;
                UXneu = coruri * coswind - corqri * sinwind;
                UYneu = coruri * sinwind + corqri * coswind;
                if (UXint == 0) UXint = 0.001F;
                if (UYint == 0) UYint = 0.001F;
                xrikor = UXneu / UXint;
                yrikor = UYneu / UYint;

                //bouyancy of the tunnel jet
                buoy = 0;
                B = TS_ExitTemperature / 283 * 9.81F;
                HydrD = 4 * Program.TS_Height[Kenn_NTeil] * Program.TS_Width[Kenn_NTeil] / (2 * (Program.TS_Height[Kenn_NTeil] + Program.TS_Width[Kenn_NTeil]));
                if (B >= 0)
                {
                    TLT0 = MathF.Sqrt(HydrD / (1.8F * (B + 0.00001F)));
                }
                else
                {
                    B = 0;
                }

                //double dummy = corqri + coruri + coswind + sinwind + UXneu + UYneu + xrikor + yrikor + B + HydrD + TLT0;

                AHintold = AHint;
            }

            int depo_reflection_counter = -5; // counter to ensure, a particle moves top to down
            bool TerrainStepAllowed = true;   //19.05.25 Ku: Flag, that one step is allowed

        /*
         *      LOOP OVER THE TIME-STEPS
         */
        MOVE_FORWARD:
            //for (int izeit = 1; izeit <= 100000000; izeit++)
            while (timestep_number <= Max_Loops)
            {
                ++timestep_number;

                //if particle is within the user-defined tunnel-entrance zone it is removed (sucked-into the tunnel)
                if (Program.TunnelEntr == true)
                    if ((Program.TUN_ENTR[IndexId][IndexJd] == 1) && (differenz <= 5))
                        goto REMOVE_PARTICLE;

                //interpolate wind field
                IntWindCalculate(IndexI, IndexJ, IndexId, IndexJd, AHint, ref UXint, ref UYint, ref UZint, ref IndexK, xcoord_nteil, ycoord_nteil, zcoord_nteil);
                windge = MathF.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint));
                if (windge == 0) windge = 0.01F;

                //particles below the surface or buildings are removed
                if (differenz < 0)
                    goto REMOVE_PARTICLE;

                /*
				 *   VERTICAL DIFFUSION ACCORING TO FRANZESE, 1999
				 */

                //dissipation
                float ObLength = Program.Ob[IUstern][JUstern];
                float Ustern = Program.Ustern[IUstern][JUstern];

                float rough = Program.Z0Gramm[IUstern][JUstern];
                if (rough < diff_building)
                    rough = diff_building;

                float eps = Program.Pow3(Ustern) / rough / 0.4F *
                                    MathF.Pow(1F + 0.5F * MathF.Pow(diff_building / Math.Abs(ObLength), 0.8F), 1.8F);
                /*
                float eps = (float)(Program.Pow3(Ustern) / Program.fl_max(diff_building, Program.Z0GRAMM[IUstern][JUstern]) / 0.4F *
                                    Math.Pow(1 + 0.5F * Math.Pow(diff_building / Math.Abs(ObLength), 0.8), 1.8));
                 */

                depo_reflection_counter++;
                float W0int = 0; float varw2 = 0; float skew = 0; float curt = 0; float dskew = 0;
                float wstern = Ustern * 2.9F;
                float vert_blh = diff_building / blh;
                float hterm = (1 - vert_blh);
                float varwu = 0; float dvarw = 0; float dcurt = 0;

                if (ObLength >= 0)
                {
                    if (ISTATISTIK != 3)
                        W0int = Ustern * Program.StdDeviationW * 1.25F;
                    else
                        W0int = Program.W0int;
                    varw = Program.Pow2(W0int);
                    varw2 = Program.Pow2(varw);
                    curt = 3 * varw2;
                }
                else
                {
                    if (ISTATISTIK == 3)
                    {
                        varw = Program.Pow2(Program.W0int);
                    }
                    else
                    {
                        varw = Program.Pow2(Ustern) * Program.Pow2(1.15F + 0.1F * MathF.Pow(blh / (-ObLength), 0.67F)) * Program.Pow2(Program.StdDeviationW);
                    }
                    varw2 = Program.Pow2(varw);

                    if ((diff_building < Program.BdLayH10) || (diff_building >= blh))
                    {
                        skew = 0;
                        dskew = 0;
                    }
                    else
                    {
                        skew = a3 * diff_building * Program.Pow2(hterm) * Program.Pow3(wstern) / blh;
                        dskew = a3 * Program.Pow3(wstern) / blh * Program.Pow2(hterm) - 2 * a3 * Program.Pow3(wstern) * diff_building * hterm / Program.Pow2(blh);
                    }

                    if (diff_building < Program.BdLayH10)
                    {
                        curt = 3 * varw2;
                    }
                    else
                    {
                        curt = 3.5F * varw2;
                    }
                }

                //time-step calculation
                if (tunfak == 1)
                {
                    idt = 0.1F * varw / eps;
                    //in case plume rise the time step shall not exceed 0.2s in the first 20 seconds
                    if (SourceType == 0 && auszeit < 20) idt = MathF.Min(0.2F, idt);

                    float idtt = 0.5F * Math.Min(Program.GralDx, DXK) / (windge + MathF.Sqrt(Program.Pow2(velxold) + Program.Pow2(velyold)));
                    if (idtt < idt) idt = idtt;
                    Math.Clamp(idt, 0.1F, 4F);
                    /*
					idt = (float)Math.Min(idt, 0.5F * Math.Min(Program.GralDx, DXK) / (windge + Math.Sqrt(Program.Pow2(velxold) + Program.Pow2(velyold))));
					idt = (float)Math.Min(4, idt);
                    idt = (float)Program.fl_max(0.1F, idt);
                     */
                }

                float dummyterm = curt - Program.Pow2(skew) / varw - varw2;
                if (dummyterm == 0) dummyterm = 0.000001F;
                float alpha = (0.3333F * dcurt - skew / (2 * varw) * (dskew - Program.C0z * eps) - varw * dvarw) / dummyterm;
                //alpha needs to be bounded to keep algorithm stable
                Math.Clamp(alpha, -0.1F, 0.1F);

                /*
				alpha = (float)Math.Min(alpha, 0.1F);
                alpha = (float)Program.fl_max(alpha, -0.1F);
                 */

                float beta = (dskew - 2 * skew * alpha - Program.C0z * eps) / (2 * varw);

                float gamma = dvarw - varw * alpha;

                //particle acceleration
                float acc = alpha * Program.Pow2(velzold) + beta * velzold + gamma;

                //new vertical wind speed
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                //float zahl1 = (float)SimpleRNG.GetNormal();
                if (zahl1 > 2) zahl1 = 2;
                if (zahl1 < -2) zahl1 = -2;
                float velz = acc * idt + MathF.Sqrt(Program.C0z * eps * idt) * zahl1 + velzold;

                //******************************************************************************************************************** OETTL, 31 AUG 2016
                //in adjecent cells to vertical solid walls, turbulent velocities are only allowed in the direction away from the wall
                if (velz == 0)
                    velz = 0.01F;

                short _kkart = Program.KKART[IndexI][IndexJ];
                //solid wall west of the particle
                if ((Program.KKART[IndexI - 1][IndexJ] > _kkart) && (IndexK <= Program.KKART[IndexI - 1][IndexJ]) && (Program.CUTK[IndexI - 1][IndexJ] > 0))
                {
                    velz += velz / MathF.Abs(velz) * 0.67F * Program.FloatMax(-Program.UK[IndexI + 1][IndexJ][IndexK], 0);
                }
                //solid wall east of the particle
                if ((Program.KKART[IndexI + 1][IndexJ] > _kkart) && (IndexK <= Program.KKART[IndexI + 1][IndexJ]) && (Program.CUTK[IndexI + 1][IndexJ] > 0))
                {
                    velz += velz / MathF.Abs(velz) * 0.67F * Program.FloatMax(Program.UK[IndexI][IndexJ][IndexK], 0);
                }
                //solid wall south of the particle
                if ((Program.KKART[IndexI][IndexJ - 1] > _kkart) && (IndexK <= Program.KKART[IndexI][IndexJ - 1]) && (Program.CUTK[IndexI][IndexJ - 1] > 0))
                {
                    velz += velz / MathF.Abs(velz) * 0.67F * Program.FloatMax(-Program.VK[IndexI][IndexJ + 1][IndexK], 0);
                }
                //solid wall north of the particle
                if ((Program.KKART[IndexI][IndexJ + 1] > _kkart) && (IndexK <= Program.KKART[IndexI][IndexJ + 1]) && (Program.CUTK[IndexI][IndexJ + 1] > 0))
                {
                    velz += velz / MathF.Abs(velz) * 0.67F * Program.FloatMax(Program.VK[IndexI][IndexJ][IndexK], 0);
                }
                //******************************************************************************************************************** OETTL, 31 AUG 2016

                if (ObLength >= 0)
                {
                    Math.Clamp(velz, -3, 3);
                }
                else
                {
                    Math.Clamp(velz, -5, 5);
                }

                //bouyancy effects for plume rise
                if (SourceType == 0)
                {
                    //plume rise following Hurley, 2005 (TAPM)
                    float epshurly = 1.5F * Program.Pow3(wp) / (meanhurly + 0.1F);
                    float epsambiente = 0;

                    aufhurly = 0;
                    sigmauhurly = 0;

                    epsambiente = eps;

                    if (epshurly <= epsambiente) wp = 0;

                    if (wp > 0)
                    {
                        float stab = 0;
                        float ahurly = 0.1F;
                        float bhurly = 0.6F;
                        if (ObLength >= 0)
                        {
                            stab = 0.04F * MathF.Pow(2.73F, -ObLength * 0.05F);
                        }

                        //plume-rise velocity
                        float standwind = windge * 0.31F + 0.25F;

                        m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                        m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                        u_rg = (m_z << 16) + m_w;
                        u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                        m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                        m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                        u_rg = (m_z << 16) + m_w;
                        zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                        float fmodul = 1 + standwind * zahl1;
                        fmodul = Program.FloatMax(fmodul, 0);
                        float shurly = 9.81F / 273 * stab;

                        Ghurly += 2 * Rhurly * (ahurly * Program.Pow2(wp) + bhurly * windge * fmodul * wp
                                                        + 0.1F * up * (MathF.Sqrt(0.5F * (Program.Pow2(velxold) + Program.Pow2(velyold))))) * idt;
                        Fhurly += -shurly * Mhurly / up * (1 / 2.25F * windge * fmodul + wp) * idt;
                        Mhurly += Fhurly * idt;
                        {
                            Rhurly = MathF.Sqrt((Ghurly + Fhurly / 9.8F) / up);
                            up = MathF.Sqrt(Program.Pow2(windge * fmodul) + Program.Pow2(wp));
                        }
                        float sigmawphurly = (ahurly * Program.Pow2(wp) + bhurly * windge * fmodul * wp) / 4.2F / up;
                        sigmauhurly = 2 * sigmawphurly;
                        float wpold = wp;
                        wp = Program.FloatMax(Mhurly / Ghurly, 0);
                        float wpmittel = (wp + wpold) * 0.5F;
                        meanhurly += wpmittel * idt;

                        m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                        m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                        u_rg = (m_z << 16) + m_w;
                        u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                        m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                        m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                        u_rg = (m_z << 16) + m_w;
                        zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                        aufhurly = (wpmittel + zahl1 * sigmawphurly) * idt;
                    }

                    if (tunfak == 1) zcoord_nteil += (velz + UZint) * idt + aufhurly;
                    velzold = velz;
                }
                else
                {
                    if (tunfak == 1) zcoord_nteil += (velz + UZint) * idt;
                    velzold = velz;
                }

                if (Deposition_type > 0 && vsed > 0) // compute sedimentation for this particle
                {
                    zcoord_nteil -= vsed * idt;
                }

                //control dispersion time of particles
                auszeit += idt;
                if (ISTATIONAER == 0)
                {
                    if (auszeit >= tgesamt)
                    {
                        //particles from point sources with exit velocities are tracked until this velocity becomes almost zero
                        if (SourceType == 0)
                        {
                            if (wp < 0.01)
                            {
                                trans_conz.Conz5dZeitschleife(reflexion_flag, zcoord_nteil, AHint, masse, Area_cart, idt, xsi, eta, SG_nteil);
                                goto REMOVE_PARTICLE;
                            }
                        }
                        //particles from portal sources with exit velocities are tracked until this velocity became zero
                        else if (SourceType == 1)
                        {
                            if (tunfak == 1)
                            {
                                trans_conz.Conz5dZeitschleife(reflexion_flag, zcoord_nteil, AHint, masse, Area_cart, idt, xsi, eta, SG_nteil);
                                goto REMOVE_PARTICLE;
                            }
                        }
                        else
                        {
                            trans_conz.Conz5dZeitschleife(reflexion_flag, zcoord_nteil, AHint, masse, Area_cart, idt, xsi, eta, SG_nteil);
                            goto REMOVE_PARTICLE;
                        }
                    }
                }

                //remove particles above maximum orography
                if (zcoord_nteil >= Program.ModelTopHeight)
                    goto REMOVE_PARTICLE;

                //new space coordinates due to turbulent movements
                float corx = velxold * idt;
                float cory = velyold * idt;

                /*
				 *    HORIZONTAL DIFFUSION ACCORDING TO ANFOSSI ET AL. (2006)
				 */

                //vertical interpolation of horizontal standard deviations of wind speed components
                IntStandCalculate(nteil, IUstern, JUstern, diff_building, windge, sigmauhurly, ref U0int, ref V0int);

                //determination of the meandering parameters according to Oettl et al. (2006)
                float param = 0;
                if (ISTATISTIK != 3)
                    param = Program.FloatMax(8.5F / Program.Pow2(windge + 1), 0F);
                //param = (float)Math.Max(8.5F / Program.Pow2(windge + 1), 0);
                else
                    param = Program.MWindMEander;

                float T3 = 0;
                float termp = 0;
                float termq = 0;
                if (windge >= 2.5F || Program.MeanderingOff || param == 0 || Program.TAUS <= 600)
                {
                    T3 = 2 * Program.Pow2(V0int) / Program.C0x / eps;
                    termp = 1 / T3;
                }
                else
                {
                    termp = 1 / (2 * Program.Pow2(V0int) / Program.C0x / eps);
                    if (ISTATISTIK != 3)
                    {
                        float Tstern = 200 * param + 350;
                        T3 = Tstern / 6.28F / (Program.Pow2(param) + 1) * param;
                    }
                    else if (ISTATISTIK == 3)
                        T3 = Program.TWindMeander;

                    termq = param / (Program.Pow2(param) + 1) / T3;
                }

                //random numbers for horizontal turbulent velocities
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                float zuff1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                //float zuff1 = (float)SimpleRNG.GetNormal();
                Math.Clamp(zuff1, -2, 2);

                //random numbers for horizontal turbulent velocities
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                float zuff2 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);
                Math.Clamp(zuff2, -2, 2);

                float velX = velxold + UXint;
                float velY = velyold + UYint;
                //horizontal Langevin equations
                {
                    float velx = velxold - (termp * velxold + termq * velyold) * idt + U0int * MathF.Sqrt(2 * termp * idt) * zuff1
                    + idt * (DUDX * velX + DUDY * velY + U0int * DSIGUDX)
                    + idt * (velxold / U0int * (DSIGUDX * velX + DSIGUDY * velY));

                    float vely = velyold - (termp * velyold - termq * velxold) * idt + V0int * MathF.Sqrt(2 * termp * idt) * zuff2
                    + idt * (DVDX * velX + DVDY * velY + V0int * DSIGVDY)
                    + idt * (velyold / V0int * (DSIGVDX * velX + DSIGVDY * velY));
                    velxold = velx;
                    velyold = vely;
                }

                //update coordinates
                if (tunfak == 1)
                {
                    xcoord_nteil += idt * UXint + corx;
                    ycoord_nteil += idt * UYint + cory;
                }

                /*
				 *    TUNNEL MODULE
				 */
                if (tunfak == 0)
                {
                    //switch to standard dispersion
                    if ((Math.Abs(UXint - tunx) < 0.01) && (Math.Abs(UYint - tuny) < 0.01))
                    {
                        tunfak = 1;
                    }
                    if (tunpa <= 0) tunfak = 1;

                    //particles within user-defined opposite lane are treated with standard dispersion (tunnel jet is destroyed there)
                    if (Program.TunnelOppLane == true)
                    {
                        if ((Program.OPP_LANE[IndexId][IndexJd] == 1) && (differenz <= 4))
                        {
                            tunfak = 1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velxold = 2 * zahl1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velyold = 2 * zahl1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velzold = 2 * zahl1;

                            goto MOVE_FORWARD;
                        }
                    }

                    UXint = UXneu;
                    UYint = UYneu;

                    //maximum time step
                    idt = Program.GralDx / (tunpa + 0.05F) * 0.5F;
                    Math.Clamp(idt, 0.05F, 0.5F);

                    //idt = (float)Math.Min(Program.GralDx / (tunpa + 0.05F) * 0.5F, 0.5);
                    float tunpao = tunpa;

                    //dispersion time of each particle
                    auszeit += idt;

                    //calculation of cross- and along-wind
                    float windpa = (float)(UXint * tuncos + UYint * tunsin);
                    float windqu = (float)(-UXint * tunsin + UYint * tuncos);

                    //bending tunnel jet
                    if (windqu == 0) windqu = 0.000001F;
                    float tunjettraeg = 1;
                    tunjettraeg = 5 * MathF.Exp(-0.005F * Program.TS_Area[Kenn_NTeil] * TS_ExitVelocity);
                    tunjettraeg = Program.FloatMax(tunjettraeg, 0.05F);
                    tunjettraeg = Math.Min(tunjettraeg, 1.5F);
                    tunqu = idt * 0.5F * Program.Pow2(windqu) * Math.Abs(windqu) / windqu * tunjettraeg;

                    //decelaration of the tunnel jet by friction/entrainment
                    tunjettraeg = 0.3F;
                    tunpa -= auszeit * idt * (tunpa - windpa) / (Program.Pow2(tunbreitalt)) * tunjettraeg;
                    if (tunpa < 0)
                    {
                        tunpa = 0;
                        tunfak = 1;
                    }

                    //calculation of jet stream velocities components
                    tunx = tunpa * tuncos - tunqu * tunsin;
                    tuny = tunpa * tunsin + tunqu * tuncos;

                    //coordinates of the central jet stream
                    xtun += idt * tunx;
                    ytun += idt * tuny;
                    tuncos = (float)(tunx / (Math.Sqrt(Program.Pow2(tunx) + Program.Pow2(tuny) + 0.001F)));
                    tunsin = (float)(tuny / (Math.Sqrt(Program.Pow2(tunx) + Program.Pow2(tuny) + 0.001F)));

                    //lateral widening of jet stream due to decelearation (mass continuity); but no narrowing when acceleration
                    if (tunpao > tunpa)
                    {
                        tunbreit = tunbreitalt * (Math.Abs(tunpao / (tunpa + 0.01F)));
                    }
                    yold = yneu;
                    yneu = yold + yold / tunbreitalt * (tunbreitalt - tunbreitalt);
                    tunbreitalt = Program.FloatMax(tunbreit, 0.000001F);

                    //linear increase of time-scale for jet-stream turbulence
                    float TLW = 2 * diff_building / (tunpa + 0.01F);

                    //time-scale for ambient-air turbulence
                    float sigmaz = 1.25F * Ustern;
                    float epsa = (Program.Pow3(Ustern) / 0.4F / Program.FloatMax(diff_building, Program.Z0Gramm[IUstern][JUstern]) *
                                         MathF.Pow(1 + 0.5F * MathF.Pow(diff_building / Math.Abs(ObLength), 0.8F), 1.8F));
                    float epst = 0.06F * Program.Pow2(tunpa) / TLW;

                    //in case of negative temperature differences (stable jet stream) the Lagrangian time-scale is reduced
                    if (TS_ExitTemperature < 0)
                    {
                        epst = Math.Max(epst / (10 * Math.Max(0.1F, Program.Pow2(TS_ExitTemperature))), epst * 0.01F);
                    }
                    else
                    {
                        epst *= (1 + TS_ExitTemperature * 0.5F) * 0.5F;
                    }

                    float TE = 2 * Program.Pow2(sigmaz) / Program.C0z / epsa;
                    float termw = 1 / TLW;

                    //temperature difference tunnel-jet <-> ambient air
                    m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                    m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                    u_rg = (m_z << 16) + m_w;
                    u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                    m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                    m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                    u_rg = (m_z << 16) + m_w;
                    zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                    float zuff4 = idt * zahl1;
                    buoy += -buoy * termw * idt + zuff4 * MathF.Sqrt(epst);

                    //new z-coordinate of particles
                    zturb = buoy * idt;
                    if (TS_ExitTemperature >= 0) zturb = buoy * idt + velz * idt;
                    zcoord_nteil += zturb;

                    //mass-continuity in bending areas
                    double xoldy = xneuy;
                    double yoldy = yneuy;
                    xneuy = xtun - yteilchen * tunsin;
                    yneuy = ytun + yteilchen * tuncos;

                    //particle coordinates

                    xturb += corx;
                    yturb += cory;
                    xcoord_nteil = xtun - yneu * tunsin + xturb;
                    ycoord_nteil = ytun + yneu * tuncos + yturb;

                    /*
					Program.xturb[nteil] += corx;
					Program.yturb[nteil] += cory;
					xcoord_nteil = xtun - yneu * tunsin + Program.xturb[nteil];
					ycoord_nteil = ytun + yneu * tuncos + Program.yturb[nteil];
                     */

                    //coordinate transformation towards model grid
                    xsi = xcoord_nteil - IKOOAGRAL;
                    eta = ycoord_nteil - JKOOAGRAL;
                    xsi1 = xcoord_nteil - Program.IKOOA;
                    eta1 = ycoord_nteil - Program.JKOOA;
                    if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                    {
                        goto REMOVE_PARTICLE;
                    }

                    int IndexIalt = 1;
                    int IndexJalt = 1;
                    if (topo == 1)
                    {
                        IndexIalt = IndexI;
                        IndexJalt = IndexJ;
                        IndexI = (int)(xsi * DXK_rez) + 1;
                        IndexJ = (int)(eta * DYK_rez) + 1;
                        if ((IndexI < 1) || (IndexI > Program.NII) || (IndexJ < 1) || (IndexJ > Program.NJJ))
                            goto REMOVE_PARTICLE;

                        IUstern = (int)(xsi1 * DDX1_rez) + 1;
                        JUstern = (int)(eta1 * DDY1_rez) + 1;
                        if ((IUstern > Program.NX) || (JUstern > Program.NY) || (IUstern < 1) || (JUstern < 1))
                            goto REMOVE_PARTICLE;

                        IndexId = IndexI;
                        IndexJd = IndexJ;

                        //interpolated orography
                        AHintold = AHint;
                        AHint = Program.AHK[IndexI][IndexJ];

                        differenz = zcoord_nteil - AHint;
                        diff_building = differenz;
                    }
                    else
                    {
                        IndexIalt = IndexI;
                        IndexJalt = IndexJ;
                        IndexI = (int)(xsi1 * DDX1_rez) + 1;
                        IndexJ = (int)(eta1 * DDY1_rez) + 1;
                        if ((IndexI < 1) || (IndexI > Program.NI) || (IndexJ < 1) || (IndexJ > Program.NJ))
                            goto REMOVE_PARTICLE;

                        IndexId = (int)(xsi * DXK_rez) + 1;
                        IndexJd = (int)(eta * DYK_rez) + 1;
                        if ((IndexId > Program.NII) || (IndexJd > Program.NJJ) || (IndexId < 1) || (IndexJd < 1))
                            goto REMOVE_PARTICLE;

                        //interpolated orography
                        AHintold = AHint;
                        differenz = zcoord_nteil;
                        diff_building = differenz;

                    }

                    //remove particles above maximum orography
                    if (zcoord_nteil >= Program.ModelTopHeight)
                        goto REMOVE_PARTICLE;

                    /*
					if (topo == 1)
					{
						if (zcoord_nteil >= Program.AHMAX + 290)
							goto REMOVE_PARTICLE;
					}
					//remove particles above maximum height up to which a flow field is computed in the presence of buildings
					if (topo == 0)
					{
						if ((zcoord_nteil >= 800) && (Program.gebdia == true))
							goto REMOVE_PARTICLE;
					}
                     */

                    //in case that orography is taken into account and if there is no particle, then, the particle trajectories are following the terrain
                    if (topo == 1)
                        if (Program.CUTK[IndexI][IndexJ] == 0) zcoord_nteil += (AHint - AHintold);

                    //time-step correction to ensure mass-conservation for bending tunnel jets
                    geschwneu = (float)(Math.Sqrt(Program.Pow2(xneuy - xoldy) + Program.Pow2(yneuy - yoldy)) / idt);
                    if (geschwalt == 0) geschwalt = geschwneu;
                    faktzeit = Program.FloatMax(faktzeit * geschwneu / (geschwalt + 0.001F), 0.001F);
                    idt *= faktzeit;
                    if (idt < 0.01) idt = 0.01f; // 29.12.2017 Kuntner: avoid extremely low time-steps at extreme wind speeds
                    geschwalt = geschwneu;

                    //remove particles above GRAMM orography
                    if (topo == 1)
                    {
                        if (zcoord_nteil >= Program.ModelTopHeight)
                            goto REMOVE_PARTICLE;
                        //if (zcoord_nteil >= Program.AHMAX + 290)
                        //	goto REMOVE_PARTICLE;
                    }

                    //reflexion of particles from tunnel portals
                    reflexion_flag = 0;
                    int IndexKOld = 0;
                    if (topo == 1)
                    {
                        if (Program.BuildingTerrExist == true)
                        {
                            IndexKOld = IndexK;
                            IndexK = BinarySearch(zcoord_nteil - Program.AHMIN); //19.05.25 Ku

                            if ((IndexK <= Program.KKART[IndexI][IndexJ]) && (Program.CUTK[IndexI][IndexJ] > 0))
                            {
                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
                                    int ik = (int)(xsi * dx_rez) + 1;
                                    int jk = (int)(eta * dy_rez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                    if (masse <= 0)
                                        goto REMOVE_PARTICLE;
                                }

                                tunfak = 1;
                                reflexion_flag = 1;

                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                                velzold = (MathF.Sqrt(varw) + 0.82F * tunpa) * zahl1;

                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);
                                velxold = (U0int + 0.82F * tunpa) * zahl1;

                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);
                                velyold = (V0int + 0.82F * tunpa) * zahl1;

                                tunpa = 0.01F;
                                goto MOVE_FORWARD;
                            }
                        }
                    }
                    else if ((topo == 0) && (Program.BuildingFlatExist == true))
                    {
                        IndexKOld = IndexK;
                        IndexK = BinarySearch(zcoord_nteil); //19.05.25 Ku

                        if (IndexK <= Program.KKART[IndexId][IndexJd])
                        {
                            // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                            if (Deposition_type > 0 && depo_reflection_counter >= 0)
                            {
                                float Pd1 = Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
                                int ik = (int)(xsi * dx_rez) + 1;
                                int jk = (int)(eta * dy_rez) + 1;
                                double[] depo_L = Program.Depo_conz[ik][jk];
                                double conc = masse * Pd1 * area_rez_fac;
                                lock (depo_L)
                                {
                                    depo_L[SG_nteil] += conc;
                                }
                                masse -= masse * Pd1;
                                depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                if (masse <= 0)
                                    goto REMOVE_PARTICLE;
                            }

                            tunfak = 1;
                            reflexion_flag = 1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);
                            velzold = (MathF.Sqrt(varw) + 0.82F * tunpa) * zahl1;


                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velxold = (U0int + 0.82F * tunpa) * zahl1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velyold = (V0int + 0.82F * tunpa) * zahl1;

                            tunpa = 0.01F;
                            goto MOVE_FORWARD;
                        }
                    }

                    //reflexion at the surface and at the top of the boundary layer
                    if (differenz <= 0)
                    {
                        zcoord_nteil = AHint - differenz + 0.01F;
                        differenz = zcoord_nteil - AHint;
                        diff_building = differenz;

                        // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                        if (Deposition_type > 0 && depo_reflection_counter >= 0)
                        {
                            float Pd1 = Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
                            int ik = (int)(xsi * dx_rez) + 1;
                            int jk = (int)(eta * dy_rez) + 1;
                            double[] depo_L = Program.Depo_conz[ik][jk];
                            double conc = masse * Pd1 * area_rez_fac;
                            lock (depo_L)
                            {
                                depo_L[SG_nteil] += conc;
                            }
                            masse -= masse * Pd1;
                            depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                            if (masse <= 0)
                                goto REMOVE_PARTICLE;
                        }
                    }
                    if (diff_building >= blh)
                        goto REMOVE_PARTICLE;

                    goto MOVE_TO_CONCENTRATIONCALCULATION;

                } //END OF TUNNEL MODUL

                //in case that particle was reflected, flag is set to 1 and subsequently concentrations are not computed
                reflexion_flag = 0;
                int back = 1;
                while (back == 1)
                {
                    //coordinate transformation towards model grid
                    xsi = xcoord_nteil - IKOOAGRAL;
                    eta = ycoord_nteil - JKOOAGRAL;
                    xsi1 = xcoord_nteil - Program.IKOOA;
                    eta1 = ycoord_nteil - Program.JKOOA;
                    if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                    {
                        goto REMOVE_PARTICLE;
                    }

                    int IndexIalt;
                    int IndexJalt;
                    int IndexIdalt = 1;
                    int IndexJdalt = 1;

                    if (topo == 1)
                    {
                        IndexIalt = IndexI;
                        IndexJalt = IndexJ;
                        IndexI = (int)(xsi * DXK_rez) + 1;
                        IndexJ = (int)(eta * DYK_rez) + 1;

                        //19.05.25 Ku: allow a relative step along the terrain near the start point one times
                        if (auszeit < 20 && TerrainStepAllowed && SourceType > 1) // Near the source, terrain correction 1 times, line and area sources only
                        {
                            int IndexIDelta = 2 * IndexI - IndexIalt;
                            int IndexJDelta = 2 * IndexJ - IndexJalt;
                            if (IndexIDelta > 1 && IndexIDelta < Program.NII && IndexJDelta > 1 && IndexJDelta < Program.NJJ)
                            {
                                if (Program.CUTK[IndexI][IndexJ] == 0 && Program.CUTK[IndexIalt][IndexJalt] == 0 && Program.CUTK[IndexIDelta][IndexJDelta] == 0) //No buildings
                                {
                                    int KKARTOld = Program.KKART[IndexIalt][IndexJalt];
                                    int KKARTNew = Program.KKART[IndexIDelta][IndexJDelta];

                                    if (KKARTOld != KKARTNew &&                 //Terrain step
                                        Math.Abs(KKARTOld - KKARTNew) < 3)     // step < 3
                                    {
                                        float AHK = Program.AHK[IndexIDelta][IndexJDelta];
                                        float AHKold = Program.AHK[IndexIalt][IndexJalt];

                                        if (Math.Abs(AHK - AHKold) > Program.GralDx * 0.5F) // vertical step > Concentration raster / 2
                                        {
                                            float faktor = Math.Abs(zcoord_nteil - Program.AHK[IndexI][IndexJ]) / Program.DZK[KKARTNew];
                                            if (faktor < 3) // avoid overflow and increase perf.
                                            {
                                                int KKARTRecent = Program.KKART[IndexI][IndexJ];
                                                faktor = MathF.Exp(-faktor * faktor * faktor * faktor * 0.01F);
                                                if (KKARTRecent == KKARTOld && KKARTNew < KKARTRecent) // particle is still at a higher cell
                                                {
                                                    faktor = 0F;
                                                }
                                                else if (KKARTNew < KKARTOld) // reduce correction at a downstep
                                                {
                                                    faktor *= 0.5F;
                                                }

                                                if (faktor > 0)
                                                {
                                                    zcoord_nteil += (AHK - Program.AHK[IndexIalt][IndexJalt]) * faktor * 0.8F;
                                                    if (zcoord_nteil < Program.AHK[IndexI][IndexJ])
                                                    {
                                                        zcoord_nteil = Program.AHK[IndexI][IndexJ] + 0.2F;
                                                    }
                                                    TerrainStepAllowed = false;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        IUstern = (int)(xsi1 * DDX1_rez) + 1;
                        JUstern = (int)(eta1 * DDY1_rez) + 1;

                        if ((IndexI > Program.NII) || (IndexJ > Program.NJJ) || (IndexI < 1) || (IndexJ < 1))
                            goto REMOVE_PARTICLE;

                        if ((IUstern > Program.NX) || (JUstern > Program.NY) || (IUstern < 1) || (JUstern < 1))
                            goto REMOVE_PARTICLE;

                        IndexId = IndexI;
                        IndexJd = IndexJ;
                    }
                    else
                    {
                        IndexIalt = IndexI;
                        IndexJalt = IndexJ;
                        IndexI = (int)(xsi1 * DDX1_rez) + 1;
                        IndexJ = (int)(eta1 * DDY1_rez) + 1;

                        if ((IndexI > Program.NII) || (IndexJ > Program.NJJ) || (IndexI < 1) || (IndexJ < 1))
                            goto REMOVE_PARTICLE;

                        IndexIdalt = IndexId;
                        IndexJdalt = IndexJd;
                        IndexId = (int)(xsi * DXK_rez) + 1;
                        IndexJd = (int)(eta * DYK_rez) + 1;
                        if ((IndexId > Program.NII) || (IndexJd > Program.NJJ) || (IndexId < 1) || (IndexJd < 1))
                            goto REMOVE_PARTICLE;
                    }

                    //reflexion of particles within buildings and at the surface
                    back = 0;
                    if (topo == 1)
                    {
                        int IndexKOld = IndexK;

                        IndexK = BinarySearch(zcoord_nteil - Program.AHMIN); //19.05.25 Ku
                        if (IndexK <= Program.KKART[IndexI][IndexJ])
                        {
                            if ((IndexK > Program.KKART[IndexIalt][IndexJalt]) && (IndexK == IndexKOld))
                            {
                                if (idt * UXint + corx <= 0)
                                    xcoord_nteil = 2 * (IKOOAGRAL + IndexI * DXK) - xcoord_nteil + 0.01F;
                                else
                                    xcoord_nteil = 2 * (IKOOAGRAL + (IndexI - 1) * DXK) - xcoord_nteil - 0.01F;

                                if (idt * UYint + cory <= 0)
                                    ycoord_nteil = 2 * (JKOOAGRAL + IndexJ * DYK) - ycoord_nteil + 0.01F;
                                else
                                    ycoord_nteil = 2 * (JKOOAGRAL + (IndexJ - 1) * DYK) - ycoord_nteil - 0.01F;
                            }
                            else if ((IndexK > Program.KKART[IndexIalt][IndexJ]) && (IndexK == IndexKOld))
                            {
                                if (idt * UXint + corx <= 0)
                                    xcoord_nteil = 2 * (IKOOAGRAL + IndexI * DXK) - xcoord_nteil + 0.01F;
                                else
                                    xcoord_nteil = 2 * (IKOOAGRAL + (IndexI - 1) * DXK) - xcoord_nteil - 0.01F;
                            }
                            else if ((IndexK > Program.KKART[IndexI][IndexJalt]) && (IndexK == IndexKOld))
                            {
                                if (idt * UYint + cory <= 0)
                                    ycoord_nteil = 2 * (JKOOAGRAL + IndexJ * DYK) - ycoord_nteil + 0.01F;
                                else
                                    ycoord_nteil = 2 * (JKOOAGRAL + (IndexJ - 1) * DYK) - ycoord_nteil - 0.01F;
                            }
                            else if ((IndexKOld > Program.KKART[IndexI][IndexJ]) && (IndexI == IndexIalt) && (IndexJ == IndexJalt))
                            {
                                zcoord_nteil = 2 * Program.AHK[IndexI][IndexJ] - zcoord_nteil + 0.01F;
                                differenz = zcoord_nteil - AHint;
                                diff_building = differenz;
                                velzold = -velzold;

                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
                                    int ik = (int)(xsi * dx_rez) + 1;
                                    int jk = (int)(eta * dy_rez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                    if (masse <= 0)
                                        goto REMOVE_PARTICLE;
                                }
                            }
                            else
                            {
                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
                                    int ik = (int)(xsi * dx_rez) + 1;
                                    int jk = (int)(eta * dy_rez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                    if (masse <= 0)
                                        goto REMOVE_PARTICLE;
                                }

                                if (tunpa == 0)
                                {
                                    xcoord_nteil -= (idt * UXint + corx);
                                    ycoord_nteil -= (idt * UYint + cory);
                                    zcoord_nteil -= (idt * (UZint + velz) + aufhurly);
                                    differenz = zcoord_nteil - AHint;
                                    diff_building = differenz;
                                }

                                int vorzeichen = 1;
                                if (velzold < 0) vorzeichen = -1;

                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                u1_rg = (u_rg + 1) * RNG_Const;
                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                                velzold = zahl1 * MathF.Sqrt(varw);
                                if (vorzeichen < 0)
                                {
                                    velzold = Math.Abs(velzold);
                                }
                                else
                                {
                                    velzold = -Math.Abs(velzold);
                                }
                            }

                            int vorzeichen1 = 1;
                            if (velxold < 0) vorzeichen1 = -1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * RNG_Const;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velxold = zahl1 * U0int * 3;
                            if (vorzeichen1 < 0)
                            {
                                velxold = Math.Abs(velxold);
                            }
                            else
                            {
                                velxold = -Math.Abs(velxold);
                            }

                            vorzeichen1 = 1;
                            if (velyold < 0) vorzeichen1 = -1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * RNG_Const;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velyold = zahl1 * V0int * 3;
                            if (vorzeichen1 < 0)
                            {
                                velyold = Math.Abs(velyold);
                            }
                            else
                            {
                                velyold = -Math.Abs(velyold);
                            }

                            back = 1;
                            idt = Program.FloatMax(idt * 0.5F, 0.05F);
                            if (tunpa > 0)
                                goto MOVE_FORWARD;
                        }
                    }
                    else if ((topo == 0) && (Program.BuildingFlatExist == true))
                    {
                        int kIndexKOld = IndexK;
                        IndexK = BinarySearch(zcoord_nteil); //19.05.25 Ku

                        //particle inside buildings
                        if (IndexK <= Program.KKART[IndexId][IndexJd])
                        {
                            if ((IndexK > Program.KKART[IndexIdalt][IndexJdalt]) && (IndexK == kIndexKOld))
                            {
                                if (idt * UXint + corx <= 0)
                                    xcoord_nteil = 2 * (IKOOAGRAL + IndexId * DXK) - xcoord_nteil + 0.01F;
                                else
                                    xcoord_nteil = 2 * (IKOOAGRAL + (IndexId - 1) * DXK) - xcoord_nteil - 0.01F;

                                if (idt * UYint + cory <= 0)
                                    ycoord_nteil = 2 * (JKOOAGRAL + IndexJd * DYK) - ycoord_nteil + 0.01F;
                                else
                                    ycoord_nteil = 2 * (JKOOAGRAL + (IndexJd - 1) * DYK) - ycoord_nteil - 0.01F;
                            }
                            else if ((IndexK > Program.KKART[IndexIdalt][IndexJd]) && (IndexK == kIndexKOld))
                            {
                                if (idt * UXint + corx <= 0)
                                    xcoord_nteil = 2 * (IKOOAGRAL + IndexId * DXK) - xcoord_nteil + 0.01F;
                                else
                                    xcoord_nteil = 2 * (IKOOAGRAL + (IndexId - 1) * DXK) - xcoord_nteil - 0.01F;
                            }
                            else if ((IndexK > Program.KKART[IndexId][IndexJdalt]) && (IndexK == kIndexKOld))
                            {
                                if (idt * UYint + cory <= 0)
                                    ycoord_nteil = 2 * (JKOOAGRAL + IndexJd * DYK) - ycoord_nteil + 0.01F;
                                else
                                    ycoord_nteil = 2 * (JKOOAGRAL + (IndexJd - 1) * DYK) - ycoord_nteil - 0.01F;
                            }
                            else if ((kIndexKOld > Program.KKART[IndexId][IndexJd]) && (IndexId == IndexIdalt) && (IndexJd == IndexJdalt))
                            {
                                zcoord_nteil = 2 * Program.AHK[IndexId][IndexJd] - zcoord_nteil + 0.01F;
                                differenz = zcoord_nteil - AHint;
                                velzold = -velzold;

                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
                                    int ik = (int)(xsi * dx_rez) + 1;
                                    int jk = (int)(eta * dy_rez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                    if (masse <= 0)
                                        goto REMOVE_PARTICLE;
                                }
                            }
                            else
                            {
                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
                                    int ik = (int)(xsi * dx_rez) + 1;
                                    int jk = (int)(eta * dy_rez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                    if (masse <= 0)
                                        goto REMOVE_PARTICLE;
                                }

                                if (tunpa == 0)
                                {
                                    xcoord_nteil -= (idt * UXint + corx);
                                    ycoord_nteil -= (idt * UYint + cory);
                                    zcoord_nteil -= (idt * (UZint + velz) + aufhurly);
                                    differenz = zcoord_nteil - AHint;
                                }

                                int vorzeichen = 1;
                                if (velzold < 0) vorzeichen = -1;

                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                u1_rg = (u_rg + 1) * RNG_Const;
                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                                velzold = zahl1 * MathF.Sqrt(varw);
                                if (vorzeichen < 0)
                                {
                                    velzold = Math.Abs(velzold);
                                }
                                else
                                {
                                    velzold = -Math.Abs(velzold);
                                }
                            }

                            int vorzeichen1 = 1;
                            if (velxold < 0) vorzeichen1 = -1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * RNG_Const;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velxold = zahl1 * U0int * 3;
                            if (vorzeichen1 < 0)
                            {
                                velxold = Math.Abs(velxold);
                            }
                            else
                            {
                                velxold = -Math.Abs(velxold);
                            }

                            vorzeichen1 = 1;
                            if (velyold < 0) vorzeichen1 = -1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * RNG_Const;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                            velyold = zahl1 * V0int * 3;
                            if (vorzeichen1 < 0)
                            {
                                velyold = Math.Abs(velyold);
                            }
                            else
                            {
                                velyold = -Math.Abs(velyold);
                            }

                            back = 1;
                            idt = Program.FloatMax(idt * 0.5F, 0.05F);
                            if (tunpa > 0)
                                goto MOVE_FORWARD;
                        }
                    }

                    if (back > 0)
                    {
                        reflexion_flag = 1;
                        reflexion_number++;
                        //in rare cases, particles get trapped near solid boundaries. Such particles are abandoned after a certain number of reflexions
                        if (reflexion_number > Max_Reflections)
                            goto REMOVE_PARTICLE;
                    }

                }   //END OF THE REFLEXION ALGORITHM

                //interpolation of orography
                AHintold = AHint;
                if (topo == 1)
                {
                    if ((IndexI < 1) || (IndexI > Program.NII) || (IndexJ < 1) || (IndexJ > Program.NJJ))
                        goto REMOVE_PARTICLE;

                    AHint = Program.AHK[IndexI][IndexJ];

                    differenz = zcoord_nteil - AHint;
                    diff_building = differenz;
                }
                else
                {
                    if ((IndexI < 1) || (IndexI > Program.NII) || (IndexJ < 1) || (IndexJ > Program.NJJ))
                        goto REMOVE_PARTICLE;

                    differenz = zcoord_nteil;
                    diff_building = differenz;
                }

                //reflexion at the surface and at the top of the boundary layer
                if ((diff_building >= blh) && (Program.Ob[IUstern][JUstern] < 0))
                {
                    if ((topo == 1) && (UZint >= 0))
                        goto REMOVE_PARTICLE;
                    zcoord_nteil = blh + AHint - (diff_building - blh) - 0.01F;
                    velzold = -velzold;
                    differenz = zcoord_nteil - AHint;
                    diff_building = differenz;
                }

                if (differenz <= 0)
                {
                    zcoord_nteil = AHint - differenz + 0.01F;
                    velzold = -velzold;
                    differenz = zcoord_nteil - AHint;
                    diff_building = differenz;

                    // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                    if (Deposition_type > 0 && depo_reflection_counter >= 0)
                    {
                        float Pd1 = Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
                        int ik = (int)(xsi * dx_rez) + 1;
                        int jk = (int)(eta * dy_rez) + 1;
                        double[] depo_L = Program.Depo_conz[ik][jk];
                        double conc = masse * Pd1 * area_rez_fac;
                        lock (depo_L)
                        {
                            depo_L[SG_nteil] += conc;
                        }
                        masse -= masse * Pd1;
                        depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                        if (masse <= 0)
                            goto REMOVE_PARTICLE;
                    }
                }

            MOVE_TO_CONCENTRATIONCALCULATION:

                //coordinate indices
                int iko = (int)(xsi * dx_rez) + 1;
                int jko = (int)(eta * dy_rez) + 1;

                if ((iko >= Program.NXL) || (iko < 0) || (jko >= Program.NYL) || (jko < 0))
                    goto REMOVE_PARTICLE;

                float zcoordRelative = 0;
                if ((Program.BuildingTerrExist == true) || (Program.BuildingFlatExist == true))
                {
                    zcoordRelative = zcoord_nteil - AHint + Program.CUTK[IndexI][IndexJ];
                }
                else
                {
                    zcoordRelative = zcoord_nteil - AHint;
                }
                for (int II = 0; II < kko.Length; II++)
                {
                    float slice = (zcoordRelative - Program.HorSlices[II]) * dz_rez;
                    if (Math.Abs(slice) > Int16.MaxValue)
                        goto REMOVE_PARTICLE;
                    kko[II] = Program.ConvToInt(slice);
                }

                //decay rate
                if (decay_rate > 0)
                {
                    masse *= Math.Exp(-decay_rate * idt);
                }

                // compute Wet deposition
                if (Program.WetDeposition == true && Program.WetDepoRW != 0)
                {
                    double epsilonW_Masse = masse * Program.WetDepoRW * idt; // deposited mass
                    bool ex = false;

                    if (epsilonW_Masse > masse)
                    {
                        epsilonW_Masse = masse;
                        ex = true;
                    }
                    masse -= epsilonW_Masse;

                    double[] depo_L = Program.Depo_conz[iko][jko];
                    double conc = epsilonW_Masse * area_rez_fac;
                    lock (depo_L)
                    {
                        depo_L[SG_nteil] += conc;
                    }

                    if (ex)
                        goto REMOVE_PARTICLE;
                }

                if (Deposition_type < 2) // compute concentrations for this particle
                {
                    //receptor concentrations
                    if ((Program.ReceptorsAvailable > 0) && (reflexion_flag == 0))
                    {
                        for (int irec = 1; irec < ReceptorConcentration.Length; irec++)
                        {
                            // if a receptor is inside or nearby a building, use the raster grid concentration inside or nearby the building
                            if (Program.ReceptorNearbyBuilding[irec])
                            {
                                if (iko == Program.ReceptorIInd[irec] &&
                                    jko == Program.ReceptorJInd[irec])
                                {
                                    float slice = (zcoordRelative - Program.ReceptorZ[irec]) * dz_rez;
                                    if (Program.ConvToInt(slice) == 0)
                                    {
                                        ReceptorConcentration[irec] += idt * masse;
                                    }
                                }
                            }
                            else // use the concentration at the receptor position x +- GralDx/2 and receptor y +- GralDy/2
                            {
                                if (Math.Abs(xcoord_nteil - Program.ReceptorX[irec]) < dxHalf &&
                                    Math.Abs(ycoord_nteil - Program.ReceptorY[irec]) < dyHalf)
                                {
                                    float slice = (zcoordRelative - Program.ReceptorZ[irec]) * dz_rez;
                                    if (Program.ConvToInt(slice) == 0)
                                    {
                                        ReceptorConcentration[irec] += idt * masse;
                                    }
                                }
                            }
                        }
                    }

                    //compute 2D - concentrations
                    for (int II = 0; II < kko.Length; II++)
                    {
                        if ((kko[II] == 0) && (reflexion_flag == 0))
                        {
                            float[] conz3d_L = Program.Conz3d[iko][jko][II];
                            double conc = masse * idt;
                            lock (conz3d_L)
                            {
                                conz3d_L[SG_nteil] += (float)conc;
                            }
                        }
                    }

                    //count particles in case of non-steady-state dispersion for the 3D concentration file
                    if (ISTATIONAER == 0 && Program.WriteVerticalConcentration)
                    {
                        if (reflexion_flag == 0)
                        {
                            IndexK3d = TransientConcentration.BinarySearchTransient(zcoord_nteil - AHint);
                            IndexI3d = (int)(xsi * DXK_rez) + 1;
                            IndexJ3d = (int)(eta * DYK_rez) + 1;

                            float[] conzsum_L = Program.ConzSsum[IndexI3d][IndexJ3d];
                            double conc = masse * Program.GridVolume * idt / (Area_cart * Program.DZK_Trans[IndexK3d]);
                            lock (conzsum_L)
                            {
                                conzsum_L[IndexK3d] += (float)conc;
                            }
                        }
                    }

                    //ODOUR Dispersion requires the concentration fields above and below the acutal layer
                    if (Program.Odour == true)
                    {
                        for (int II = 0; II < kko.Length; II++)
                        {
                            float slice = (zcoordRelative - (Program.HorSlices[II] + Program.GralDz)) * dz_rez;
                            kko[II] = Program.ConvToInt(Math.Min(int.MaxValue, slice));
                        }
                        for (int II = 0; II < kko.Length; II++)
                        {
                            if ((kko[II] == 0) && (reflexion_flag == 0))
                            {
                                float[] conz3dp_L = Program.Conz3dp[iko][jko][II];
                                double conc = masse * idt;
                                lock (conz3dp_L)
                                {
                                    conz3dp_L[SG_nteil] += (float)conc;
                                }
                            }
                        }
                        for (int II = 0; II < kko.Length; II++)
                        {
                            float slice = (zcoordRelative - (Program.HorSlices[II] - Program.GralDz)) * dz_rez;
                            kko[II] = Program.ConvToInt(Math.Min(int.MaxValue, slice));
                        }
                        for (int II = 0; II < kko.Length; II++)
                        {
                            if ((kko[II] == 0) && (reflexion_flag == 0))
                            {
                                float[] conz3dm_L = Program.Conz3dm[iko][jko][II];
                                double conc = masse * idt;
                                lock (conz3dm_L)
                                {
                                    conz3dm_L[SG_nteil] += (float)conc;
                                }
                            }
                        }
                    }
                } // compute concentrations

            } //END OF TIME-LOOP FOR PARTICLES

        REMOVE_PARTICLE:

            if (Program.LogLevel > 0) // additional log output
            {
                #region log_output
                if (reflexion_number > 400000)
                {
                    lock (thisLock)
                    {
                        Program.LogReflexions[5]++;
                    }
                }
                else if (reflexion_number > 100000)
                {
                    lock (thisLock)
                    {
                        Program.LogReflexions[4]++;
                    }
                }
                else if (reflexion_number > 50000)
                {
                    lock (thisLock)
                    {
                        Program.LogReflexions[3]++;
                    }
                }
                else if (reflexion_number > 10000)
                {
                    lock (thisLock)
                    {
                        Program.LogReflexions[2]++;
                    }
                }
                else if (reflexion_number > 5000)
                {
                    lock (thisLock)
                    {
                        Program.LogReflexions[1]++;
                    }
                }
                else if (reflexion_number > 500)
                {
                    lock (thisLock)
                    {
                        Program.LogReflexions[0]++;
                    }
                }

                if (timestep_number > 100000000)
                {
                    lock (thisLock)
                    {
                        Program.Log_Timesteps[5]++;
                    }
                }
                else if (timestep_number > 10000000)
                {
                    lock (thisLock)
                    {
                        Program.Log_Timesteps[4]++;
                    }
                }
                else if (timestep_number > 1000000)
                {
                    lock (thisLock)
                    {
                        Program.Log_Timesteps[3]++;
                    }
                }
                else if (timestep_number > 100000)
                {
                    lock (thisLock)
                    {
                        Program.Log_Timesteps[2]++;
                    }
                }
                else if (timestep_number > 10000)
                {
                    lock (thisLock)
                    {
                        Program.Log_Timesteps[1]++;
                    }
                }
                else if (timestep_number > 1000)
                {
                    lock (thisLock)
                    {
                        Program.Log_Timesteps[0]++;
                    }
                }

                #endregion log_output
            }

            // Add local receptor concentrations to receptor array and store maximum concentration part for each receptor
            if (Program.ReceptorsAvailable > 0)
            {
                for (int ii = 1; ii < ReceptorConcentration.Length; ii++)
                {
                    double recconc = ReceptorConcentration[ii];
                    if (recconc > 0)
                    {
                        double[] recmit_L = Program.ReceptorConc[ii];
                        lock (recmit_L)
                        {
                            recmit_L[SG_nteil] += recconc;
                        }

                        if (recconc > Program.ReceptorParticleMaxConc[ii][SG_nteil])
                        {
                            Interlocked.Exchange(ref Program.ReceptorParticleMaxConc[ii][SG_nteil], recconc);
                        }
                    }
                }
            }

            thisLock = null;
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        static float Erf(float x) // Error function
        {
            // constants
            const float a1 = 0.254829592F;
            const float a2 = -0.284496736F;
            const float a3 = 1.421413741F;
            const float a4 = -1.453152027F;
            const float a5 = 1.061405429F;
            const float p = 0.3275911F;

            // Save the sign of x
            int sign = 1;
            if (x < 0)
                sign = -1;
            x = Math.Abs(x);

            // A&S formula 7.1.26
            float t = 1 / (1 + p * x);
            float y = 1 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * MathF.Exp(-x * x);
            return sign * y;
        }

        /// <summary>
        ///Calculate the deposition probability according to VDI 3945
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static float Pd(float varw, float vsed, float vdep, int Deposition_Type, int IndexI, int IndexJ)
        {
            varw = MathF.Sqrt(varw);
            if (Deposition_Type == 2) // Deposition only - PM30 or larger
            {
                vdep *= (1F + 2F * Program.COV[IndexI][IndexJ]); // up to * 3 for large particles
            }
            else
            {
                vdep *= (1F + 0.5F * Program.COV[IndexI][IndexJ]); // up to * 1.5 for small particles and gases
            }

            // compute deposition probability according to VDI 3945 for this particle
            float Ks = vsed / (sqrt2F * varw);
            float Fs = sqrtPiF * Ks + MathF.Exp(-MathF.Pow(Ks, 2)) / (1 + Erf(Ks));
            float Pd = 4 * sqrtPiF * 3600 / Program.TAUS * vdep / (sqrt2F * varw * Fs + sqrtPiF * vdep);
            if (Pd < 1.0)
                return Pd;
            else
                return 1;
        }

    }
}
