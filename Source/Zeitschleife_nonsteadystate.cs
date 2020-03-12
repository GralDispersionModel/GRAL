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
        /// <summary>
        ///Time loop for the particles released from the transient grid 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Calculate(int i, int j, int k, int SG, float conz_trans)
        {
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

            //transfering grid-concentration into single particle mass and position
            Random rnd = new Random();
            double xcoord_nteil = Program.IKOOAGRAL + i * Program.DXK - (0.25f + (float)(rnd.NextDouble() * 0.5)) * Program.DXK;
            double ycoord_nteil = Program.JKOOAGRAL + j * Program.DYK - (0.25f + (float)(rnd.NextDouble() * 0.5)) * Program.DYK;
            float zcoord_nteil = (float)(Program.AHK[i][j] + Program.HoKartTrans[k] - (0.4f + (float)(rnd.NextDouble() * 0.2)) * Program.DZK_Trans[k]);

            double Vol_ratio = Program.DXK * Program.DYK * Program.DZK_Trans[k] / Program.GridVolume;
            double Vol_cart = Program.DXK * Program.DYK * Program.DZK_Trans[k];
            double Area_cart = Program.DXK * Program.DYK;
            double masse = conz_trans * Vol_ratio / Program.TAUS;  //mass related to the concentration grid, but not for the memory-effect grid
            double mass_real = conz_trans * Vol_cart / Program.TAUS;  //mass related to the memory-effect grid
            if (mass_real == 0) return;
            if (masse == 0) return;

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
            area_rez_fac = area_rez_fac / (Program.dx * Program.dy);  // conversion to mg/m²

            float DXK = Program.DXK;
            float DYK = Program.DYK;
            float DXK_rez = 1 / Program.DXK;
            float DYK_rez = 1 / Program.DYK;
            float DDX1_rez = 1 / Program.DDX[1];
            float DDY1_rez = 1 / Program.DDY[1];
            float dx_rez = 1 / Program.dx;
            float dy_rez = 1 / Program.dy;
            float dz_rez = 1 / Program.dz;
            float dxHalf = Program.dx * 0.5F;
            float dyHalf = Program.dy * 0.5F;

            int IFQ = Program.TS_Count;
            //Program.Sourcetype[0] = 3; 
            float blh = Program.BdLayHeight;
            int IKOOAGRAL = Program.IKOOAGRAL;
            int JKOOAGRAL = Program.JKOOAGRAL;

            Span<int> kko = stackalloc int[Program.NS + 1];
            double[] ReceptorConcentration = new double[Program.ReceptorNumber + 1];
            float a3 = 1.1F;

            int reflexion_flag = 0;
            int ISTATISTIK = Program.IStatistics;
            int ISTATIONAER = Program.ISTATIONAER;
            int topo = Program.Topo;

            int IndexI3d = 1;
            int IndexJ3d = 1;
            int IndexK3d = 1;

            TransientConcentration trans_conz = new TransientConcentration();

            /*
			 *   INITIIALIZING PARTICLE PROPERTIES -> DEPEND ON SOURCE CATEGORY
			 */
            int IUstern = 1, JUstern = 1;
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
            double xsi1 = xcoord_nteil - Program.IKOOA;
            double eta1 = ycoord_nteil - Program.JKOOA;
            if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
            {
                return; // goto REMOVE_PARTICLE;
            }

            int Max_Reflections = Math.Min(100000, Program.NII * Program.NJJ * 2); // max. 2 reflections per cell in transient mode

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
                IUstern = (int)(xsi1 * DDX1_rez) + 1;
                JUstern = (int)(eta1 * DDY1_rez) + 1;
                if ((IndexI < 1) || (IndexI > Program.NII) || (IndexJ < 1) || (IndexJ > Program.NJJ))
                    goto REMOVE_PARTICLE;

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
                if ((IndexI < 1) || (IndexI > Program.NII) || (IndexJ < 1) || (IndexJ > Program.NJJ))
                    goto REMOVE_PARTICLE;

                IndexId = (int)(xsi * DXK_rez) + 1;
                IndexJd = (int)(eta * DYK_rez) + 1;
                if ((IndexId > Program.NII) || (IndexJd > Program.NJJ) || (IndexId < 1) || (IndexJd < 1))
                    goto REMOVE_PARTICLE;

                //interpolated orography
                differenz = zcoord_nteil;
                diff_building = differenz;
            }

            //wind-field interpolation
            float UXint = 0, UYint = 0, UZint = 0;
            int IndexK = 1;
            Zeitschleife.IntWindCalculate(IndexI, IndexJ, IndexId, IndexJd, AHint, ref UXint, ref UYint, ref UZint, ref IndexK, xcoord_nteil, ycoord_nteil, zcoord_nteil);

            float windge = MathF.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint));
            if (windge == 0) windge = 0.01F;

            //variables tunpa and aufhurly needs to be defined even if not used
            float tunpa = 0;
            float aufhurly = 0;

            //vertical interpolation of horizontal standard deviations of wind component fluctuations between observations
            float U0int = 0;
            float V0int = 0;
            Zeitschleife.IntStandCalculate(0, IUstern, JUstern, diff_building, windge, sigmauhurly, ref U0int, ref V0int);

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

            int depo_reflection_counter = -5; // counter to ensure, a particle moves top to down

            int reflexion_number = 0;
            int timestep_number = 0;
            int Max_Loops = (int)(3E6 + Math.Min(9.7E7,
                Math.Max(Math.Abs(Program.EtaMaxGral - Program.EtaMinGral), Math.Abs(Program.XsiMaxGral - Program.XsiMinGral)) * 1000)); // max. Loops 1E6 (max. nr. of reflexions) + 2E6 Min + max(x,y)/0.001 

        /*
         *      LOOP OVER THE TIMESTEPS
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
                Zeitschleife.IntWindCalculate(IndexI, IndexJ, IndexId, IndexJd, AHint, ref UXint, ref UYint, ref UZint, ref IndexK, xcoord_nteil, ycoord_nteil, zcoord_nteil);
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
                idt = 0.1F * varw / eps;
                float idtt = (0.5F * Math.Min(Program.dx, DXK) / (windge + MathF.Sqrt(Program.Pow2(velxold) + Program.Pow2(velyold))));
                if (idtt < idt) idt = idtt;
                Math.Clamp(idt, 0.1F, 4F);

                //control dispersion time of particles
                auszeit += idt;
                if (auszeit >= Program.DispTimeSum)
                {
                    trans_conz.Conz5dZeitschleifeTransient(reflexion_flag, zcoord_nteil, AHint, mass_real, Area_cart, idt, xsi, eta, SG_nteil);
                    goto REMOVE_PARTICLE;
                }

                float dummyterm = curt - Program.Pow2(skew) / varw - varw2;
                if (dummyterm == 0) dummyterm = 0.000001F;
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
                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                u_rg = (m_z << 16) + m_w;
                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                //float zahl1 = (float)SimpleRNG.GetNormal();
                if (zahl1 > 2) zahl1 = 2;
                if (zahl1 < -2) zahl1 = -2;
                float velz = (acc * idt + MathF.Sqrt(Program.C0z * eps * idt) * zahl1 + velzold);
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

                zcoord_nteil += (velz + UZint) * idt;
                velzold = velz;

                if (Deposition_type > 0 && vsed > 0) // compute sedimentation for this particle
                {
                    zcoord_nteil -= vsed * idt;
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
                Zeitschleife.IntStandCalculate(0, IUstern, JUstern, diff_building, windge, sigmauhurly, ref U0int, ref V0int);

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

                    int IndexIalt = 1;
                    int IndexJalt = 1;
                    int IndexIdalt = 1;
                    int IndexJdalt = 1;
                    if (topo == 1)
                    {
                        IndexIalt = IndexI;
                        IndexJalt = IndexJ;
                        IndexI = (int)(xsi * DXK_rez) + 1;
                        IndexJ = (int)(eta * DYK_rez) + 1;
                        if ((IndexI > Program.NII) || (IndexJ > Program.NJJ) || (IndexI < 1) || (IndexJ < 1))
                            goto REMOVE_PARTICLE;

                        IUstern = (int)(xsi1 * DDX1_rez) + 1;
                        JUstern = (int)(eta1 * DDY1_rez) + 1;
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
                        IndexK = Zeitschleife.BinarySearch(zcoord_nteil - Program.AHMIN); //19.05.25 Ku

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
                                    float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
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
                                    float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
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
                                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);

                                velzold = zahl1 * MathF.Sqrt(varw);
                                if (vorzeichen < 0)
                                {
                                    velzold = MathF.Abs(velzold);
                                }
                                else
                                {
                                    velzold = -MathF.Abs(velzold);
                                }
                            }

                            int vorzeichen1 = 1;
                            if (velxold < 0) vorzeichen1 = -1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
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
                            if (velyold < 0) vorzeichen1 = -1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
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
                                goto MOVE_FORWARD;
                        }
                    }
                    else if ((topo == 0) && (Program.BuildingFlatExist == true))
                    {
                        int IndexKold = IndexK;
                        IndexK = Zeitschleife.BinarySearch(zcoord_nteil); //19.05.25 Ku

                        //particle inside buildings
                        if (IndexK <= Program.KKART[IndexId][IndexJd])
                        {
                            if ((IndexK > Program.KKART[IndexIdalt][IndexJdalt]) && (IndexK == IndexKold))
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
                            else if ((IndexK > Program.KKART[IndexIdalt][IndexJd]) && (IndexK == IndexKold))
                            {
                                if (idt * UXint + corx <= 0)
                                    xcoord_nteil = 2 * (IKOOAGRAL + IndexId * DXK) - xcoord_nteil + 0.01F;
                                else
                                    xcoord_nteil = 2 * (IKOOAGRAL + (IndexId - 1) * DXK) - xcoord_nteil - 0.01F;
                            }
                            else if ((IndexK > Program.KKART[IndexId][IndexJdalt]) && (IndexK == IndexKold))
                            {
                                if (idt * UYint + cory <= 0)
                                    ycoord_nteil = 2 * (JKOOAGRAL + IndexJd * DYK) - ycoord_nteil + 0.01F;
                                else
                                    ycoord_nteil = 2 * (JKOOAGRAL + (IndexJd - 1) * DYK) - ycoord_nteil - 0.01F;
                            }
                            else if ((IndexKold > Program.KKART[IndexId][IndexJd]) && (IndexId == IndexIdalt) && (IndexJd == IndexJdalt))
                            {
                                zcoord_nteil = 2 * Program.AHK[IndexId][IndexJd] - zcoord_nteil + 0.01F;
                                differenz = zcoord_nteil - AHint;
                                velzold = -velzold;

                                // compute deposition according to VDI 3945 for this particle- add deposition to Depo_conz[][][]
                                if (Deposition_type > 0 && depo_reflection_counter >= 0)
                                {
                                    float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
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
                                    float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
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
                                u1_rg = (u_rg + 1) * 2.328306435454494e-10;
                                m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                                m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                                u_rg = (m_z << 16) + m_w;
                                zahl1 = MathF.Sqrt(-2F * MathF.Log((float)u1_rg)) * MathF.Sin(Pi2F * (u_rg + 1) * RNG_Const);
                                velzold = zahl1 * MathF.Sqrt(varw);
                                if (vorzeichen < 0)
                                {
                                    velzold = MathF.Abs(velzold);
                                }
                                else
                                {
                                    velzold = -MathF.Abs(velzold);
                                }
                            }

                            int vorzeichen1 = 1;
                            if (velxold < 0) vorzeichen1 = -1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
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
                            if (velyold < 0) vorzeichen1 = -1;

                            m_z = 36969 * (m_z & 65535) + (m_z >> 16);
                            m_w = 18000 * (m_w & 65535) + (m_w >> 16);
                            u_rg = (m_z << 16) + m_w;
                            u1_rg = (u_rg + 1) * 2.328306435454494e-10;
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
                                goto MOVE_FORWARD;
                        }
                    }

                    if (back > 0)
                    {
                        reflexion_flag = 1;
                        reflexion_number++;
                        //in rare cases, particles get trapped near solid boundaries. Such particles are abandoned after a certain number of reflexions
                        if (reflexion_number > Max_Reflections) // reduced number in transient cases
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
                        float Pd1 = Zeitschleife.Pd(varw, vsed, vdep, Deposition_type, IndexI, IndexJ);
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
                for (int II = 1; II < kko.Length; II++)
                {
                    float slice = (zcoordRelative - Program.HorSlices[II]) * dz_rez;
                    if (Math.Abs(slice) > Int16.MaxValue)
                        goto REMOVE_PARTICLE;
                    kko[II] = Program.ConvToInt(slice);
                }

                //decay rate
                if (decay_rate > 0)
                    masse *= Math.Exp(-decay_rate * idt);

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
                            else // use the concentration at the receptor position x +- dx/2 and receptor y +- dy/2
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
                    for (int II = 1; II < kko.Length; II++)
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

                    //count particles in case of transient dispersion for the 3D concentration file
                    if (reflexion_flag == 0 && Program.WriteVerticalConcentration)
                    {
                        IndexK3d = TransientConcentration.BinarySearchTransient(zcoord_nteil - AHint);
                        IndexI3d = (int)(xsi * DXK_rez) + 1;
                        IndexJ3d = (int)(eta * DYK_rez) + 1;

                        float[] conzsum_L = Program.ConzSsum[IndexI3d][IndexJ3d];
                        double conc = mass_real * idt / (Area_cart * Program.DZK_Trans[IndexK3d]);
                        lock (conzsum_L)
                        {
                            conzsum_L[IndexK3d] += (float)conc;
                        }
                    }

                    //ODOUR Dispersion requires the concentration fields above and below the acutal layer
                    if (Program.Odour == true)
                    {
                        for (int II = 1; II < kko.Length; II++)
                        {
                            float slice = (zcoordRelative - (Program.HorSlices[II] + Program.dz)) * dz_rez;
                            kko[II] = Program.ConvToInt(Math.Min(int.MaxValue, slice));
                        }
                        for (int II = 1; II < kko.Length; II++)
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
                        for (int II = 1; II < kko.Length; II++)
                        {
                            float slice = (zcoordRelative - (Program.HorSlices[II] - Program.dz)) * dz_rez;
                            kko[II] = Program.ConvToInt(Math.Min(int.MaxValue, slice));
                        }
                        for (int II = 1; II < kko.Length; II++)
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
            return;
        }
    }
}
