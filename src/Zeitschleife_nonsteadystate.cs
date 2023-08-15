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
    /// <summary>
    ///Time loop for the transient stored particles in the transient mode 
    /// </summary>
    partial class ZeitschleifeNonSteadyState
    {
        private const float Pi2F = 2F * MathF.PI;
        private const float RNG_Const = 2.328306435454494e-10F;

        /// <summary>
        /// Organize the particles released from the transient grid  
        /// </summary>
        /// <param name="i">x grid position within the transient concentration grid</param>
        /// <param name="j">y grid position within the transient concentration grid</param>
        /// <param name="k">z grid position within the transient concentration grid</param>
        /// <param name="SG">Source group</param>
        /// <param name="TransConcentration">Concentration of the transient concentration cell [i][j][k]</param>
        /// <remarks>
        /// This class is the particle driver for particles, released by the transient grid in the transient GRAL mode
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Calculate(int i, int j, int k, int SG, float TransConcentration)
        {
            double Vol_ratio = Program.DXK * Program.DYK * Program.DZK_Trans[k] / Program.GridVolume; // volume of this transient grid cell
            double masse = TransConcentration * Vol_ratio / Program.TAUS;     //mass related to the concentration grid
            
            // how many particles (on average) were summed in this cell?
            int transConcentrationFactor = (int)(masse / Program.ParticleMassMean);
            // Split one transient particle into sub-particles if more than 10 average particles have been summed - reduce stat. error
            int transParticlesSplit = transConcentrationFactor / 10 + 1;

            if (transParticlesSplit <= 1)
            {
                ParticleDriver(i, j, k, SG, TransConcentration);
            }
            else
            {
                // call particle driver with splitted transient particles
                float transConcSplit = TransConcentration / transParticlesSplit;
                for (int p = 0; p < transParticlesSplit; p++)
                {
                    ParticleDriver(i, j, k, SG, transConcSplit);
                }
            }
        }

        /// <summary>
        /// Time loop for the particles released from the transient grid  
        /// </summary>
        /// <param name="i">x grid position within the transient concentration grid</param>
        /// <param name="j">y grid position within the transient concentration grid</param>
        /// <param name="k">z grid position within the transient concentration grid</param>
        /// <param name="SG">Source group</param>
        /// <param name="TransConcentration">Concentration of the transient concentration cell [i][j][k]</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void ParticleDriver(int i, int j, int k, int SG, float TransConcentration)
        {
            //random number generator seeds
            Random RND = new Random();
            uint m_w = (uint)(RND.Next() + 521288629);
            uint m_z = (uint)(RND.Next() + 2232121);
            
            float zahl1 = 0;
            uint u_rg = 0;
            float u1_rg = 0;

            //transfer grid-concentration and grid position into single particle mass and particle position
            double xcoord_nteil = Program.IKOOAGRAL + i * Program.DXK - (0.25 + RND.NextDouble() * 0.5) * Program.DXK;
            double ycoord_nteil = Program.JKOOAGRAL + j * Program.DYK - (0.25 + RND.NextDouble() * 0.5) * Program.DYK;
            float zcoord_nteil = (float)(Program.AHK[i][j] + Program.HoKartTrans[k] - (0.3 + RND.NextDouble() * 0.5) * Program.DZK_Trans[k]);
            RND = null;

            double Vol_ratio = Program.DXK * Program.DYK * Program.DZK_Trans[k] / Program.GridVolume;
            double Vol_cart = Program.DXK * Program.DYK * Program.DZK_Trans[k];
            double Area_cart = Program.DXK * Program.DYK;
            double masse = TransConcentration * Vol_ratio / Program.TAUS;     //mass related to the concentration grid, but not for the memory-effect grid
            double mass_real = TransConcentration * Vol_cart / Program.TAUS;  //mass related to the memory-effect grid

            if (mass_real == 0 || masse == 0)
            {
                return;
            }

            int SG_nteil = SG;

            // deposition parameters
            int Deposition_type = 0; // 0 = no deposition, 1 = depo + conc, 2 = only deposition
            float vsed = 0;
            float vdep = 0;
            // set mean deposition settings per source group
            if (Program.TransientDepo != null && Program.TransientDepo[SG_nteil].DepositionMode > 0)
            {
                vsed = (float)Program.TransientDepo[SG_nteil].Vsed;
                vdep = (float)Program.TransientDepo[SG_nteil].Vdep;
                Deposition_type = Program.TransientDepo[SG_nteil].DepositionMode;
            }

            double area_rez_fac = Program.TAUS * Program.GridVolume * 24 / 1000; // conversion factor for particle mass from µg/m³/s to mg per day
            area_rez_fac = area_rez_fac / (Program.GralDx * Program.GralDy);     // conversion to mg/m²

            float FFGridX = Program.DXK;
            float FFGridY = Program.DYK;
            float FFGridXRez = 1 / Program.DXK;
            float FFGridYRez = 1 / Program.DYK;
            float GrammGridXRez = 1 / Program.DDX[1];
            float GrammGridYRez = 1 / Program.DDY[1];
            float ConcGridXRez = 1 / Program.GralDx;
            float ConcGridYRez = 1 / Program.GralDy;
            float ConcGridZRez = 2 / Program.GralDz;
            float ConcGridXHalf = Program.GralDx * 0.5F;
            float ConcGridYHalf = Program.GralDy * 0.5F;

            int IFQ = Program.TS_Count;
            //Program.Sourcetype[0] = 3; 
            float blh = Program.BdLayHeight;
            int IKOOAGRAL = Program.IKOOAGRAL;
            int JKOOAGRAL = Program.JKOOAGRAL;

            Span<int> kko = stackalloc int[Program.NS];
            Span<double> ReceptorConcentration = stackalloc double[Program.ReceptorNumber + 1];
            float a3 = 1.1F;

            int reflexion_flag = Consts.ParticleNotReflected ;
            int ISTATISTIK = Program.IStatistics;
            int ISTATIONAER = Program.ISTATIONAER;
            int topo = Program.Topo;

            int TransientGridX = 1;
            int TransientGridY = 1;
            int TransientGridZ = 1;

            TransientConcentration trans_conz = new TransientConcentration();

            /*
             *   INITIIALIZING PARTICLE PROPERTIES -> DEPEND ON SOURCE CATEGORY
             */
            int GrammCellX = 1, GrammCellY = 1; // default value for flat terrain
            float AHint = 0;
            float AHintold = 0;
            float varw = 0, sigmauhurly = 0;

            //advection time step
            float idt = 0.1F;
            float auszeit = 0;

            float DSIGUDX = 0, DSIGUDY = 0, DSIGVDX = 0, DSIGVDY = 0;
            float DUDX = 0, DVDX = 0, DUDY = 0, DVDY = 0;

            //decay rate
            double decay_rate = Program.DecayRate[Program.SourceGroups[SG]]; // decay rate for real source group number of this source

            //cell-indices of particles moving in the microscale flow field
            double xsi = xcoord_nteil - IKOOAGRAL;
            double eta = ycoord_nteil - JKOOAGRAL;
            double xsi1 = xcoord_nteil - Program.GrammWest;
            double eta1 = ycoord_nteil - Program.GrammSouth;
            if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
            {
                return; // goto REMOVE_PARTICLE;
            }

            int Max_Reflections = Math.Min(100000, Program.NII * Program.NJJ * 2); // max. 2 reflections per cell in transient mode

            int FFCellX;
            int FFCellY;

            //interpolated orography
            float PartHeightAboveTerrain = 0;
            float PartHeightAboveBuilding = 0;

            if (topo == Consts.TerrainAvailable)
            {
                //with terrain
                FFCellX = (int)(xsi * FFGridXRez) + 1;
                FFCellY = (int)(eta * FFGridYRez) + 1;
                if ((FFCellX < 1) || (FFCellX > Program.NII) || (FFCellY < 1) || (FFCellY > Program.NJJ))
                {
                    goto REMOVE_PARTICLE;
                }

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
                FFCellX = (int)(xsi * FFGridXRez) + 1;
                FFCellY = (int)(eta * FFGridYRez) + 1;
                if ((FFCellX < 1) || (FFCellX > Program.NII) || (FFCellY < 1) || (FFCellY > Program.NJJ))
                {
                    goto REMOVE_PARTICLE;
                }

                //interpolated orography
                PartHeightAboveTerrain = zcoord_nteil;
                PartHeightAboveBuilding = PartHeightAboveTerrain;
            }

            //wind-field interpolation
            float UXint = 0, UYint = 0, UZint = 0;
            int IndexK = 1;
            (UXint, UYint, UZint, IndexK) = Zeitschleife.IntWindCalculate(FFCellX, FFCellY, AHint, xcoord_nteil, ycoord_nteil, zcoord_nteil);

            float windge = MathF.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint));
            if (windge < 0.01F)
            {
                windge = 0.01F;
            }

            //variables tunpa and aufhurly needs to be defined even if not used
            float tunpa = 0;
            float aufhurly = 0;

            float ObL = Program.Ob[GrammCellX][GrammCellY];
            float roughZ0 = Program.Z0Gramm[GrammCellX][GrammCellY];
            if (Program.AdaptiveRoughnessMax > 0)
            {
                ObL = Program.OLGral[FFCellX][FFCellY];
                roughZ0 = Program.Z0Gral[FFCellX][FFCellY];
            }
            float deltaZ = roughZ0;

            //vertical interpolation of horizontal standard deviations of wind component fluctuations between observations
            (float U0int, float V0int) = Zeitschleife.IntStandCalculate(0, roughZ0, PartHeightAboveBuilding, windge, sigmauhurly);

            //remove particles above boundary-layer
            //if ((PartHeightAboveBuilding > blh) && (Program.Ob[GrammCellX][GrammCellY] < 0)) //26042020 (Ku): removed in transient mode -> particles shoulb be tracked above blh
            //   goto REMOVE_PARTICLE;

            //initial turbulent velocities
            float velxold = 0;
            float velyold = 0;
            float velzold = 0;

            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
            u_rg = (m_z << 16) + m_w;
            u1_rg = (u_rg + 1) * 2.328306435454494e-10F;
            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
            u_rg = (m_z << 16) + m_w;
            zahl1 = MathF.Sqrt(-2F * MathF.Log(u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

            velxold = U0int * zahl1;

            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
            u_rg = (m_z << 16) + m_w;
            u1_rg = (u_rg + 1) * 2.328306435454494e-10F;
            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
            u_rg = (m_z << 16) + m_w;
            zahl1 = MathF.Sqrt(-2F * MathF.Log(u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

            velyold = V0int * zahl1;

            //dispersion time of a particle
            auszeit = 0;

            int depo_reflection_counter = -5; // counter to ensure, a particle moves top to down

            int reflexion_number = 0;
            int timestep_number = 0;
            int Max_Loops = (int)(3E6 + Math.Min(9.7E7,
                Math.Max(Math.Abs(Program.EtaMaxGral - Program.EtaMinGral), Math.Abs(Program.XsiMaxGral - Program.XsiMinGral)) * 1000)); // max. Loops 1E6 (max. nr. of reflexions) + 2E6 Min + max(x,y)/0.001 

        /*
         *      LOOP OVER THE TIMESTEPS
         */
        float ObLength = 0;
        float Ustern = 0;
        MOVE_FORWARD:
            //for (int izeit = 1; izeit <= 100000000; izeit++)
            while (timestep_number <= Max_Loops)
            {
                ++timestep_number;
                double xcoord_nteil_Prev = xcoord_nteil;
                double ycoord_nteil_Prev = ycoord_nteil;
                float zcoord_nteil_Prev = zcoord_nteil;
                int FFCellXPrev = FFCellX;
                int FFCellYPrev = FFCellY;

                //if particle is within the user-defined tunnel-entrance zone it is removed (sucked-into the tunnel)
                if (Program.TunnelEntr == true)
                {
                    if ((Program.TUN_ENTR[FFCellX][FFCellY] == 1) && (PartHeightAboveTerrain <= 5))
                    {
                        goto REMOVE_PARTICLE;
                    }
                }

                //interpolate wind field
                (UXint, UYint, UZint, IndexK) = Zeitschleife.IntWindCalculate(FFCellX, FFCellY, AHint, xcoord_nteil, ycoord_nteil, zcoord_nteil);
                windge = MathF.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint));
                if (windge == 0)
                {
                    windge = 0.01F;
                }

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
                                    MathF.Pow(1F + 0.5F * MathF.Pow(PartHeightAboveBuilding / MathF.Abs(ObLength), 0.8F), 1.8F);
                depo_reflection_counter++;
                float W0int = 0; float varw2 = 0; float skew = 0; float curt = 0; float dskew = 0;
                float wstern = Ustern * 2.9F;
                float vert_blh = PartHeightAboveBuilding / blh;
                float hterm = (1 - vert_blh);
                float varwu = 0; float dvarw = 0; float dcurt = 0;

                if (ObLength >= 0)
                {
                    if (ISTATISTIK != Consts.MeteoSonic)
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
                    if (ISTATISTIK == Consts.MeteoSonic)
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
                        skew = a3 * PartHeightAboveBuilding * Program.Pow2(hterm) * Program.Pow3(wstern) / blh;
                        dskew = a3 * Program.Pow3(wstern) / blh * Program.Pow2(hterm) - 2 * a3 * Program.Pow3(wstern) * PartHeightAboveBuilding * hterm / Program.Pow2(blh);
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
                idt = 0.1F * varw / eps;
                float idtt = 0.5F * Math.Min(Program.GralDx, FFGridX) / (windge + MathF.Sqrt(Program.Pow2(velxold) + Program.Pow2(velyold)));
                if (idtt < idt)
                {
                    idt = idtt;
                }

                Math.Clamp(idt, 0.1F, 4F);

                //control dispersion time of particles
                auszeit += idt;
                if (auszeit >= Program.DispTimeSum)
                {
                    trans_conz.Conz5dZeitschleifeTransient(reflexion_flag, zcoord_nteil, AHint, mass_real, Area_cart, idt, xsi, eta, SG_nteil);
                    goto REMOVE_PARTICLE;
                }

                float dummyterm = curt - Program.Pow2(skew) / varw - varw2;
                if (dummyterm == 0)
                {
                    dummyterm = 0.000001F;
                }

                float alpha = (0.3333F * dcurt - skew / (2 * varw) * (dskew - Program.C0z * eps) - varw * dvarw) / dummyterm;
                //alpha needs to be bounded to keep algorithm stable
                Math.Clamp(alpha, -0.1F, 0.1F);

                float beta = (dskew - 2 * skew * alpha - Program.C0z * eps) / (2 * varw);

                float gamma = dvarw - varw * alpha;

                //particle acceleration
                float acc = alpha * Program.Pow2(velzold) + beta * velzold + gamma;

                //new vertical wind speed
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                u1_rg = (u_rg + 1) * 2.328306435454494e-10F;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                zahl1 = MathF.Sqrt(-2F * MathF.Log(u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);
                ////float zahl1 = (float)SimpleRNG.GetNormal();
                Math.Clamp(zahl1, -2, 2);
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

                zcoord_nteil += (velz + UZint) * idt;
                velzold = velz;

                if (Deposition_type > 0 && vsed > 0) // compute sedimentation for this particle
                {
                    zcoord_nteil -= vsed * idt;
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
                (U0int, V0int) = Zeitschleife.IntStandCalculate(0, roughZ0, PartHeightAboveBuilding, windge, sigmauhurly);

                //determination of the meandering parameters according to Oettl et al. (2006)
                float param = 0;
                if (ISTATISTIK != Consts.MeteoSonic)
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
                    if (ISTATISTIK != Consts.MeteoSonic)
                    {
                        float Tstern = 200 * param + 350;
                        T3 = Tstern / 6.28F / (Program.Pow2(param) + 1) * param;
                    }
                    else if (ISTATISTIK == Consts.MeteoSonic)
                    {
                        T3 = Program.TWindMeander;
                    }

                    termq = param / (Program.Pow2(param) + 1) / T3;
                }

                //random numbers for horizontal turbulent velocities
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                u1_rg = (u_rg + 1) * 2.328306435454494e-10F;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                float zuff1 = MathF.Sqrt(-2F * MathF.Log(u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                //float zuff1 = (float)SimpleRNG.GetNormal();
                Math.Clamp(zuff1, -2, 2);

                //random numbers for horizontal turbulent velocities
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                u1_rg = (u_rg + 1) * 2.328306435454494e-10F;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                float zuff2 = MathF.Sqrt(-2F * MathF.Log(u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                //float zuff2 = (float)SimpleRNG.GetNormal();
                Math.Clamp(zuff1, -2, 2);

                float velX = velxold + UXint;
                float velY = velyold + UYint;
                //horizontal Langevin equations
                float velx = velxold - (termp * velxold + termq * velyold) * idt + U0int * MathF.Sqrt(2 * termp * idt) * zuff1
                + idt * (DUDX * velX + DUDY * velY + U0int * DSIGUDX)
                + idt * (velxold / U0int * (DSIGUDX * velX + DSIGUDY * velY));

                float vely = velyold - (termp * velyold - termq * velxold) * idt + V0int * MathF.Sqrt(2 * termp * idt) * zuff2
                + idt * (DVDX * velX + DVDY * velY + V0int * DSIGVDY)
                + idt * (velyold / V0int * (DSIGVDX * velX + DSIGVDY * velY));
                velxold = velx;
                velyold = vely;
                //update coordinates
                xcoord_nteil += idt * UXint + corx;
                ycoord_nteil += idt * UYint + cory;

                //in case that particle was reflected, flag is set to 1 and subsequently concentrations are not computed
                reflexion_flag = Consts.ParticleNotReflected;
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

                    if (topo == Consts.TerrainAvailable)
                    {
                        //with topography
                        FFCellXPrev = FFCellX;
                        FFCellYPrev = FFCellY;
                        FFCellX = (int)(xsi * FFGridXRez) + 1;
                        FFCellY = (int)(eta * FFGridYRez) + 1;
                        if ((FFCellX > Program.NII) || (FFCellY > Program.NJJ) || (FFCellX < 1) || (FFCellY < 1))
                        {
                            goto REMOVE_PARTICLE;
                        }

                        GrammCellX = Math.Clamp((int)(xsi1 * GrammGridXRez) + 1, 1, Program.NX);
                        GrammCellY = Math.Clamp((int)(eta1 * GrammGridYRez) + 1, 1, Program.NY);
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
                    if (Program.BuildingsExist == true || topo == Consts.TerrainAvailable)
                    {
                        int IndexKOld = IndexK;
                        IndexK = Zeitschleife.BinarySearch(zcoord_nteil - Program.AHMIN); //19.05.25 Ku

                        // Particle below building or terrain
                        if (IndexK <= Program.KKART[FFCellX][FFCellY])
                        {
                            // The particle enters a building or terrain step horizontally
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
                            // Reflect the particle in x direction
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
                            // Reflect the particle in y direction
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
                            // Particle comes from z direction on the same cell 
                            else if ((IndexKOld > Program.KKART[FFCellX][FFCellY]) && (FFCellX == FFCellXPrev) && (FFCellY == FFCellYPrev))
                            {
                                zcoord_nteil = 2 * Program.AHK[FFCellX][FFCellY] - zcoord_nteil + 0.01F;
                                PartHeightAboveTerrain = zcoord_nteil - AHint;
                                if (topo == Consts.TerrainAvailable)
                                {
                                    PartHeightAboveBuilding = PartHeightAboveTerrain;
                                }
                                velzold = -velzold;

                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > Consts.DepoOff && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                                    int ik = (int)(xsi * ConcGridXRez) + 1;
                                    int jk = (int)(eta * ConcGridYRez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for the entire reflection algorithm and 1 timestep 

                                    if (masse <= 0)
                                    {
                                        goto REMOVE_PARTICLE;
                                    }
                                }
                            }
                            // Particle comes from the z direction (above the recent cell) and from another cell
                            else if ((IndexKOld > Program.KKART[FFCellX][FFCellY]))
                            {
                                if (Deposition_type > Consts.DepoOff && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                                    int ik = (int)(xsi * ConcGridXRez) + 1;
                                    int jk = (int)(eta * ConcGridYRez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for the entire reflection algorithm and 1 timestep 

                                    if (masse <= 0)
                                    {
                                        goto REMOVE_PARTICLE;
                                    }
                                }
                                float terrainHeight = Program.AHK[FFCellX][FFCellY];
                                zcoord_nteil = MathF.Max(terrainHeight, 2 * terrainHeight - zcoord_nteil + 0.01F);
                            }
                            // Particle comes from the z direction (below the recent cell, particle direction up or down) and from another cell
                            else
                            {
                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > Consts.DepoOff && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
                                    int ik = (int)(xsi * ConcGridXRez) + 1;
                                    int jk = (int)(eta * ConcGridYRez) + 1;
                                    double[] depo_L = Program.Depo_conz[ik][jk];
                                    double conc = masse * Pd1 * area_rez_fac;
                                    lock (depo_L)
                                    {
                                        depo_L[SG_nteil] += conc;
                                    }
                                    masse -= masse * Pd1;
                                    depo_reflection_counter = -2; // block deposition 1 for the entire reflection algorithm and 1 timestep 

                                    if (masse <= 0)
                                    {
                                        goto REMOVE_PARTICLE;
                                    }
                                }

                                if (tunpa == 0)
                                {
                                    // Terrain following particles at small steps if there is no building
                                    if (Program.CUTK[FFCellX][FFCellY] < 1 && (Program.AHK[FFCellX][FFCellY] - zcoord_nteil) < 10)
                                    {
                                        // Move particle above the new terrain surface
                                        zcoord_nteil = Program.AHK[FFCellX][FFCellY] + MathF.Abs(idt * (UZint + velz));
                                    }
                                    else
                                    // Below Building or high terrain step
                                    {
                                        // reset horizontal coordinates
                                        double deltax = xcoord_nteil - xcoord_nteil_Prev;
                                        double deltay = ycoord_nteil - ycoord_nteil_Prev;
                                        
                                        xcoord_nteil -= deltax * 1.05;
                                        ycoord_nteil -= deltay * 1.05;
                                        //zcoord_nteil -= (idt * (UZint + velz) + DeltaZHurley);

                                        FFCellXPrev = FFCellX;
                                        FFCellYPrev = FFCellY;

                                        FFCellX = (int)((xcoord_nteil - IKOOAGRAL) * FFGridXRez) + 1;
                                        FFCellY = (int)((ycoord_nteil - JKOOAGRAL) * FFGridYRez) + 1;
                                        if ((FFCellX > Program.NII) || (FFCellY > Program.NJJ) || (FFCellX < 1) || (FFCellY < 1))
                                        {
                                            goto REMOVE_PARTICLE;
                                        }
                                        float newTerrainHeight = Program.AHK[FFCellX][FFCellY];
                                        // if below new terrain -> multiple reflections
                                        if (zcoord_nteil < newTerrainHeight)
                                        {
                                            zcoord_nteil = 2 * newTerrainHeight - zcoord_nteil;
                                        }

                                        AHintold = AHint;
                                        AHint = newTerrainHeight;
                                        PartHeightAboveTerrain = zcoord_nteil - AHint;
                                        if (topo == Consts.TerrainAvailable)
                                        {
                                            PartHeightAboveBuilding = PartHeightAboveTerrain;
                                        }
                                    }
                                }
                                
                                int vorzeichen = -1;
                                if (velzold < 0)
                                {
                                    vorzeichen = 1;
                                }

                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                u1_rg = (u_rg + 1) * 2.328306435454494e-10F;
                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                zahl1 = MathF.Sqrt(-2F * MathF.Log(u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                                velzold = MathF.Abs(zahl1 * MathF.Sqrt(varw)) * vorzeichen;
                                
                            }

                            int vorzeichen1 = -1;
                            if (velxold < 0)
                            {
                                vorzeichen1 = 1;
                            }

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10F;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log(u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);
                            velxold = MathF.Abs(zahl1 * U0int * 3) * vorzeichen1;
                            
                            vorzeichen1 = -1;
                            if (velyold < 0)
                            {
                                vorzeichen1 = 1;
                            }

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10F;
                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            zahl1 = MathF.Sqrt(-2F * MathF.Log(u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);
                            velyold = MathF.Abs(zahl1 * V0int * 3) * vorzeichen1;
                            
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
                        reflexion_flag = Consts.ParticleReflected;
                        reflexion_number++;
                        //in rare cases, particles get trapped near solid boundaries. Such particles are abandoned after a certain number of reflexions
                        if (reflexion_number > Max_Reflections) // reduced number in transient cases
                        {
                            goto REMOVE_PARTICLE;
                        }
                    }

                }   //END OF THE REFLEXION ALGORITHM

                //interpolation of orography
                AHintold = AHint;
                if (topo == Consts.TerrainAvailable)
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
                ObL = Program.Ob[GrammCellX][GrammCellY];
                if (Program.AdaptiveRoughnessMax > 0)
                {
                    ObL = Program.OLGral[FFCellX][FFCellY];
                }
                if ((PartHeightAboveBuilding >= blh) && (ObL < 0))
                {
                    //if ((topo == 1) && (UZint >= 0)) //26042020 (Ku): removed in transient mode -> particles shoulb be tracked above blh
                    //    goto REMOVE_PARTICLE;

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
                        float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, FFCellX, FFCellY);
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

                //coordinate indices
                int iko = (int)(xsi * ConcGridXRez) + 1;
                int jko = (int)(eta * ConcGridYRez) + 1;

                if ((iko > Program.NXL) || (iko < 0) || (jko > Program.NYL) || (jko < 0))
                {
                    goto REMOVE_PARTICLE;
                }

                float zcoordRelative = 0;
                if (Program.BuildingsExist == true && Program.Topo == Consts.TerrainAvailable)
                {
                    //AHint contains the building height if terrain is available therefore the terrain height has to be corrected
                    zcoordRelative = zcoord_nteil - AHint + Program.CUTK[FFCellX][FFCellY];
                }
                else
                {
                    zcoordRelative = zcoord_nteil - AHint;
                }
                for (int II = 0; II < kko.Length; II++)
                {
                    float slice = (zcoordRelative - Program.HorSlices[II]) * ConcGridZRez;
                    if (MathF.Abs(slice) > Int16.MaxValue)
                    {
                        goto REMOVE_PARTICLE;
                    }

                    kko[II] = (int) slice;
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
                        ex = true; // masse <= 0!
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
                    if ((Program.ReceptorsAvailable) && (reflexion_flag == Consts.ParticleNotReflected))
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
                                    if ((int) slice == 0)
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
                                    if ((int)(slice) == 0)
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
                        if ((kko[II] == 0) && (reflexion_flag == Consts.ParticleNotReflected))
                        {
                            float[] conz3d_L = Program.Conz3d[iko][jko][II];
                            double conc = masse * idt;
                            lock (conz3d_L)
                            {
                                conz3d_L[SG_nteil] += (float)conc;
                            }
                        }
                    }

                    //count particles in case of transient dispersion for the 3D concentration file
                    if (reflexion_flag == Consts.ParticleNotReflected && Program.WriteVerticalConcentration)
                    {
                        TransientGridZ = TransientConcentration.BinarySearchTransient(zcoord_nteil - AHint);
                        TransientGridX = (int)(xsi * FFGridXRez) + 1;
                        TransientGridY = (int)(eta * FFGridYRez) + 1;

                        float[] conzsum_L = Program.ConzSsum[TransientGridX][TransientGridY];
                        double conc = mass_real * idt / (Area_cart * Program.DZK_Trans[TransientGridZ]);
                        lock (conzsum_L)
                        {
                            conzsum_L[TransientGridZ] += (float)conc;
                        }
                    }

                    //ODOUR Dispersion requires the concentration fields above and below the acutal layer
                    if (Program.Odour == true)
                    {
                        for (int II = 0; II < kko.Length; II++)
                        {
                            float slice = (zcoordRelative - (Program.HorSlices[II] + Program.GralDz)) * ConcGridZRez;
                            kko[II] = (int)(Math.Min(int.MaxValue, slice));
                        }
                        for (int II = 0; II < kko.Length; II++)
                        {
                            if ((kko[II] == 0) && (reflexion_flag == Consts.ParticleNotReflected))
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
                            kko[II] = (int)(Math.Min(int.MaxValue, slice));
                        }
                        for (int II = 0; II < kko.Length; II++)
                        {
                            if ((kko[II] == 0) && (reflexion_flag == Consts.ParticleNotReflected))
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

            // Add local receptor concentrations to receptor array and store maximum concentration part for each receptor
            if (Program.ReceptorsAvailable)
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
            return;
        }
    }
}
