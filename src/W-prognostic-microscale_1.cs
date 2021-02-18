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
    public class W_PrognosticMicroscaleV1
    {
        static readonly Vector<float> Vect_0 = Vector<float>.Zero;
        static readonly Vector<float> Vect_1 = Vector<float>.One;
        static readonly Vector<float> Vect_001 = new Vector<float>(0.01F);
        static readonly Vector<float> Vect_01 = new Vector<float>(0.1F);
        static readonly Vector<float> Vect_025 = new Vector<float>(0.25F);
        static readonly Vector<float> Vect_05 = new Vector<float>(0.5F);
        static readonly Vector<float> Vect_099 = new Vector<float>(0.99F);
        static readonly Vector<float> Vect_0071 = new Vector<float>(0.071F);
        static readonly Vector<float> Vect_2 = new Vector<float>(2F);
        static readonly Vector<float> Vect_15 = new Vector<float>(15);
        static readonly Vector<float> Vect_100 = new Vector<float>(100);
        static readonly Vector<float> Vect_0001 = new Vector<float>(0.0001F);

        static readonly int SIMD = Vector<float>.Count;


        /// <summary>
        /// Momentum equations for the w wind component - algebraic mixing lenght model
        /// </summary>
        /// <param name="IS">ADI direction for x cells</param>
        /// <param name="JS">ADI direction for y cells</param>
        /// <param name="Cmueh">Constant</param>
        /// <param name="VISHMIN">Minimum horizontal turbulent exchange coefficients </param>
        /// <param name="AREAxy">Area of a flow field cell</param>
        /// <param name="building_Z0">Roughness of buildings</param>
        /// <param name="relax">Relaxation factor</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, float relax)
        {
            Vector<float> DXK_V = new Vector<float>(Program.DXK);
            Vector<float> DYK_V = new Vector<float>(Program.DYK);
            Vector<float> VISHMIN_V = new Vector<float>(VISHMIN);
            Vector<float> AREAxy_V = new Vector<float>(AREAxy);

            Parallel.For(2, Program.NII, Program.pOptions, i1 =>
            {
                Span<float> PIMW = stackalloc float[Program.KADVMAX + 1];
                Span<float> QIMW = stackalloc float[Program.KADVMAX + 1];
                Span<float> Mask = stackalloc float[SIMD];
                Span<float> AIM_A = stackalloc float[SIMD];
                Span<float> BIM_A = stackalloc float[SIMD];
                Span<float> CIM_A = stackalloc float[SIMD];
                Span<float> DIMW_A = stackalloc float[SIMD];
                
                Vector<float> DUDZE, DUDZW, DVDZN, DVDZS, DWDZT, DWDZB;
                Vector<float> DE, DW, DS, DN, DT, DB;
                Vector<float> FE, FW, FS, FN, FT, FB;
                Vector<float> PE, PW, PS, PN, PT, PB;
                Vector<float> BIM, CIM, AE1, AW1, AS1, AN1, AIM;
                Vector<float> DIMW, windhilf;
                Vector<float> DDPZ;

                Vector<float> UKSim_LV, UKSimM_LV;
                Vector<float> VKSjm_LV, VKSjmM_LV;

                Vector<float> WKS_V, VKS_V, UKS_V, DZK_V;
                Vector<float> DXKDZK, DYKDZK, AP0_V, intern, intern2;

                Vector<float> VIS, zs, mixL;
                Vector<float> HOKART_KKART;

                Vector<float> Mask_V = Vector<float>.Zero;
                Vector<float> DUDZ, DVDZ;

                Vector<float> UKS_M_V, VKS_M_V, WK_LV;

                int i = i1;
                if (IS == -1)
                {
                    i = Program.NII - i1 + 1;
                }

                for (int j1 = 2; j1 < Program.NJJ; ++j1)
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

                        Single[] WK_L = Program.WK[i][j];
                        Single[] WKim_L = Program.WK[iM1][j];
                        Single[] WKip_L = Program.WK[iP1][j];
                        Single[] WKjm_L = Program.WK[i][jM1];
                        Single[] WKjp_L = Program.WK[i][jP1];
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
                        Single[] VKSjm_L = Program.VKS[i][jM1];
                        Single[] VKSjp_L = Program.VKS[i][jP1];

                        Single[] UK_L = Program.UK[i][j];
                        Single[] VK_L = Program.VK[i][j];

                        int KKART_LL = Program.KKART[i][j];
                        int Vert_Index_LL = Program.VerticalIndex[i][j];
                        float Ustern_terrain_helpterm = Program.UsternTerrainHelpterm[i][j];
                        float Ustern_obstacles_helpterm = Program.UsternObstaclesHelpterm[i][j];
                        bool ADVDOM_JM = (Program.ADVDOM[i][jM1] < 1) || (j == 2);
                        bool ADVDOM_JP = (Program.ADVDOM[i][jP1] < 1) || (j == Program.NJJ - 1);
                        bool ADVDOM_IM = (Program.ADVDOM[iM1][j] < 1) || (i == 2);
                        bool ADVDOM_IP = (Program.ADVDOM[iP1][j] < 1) || (i == Program.NII - 1);

                        float [] VEG_L = Program.VEG[i][j];
                        bool VegetationExists = VEG_L != null;
                        float COV_L = (float) Program.COV[i][j];

                        HOKART_KKART = new Vector<float>(Program.HOKART[KKART_LL]);

                        int KSTART = 1;
                        if (Program.CUTK[i][j] == 0)
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

                            UKS_M_V = new Vector<float>(UKS_L, kM1);
                            VKS_M_V = new Vector<float>(VKS_L, kM1);

                            //turbulence modelling
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
                            UKSim_LV = new Vector<float>(UKSim_L, k);
                            UKSimM_LV = new Vector<float>(UKSim_L, kM1);
                            VKSjm_LV = new Vector<float>(VKSjm_L, k);
                            VKSjmM_LV = new Vector<float>(VKSjm_L, kM1);

                            FE = Vect_025 * (UKS_V + new Vector<float>(UKSip_L, k) + UKS_M_V + new Vector<float>(UKSip_L, kM1)) * DYKDZK;
                            FW = Vect_025 * (UKS_V + UKSim_LV + UKS_M_V + UKSimM_LV) * DYKDZK;
                            FS = Vect_025 * (VKS_V + VKSjm_LV + VKS_M_V + VKSjmM_LV) * DXKDZK;
                            FN = Vect_025 * (VKS_V + new Vector<float>(VKSjp_L, k) + VKS_M_V + new Vector<float>(VKSjp_L, kM1)) * DXKDZK;
                            FT = WKS_V * AREAxy_V;
                            FB = Vector<float>.Zero;

                            if (mask_v_enable)
                            {
                                FB = new Vector<float>(WKS_L, kM1) * AREAxy_V * Mask_V;
                            }
                            else if (k > KKART_LL_P1) // otherwise k < KKART_LL_P1 and FB=0!
                            {
                                FB = new Vector<float>(WKS_L, kM1) * AREAxy_V;
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
                            DDPZ = new Vector<float>(DPMNEW_L, kM1) - new Vector<float>(DPMNEW_L, k);
                            WK_LV = new Vector<float>(WK_L, k);

                            if (VegetationExists)
                            {
                                intern2 = Vect_05 * (VKSjm_LV + VKS_V);
                                intern = Vect_05 * (UKSim_LV + UKS_V);
                                windhilf = Vector.Max(Vector.SquareRoot(intern * intern + intern2 * intern2), Vect_001);
                                DIMW = AW1 * new Vector<float>(WKim_L, k) +
                                       AS1 * new Vector<float>(WKjm_L, k) +
                                       AE1 * new Vector<float>(WKip_L, k) +
                                       AN1 * new Vector<float>(WKjp_L, k) +
                                       AP0_V * Vect_05 * (new Vector<float>(WKS_L, kM1) + WKS_V) +
                                       DDPZ * AREAxy_V -
                                       new Vector<float>(VEG_L, kM1) * WK_LV * windhilf * AREAxy_V * DZK_V;
                            }
                            else
                            {
                                DIMW = AW1 * new Vector<float>(WKim_L, k) +
                                   AS1 * new Vector<float>(WKjm_L, k) +
                                   AE1 * new Vector<float>(WKip_L, k) +
                                   AN1 * new Vector<float>(WKjp_L, k) +
                                   AP0_V * Vect_05 * (new Vector<float>(WKS_L, kM1) + WKS_V) +
                                   DDPZ * AREAxy_V;
                            }

                            //additional terms of the eddy-viscosity model
                            if (k > KKART_LL_P1)
                            {
                                DUDZE = (Vect_05 * (new Vector<float>(UKS_L, kP1) + UKS_V) - Vect_05 * (UKS_V + UKS_M_V)) * DYKDZK;
                                DUDZW = (Vect_05 * (new Vector<float>(UKSim_L, kP1) + UKSim_LV) - Vect_05 * (UKSim_LV + UKSimM_LV)) * DYKDZK;
                                DVDZN = (Vect_05 * (new Vector<float>(VKS_L, kP1) + VKS_V) - Vect_05 * (VKS_V + VKS_M_V)) * DXKDZK;
                                DVDZS = (Vect_05 * (new Vector<float>(VKSjm_L, kP1) + VKSjm_LV) - Vect_05 * (VKSjm_LV + VKSjmM_LV)) * DXKDZK;
                                DWDZT = (new Vector<float>(WK_L, kP1) - WK_LV) * AREAxy_V;
                                DWDZB = (WK_LV - new Vector<float>(WK_L, kM1)) * AREAxy_V;

                                if (mask_v_enable) // compute ADD_DIFF and add to DIMW
                                {
                                    DIMW += VIS / DZK_V * (DUDZE - DUDZW + DVDZN - DVDZS + DWDZT - DWDZB) * Mask_V;
                                }
                                else
                                {
                                    DIMW += VIS / DZK_V * (DUDZE - DUDZW + DVDZN - DVDZS + DWDZT - DWDZB);
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
                            DIMW.CopyTo(DIMW_A);

                            for (int kint = kint_s; kint < AIM_A.Length; ++kint) // loop over all SIMD values
                            {
                                int index_i = k_real + kint;

                                if (index_i < KEND)
                                {
                                    if (index_i > KKART_LL_P1)
                                    {
                                        float TERMP = 1 / (AIM_A[kint] - CIM_A[kint] * PIMW[index_i - 1]);
                                        PIMW[index_i] = BIM_A[kint] * TERMP;
                                        QIMW[index_i] = (DIMW_A[kint] + CIM_A[kint] * QIMW[index_i - 1]) * TERMP;
                                    }
                                    else
                                    {
                                        float TERMP = 1 / AIM_A[kint];
                                        PIMW[index_i] = BIM_A[kint] * TERMP;
                                        QIMW[index_i] = DIMW_A[kint] * TERMP;
                                    }
                                }
                            }

                            if (end)
                            {
                                k = KEND + 1;
                            }

                        } // loop over all k

                        //OBTAIN NEW W-COMPONENTS
                        for (int k = Vert_Index_LL; k >= KSTART; --k)
                        {
                            WK_L[k] += relax * (PIMW[k] * WK_L[k + 1] + QIMW[k] - WK_L[k]);
                        }
                    }
                }
            });
        }
    }
}
