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
using System.Runtime.CompilerServices;

namespace GRAL_2001
{
    public static class V_PrognosticMicroscaleV1
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
        /// Momentum equations for the v wind component - algebraic mixing lenght model
        /// </summary>   
        /// <param name="IS">ADI direction for x cells</param>
        /// <param name="JS">ADI direction for y cells</param>
        /// <param name="Cmueh">Constant</param>
        /// <param name="VISHMIN">Minimum horizontal turbulent exchange coefficients </param>
        /// <param name="AREAxy">Area of a flow field cell</param>
        /// <param name="VG">Geostrophic wind</param>
        /// <param name="building_Z0">Roughness of buildings</param>
        /// <param name="relax">Relaxation factor</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, Single VG, float relax)
        {
            float DXK = Program.DXK; float DYK = Program.DYK;
            Vector<float> DXK_V = new Vector<float>(DXK);
            Vector<float> DYK_V = new Vector<float>(DYK);
            Vector<float> VISHMIN_V = new Vector<float>(VISHMIN);
            Vector<float> AREAxy_V = new Vector<float>(AREAxy);
            Vector<float> VG_V = new Vector<float>(VG);

            Parallel.For(2, Program.NII, Program.pOptions, i1 =>
            {
                Span<float> PIMV = stackalloc float[Program.KADVMAX + 1];
                Span<float> QIMV = stackalloc float[Program.KADVMAX + 1];
                Span<float> Mask = stackalloc float[SIMD];
                Span<float> Mask2 = stackalloc float[SIMD];
                Span<float> AIM_A = stackalloc float[SIMD];
                Span<float> BIM_A = stackalloc float[SIMD];
                Span<float> CIM_A = stackalloc float[SIMD];
                Span<float> DIMV_A = stackalloc float[SIMD];

                Vector<float> DVDYN, DVDYS, DUDYE, DUDYW, DWDYT, DWDYB;
                Vector<float> DE, DW, DS, DN, DT, DB;
                Vector<float> FE, FW, FS, FN, FT, FB;
                Vector<float> PE, PW, PS, PN, PT, PB;
                Vector<float> BIM, CIM, AE1, AW1, AS1, AN1, AIM;
                Vector<float> DIMV, windhilf;
                Vector<float> DDPY;

                Vector<float> UKSip_LV, UKSjm_LV, UKSipjm_LV, UKSim_LV, UKSimjm_LV, VKjp_LV, VKjm_LV, VKSim_LV;
                Vector<float> VKSjm_LV, WKSjm_LV, WKSkM_LV;

                Vector<float> WKS_V, VKS_V, UKS_V, DZK_V;
                Vector<float> DXKDZK, DYKDZK, AP0_V, intern, intern2;

                Vector<float> VIS, zs, mixL;
                Vector<float> VK_LV;
                Vector<float> HOKART_KKART;

                Vector<float> Mask_V = Vector<float>.Zero;
                Vector<float> DUDZ, DVDZ;

                int i = i1;
                if (IS == -1)
                {
                    i = Program.NII - i1 + 1;
                }

                for (int j1 = 3; j1 <= Program.NJJ - 1; ++j1)
                {
                    int j = j1;
                    if (JS == -1)
                    {
                        j = Program.NJJ - j1 + 1;
                    }

                    if (Program.ADVDOM[i][j] == 1)
                    {
                        //inside a prognostic sub domain
                        int jM1 = j - 1;
                        int jP1 = j + 1;
                        int iM1 = i - 1;
                        int iP1 = i + 1;

                        Single[] VK_L = Program.VK[i][j];
                        Single[] UKS_L = Program.UKS[i][j];
                        Single[] VKS_L = Program.VKS[i][j];
                        Single[] WKS_L = Program.WKS[i][j];
                        Single[] DPMNEW_L = Program.DPMNEW[i][j];
                        Single[] VKim_L = Program.VK[iM1][j];
                        Single[] VKip_L = Program.VK[iP1][j];
                        Single[] VKjm_L = Program.VK[i][jM1];
                        Single[] VKjp_L = Program.VK[i][jP1];
                        Single[] UKSim_L = Program.UKS[iM1][j];
                        Single[] UKSip_L = Program.UKS[iP1][j];
                        Single[] UKSjm_L = Program.UKS[i][jM1];
                        Single[] UKSipjm_L = Program.UKS[iP1][jM1];
                        Single[] UKSimjm_L = Program.UKS[iM1][jM1];
                        Single[] VKSjm_L = Program.VKS[i][jM1];
                        Single[] VKSim_L = Program.VKS[iM1][j];
                        Single[] WKSjm_L = Program.WKS[i][jM1];
                        Single[] DPMNEWjm_L = Program.DPMNEW[i][jM1];

                        Single[] UKSipjp_L = Program.UKS[iP1][jP1];
                        Single[] UKSimjp_L = Program.UKS[iM1][jP1];
                        Single[] WKSjp_L = Program.WK[i][jP1];
                        Single[] UK_L = Program.UK[i][j];

                        int KKART_LL = Program.KKART[i][j];
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

                        HOKART_KKART = new Vector<float>(Program.HOKART[KKART_LL]);

                        int KSTART = 1;
                        if (CUTK_L == 0) // building heigth == 0 m
                        {
                            KSTART = KKART_LL + 1;
                        }

                        int KKART_LL_P1 = KKART_LL + 1;

                        int KEND = Vert_Index_LL + 1;
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
                            VK_LV = new Vector<float>(VK_L, k);
                            zs = new Vector<float>(Program.HOKART, k) - HOKART_KKART - DZK_V * Vect_05;

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
                            // To do : Set a Mask to take the right values
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
                            UKSip_LV = new Vector<float>(UKSip_L, k);
                            UKSjm_LV = new Vector<float>(UKSjm_L, k);
                            UKSipjm_LV = new Vector<float>(UKSipjm_L, k);
                            UKSim_LV = new Vector<float>(UKSim_L, k);
                            VKSim_LV = new Vector<float>(VKSim_L, k);
                            UKSimjm_LV = new Vector<float>(UKSimjm_L, k);
                            VKSjm_LV = new Vector<float>(VKSjm_L, k);
                            WKSjm_LV = new Vector<float>(WKSjm_L, k);
                            WKSkM_LV = new Vector<float>(WKS_L, kM1);

                            FE = Vect_025 * (UKS_V + UKSip_LV + UKSjm_LV + UKSipjm_LV) * DYKDZK;
                            FW = Vect_025 * (UKS_V + UKSim_LV + UKSjm_LV + UKSimjm_LV) * DYKDZK;
                            FS = VKSjm_LV * DXKDZK;
                            FN = VKS_V * DXKDZK;
                            FT = Vect_025 * (WKSjm_LV + WKS_V + new Vector<float>(WKSjm_L, kP1) + new Vector<float>(WKS_L, kP1)) * AREAxy_V;
                            FB = Vector<float>.Zero;

                            if (mask_v_enable)
                            {
                                FB = Vect_025 * (WKSjm_LV + WKS_V + new Vector<float>(WKSjm_L, kM1) + WKSkM_LV) * AREAxy_V * Mask_V;
                            }
                            else if (k > KKART_LL_P1) // otherwise k < KKART_LL_P1 and FB=0!
                            {
                                FB = Vect_025 * (WKSjm_LV + WKS_V + new Vector<float>(WKSjm_L, kM1) + WKSkM_LV) * AREAxy_V;
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

                            DDPY = new Vector<float>(DPMNEWjm_L, k) - new Vector<float>(DPMNEW_L, k);

                            VKjp_LV = new Vector<float>(VKjp_L, k);
                            VKjm_LV = new Vector<float>(VKjm_L, k);

                            DIMV = AW1 * new Vector<float>(VKim_L, k) +
                                   AS1 * VKjm_LV +
                                   AE1 * new Vector<float>(VKip_L, k) +
                                   AN1 * VKjp_LV +
                                   AP0_V * Vect_05 * (VKSjm_LV + VKS_V) + DDPY * DXKDZK +
                                   (Program.CorolisParam * (VG_V - VK_LV) -
                                   new Vector<float>(VEG_L, kM1) * VK_LV * windhilf) * AREAxy_V * DZK_V;

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
                                    //above terrain
                                    float Ustern_Buildings = Ustern_terrain_helpterm * windhilf[ii];
                                    if (Z0 >= DZK_V[ii] * 0.1F)
                                    {
                                        int ind = k + ii + 1;
                                        Ustern_Buildings = Ustern_terrain_helpterm * MathF.Sqrt(Program.Pow2(0.5F * ((UKSim_L[ind]) + UKS_L[ind])) + Program.Pow2(0.5F * ((VKSim_L[ind]) + VKS_L[ind])));
                                    }
                                    Vector<float> Ustern_Buildings_V = new Vector<float>(Ustern_Buildings * Ustern_Buildings);
                                    DIMV -= VK_LV / windhilf * Ustern_Buildings_V * AREAxy_V * new Vector<float>(Mask2);
                                }
                                else
                                {
                                    //above a building
                                    float Ustern_Buildings = Ustern_obstacles_helpterm * windhilf[ii];
                                    Vector<float> Ustern_Buildings_V = new Vector<float>(Ustern_Buildings * Ustern_Buildings);
                                    DIMV -= VK_LV / windhilf * Ustern_Buildings_V * AREAxy_V * new Vector<float>(Mask2);
                                }
                            }

                            //additional terms of the eddy-viscosity model
                            if (k > KKART_LL_P1)
                            {
                                DVDYN = (VKjp_LV - VK_LV) * DXKDZK;
                                DVDYS = (VK_LV - VKjm_LV) * DXKDZK;
                                DUDYE = (Vect_05 * (new Vector<float>(UKSipjp_L, k) + UKSip_LV) - Vect_05 * (UKSip_LV + UKSipjm_LV)) * DXKDZK;
                                DUDYW = (Vect_05 * (new Vector<float>(UKSimjp_L, k) + UKSim_LV) - Vect_05 * (UKSim_LV + UKSimjm_LV)) * DXKDZK;
                                DWDYT = (Vect_05 * (new Vector<float>(WKSjp_L, k) + WKS_V) - Vect_05 * (WKS_V + WKSjm_LV)) * AREAxy_V;
                                DWDYB = (Vect_05 * (new Vector<float>(WKSjp_L, kM1) + WKSkM_LV) - Vect_05 * (WKSkM_LV + WKSjm_LV)) * AREAxy_V;

                                if (mask_v_enable) // compute ADD_DIFF and add to DIMW
                                {
                                    DIMV += VIS / DXK_V * (DUDYE - DUDYW + DVDYN - DVDYS + DWDYT - DWDYB) * Mask_V;
                                }
                                else
                                {
                                    DIMV += VIS / DXK_V * (DUDYE - DUDYW + DVDYN - DVDYS + DWDYT - DWDYB);
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
                            DIMV.CopyTo(DIMV_A);

                            for (int kint = kint_s; kint < AIM_A.Length; ++kint) // loop over all SIMD values
                            {
                                int index_i = k_real + kint;

                                if (index_i < KEND)
                                {
                                    if (index_i > KKART_LL_P1)
                                    {
                                        float TERMP = 1 / (AIM_A[kint] - CIM_A[kint] * PIMV[index_i - 1]);
                                        PIMV[index_i] = BIM_A[kint] * TERMP;
                                        QIMV[index_i] = (DIMV_A[kint] + CIM_A[kint] * QIMV[index_i - 1]) * TERMP;
                                    }
                                    else
                                    {
                                        float TERMP = 1 / AIM_A[kint];
                                        PIMV[index_i] = BIM_A[kint] * TERMP;
                                        QIMV[index_i] = DIMV_A[kint] * TERMP;
                                    }
                                }
                            }

                            if (end)
                            {
                                k = KEND + 1;
                            }

                        } // loop over all k

                        //OBTAIN NEW V-COMPONENTS
                        int KKARTm_LL = Math.Max(KKART_LL, Program.KKART[i][j - 1]);
                        lock (VK_L)
                        {
                            for (int k = Vert_Index_LL; k >= KSTART; --k)
                            {
                                if (KKARTm_LL < k)
                                {
                                    VK_L[k] += relax * (PIMV[k] * VK_L[k + 1] + QIMV[k] - VK_L[k]);
                                }
                            }
                        }
                    }
                }
            });
        }

    }
}
