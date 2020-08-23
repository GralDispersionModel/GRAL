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
using System.Numerics;
using System.Threading.Tasks;

namespace GRAL_2001
{
    public static class U_PrognosticMicroscaleV1
    {
        static readonly Vector<float> Vect_0 = Vector<float>.Zero;
        static readonly Vector<float> Vect_1 = Vector<float>.One;
        static readonly Vector<float> Vect_01 = new Vector<float>(0.1F);
        static readonly Vector<float> Vect_025 = new Vector<float>(0.25F);
        static readonly Vector<float> Vect_05 = new Vector<float>(0.5F);
        static readonly Vector<float> Vect_099 = new Vector<float>(0.99F);
        static readonly Vector<float> Vect_0071 = new Vector<float>(0.071F);
        static readonly Vector<float> Vect_2 = new Vector<float>(2F);
        static readonly Vector<float> Vect_001 = new Vector<float>(0.01F);
        static readonly Vector<float> Vect_15 = new Vector<float>(15);
        static readonly Vector<float> Vect_100 = new Vector<float>(100);
        static readonly Vector<float> Vect_0001 = new Vector<float>(0.0001F);
        static readonly int SIMD = Vector<float>.Count;


        /// <summary>
        /// Momentum equations for the u wind component - algebraic mixing lenght model
        /// </summary>
        /// <param name="IS">ADI direction for x cells</param>
        /// <param name="JS">ADI direction for y cells</param>
        /// <param name="Cmueh">Constant</param>
        /// <param name="VISHMIN">Minimum horizontal turbulent exchange coefficients </param>
        /// <param name="AREAxy">Area of a flow field cell</param>
        /// <param name="UG">Geostrophic wind</param>
        /// <param name="building_Z0">Roughness of buildings</param>
        /// <param name="relax">Relaxation factor</param>
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, Single UG, float relax)
        {
            float DXK = Program.DXK; float DYK = Program.DYK;
            Vector<float> DXK_V = new Vector<float>(DXK);
            Vector<float> DYK_V = new Vector<float>(DYK);
            Vector<float> VISHMIN_V = new Vector<float>(VISHMIN);
            Vector<float> AREAxy_V = new Vector<float>(AREAxy);
            Vector<float> UG_V = new Vector<float>(UG);

            Parallel.For(3, Program.NII, Program.pOptions, i1 =>
            {
                Span<float> PIMU = stackalloc float[Program.KADVMAX + 1];
                Span<float> QIMU = stackalloc float[Program.KADVMAX + 1];
                Span<float> Mask = stackalloc float[SIMD];
                Span<float> Mask2 = stackalloc float[SIMD];
                Span<float> AIM_A = stackalloc float[SIMD];
                Span<float> BIM_A = stackalloc float[SIMD];
                Span<float> CIM_A = stackalloc float[SIMD];
                Span<float> DIMU_A = stackalloc float[SIMD];

                Vector<float> DVDXN, DVDXS, DUDXE, DUDXW, DWDXT, DWDXB;
                Vector<float> DE, DW, DS, DN, DT, DB;
                Vector<float> FE, FW, FS, FN, FT, FB;
                Vector<float> PE, PW, PS, PN, PT, PB;
                Vector<float> BIM, CIM, AE1, AW1, AS1, AN1, AIM;
                Vector<float> DIM_U, windhilf;
                Vector<float> DDPX;

                Vector<float> UKSim_LV;
                Vector<float> VKSjm_LV, WKSim_LV, WKSimM_LV, WKSkM_LV;

                Vector<float> VKSimjm_LV, VKSimjp_LV, VKSjp_LV, UKip_LV, VKSim_LV;

                Vector<float> WKS_V, VKS_V, UKS_V, DZK_V;
                Vector<float> DXKDZK, DYKDZK, AP0_V, intern, intern2;

                Vector<float> VIS, zs, mixL;
                Vector<float> UK_LV, UKim_LV;
                Vector<float> HOKART_KKART_V;

                Vector<float> Mask_V = Vector<float>.Zero;
                Vector<float> DUDZ, DVDZ;

                int i = i1;
                if (IS == -1)
                {
                    i = Program.NII - i1 + 1;
                }

                for (int j1 = 2; j1 <= Program.NJJ - 1; ++j1)
                {
                    int j = j1;
                    if (JS == -1)
                    {
                        j = Program.NJJ - j1 + 1;
                    }

                    if (Program.ADVDOM[i][j] == 1)
                    {
                        //inside a prognostic sub d omain
                        int jM1 = j - 1;
                        int jP1 = j + 1;
                        int iM1 = i - 1;
                        int iP1 = i + 1;

                        Single[] UK_L = Program.UK[i][j];
                        Single[] UKS_L = Program.UKS[i][j];
                        Single[] VKS_L = Program.VKS[i][j];
                        Single[] WKS_L = Program.WKS[i][j];
                        Single[] DPMNEW_L = Program.DPMNEW[i][j];
                        Single[] UKim_L = Program.UK[iM1][j];
                        Single[] UKip_L = Program.UK[iP1][j];
                        Single[] UKjm_L = Program.UK[i][jM1];
                        Single[] UKjp_L = Program.UK[i][jP1];
                        Single[] UKSim_L = Program.UKS[iM1][j];
                        Single[] VKSim_L = Program.VKS[iM1][j];
                        Single[] VKSimjm_L = Program.VKS[iM1][jM1];
                        Single[] VKSjm_L = Program.VKS[i][jM1];
                        Single[] VKSimjp_L = Program.VKS[iM1][jP1];
                        Single[] VKSjp_L = Program.VKS[i][jP1];
                        Single[] WKSim_L = Program.WKS[iM1][j];
                        Single[] DPMNEWim_L = Program.DPMNEW[iM1][j];

                        Single[] VKSipjp_L = Program.VKS[iP1][jP1];
                        Single[] VKSipjm_L = Program.VKS[iP1][jM1];
                        Single[] WKSip_L = Program.WK[iP1][j];
                        Single[] VK_L = Program.VK[i][j];

                        int Vert_Index_LL = Program.VerticalIndex[i][j];
                        float Ustern_terrain_helpterm = Program.UsternTerrainHelpterm[i][j];
                        float Ustern_obstacles_helpterm = Program.UsternObstaclesHelpterm[i][j];
                        bool ADVDOM_JM = (Program.ADVDOM[i][jM1] < 1) || (j == 2);
                        bool ADVDOM_JP = (Program.ADVDOM[i][jP1] < 1) || (j == Program.NJJ - 1);
                        bool ADVDOM_IM = (Program.ADVDOM[iM1][j] < 1) || (i == 2);
                        bool ADVDOM_IP = (Program.ADVDOM[iP1][j] < 1) || (i == Program.NII - 1);

                        float CUTK_L = Program.CUTK[i][j];
                        Single[] VEG_L = Program.VEG[i][j];
                        Single COV_L = Program.COV[i][j];

                        float Z0 = Program.Z0;
                        if (Program.AdaptiveRoughnessMax < 0.01)
                        {
                            //Use GRAMM Roughness with terrain or Roughness from point 1,1 without terrain
                            int IUstern = 1;
                            int JUstern = 1;
                            if ((Program.Topo == 1) && (Program.LandUseAvailable == true))
                            {
                                double x = i * Program.DXK + Program.GralWest;
                                double y = j * Program.DYK + Program.GralSouth;
                                double xsi1 = x - Program.GrammWest;
                                double eta1 = y - Program.GrammSouth;
                                IUstern = Math.Clamp((int)(xsi1 / Program.DDX[1]) + 1, 1, Program.NX);
                                JUstern = Math.Clamp((int)(eta1 / Program.DDY[1]) + 1, 1, Program.NY);
                            }
                            Z0 = Program.Z0Gramm[IUstern][JUstern];
                        }
                        else
                        {
                            Z0 = Program.Z0Gral[i][j];
                        }

                        int KKART_LL = Program.KKART[i][j];
                        HOKART_KKART_V = new Vector<float>(Program.HOKART[KKART_LL]);

                        int KSTART = 1;
                        if (CUTK_L == 0) // building heigth == 0 m
                        {
                            KSTART = KKART_LL + 1;
                        }

                        int KKART_LL_P1 = KKART_LL + 1;

                        int KEND = Vert_Index_LL + 1; // simpify for-loop
                        bool end = false; // flag for the last loop 
                        int k_real = 0;

                        for (int k = KSTART; k < KEND; k += SIMD)
                        {
                            // to compute the top levels - reduce k, remember k_real and set end to true
                            k_real = k;
                            if (k > KEND - SIMD)
                            {
                                k = KEND - SIMD;
                                end = true;
                            }
                            int kM1 = k - 1;
                            int kP1 = k + 1;

                            WKS_V = new Vector<float>(WKS_L, k);
                            VKS_V = new Vector<float>(VKS_L, k);
                            UKS_V = new Vector<float>(UKS_L, k);

                            DZK_V = new Vector<float>(Program.DZK, k);

                            DXKDZK = DXK_V * DZK_V;
                            DYKDZK = DYK_V * DZK_V;

                            //turbulence modelling
                            zs = new Vector<float>(Program.HOKART, k) - HOKART_KKART_V - DZK_V * Vect_05;

                            VIS = Vector<float>.Zero;

                            // Create Mask if KKART_LL is between k and k + SIMD
                            bool mask_v_enable = false;
                            if (KKART_LL_P1 >= k && KKART_LL_P1 < (k + SIMD))
                            {
                                for (int ii = 0; ii < Mask.Length; ++ii)
                                {
                                    if ((k + ii) > KKART_LL_P1)
                                    {
                                        Mask[ii] = 1;
                                    }
                                    else
                                    {
                                        Mask[ii] = 0;
                                    }
                                }
                                Mask_V = new Vector<float>(Mask);
                                mask_v_enable = true;
                            }

                            if ((k + SIMD) > KKART_LL_P1)
                            {
                                //k-eps model
                                //float VIS = (float)Math.Sqrt(TURB_L[k]) * zs * Cmueh;

                                //mixing-length model
                                mixL = Vector.Min(Vect_0071 * zs, Vect_100);

                                //adapted mixing-leng th within vegetation layers
                                if (COV_L > 0)
                                {
                                    mixL *= (Vect_1 - Vect_099 * new Vector<float>(COV_L));
                                    //mixL = (float)Math.Min(0.071 * Math.Max(1 - Math.Min(2 * VEG_L[kM1], 1), 0.05) * zs, 100);
                                }

                                intern = (new Vector<float>(UK_L, kP1) - new Vector<float>(UK_L, kM1)) / (Vect_2 * DZK_V);
                                DUDZ = intern * intern;

                                intern = (new Vector<float>(VK_L, kP1) - new Vector<float>(VK_L, kM1)) / (Vect_2 * DZK_V);
                                DVDZ = intern * intern;

                                if (mask_v_enable)
                                {
                                    VIS = Vector.Min(mixL * mixL * Vector.SquareRoot(Vect_05 * (DUDZ + DVDZ)), Vect_15) * Mask_V;
                                }
                                else
                                {
                                    VIS = Vector.Min(mixL * mixL * Vector.SquareRoot(Vect_05 * (DUDZ + DVDZ)), Vect_15);
                                }
                            }

                            DE = Vector.Max(VIS, VISHMIN_V) * DYKDZK / DXK_V;
                            DW = DE;
                            DS = Vector.Max(VIS, VISHMIN_V) * DXKDZK / DYK_V;
                            DN = DS;
                            DT = Vector.Max(VIS, VISHMIN_V) * AREAxy_V / DZK_V;
                            DB = Vector<float>.Zero;
                            if (mask_v_enable)
                            {
                                DB = DT * Mask_V;
                            }
                            else if (k > KKART_LL_P1) // otherwise k < KKART_LL_P1 and DB=0!
                            {
                                DB = DT;
                            }

                            //BOUNDARY CONDITIONS FOR DIFFUSION TERMS
                            if (ADVDOM_JM)
                            {
                                DS = Vector<float>.Zero;
                            }

                            if (ADVDOM_JP)
                            {
                                DN = Vector<float>.Zero;
                            }

                            if (ADVDOM_IM)
                            {
                                DW = Vector<float>.Zero;
                            }

                            if (ADVDOM_IP)
                            {
                                DE = Vector<float>.Zero;
                            }

                            //ADVECTION TERMS
                            VKSimjp_LV = new Vector<float>(VKSimjp_L, k);
                            VKSjp_LV = new Vector<float>(VKSjp_L, k);
                            VKSim_LV = new Vector<float>(VKSim_L, k);
                            VKSimjm_LV = new Vector<float>(VKSimjm_L, k);
                            VKSjm_LV = new Vector<float>(VKSjm_L, k);
                            WKSim_LV = new Vector<float>(WKSim_L, k);
                            UKSim_LV = new Vector<float>(UKSim_L, k);

                            FE = UKS_V * DYKDZK;
                            FW = UKSim_LV * DYKDZK;
                            FS = Vect_025 * (VKSim_LV + VKS_V + VKSimjm_LV + VKSjm_LV) * DXKDZK;
                            FN = Vect_025 * (VKSim_LV + VKS_V + VKSimjp_LV + VKSjp_LV) * DXKDZK;
                            FT = Vect_025 * (WKSim_LV + WKS_V + new Vector<float>(WKSim_L, kP1) + new Vector<float>(WKS_L, kP1)) * AREAxy_V;
                            FB = Vector<float>.Zero;

                            WKSkM_LV = new Vector<float>(WKS_L, kM1);
                            WKSimM_LV = new Vector<float>(WKSim_L, kM1);

                            if (mask_v_enable)
                            {
                                FB = Vect_025 * (WKSim_LV + WKS_V + WKSimM_LV + WKSkM_LV) * AREAxy_V * Mask_V;
                            }
                            else if (k > KKART_LL_P1) // otherwise k < KKART_LL_P1 and FB=0!
                            {
                                FB = Vect_025 * (WKSim_LV + WKS_V + WKSimM_LV + WKSkM_LV) * AREAxy_V;
                            }

                            //PECLET NUMBERS
                            DE = Vector.Max(DE, Vect_0001);
                            DB = Vector.Max(DB, Vect_0001);
                            DW = Vector.Max(DW, Vect_0001);
                            DS = Vector.Max(DS, Vect_0001);
                            DN = Vector.Max(DN, Vect_0001);
                            DT = Vector.Max(DT, Vect_0001);

                            PE = Vector.Abs(FE / DE);
                            PB = Vector.Abs(FB / DB);
                            PW = Vector.Abs(FW / DW);
                            PS = Vector.Abs(FS / DS);
                            PN = Vector.Abs(FN / DN);
                            PT = Vector.Abs(FT / DT);

                            //POWER LAW ADVECTION SCHEME
                            intern = Vect_1 - Vect_01 * PT;
                            BIM = DT * Vector.Max(Vect_0, intern * intern * intern * intern * intern)
                           + Vector.Max(-FT, Vect_0);

                            intern = Vect_1 - Vect_01 * PB;
                            CIM = DB * Vector.Max(Vect_0, intern * intern * intern * intern * intern)
                           + Vector.Max(FB, Vect_0);

                            intern = Vect_1 - Vect_01 * PE;
                            AE1 = DE * Vector.Max(Vect_0, intern * intern * intern * intern * intern)
                           + Vector.Max(-FE, Vect_0);

                            intern = Vect_1 - Vect_01 * PW;
                            AW1 = DW * Vector.Max(Vect_0, intern * intern * intern * intern * intern)
                           + Vector.Max(FW, Vect_0);

                            intern = Vect_1 - Vect_01 * PS;
                            AS1 = DS * Vector.Max(Vect_0, intern * intern * intern * intern * intern)
                           + Vector.Max(FS, Vect_0);

                            intern = Vect_1 - Vect_01 * PN;
                            AN1 = DN * Vector.Max(Vect_0, intern * intern * intern * intern * intern)
                           + Vector.Max(-FN, Vect_0);

                            //UPWIND SCHEME
                            /*
                                double BIM = DT + Program.fl_max(-FT, 0);
                                double CIM = DB + Program.fl_max(FB, 0);
                                double AE1 = DE + Program.fl_max(-FE, 0);
                                double AW1 = DW + Program.fl_max(FW, 0);
                                double AS1 = DS + Program.fl_max(FS, 0);
                                double AN1 = DN + Program.fl_max(-FN, 0);
                             */

                            AP0_V = new Vector<float>(PrognosticFlowfield.AP0, k);

                            AIM = BIM + CIM + AW1 + AS1 + AE1 + AN1 + AP0_V;

                            //SOURCE TERMS
                            intern2 = Vect_05 * (VKSim_LV + VKS_V);
                            intern = Vect_05 * (UKSim_LV + UKS_V);

                            windhilf = Vector.Max(Vector.SquareRoot(intern * intern + intern2 * intern2), Vect_001);

                            DDPX = new Vector<float>(DPMNEWim_L, k) - new Vector<float>(DPMNEW_L, k);

                            UKim_LV = new Vector<float>(UKim_L, k);
                            UKip_LV = new Vector<float>(UKip_L, k);
                            UKSim_LV = new Vector<float>(UKSim_L, k);
                            UK_LV = new Vector<float>(UK_L, k);

                            DIM_U = AW1 * UKim_LV +
                                    AS1 * new Vector<float>(UKjm_L, k) +
                                    AE1 * UKip_LV +
                                    AN1 * new Vector<float>(UKjp_L, k) +
                                    AP0_V * Vect_05 * (UKSim_LV + UKS_V) +
                                    DDPX * DYKDZK +
                                    (Program.CorolisParam * (UG_V - UK_LV) -
                                    new Vector<float>(VEG_L, kM1) * UK_LV * windhilf) * AREAxy_V * DZK_V;


                            //BOUNDARY CONDITION AT SURFACES (OBSTACLES AND TERRAIN)
                            if ((k <= KKART_LL_P1) && ((k + SIMD) > KKART_LL_P1))
                            {
                                Mask2.Clear();
                                //Array.Clear(Mask2, 0, Mask2.Length);
                                int ii = KKART_LL_P1 - k;   // 0 to SIMD - 1
                                Mask2[ii] = 1;              // Set the mask2 at SIMD position k == KKART_LL_P1 to 1

                                intern = Vect_05 * (UKSim_LV + UKS_V);
                                intern2 = Vect_05 * (VKSim_LV + VKS_V);
                                windhilf = Vector.Max(Vector.SquareRoot(intern * intern + intern2 * intern2), Vect_01);

                                if (CUTK_L < 1) // building heigth < 1 m
                                {
                                    // above terrain
                                    float Ustern_Buildings = Ustern_terrain_helpterm * windhilf[ii];
                                    if (Z0 >= DZK_V[ii] * 0.1F)
                                    {
                                        int ind = k + ii + 1;
                                        Ustern_Buildings = Ustern_terrain_helpterm * MathF.Sqrt(Program.Pow2(0.5F * ((UKSim_L[ind]) + UKS_L[ind])) + Program.Pow2(0.5F * ((VKSim_L[ind]) + VKS_L[ind])));
                                    }

                                    Vector<float> Ustern_Buildings_V = new Vector<float>(Ustern_Buildings * Ustern_Buildings);
                                    DIM_U -= UK_LV / windhilf * Ustern_Buildings_V * AREAxy_V * new Vector<float>(Mask2);
                                }
                                else
                                {
                                    //above a building
                                    float Ustern_Buildings = Ustern_obstacles_helpterm * windhilf[ii];
                                    Vector<float> Ustern_Buildings_V = new Vector<float>(Ustern_Buildings * Ustern_Buildings);
                                    DIM_U -= UK_LV / windhilf * Ustern_Buildings_V * AREAxy_V * new Vector<float>(Mask2);
                                }
                            }

                            //additional terms of the eddy-viscosity model
                            if (k > KKART_LL_P1)
                            {
                                DUDXE = (UKip_LV - UK_LV) * DYKDZK;
                                DUDXW = (UK_LV - UKim_LV) * DYKDZK;
                                DVDXN = (Vect_05 * (new Vector<float>(VKSipjp_L, k) + VKSjp_LV) - Vect_05 * (VKSjp_LV + VKSimjp_LV)) * DXKDZK;
                                DVDXS = (Vect_05 * (new Vector<float>(VKSipjm_L, k) + VKSjm_LV) - Vect_05 * (VKSjm_LV + VKSimjm_LV)) * DXKDZK;
                                DWDXT = (Vect_05 * (new Vector<float>(WKSip_L, k) + WKS_V) - Vect_05 * (WKS_V + WKSim_LV)) * AREAxy_V;
                                DWDXB = (Vect_05 * (new Vector<float>(WKSip_L, kM1) + WKSkM_LV) - Vect_05 * (WKSkM_LV + WKSimM_LV)) * AREAxy_V;

                                if (mask_v_enable) // compute ADD_DIFF and add to DIMW
                                {
                                    DIM_U += VIS / DXK_V * (DUDXE - DUDXW + DVDXN - DVDXS + DWDXT - DWDXB) * Mask_V;
                                }
                                else
                                {
                                    DIM_U += VIS / DXK_V * (DUDXE - DUDXW + DVDXN - DVDXS + DWDXT - DWDXB);
                                }
                            }

                            //RECURRENCE FORMULA
                            int kint_s = 0;
                            if (end) // restore original indices 
                            {
                                kint_s = k_real - k;
                                k_real -= kint_s;
                            }

                            AIM.CopyTo(AIM_A);
                            BIM.CopyTo(BIM_A);
                            CIM.CopyTo(CIM_A);
                            DIM_U.CopyTo(DIMU_A);

                            for (int kint = kint_s; kint < AIM_A.Length; ++kint) // loop over all SIMD values
                            {
                                int index_i = k_real + kint;

                                if (index_i < KEND)
                                {
                                    if (index_i > KKART_LL_P1)
                                    {
                                        float TERMP = 1 / (AIM_A[kint] - CIM_A[kint] * PIMU[index_i - 1]);
                                        PIMU[index_i] = BIM_A[kint] * TERMP;
                                        QIMU[index_i] = (DIMU_A[kint] + CIM_A[kint] * QIMU[index_i - 1]) * TERMP;
                                    }
                                    else
                                    {
                                        float TERMP = 1 / AIM_A[kint];
                                        PIMU[index_i] = BIM_A[kint] * TERMP;
                                        QIMU[index_i] = DIMU_A[kint] * TERMP;
                                    }
                                }
                            }

                            if (end)
                            {
                                k = KEND + 1;
                            }

                        } // loop over all k

                        //OBTAIN NEW U-COMPONENTS
                        int KKARTm_LL = Math.Max(KKART_LL, Program.KKART[i - 1][j]);
                        lock (UK_L)
                        {
                            for (int k = Vert_Index_LL; k >= KSTART; --k)
                            {
                                if (KKARTm_LL < k)
                                {
                                    UK_L[k] += relax * (PIMU[k] * UK_L[k + 1] + QIMU[k] - UK_L[k]);
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}
