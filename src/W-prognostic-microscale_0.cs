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
    class W_PrognosticMicroscaleV0
    {
        public static float l_infinitive = 90;

        /// <summary>
    	/// Momentum equations for the w wind component - no diffusion
    	/// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, float building_Z0, float relax)
        {
            Parallel.For(2, Program.NII, Program.pOptions, i1 =>
            {
                float DXK = Program.DXK; float DYK = Program.DYK;
                int KKART_LL, Vert_Index_LL;
                float AREAxy_L = AREAxy;

                double[] PIMW = new double[Program.KADVMAX + 1];
                double[] QIMW = new double[Program.KADVMAX + 1];

                for (int j1 = 2; j1 <= Program.NJJ - 1; j1++)
                {
                    int j = j1;
                    if (JS == -1)
                    {
                        j = Program.NJJ - j1 + 1;
                    }

                    int i = i1;
                    if (IS == -1)
                    {
                        i = Program.NII - i1 + 1;
                    }

                    if (Program.ADVDOM[i][j] == 1)
                    {
                        Single[] TURB_L = Program.TURB[i][j];
                        Single[] WK_L = Program.WK[i][j];
                        Single[] WKim_L = Program.WK[i - 1][j];
                        Single[] WKip_L = Program.WK[i + 1][j];
                        Single[] WKjm_L = Program.WK[i][j - 1];
                        Single[] WKjp_L = Program.WK[i][j + 1];
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
                        Single[] VKSjm_L = Program.VKS[i][j - 1];
                        Single[] VKSjp_L = Program.VKS[i][j + 1];


                        Single[] UK_L = Program.UK[i][j];
                        Single[] VK_L = Program.VK[i][j];
                        KKART_LL = Program.KKART[i][j];
                        Vert_Index_LL = Program.VerticalIndex[i][j];

                        int KSTART = 1;
                        if (Program.CUTK[i][j] == 0)
                        {
                            KSTART = KKART_LL + 1;
                        }

                        for (int k = KSTART; k <= Vert_Index_LL; k++)
                        {
                            float DZK_K = Program.DZK[k];
                            float DXKDZK = DXK * DZK_K;
                            float DYKDZK = DYK * DZK_K;
                            float UKS_LL = UKS_L[k], UKS_LL_m = UKS_L[k - 1], VKS_LL = VKS_L[k], VKS_LL_m = VKS_L[k - 1];
                            float AP0 = PrognosticFlowfield.AP0[k];

                            //ADVECTION TERMS
                            float FE = 0.25F * (UKS_LL + UKSip_L[k] + UKS_LL_m + UKSip_L[k - 1]) * DYKDZK;
                            float FW = 0.25F * (UKS_LL + UKSim_L[k] + UKS_LL_m + UKSim_L[k - 1]) * DYKDZK;
                            float FS = 0.25F * (VKS_LL + VKSjm_L[k] + VKS_LL_m + VKSjm_L[k - 1]) * DXKDZK;
                            float FN = 0.25F * (VKS_LL + VKSjp_L[k] + VKS_LL_m + VKSjp_L[k - 1]) * DXKDZK;
                            float FT = WKS_L[k] * AREAxy_L;
                            float FB = 0;
                            if (k > KKART_LL + 1)
                            {
                                FB = WKS_L[k - 1] * AREAxy_L;
                            }

                            //POWER LAW ADVECTION SCHEME
                            float BIM = Program.FloatMax(-FT, 0);
                            float CIM = Program.FloatMax(FB, 0);
                            float AE1 = Program.FloatMax(-FE, 0);
                            float AW1 = Program.FloatMax(FW, 0);
                            float AS1 = Program.FloatMax(FS, 0);
                            float AN1 = Program.FloatMax(-FN, 0);

                            float AIM = BIM + CIM + AW1 + AS1 + AE1 + AN1 + AP0;

                            //SOURCE TERMS
                            float DDPZ = DPMNEW_L[k - 1] - DPMNEW_L[k];
                            float DIMW = (float)(AW1 * WKim_L[k] + AS1 * WKjm_L[k] + AE1 * WKip_L[k] + AN1 * WKjp_L[k] +
                                AP0 * 0.5 * (WKS_L[k - 1] + WKS_L[k]) + DDPZ * AREAxy_L);

                            //RECURRENCE FORMULA
                            if (k > KKART_LL + 1)
                            {
                                PIMW[k] = (BIM / (AIM - CIM * PIMW[k - 1]));
                                QIMW[k] = ((DIMW + CIM * QIMW[k - 1]) / (AIM - CIM * PIMW[k - 1]));
                            }
                            else
                            {
                                PIMW[k] = (BIM / AIM);
                                QIMW[k] = (DIMW / AIM);
                            }
                        }
                        //OBTAIN NEW W-COMPONENTS
                        for (int k = Vert_Index_LL; k >= KSTART; k--)
                        {
                            WK_L[k] += (float)(relax * (PIMW[k] * WK_L[k + 1] + QIMW[k] - WK_L[k]));
                        }
                    }
                }
            });
        }
    }
}
