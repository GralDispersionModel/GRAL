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
using System.Threading.Tasks;

namespace GRAL_2001
{
    class V_PrognosticMicroscaleV0
    {
        public static float l_infinitive = 90;

        /// <summary>
    	/// Momentum equations for the v wind component - no diffusion
    	/// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, Single VG, float building_Z0, float relax)
        {
            Parallel.For(2, Program.NII, Program.pOptions, i1 =>
            {
                float DXK = Program.DXK; float DYK = Program.DYK;
                int KKART_LL, Vert_Index_LL;
                float AREAxy_L = AREAxy;
                Single[] PIMV = new Single[Program.KADVMAX + 1];
                Single[] QIMV = new Single[Program.KADVMAX + 1];

                for (int j1 = 3; j1 <= Program.NJJ - 1; j1++)
                {
                    int j = j1;
                    if (JS == -1) j = Program.NJJ - j1 + 1;
                    int i = i1;
                    if (IS == -1) i = Program.NII - i1 + 1;

                    if (Program.ADVDOM[i][j] == 1)
                    {
                        Single[] TURB_L = Program.TURB[i][j];
                        Single[] VK_L = Program.VK[i][j];
                        Single[] UKS_L = Program.UKS[i][j];
                        Single[] VKS_L = Program.VKS[i][j];
                        Single[] WKS_L = Program.WKS[i][j];
                        Single[] DPMNEW_L = Program.DPMNEW[i][j];
                        Single[] VKim_L = Program.VK[i - 1][j];
                        Single[] VKip_L = Program.VK[i + 1][j];
                        Single[] VKjm_L = Program.VK[i][j - 1];
                        Single[] VKjp_L = Program.VK[i][j + 1];
                        Single[] UKSim_L = Program.UKS[i - 1][j];
                        Single[] UKSip_L = Program.UKS[i + 1][j];
                        Single[] UKSjm_L = Program.UKS[i][j - 1];
                        Single[] UKSipjm_L = Program.UKS[i + 1][j - 1];
                        Single[] UKSimjm_L = Program.UKS[i - 1][j - 1];
                        Single[] VKSjm_L = Program.VKS[i][j - 1];
                        Single[] VKSim_L = Program.VKS[i - 1][j];
                        Single[] WKSjm_L = Program.WKS[i][j - 1];
                        Single[] DPMNEWjm_L = Program.DPMNEW[i][j - 1];

                        Single[] UKSipjp_L = Program.UKS[i + 1][j + 1];
                        Single[] UKSimjp_L = Program.UKS[i - 1][j + 1];
                        Single[] WKSjp_L = Program.WK[i][j + 1];
                        Single[] UK_L = Program.UK[i][j];
                        KKART_LL = Program.KKART[i][j];
                        Vert_Index_LL = Program.VerticalIndex[i][j];
                        float Ustern_terrain_helpterm = Program.UsternTerrainHelpterm[i][j];
                        float Ustern_obstacles_helpterm = Program.UsternObstaclesHelpterm[i][j];
                        float CUTK_L = Program.CUTK[i][j];

                        int KSTART = 1;
                        if (CUTK_L == 0)
                            KSTART = KKART_LL + 1;
                        for (int k = KSTART; k <= Vert_Index_LL; k++)
                        {
                            float DZK_K = Program.DZK[k];
                            float DXKDZK = DXK * DZK_K;
                            float DYKDZK = DYK * DZK_K;

                            //ADVECTION TERMS
                            float FE = 0.25F * (UKS_L[k] + UKSip_L[k] + UKSjm_L[k] + UKSipjm_L[k]) * DYKDZK;
                            float FW = 0.25F * (UKS_L[k] + UKSim_L[k] + UKSjm_L[k] + UKSimjm_L[k]) * DYKDZK;
                            float FS = VKSjm_L[k] * DXKDZK;
                            float FN = VKS_L[k] * DXKDZK;
                            float FT = 0.25F * (WKSjm_L[k] + WKS_L[k] + WKSjm_L[k + 1] + WKS_L[k + 1]) * AREAxy_L;
                            float FB = 0;
                            if (k > KKART_LL + 1)
                                FB = 0.25F * (WKSjm_L[k] + WKS_L[k] + WKSjm_L[k - 1] + WKS_L[k - 1]) * AREAxy_L;

                            //POWER LAW ADVECTION SCHEME
                            float BIM = Program.FloatMax(-FT, 0);
                            float CIM = Program.FloatMax(FB, 0);
                            float AE1 = Program.FloatMax(-FE, 0);
                            float AW1 = Program.FloatMax(FW, 0);
                            float AS1 = Program.FloatMax(FS, 0);
                            float AN1 = Program.FloatMax(-FN, 0);

                            float AIM = BIM + CIM + AW1 + AS1 + AE1 + AN1 + PrognosticFlowfield.AP0[k];

                            //SOURCE TERMS
                            float DDPY = DPMNEWjm_L[k] - DPMNEW_L[k];
                            float DIMV = (float)(AW1 * VKim_L[k] + AS1 * VKjm_L[k] + AE1 * VKip_L[k] + AN1 * VKjp_L[k] +
                                PrognosticFlowfield.AP0[k] * 0.5 * (VKSjm_L[k] + VKS_L[k]) + DDPY * DXKDZK + Program.CorolisParam * (VG - VK_L[k]) * AREAxy_L * DZK_K);

                            //BOUNDARY CONDITION AT SURFACES (OBSTACLES AND TERRAIN)
                            if ((k == KKART_LL + 1) && (CUTK_L == 0))
                            {
                                float xhilf = (float)((float)i * (DXK - DXK * 0.5));
                                float yhilf = (float)((float)j * (DYK - DYK * 0.5));
                                float windhilf = Program.FloatMax((float)Math.Sqrt(Program.Pow2(0.5 * ((UKSim_L[k]) + UKS_L[k])) + Program.Pow2(0.5 * ((VKSim_L[k]) + VKS_L[k]))), 0.01F);
                                int IUstern = (int)(xhilf / Program.DDX[1]) + 1;
                                int JUstern = (int)(yhilf / Program.DDY[1]) + 1;
                                float Ustern_Buildings = Ustern_terrain_helpterm * windhilf;
                                if (Program.Z0Gramm[IUstern][JUstern] >= DZK_K * 0.1)
                                    Ustern_Buildings = Ustern_terrain_helpterm * (float)Math.Sqrt(Program.Pow2(0.5 * ((UKSim_L[k + 1]) + UKS_L[k + 1])) + Program.Pow2(0.5 * ((VKSim_L[k + 1]) + VKS_L[k + 1])));
                                DIMV -= (float)(VK_L[k] / windhilf * Program.Pow2(Ustern_Buildings) * AREAxy_L);
                            }
                            else if ((k == KKART_LL + 1) && (CUTK_L == 1))
                            {
                                float windhilf = Program.FloatMax((float)Math.Sqrt(Program.Pow2(0.5 * ((UKSim_L[k]) + UKS_L[k])) + Program.Pow2(0.5 * ((VKSim_L[k]) + VKS_L[k]))), 0.01F);
                                float Ustern_Buildings = Ustern_obstacles_helpterm * windhilf;
                                DIMV -= (float)(VK_L[k] / windhilf * Program.Pow2(Ustern_Buildings) * AREAxy_L);
                            }

                            //RECURRENCE FORMULA
                            if (k > KKART_LL + 1)
                            {
                                PIMV[k] = (BIM / (AIM - CIM * PIMV[k - 1]));
                                QIMV[k] = ((DIMV + CIM * QIMV[k - 1]) / (AIM - CIM * PIMV[k - 1]));
                            }
                            else
                            {
                                PIMV[k] = (BIM / AIM);
                                QIMV[k] = (DIMV / AIM);
                            }
                        }
                        //OBTAIN NEW V-COMPONENTS
                        for (int k = Vert_Index_LL; k >= KSTART; k--)
                        {
                            if ((KKART_LL < k) && (Program.KKART[i][j - 1] < k))
                            {
                                VK_L[k] += (relax * (PIMV[k] * VK_L[k + 1] + QIMV[k] - VK_L[k]));
                            }
                        }
                    }
                }
            });
        }

        //should be faster than MyPow - Function
        static Func<double, int, double> MyPow = (double num, int exp) =>
        {
            double result = 1.0;
            while (exp > 0)
            {
                if (exp % 2 == 1)
                    result *= num;
                exp >>= 1;
                num *= num;
            };
            return result;
        };

    }
}
