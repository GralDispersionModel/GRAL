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
        private const float aHurley = 0.1F;
        private const float bHurley = 0.6F;

        private const float a1Const = 0.05F;
        private const float a2Const = 1.7F;
        private const float a3Const = 1.1F;

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

            // variables for the Hurley plume rise algorithm
            float MeanHurley = 0;
            float GHurley = 0;
            float FHurley = 0;
            float MHurley = 0;
            float RHurley = 0;
            float wpHurley = 0;
            float upHurley = 0;
            float DeltaZHurley = 0;
            float SigmauHurley = 0;

            //Grid variables
            int GrammCellX = 1, GrammCellY = 1; // default value for flat terrain
            int FFCellX, FFCellY;               // Flow field cells
            float FFGridX = Program.DXK;
            float FFGridY = Program.DYK;
            float FFGridXRez = 1 / Program.DXK;
            float FFGridYRez = 1 / Program.DYK;
            float GrammGridXRez = 1 / Program.DDX[1];
            float GrammGridYRez = 1 / Program.DDY[1];
            float ConcGridXRez = 1 / Program.GralDx;
            float ConcGridYRez = 1 / Program.GralDy;
            float ConcGridZRez = 1 / Program.GralDz;
            float ConcGridXHalf = Program.GralDx * 0.5F;
            float ConcGridYHalf = Program.GralDy * 0.5F;

            int IFQ = Program.TS_Count;
            int Kenn_NTeil = Program.ParticleSource[nteil];       // number of source
            if (Kenn_NTeil == 0)
            {
                return;
            }

            int SourceType = Program.SourceType[nteil]; // sourcetype
            float blh = Program.BdLayHeight;
            int IKOOAGRAL = Program.IKOOAGRAL;
            int JKOOAGRAL = Program.JKOOAGRAL;

            Span<int> kko = stackalloc int[Program.NS];
            Span<double> ReceptorConcentration = stackalloc double[Program.ReceptorNumber + 1];

            int reflexion_flag = 0;
            int ISTATISTIK = Program.IStatistics;
            int ISTATIONAER = Program.ISTATIONAER;
            int topo = Program.Topo;

            int TransientGridX = 1;
            int TransientGridY = 1;
            int TransientGridZ = 1;
            Boolean emission_timeseries = Program.EmissionTimeseriesExist;

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

            float AHint = 0;
            float AHintold = 0;
            float varw = 0;
            double xturb = 0;  // entrainment turbulence for tunnel jet streams in x-direction
            double yturb = 0;  // entrainment turbulence for tunnel jet streams in y-direction

            //advection time step
            float idt = 0.1F;
            float auszeit = 0;

            //flag indicating if particle belongs to a tunnel portal 1 = not a portal
            int tunfak = 1;

            float DSIGUDX = 0, DSIGUDY = 0, DSIGVDX = 0, DSIGVDY = 0;
            float DUDX = 0, DVDX = 0, DUDY = 0, DVDY = 0;

            //relative coordinates of particles inside model domain (used to calc. cell-indices of particles moving in the microscale flow field)
            double xsi = xcoord_nteil - IKOOAGRAL;
            double eta = ycoord_nteil - JKOOAGRAL;
            double xsi1 = xcoord_nteil - Program.GrammWest;
            double eta1 = ycoord_nteil - Program.GrammSouth;
            if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
            {
                goto REMOVE_PARTICLE;
            }

            int Max_Loops = (int)(3E6 + Math.Min(9.7E7,
                Math.Max(Math.Abs(Program.EtaMaxGral - Program.EtaMinGral), Math.Abs(Program.XsiMaxGral - Program.XsiMinGral)) * 1000)); // max. Loops 1E6 (max. nr. of reflexions) + 2E6 Min + max(x,y)/0.001 

            int Max_Reflections = Math.Min(1000000, Program.NII * Program.NJJ * 10); // max. 10 reflections per cell

            //interpolated orography
            float PartHeightAboveTerrain = 0;
            float PartHeightAboveBuilding = 0;

            FFCellX = (int)(xsi * FFGridXRez) + 1;
            FFCellY = (int)(eta * FFGridYRez) + 1;
            if ((FFCellX < 1) || (FFCellX > Program.NII) || (FFCellY < 1) || (FFCellY > Program.NJJ))
            {
                goto REMOVE_PARTICLE;
            }

            if (topo == 1)
            {
                //with terrain
                GrammCellX = Math.Clamp((int)(xsi1 * GrammGridXRez) + 1, 1, Program.NX);
                GrammCellY = Math.Clamp((int)(eta1 * GrammGridYRez) + 1, 1, Program.NY);

                //interpolated orography
                AHint = Program.AHK[FFCellX][FFCellY];
                PartHeightAboveTerrain = zcoord_nteil - AHint;
                PartHeightAboveBuilding = PartHeightAboveTerrain;
            }
            else
            {
                //flat terrain
                PartHeightAboveTerrain = zcoord_nteil;
                PartHeightAboveBuilding = PartHeightAboveTerrain;
            }

            //wind-field interpolation
            float UXint = 0, UYint = 0, UZint = 0;
            int IndexK = 1;
            (UXint, UYint, UZint, IndexK) = IntWindCalculate(FFCellX, FFCellY, AHint, xcoord_nteil, ycoord_nteil, zcoord_nteil);
            float windge = MathF.Max(0.1F, MathF.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint)));

            //variables tunpa needs to be defined even if not used
            float tunpa = 0;

            float ObL = Program.Ob[GrammCellX][GrammCellY];
            float roughZ0 = Program.Z0Gramm[GrammCellX][GrammCellY];
            if (Program.AdaptiveRoughnessMax > 0)
            {
                ObL = Program.OLGral[FFCellX][FFCellY];
                roughZ0 = Program.Z0Gral[FFCellX][FFCellY];
            }
            float deltaZ = roughZ0;

            //vertical interpolation of horizontal standard deviations of wind component fluctuations between observations
            (float U0int, float V0int) = IntStandCalculate(nteil, roughZ0, PartHeightAboveBuilding, windge, SigmauHurley);

            //remove particles above boundary-layer
            if ((PartHeightAboveBuilding > blh) && (Program.ISTATIONAER != 0) && (ObL < 0)) //26042020 (Ku): removed for transient mode -> particles shoulb be tracked above blh
            {
                goto REMOVE_PARTICLE;
            }

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
                ExitVelocity = MathF.Max(0, ExitVelocity);
                
                float ExitTemperature = Program.PS_T[Kenn_NTeil];
                int tempTimeSeriesIndex = Program.PS_TimeSeriesTemperature[Kenn_NTeil];
                if (tempTimeSeriesIndex != -1) // Time series for exit temperature
                {
                    if ((Program.IWET - 1) < Program.PS_TimeSerTempValues[tempTimeSeriesIndex].Value.Length)
                    {
                        ExitTemperature = Program.PS_TimeSerTempValues[tempTimeSeriesIndex].Value[Program.IWET - 1] + 273;
                    }
                }
                ExitTemperature = MathF.Max(273, ExitTemperature);

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
                fmodul = Math.Clamp(fmodul, 0.5F, 1.5F);

                FHurley = (9.81F * ExitVelocity * Program.Pow2(Program.PS_D[Kenn_NTeil] * 0.5F) *
                                 (ExitTemperature - 273F) / ExitTemperature);
                GHurley = (273 / ExitTemperature * ExitVelocity * Program.Pow2(Program.PS_D[Kenn_NTeil] * 0.5F));
                MHurley = GHurley * ExitVelocity;
                RHurley = MathF.Sqrt(ExitVelocity / MathF.Sqrt(Program.Pow2(windge * fmodul) + Program.Pow2(ExitVelocity)));
                wpHurley = ExitVelocity;
                upHurley = MathF.Sqrt(Program.Pow2(windge * fmodul) + Program.Pow2(wpHurley));
                MeanHurley = 0;
                DeltaZHurley = 0;
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
                if (UXint == 0)
                {
                    UXint = 0.001F;
                }

                if (UYint == 0)
                {
                    UYint = 0.001F;
                }

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
            float ObLength = 0;
            float Ustern = 0;
        MOVE_FORWARD:
            while (timestep_number <= Max_Loops)
            {
                ++timestep_number;

                //if particle is within the user-defined tunnel-entrance zone it is removed (sucked-into the tunnel)
                if (Program.TunnelEntr == true)
                {
                    if ((Program.TUN_ENTR[FFCellX][FFCellY] == 1) && (PartHeightAboveTerrain <= 5))
                    {
                        goto REMOVE_PARTICLE;
                    }
                }

                //interpolate wind field
                (UXint, UYint, UZint, IndexK) = IntWindCalculate(FFCellX, FFCellY, AHint, xcoord_nteil, ycoord_nteil, zcoord_nteil);
                windge = MathF.Max(0.01F, MathF.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint)));

                //particles below the surface or buildings are removed
                if (PartHeightAboveTerrain < 0)
                {
                    goto REMOVE_PARTICLE;
                }

                /*
                 *   VERTICAL DIFFUSION ACCORING TO FRANZESE, 1999
                 */

                //dissipation            
                if (Program.AdaptiveRoughnessMax > 0)
                {
                    ObLength = Program.OLGral[FFCellX][FFCellY];
                    Ustern = Program.USternGral[FFCellX][FFCellY];
                    roughZ0 = Program.Z0Gral[FFCellX][FFCellY];
                }
                else
                {
                    ObLength = Program.Ob[GrammCellX][GrammCellY];
                    Ustern = Program.Ustern[GrammCellX][GrammCellY];
                    roughZ0 = Program.Z0Gramm[GrammCellX][GrammCellY];
                }
                deltaZ = roughZ0;
                if (deltaZ < PartHeightAboveBuilding)
                {
                    deltaZ = PartHeightAboveBuilding;
                }

                float eps = Program.Pow3(Ustern) / deltaZ / 0.4F *
                                    MathF.Pow(1F + 0.5F * MathF.Pow(PartHeightAboveBuilding / Math.Abs(ObLength), 0.8F), 1.8F);
                /*
                float eps = (float)(Program.Pow3(Ustern) / Program.fl_max(diff_building, Program.Z0GRAMM[IUstern][JUstern]) / 0.4F *
                                    Math.Pow(1 + 0.5F * Math.Pow(diff_building / Math.Abs(ObLength), 0.8), 1.8));
                 */

                depo_reflection_counter++;
                float W0int = 0; float varw2 = 0; float skew = 0; float curt = 0; float dskew = 0;
                float wstern = Ustern * 2.9F;
                float vert_blh = PartHeightAboveBuilding / blh;
                float hterm = (1 - vert_blh);
                float varwu = 0; float dvarw = 0; float dcurt = 0;

                if (ObLength >= 0)
                {
                    if (ISTATISTIK != 3)
                    {
                        W0int = Ustern * Program.StdDeviationW * 1.25F;
                    }
                    else
                    {
                        W0int = Program.W0int;
                    }

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

                    if ((PartHeightAboveBuilding < Program.BdLayH10) || (PartHeightAboveBuilding >= blh))
                    {
                        skew = 0;
                        dskew = 0;
                    }
                    else
                    {
                        skew = a3Const * PartHeightAboveBuilding * Program.Pow2(hterm) * Program.Pow3(wstern) / blh;
                        dskew = a3Const * Program.Pow3(wstern) / blh * Program.Pow2(hterm) - 2 * a3Const * Program.Pow3(wstern) * PartHeightAboveBuilding * hterm / Program.Pow2(blh);
                    }

                    if (PartHeightAboveBuilding < Program.BdLayH10)
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
                    if (SourceType == 0 && auszeit < 20)
                    {
                        idt = MathF.Min(0.2F, idt);
                    }

                    float idtt = 0.5F * Math.Min(Program.GralDx, FFGridX) / (windge + MathF.Sqrt(Program.Pow2(velxold) + Program.Pow2(velyold)));
                    if (idtt < idt)
                    {
                        idt = idtt;
                    }

                    Math.Clamp(idt, 0.1F, 4F);
                    /*
                    idt = (float)Math.Min(idt, 0.5F * Math.Min(Program.GralDx, DXK) / (windge + Math.Sqrt(Program.Pow2(velxold) + Program.Pow2(velyold))));
                    idt = (float)Math.Min(4, idt);
                    idt = (float)Program.fl_max(0.1F, idt);
                     */
                }

                float dummyterm = curt - Program.Pow2(skew) / varw - varw2;
                if (dummyterm == 0)
                {
                    dummyterm = 0.000001F;
                }

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
                if (zahl1 > 2)
                {
                    zahl1 = 2;
                }

                if (zahl1 < -2)
                {
                    zahl1 = -2;
                }

                float velz = acc * idt + MathF.Sqrt(Program.C0z * eps * idt) * zahl1 + velzold;

                //******************************************************************************************************************** OETTL, 31 AUG 2016
                //in adjecent cells to vertical solid walls, turbulent velocities are only allowed in the direction away from the wall
                if (velz == 0)
                {
                    velz = 0.01F;
                }

                short _kkart = Program.KKART[FFCellX][FFCellY];
                //solid wall west of the particle
                if ((Program.KKART[FFCellX - 1][FFCellY] > _kkart) && (IndexK <= Program.KKART[FFCellX - 1][FFCellY]) && (Program.CUTK[FFCellX - 1][FFCellY] > 0))
                {
                    velz += velz / MathF.Abs(velz) * 0.67F * Program.FloatMax(-Program.UK[FFCellX + 1][FFCellY][IndexK], 0);
                }
                //solid wall east of the particle
                if ((Program.KKART[FFCellX + 1][FFCellY] > _kkart) && (IndexK <= Program.KKART[FFCellX + 1][FFCellY]) && (Program.CUTK[FFCellX + 1][FFCellY] > 0))
                {
                    velz += velz / MathF.Abs(velz) * 0.67F * Program.FloatMax(Program.UK[FFCellX][FFCellY][IndexK], 0);
                }
                //solid wall south of the particle
                if ((Program.KKART[FFCellX][FFCellY - 1] > _kkart) && (IndexK <= Program.KKART[FFCellX][FFCellY - 1]) && (Program.CUTK[FFCellX][FFCellY - 1] > 0))
                {
                    velz += velz / MathF.Abs(velz) * 0.67F * Program.FloatMax(-Program.VK[FFCellX][FFCellY + 1][IndexK], 0);
                }
                //solid wall north of the particle
                if ((Program.KKART[FFCellX][FFCellY + 1] > _kkart) && (IndexK <= Program.KKART[FFCellX][FFCellY + 1]) && (Program.CUTK[FFCellX][FFCellY + 1] > 0))
                {
                    velz += velz / MathF.Abs(velz) * 0.67F * Program.FloatMax(Program.VK[FFCellX][FFCellY][IndexK], 0);
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
                    float epshurly = 1.5F * Program.Pow3(wpHurley) / (MeanHurley + 0.1F);
                    float epsambiente = 0;

                    DeltaZHurley = 0;
                    SigmauHurley = 0;

                    epsambiente = eps;

                    if (epshurly <= epsambiente)
                    {
                        wpHurley = 0;
                    }

                    if (wpHurley > 0)
                    {
                        float stab = 0;

                        if (ObLength >= 0)
                        {
                            stab = 0.04F * MathF.Exp(-ObLength * 0.05F);
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
                        fmodul = Math.Clamp(fmodul, 0.1F, 5F);
                        float sHurley = 9.81F / 273 * stab;

                        GHurley += 2 * RHurley * (aHurley * Program.Pow2(wpHurley) + bHurley * windge * fmodul * wpHurley
                                                        + 0.1F * upHurley * (MathF.Sqrt(0.5F * (Program.Pow2(velxold) + Program.Pow2(velyold))))) * idt;
                        FHurley += -sHurley * MHurley / upHurley * (1 / 2.25F * windge * fmodul + wpHurley) * idt;
                        MHurley += FHurley * idt;
                        {
                            RHurley = MathF.Sqrt((GHurley + FHurley / 9.8F) / upHurley);
                            upHurley = MathF.Sqrt(Program.Pow2(windge * fmodul) + Program.Pow2(wpHurley));
                        }
                        float sigmawpHurley = (aHurley * Program.Pow2(wpHurley) + bHurley * windge * fmodul * wpHurley) / 4.2F / upHurley;
                        SigmauHurley = 2 * sigmawpHurley;
                        float wpold = wpHurley;
                        wpHurley = Program.FloatMax(MHurley / GHurley, 0);
                        float wpmittel = (wpHurley + wpold) * 0.5F;
                        MeanHurley += wpmittel * idt;

                        m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                        m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                        u_rg = (m_z << 16) + m_w;
                        u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                        m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                        m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                        u_rg = (m_z << 16) + m_w;
                        zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                        DeltaZHurley = (wpmittel + zahl1 * sigmawpHurley) * idt;
                    }

                    if (tunfak == 1)
                    {
                        zcoord_nteil += (velz + UZint) * idt + DeltaZHurley;
                    }

                    velzold = velz;
                }
                else
                {
                    if (tunfak == 1)
                    {
                        zcoord_nteil += (velz + UZint) * idt;
                    }

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
                            if (wpHurley < 0.01)
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
                {
                    goto REMOVE_PARTICLE;
                }

                //new space coordinates due to turbulent movements
                float corx = velxold * idt;
                float cory = velyold * idt;

                /*
                 *    HORIZONTAL DIFFUSION ACCORDING TO ANFOSSI ET AL. (2006)
                 */

                //vertical interpolation of horizontal standard deviations of wind speed components
                (U0int, V0int) = IntStandCalculate(nteil, roughZ0, PartHeightAboveBuilding, windge, SigmauHurley);

                //determination of the meandering parameters according to Oettl et al. (2006)
                float param = 0;
                if (ISTATISTIK != 3)
                {
                    param = Program.FloatMax(8.5F / Program.Pow2(windge + 1), 0F);
                }
                //param = (float)Math.Max(8.5F / Program.Pow2(windge + 1), 0);
                else
                {
                    param = Program.MWindMEander;
                }

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
                    {
                        T3 = Program.TWindMeander;
                    }

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
                    if (tunpa <= 0)
                    {
                        tunfak = 1;
                    }

                    //particles within user-defined opposite lane are treated with standard dispersion (tunnel jet is destroyed there)
                    if (Program.TunnelOppLane == true)
                    {
                        if ((Program.OPP_LANE[FFCellX][FFCellY] == 1) && (PartHeightAboveTerrain <= 4))
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
                    if (windqu == 0)
                    {
                        windqu = 0.000001F;
                    }

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
                    float TLW = 2 * PartHeightAboveBuilding / (tunpa + 0.01F);

                    //time-scale for ambient-air turbulence
                    float sigmaz = 1.25F * Ustern;
                    float epsa = (Program.Pow3(Ustern) / 0.4F / Program.FloatMax(PartHeightAboveBuilding, Program.Z0Gramm[GrammCellX][GrammCellY]) *
                                         MathF.Pow(1 + 0.5F * MathF.Pow(PartHeightAboveBuilding / Math.Abs(ObLength), 0.8F), 1.8F));
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
                    if (TS_ExitTemperature >= 0)
                    {
                        zturb = buoy * idt + velz * idt;
                    }

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
                    xsi1 = xcoord_nteil - Program.GrammWest;
                    eta1 = ycoord_nteil - Program.GrammSouth;
                    if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                    {
                        goto REMOVE_PARTICLE;
                    }

                    int IndexIalt = 1;
                    int IndexJalt = 1;
                    if (topo == 1)
                    {
                        //with topography
                        IndexIalt = FFCellX;
                        IndexJalt = FFCellY;
                        //index in the flow field grid
                        FFCellX = (int)(xsi * FFGridXRez) + 1;
                        FFCellY = (int)(eta * FFGridYRez) + 1;
                        if ((FFCellX < 1) || (FFCellX > Program.NII) || (FFCellY < 1) || (FFCellY > Program.NJJ))
                        {
                            goto REMOVE_PARTICLE;
                        }

                        //index in the GRAMM grid
                        GrammCellX = Math.Clamp((int)(xsi1 * GrammGridXRez) + 1, 1, Program.NX);
                        GrammCellY = Math.Clamp((int)(eta1 * GrammGridYRez) + 1, 1, Program.NY);

                        //interpolated orography
                        AHintold = AHint;
                        AHint = Program.AHK[FFCellX][FFCellY];

                        PartHeightAboveTerrain = zcoord_nteil - AHint;
                        PartHeightAboveBuilding = PartHeightAboveTerrain;
                    }
                    else
                    {
                        //flat terrain
                        FFCellX = (int)(xsi * FFGridXRez) + 1;
                        FFCellY = (int)(eta * FFGridYRez) + 1;
                        if ((FFCellX > Program.NII) || (FFCellX > Program.NJJ) || (FFCellY < 1) || (FFCellY < 1))
                        {
                            goto REMOVE_PARTICLE;
                        }

                        //interpolated orography
                        AHintold = AHint;
                        PartHeightAboveTerrain = zcoord_nteil;
                        PartHeightAboveBuilding = PartHeightAboveTerrain;
                    }

                    //remove particles above maximum orography
                    if (zcoord_nteil >= Program.ModelTopHeight)
                    {
                        goto REMOVE_PARTICLE;
                    }

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
                    {
                        if (Program.CUTK[FFCellX][FFCellY] == 0)
                        {
                            zcoord_nteil += (AHint - AHintold);
                        }
                    }

                    //time-step correction to ensure mass-conservation for bending tunnel jets
                    geschwneu = (float)(Math.Sqrt(Program.Pow2(xneuy - xoldy) + Program.Pow2(yneuy - yoldy)) / idt);
                    if (geschwalt == 0)
                    {
                        geschwalt = geschwneu;
                    }

                    faktzeit = Program.FloatMax(faktzeit * geschwneu / (geschwalt + 0.001F), 0.001F);
                    idt *= faktzeit;
                    if (idt < 0.01)
                    {
                        idt = 0.01f; // 29.12.2017 Kuntner: avoid extremely low time-steps at extreme wind speeds
                    }

                    geschwalt = geschwneu;

                    //remove particles above GRAMM orography
                    if (topo == 1)
                    {
                        if (zcoord_nteil >= Program.ModelTopHeight)
                        {
                            goto REMOVE_PARTICLE;
                        }
                        //if (zcoord_nteil >= Program.AHMAX + 290)
                        //	goto REMOVE_PARTICLE;
                    }

                    //reflexion of particles from tunnel portals
                    reflexion_flag = 0;
                    int IndexKOld = 0;
                    if (topo == 1)
                    {
                        if (Program.BuildingsExist == true)
                        {
                            IndexKOld = IndexK;
                            IndexK = BinarySearch(zcoord_nteil - Program.AHMIN); //19.05.25 Ku

                            if ((IndexK <= Program.KKART[FFCellX][FFCellY]) && (Program.CUTK[FFCellX][FFCellY] > 0))
                            {
                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                                    int ik = (int)(xsi * ConcGridXRez) + 1;
                                    int jk = (int)(eta * ConcGridYRez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                    if (masse <= 0)
                                    {
                                        goto REMOVE_PARTICLE;
                                    }
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
                    else if ((topo == 0) && (Program.BuildingsExist == true))
                    {
                        //flat terrain with buildings
                        IndexKOld = IndexK;
                        IndexK = BinarySearch(zcoord_nteil); //19.05.25 Ku

                        if (IndexK <= Program.KKART[FFCellX][FFCellY])
                        {
                            // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                            if (Deposition_type > 0 && depo_reflection_counter >= 0)
                            {
                                float Pd1 = Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                                int ik = (int)(xsi * ConcGridXRez) + 1;
                                int jk = (int)(eta * ConcGridYRez) + 1;
                                double[] depo_L = Program.Depo_conz[ik][jk];
                                double conc = masse * Pd1 * area_rez_fac;
                                lock (depo_L)
                                {
                                    depo_L[SG_nteil] += conc;
                                }
                                masse -= masse * Pd1;
                                depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                if (masse <= 0)
                                {
                                    goto REMOVE_PARTICLE;
                                }
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
                    if (PartHeightAboveTerrain <= 0)
                    {
                        zcoord_nteil = AHint - PartHeightAboveTerrain + 0.01F;
                        PartHeightAboveTerrain = zcoord_nteil - AHint;
                        PartHeightAboveBuilding = PartHeightAboveTerrain;

                        // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                        if (Deposition_type > 0 && depo_reflection_counter >= 0)
                        {
                            float Pd1 = Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                            int ik = (int)(xsi * ConcGridXRez) + 1;
                            int jk = (int)(eta * ConcGridYRez) + 1;
                            double[] depo_L = Program.Depo_conz[ik][jk];
                            double conc = masse * Pd1 * area_rez_fac;
                            lock (depo_L)
                            {
                                depo_L[SG_nteil] += conc;
                            }
                            masse -= masse * Pd1;
                            depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                            if (masse <= 0)
                            {
                                goto REMOVE_PARTICLE;
                            }
                        }
                    }

                    if (PartHeightAboveBuilding >= blh && Program.ISTATIONAER != 0) //26042020 (Ku): removed for transient mode -> particles shoulb be tracked above blh 
                    {
                        goto REMOVE_PARTICLE;
                    }

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
                    xsi1 = xcoord_nteil - Program.GrammWest;
                    eta1 = ycoord_nteil - Program.GrammSouth;
                    if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                    {
                        goto REMOVE_PARTICLE;
                    }

                    int FFCellXPrev;
                    int FFCellYPrev;

                    if (topo == 1)
                    {
                        //with topography
                        FFCellXPrev = FFCellX;
                        FFCellYPrev = FFCellY;
                        FFCellX = (int)(xsi * FFGridXRez) + 1;
                        FFCellY = (int)(eta * FFGridYRez) + 1;

                        //19.05.25 Ku: allow a relative step along the terrain near the start point one times
                        if (auszeit < 20 && TerrainStepAllowed && SourceType > 1) // Near the source, terrain correction 1 times, line and area sources only
                        {
                            int IndexXDelta = 2 * FFCellX - FFCellXPrev;
                            int IndexYDelta = 2 * FFCellY - FFCellYPrev;
                            if (IndexXDelta > 1 && IndexXDelta < Program.NII && IndexYDelta > 1 && IndexYDelta < Program.NJJ)
                            {
                                if (Program.CUTK[FFCellX][FFCellY] == 0 && Program.CUTK[FFCellXPrev][FFCellYPrev] == 0 && Program.CUTK[IndexXDelta][IndexYDelta] == 0) //No buildings
                                {
                                    int KKARTOld = Program.KKART[FFCellXPrev][FFCellYPrev];
                                    int KKARTNew = Program.KKART[IndexXDelta][IndexYDelta];

                                    if (KKARTOld != KKARTNew &&                 //Terrain step
                                        Math.Abs(KKARTOld - KKARTNew) < 3)     // step < 3
                                    {
                                        float AHK = Program.AHK[IndexXDelta][IndexYDelta];
                                        float AHKold = Program.AHK[FFCellXPrev][FFCellYPrev];

                                        if (Math.Abs(AHK - AHKold) > Program.GralDx * 0.5F) // vertical step > Concentration raster / 2
                                        {
                                            float faktor = Math.Abs(zcoord_nteil - Program.AHK[FFCellX][FFCellY]) / Program.DZK[KKARTNew];
                                            if (faktor < 3) // avoid overflow and increase perf.
                                            {
                                                int KKARTRecent = Program.KKART[FFCellX][FFCellY];
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
                                                    zcoord_nteil += (AHK - Program.AHK[FFCellXPrev][FFCellYPrev]) * faktor * 0.8F;
                                                    if (zcoord_nteil < Program.AHK[FFCellX][FFCellY])
                                                    {
                                                        zcoord_nteil = Program.AHK[FFCellX][FFCellY] + 0.2F;
                                                    }
                                                    TerrainStepAllowed = false;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        GrammCellX = Math.Clamp((int)(xsi1 * GrammGridXRez) + 1, 1, Program.NX);
                        GrammCellY = Math.Clamp((int)(eta1 * GrammGridYRez) + 1, 1, Program.NY);

                        if ((FFCellX > Program.NII) || (FFCellY > Program.NJJ) || (FFCellX < 1) || (FFCellY < 1))
                        {
                            goto REMOVE_PARTICLE;
                        }
                    }
                    else
                    {
                        //flat terrain
                        FFCellXPrev = FFCellX;
                        FFCellYPrev = FFCellY;
                        FFCellX = (int)(xsi * FFGridXRez) + 1;
                        FFCellY = (int)(eta * FFGridYRez) + 1;
                        if ((FFCellX > Program.NII) || (FFCellY > Program.NJJ) || (FFCellX < 1) || (FFCellY < 1))
                        {
                            goto REMOVE_PARTICLE;
                        }
                    }

                    //reflexion of particles within buildings and at the surface
                    back = 0;
                    if (Program.BuildingsExist == true || topo == 1)
                    {
                        int IndexKOld = IndexK;
                        IndexK = BinarySearch(zcoord_nteil - Program.AHMIN); //19.05.25 Ku

                        if (IndexK <= Program.KKART[FFCellX][FFCellY])
                        {
                            if ((IndexK > Program.KKART[FFCellXPrev][FFCellYPrev]) && (IndexK == IndexKOld))
                            {
                                if (idt * UXint + corx <= 0)
                                {
                                    xcoord_nteil = 2 * (IKOOAGRAL + FFCellX * FFGridX) - xcoord_nteil + 0.01F;
                                }
                                else
                                {
                                    xcoord_nteil = 2 * (IKOOAGRAL + (FFCellX - 1) * FFGridX) - xcoord_nteil - 0.01F;
                                }

                                if (idt * UYint + cory <= 0)
                                {
                                    ycoord_nteil = 2 * (JKOOAGRAL + FFCellY * FFGridY) - ycoord_nteil + 0.01F;
                                }
                                else
                                {
                                    ycoord_nteil = 2 * (JKOOAGRAL + (FFCellY - 1) * FFGridY) - ycoord_nteil - 0.01F;
                                }
                            }
                            else if ((IndexK > Program.KKART[FFCellXPrev][FFCellY]) && (IndexK == IndexKOld))
                            {
                                if (idt * UXint + corx <= 0)
                                {
                                    xcoord_nteil = 2 * (IKOOAGRAL + FFCellX * FFGridX) - xcoord_nteil + 0.01F;
                                }
                                else
                                {
                                    xcoord_nteil = 2 * (IKOOAGRAL + (FFCellX - 1) * FFGridX) - xcoord_nteil - 0.01F;
                                }
                            }
                            else if ((IndexK > Program.KKART[FFCellX][FFCellYPrev]) && (IndexK == IndexKOld))
                            {
                                if (idt * UYint + cory <= 0)
                                {
                                    ycoord_nteil = 2 * (JKOOAGRAL + FFCellY * FFGridY) - ycoord_nteil + 0.01F;
                                }
                                else
                                {
                                    ycoord_nteil = 2 * (JKOOAGRAL + (FFCellY - 1) * FFGridY) - ycoord_nteil - 0.01F;
                                }
                            }
                            else if ((IndexKOld > Program.KKART[FFCellX][FFCellY]) && (FFCellX == FFCellXPrev) && (FFCellY == FFCellYPrev))
                            {
                                zcoord_nteil = 2 * Program.AHK[FFCellX][FFCellY] - zcoord_nteil + 0.01F;
                                PartHeightAboveTerrain = zcoord_nteil - AHint;
                                if (topo == 1)
                                {
                                    PartHeightAboveBuilding = PartHeightAboveTerrain;
                                }
                                velzold = -velzold;

                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                                    int ik = (int)(xsi * ConcGridXRez) + 1;
                                    int jk = (int)(eta * ConcGridYRez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                    if (masse <= 0)
                                    {
                                        goto REMOVE_PARTICLE;
                                    }
                                }
                            }
                            else
                            {
                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                                    int ik = (int)(xsi * ConcGridXRez) + 1;
                                    int jk = (int)(eta * ConcGridYRez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                                    if (masse <= 0)
                                    {
                                        goto REMOVE_PARTICLE;
                                    }
                                }

                                if (tunpa == 0)
                                {
                                    xcoord_nteil -= (idt * UXint + corx);
                                    ycoord_nteil -= (idt * UYint + cory);
                                    zcoord_nteil -= (idt * (UZint + velz) + DeltaZHurley);
                                    PartHeightAboveTerrain = zcoord_nteil - AHint;
                                    if (topo == 1)
                                    {
                                        PartHeightAboveBuilding = PartHeightAboveTerrain;
                                    }
                                }

                                int vorzeichen = 1;
                                if (velzold < 0)
                                {
                                    vorzeichen = -1;
                                }

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
                            if (velxold < 0)
                            {
                                vorzeichen1 = -1;
                            }

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
                                velxold = MathF.Abs(velxold);
                            }
                            else
                            {
                                velxold = -MathF.Abs(velxold);
                            }

                            vorzeichen1 = 1;
                            if (velyold < 0)
                            {
                                vorzeichen1 = -1;
                            }

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
                                velyold = MathF.Abs(velyold);
                            }
                            else
                            {
                                velyold = -MathF.Abs(velyold);
                            }

                            back = 1;
                            idt = Program.FloatMax(idt * 0.5F, 0.05F);
                            if (tunpa > 0)
                            {
                                goto MOVE_FORWARD;
                            }
                        }
                    }

                    if (back > 0)
                    {
                        reflexion_flag = 1;
                        reflexion_number++;
                        //in rare cases, particles get trapped near solid boundaries. Such particles are abandoned after a certain number of reflexions
                        if (reflexion_number > Max_Reflections)
                        {
                            goto REMOVE_PARTICLE;
                        }
                    }

                }   //END OF THE REFLEXION ALGORITHM

                //interpolation of orography
                AHintold = AHint;
                if (topo == 1)
                {
                    if ((FFCellX < 1) || (FFCellX > Program.NII) || (FFCellY < 1) || (FFCellY > Program.NJJ))
                    {
                        goto REMOVE_PARTICLE;
                    }

                    AHint = Program.AHK[FFCellX][FFCellY];

                    PartHeightAboveTerrain = zcoord_nteil - AHint;
                    PartHeightAboveBuilding = PartHeightAboveTerrain;
                }
                else
                {
                    if ((FFCellX < 1) || (FFCellX > Program.NII) || (FFCellY < 1) || (FFCellY > Program.NJJ))
                    {
                        goto REMOVE_PARTICLE;
                    }

                    PartHeightAboveTerrain = zcoord_nteil;
                    PartHeightAboveBuilding = PartHeightAboveTerrain;
                }

                //reflexion at the surface and at the top of the boundary layer               
                if (Program.AdaptiveRoughnessMax > 0)
                {
                    ObL = Program.OLGral[FFCellX][FFCellY];
                }
                else
                {
                    ObL = Program.Ob[GrammCellX][GrammCellY];
                }

                if ((PartHeightAboveBuilding >= blh) && (ObL < 0))
                {
                    if ((topo == 1) && (UZint >= 0) && Program.ISTATIONAER != 0) //26042020 (Ku): removed for transient mode -> particles shoulb be tracked above blh
                    {
                        goto REMOVE_PARTICLE;
                    }

                    zcoord_nteil = blh + AHint - (PartHeightAboveBuilding - blh) - 0.01F;
                    velzold = -velzold;
                    PartHeightAboveTerrain = zcoord_nteil - AHint;
                    PartHeightAboveBuilding = PartHeightAboveTerrain;
                }

                if (PartHeightAboveTerrain <= 0)
                {
                    zcoord_nteil = AHint - PartHeightAboveTerrain + 0.01F;
                    velzold = -velzold;
                    PartHeightAboveTerrain = zcoord_nteil - AHint;
                    PartHeightAboveBuilding = PartHeightAboveTerrain;

                    // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                    if (Deposition_type > 0 && depo_reflection_counter >= 0)
                    {
                        float Pd1 = Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                        int ik = (int)(xsi * ConcGridXRez) + 1;
                        int jk = (int)(eta * ConcGridYRez) + 1;
                        double[] depo_L = Program.Depo_conz[ik][jk];
                        double conc = masse * Pd1 * area_rez_fac;
                        lock (depo_L)
                        {
                            depo_L[SG_nteil] += conc;
                        }
                        masse -= masse * Pd1;
                        depo_reflection_counter = -2; // block deposition 1 for 1 timestep

                        if (masse <= 0)
                        {
                            goto REMOVE_PARTICLE;
                        }
                    }
                }

            MOVE_TO_CONCENTRATIONCALCULATION:

                //coordinate indices
                int iko = (int)(xsi * ConcGridXRez) + 1;
                int jko = (int)(eta * ConcGridYRez) + 1;

                if ((iko > Program.NXL) || (iko < 0) || (jko > Program.NYL) || (jko < 0))
                {
                    goto REMOVE_PARTICLE;
                }

                float zcoordRelative = 0;
                if (Program.BuildingsExist == true)
                {
                    zcoordRelative = zcoord_nteil - AHint + Program.CUTK[FFCellX][FFCellY];
                }
                else
                {
                    zcoordRelative = zcoord_nteil - AHint;
                }
                for (int II = 0; II < kko.Length; II++)
                {
                    float slice = (zcoordRelative - Program.HorSlices[II]) * ConcGridZRez;
                    if (Math.Abs(slice) > Int16.MaxValue)
                    {
                        goto REMOVE_PARTICLE;
                    }

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
                    {
                        goto REMOVE_PARTICLE;
                    }
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
                                    float slice = (zcoord_nteil - AHint - Program.ReceptorZ[irec]) * ConcGridZRez;
                                    if (Program.ConvToInt(slice) == 0)
                                    {
                                        ReceptorConcentration[irec] += idt * masse;
                                    }
                                }
                            }
                            else // use the concentration at the receptor position x +- GralDx/2 and receptor y +- GralDy/2
                            {
                                if (Math.Abs(xcoord_nteil - Program.ReceptorX[irec]) < ConcGridXHalf &&
                                    Math.Abs(ycoord_nteil - Program.ReceptorY[irec]) < ConcGridYHalf)
                                {
                                    float slice = (zcoord_nteil - AHint - Program.ReceptorZ[irec]) * ConcGridZRez;
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
                            TransientGridZ = TransientConcentration.BinarySearchTransient(zcoord_nteil - AHint);
                            TransientGridX = (int)(xsi * FFGridXRez) + 1;
                            TransientGridY = (int)(eta * FFGridYRez) + 1;

                            float[] conzsum_L = Program.ConzSsum[TransientGridX][TransientGridY];
                            double conc = masse * Program.GridVolume * idt / (Area_cart * Program.DZK_Trans[TransientGridZ]);
                            lock (conzsum_L)
                            {
                                conzsum_L[TransientGridZ] += (float)conc;
                            }
                        }
                    }

                    //ODOUR Dispersion requires the concentration fields above and below the acutal layer
                    if (Program.Odour == true)
                    {
                        for (int II = 0; II < kko.Length; II++)
                        {
                            float slice = (zcoordRelative - (Program.HorSlices[II] + Program.GralDz)) * ConcGridZRez;
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
                            float slice = (zcoordRelative - (Program.HorSlices[II] - Program.GralDz)) * ConcGridZRez;
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
            {
                sign = -1;
            }

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
            {
                return Pd;
            }
            else
            {
                return 1;
            }
        }

    }
}
