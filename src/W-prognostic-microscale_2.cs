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
    class W_PrognosticMicroscaleV2
    {
        public static float l_infinitive = 90;

        /// <summary>
    	/// Momentum equations for the w wind component - k-epsilon model
    	/// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, float building_Z0, float relax)
        {
            Parallel.For(2, Program.NII, Program.pOptions, i1 =>
            {
                float DXK = Program.DXK; float DYK = Program.DYK;
                int KKART_LL, Vert_Index_LL;
                float AREAxy_L = AREAxy;
                Single[] PIMW = new Single[Program.KADVMAX + 1];
                Single[] QIMW = new Single[Program.KADVMAX + 1];

                for (int j1 = 2; j1 <= Program.NJJ - 1; j1++)
                {
                    int j = j1;
                    if (JS == -1) j = Program.NJJ - j1 + 1;
                    int i = i1;
                    if (IS == -1) i = Program.NII - i1 + 1;

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
                            KSTART = KKART_LL + 1;
                        for (int k = KSTART; k <= Vert_Index_LL; k++)
                        {
                            float DZK_K = Program.DZK[k];
                            float DXKDZK = DXK * DZK_K;
                            float DYKDZK = DYK * DZK_K;
                            float UKS_LL = UKS_L[k], UKS_LL_m = UKS_L[k - 1], VKS_LL = VKS_L[k], VKS_LL_m = VKS_L[k - 1];

                            //turbulence modelling
                            float zs = Program.HOKART[k] - Program.HOKART[KKART_LL] - DZK_K * 0.5F;
                            float VIS = (float)Math.Sqrt(TURB_L[k]) * zs * Cmueh;

                            float DE = Program.FloatMax(VIS, VISHMIN) * DYKDZK / DXK;
                            float DW = DE;
                            float DS = Program.FloatMax(VIS, VISHMIN) * DXKDZK / DYK;
                            float DN = DS;
                            float DT = Program.FloatMax(VIS, VISHMIN) * AREAxy_L / DZK_K;
                            float DB = 0;
                            if (k > KKART_LL + 1)
                                DB = DT;

                            //BOUNDARY CONDITIONS FOR DIFFUSION TERMS
                            if ((Program.ADVDOM[i][j - 1] < 1) || (j == 2))
                                DS = 0;
                            if ((Program.ADVDOM[i][j + 1] < 1) || (j == Program.NJJ - 1))
                                DN = 0;
                            if ((Program.ADVDOM[i - 1][j] < 1) || (i == 2))
                                DW = 0;
                            if ((Program.ADVDOM[i + 1][j] < 1) || (i == Program.NII - 1))
                                DE = 0;


                            //ADVECTION TERMS
                            float FE = 0.25F * (UKS_LL + UKSip_L[k] + UKS_LL_m + UKSip_L[k - 1]) * DYKDZK;
                            float FW = 0.25F * (UKS_LL + UKSim_L[k] + UKS_LL_m + UKSim_L[k - 1]) * DYKDZK;
                            float FS = 0.25F * (VKS_LL + VKSjm_L[k] + VKS_LL_m + VKSjm_L[k - 1]) * DXKDZK;
                            float FN = 0.25F * (VKS_LL + VKSjp_L[k] + VKS_LL_m + VKSjp_L[k - 1]) * DXKDZK;
                            float FT = WKS_L[k] * AREAxy_L;
                            float FB = 0;
                            if (k > KKART_LL + 1)
                                FB = WKS_L[k - 1] * AREAxy_L;

                            //PECLET NUMBERS
                            DE = Program.FloatMax(DE, 0.0001F);
                            DB = Program.FloatMax(DB, 0.0001F);
                            DW = Program.FloatMax(DW, 0.0001F);
                            DS = Program.FloatMax(DS, 0.0001F);
                            DN = Program.FloatMax(DN, 0.0001F);
                            DT = Program.FloatMax(DT, 0.0001F);

                            float PE = Math.Abs(FE / DE);
                            float PB = Math.Abs(FB / DB);
                            float PW = Math.Abs(FW / DW);
                            float PS = Math.Abs(FS / DS);
                            float PN = Math.Abs(FN / DN);
                            float PT = Math.Abs(FT / DT);

                            //POWER LAW ADVECTION SCHEME
                            float BIM = DT * Program.FloatMax(0, (float)Program.Pow5(1 - 0.1 * PT)) + Program.FloatMax(-FT, 0);
                            float CIM = DB * Program.FloatMax(0, (float)Program.Pow5(1 - 0.1 * PB)) + Program.FloatMax(FB, 0);
                            float AE1 = DE * Program.FloatMax(0, (float)Program.Pow5(1 - 0.1 * PE)) + Program.FloatMax(-FE, 0);
                            float AW1 = DW * Program.FloatMax(0, (float)Program.Pow5(1 - 0.1 * PW)) + Program.FloatMax(FW, 0);
                            float AS1 = DS * Program.FloatMax(0, (float)Program.Pow5(1 - 0.1 * PS)) + Program.FloatMax(FS, 0);
                            float AN1 = DN * Program.FloatMax(0, (float)Program.Pow5(1 - 0.1 * PN)) + Program.FloatMax(-FN, 0);

                            //UPWIND SCHEME
                            /*
                            double BIM = DT  + Program.fl_max(-FT, 0);
                            double CIM = DB  + Program.fl_max(FB, 0);
                            double AE1 = DE  + Program.fl_max(-FE, 0);
                            double AW1 = DW  + Program.fl_max(FW, 0);
                            double AS1 = DS  + Program.fl_max(FS, 0);
                            double AN1 = DN  + Program.fl_max(-FN, 0);
                             */

                            float AIM = BIM + CIM + AW1 + AS1 + AE1 + AN1 + PrognosticFlowfield.AP0[k];

                            //SOURCE TERMS
                            float DDPZ = DPMNEW_L[k - 1] - DPMNEW_L[k];
                            float DIMW = (float)(AW1 * WKim_L[k] + AS1 * WKjm_L[k] + AE1 * WKip_L[k] + AN1 * WKjp_L[k] +
                                PrognosticFlowfield.AP0[k] * 0.5 * (WKS_L[k - 1] + WKS_L[k]) + DDPZ * AREAxy_L);


                            //additional terms of the eddy-viscosity model
                            if (k > KKART_LL + 1)
                            {
                                float DUDZE = (0.5F * (UKS_L[k + 1] + UKS_LL) - 0.5F * (UKS_LL + UKS_LL_m)) * DYKDZK;
                                float DUDZW = (0.5F * (UKSim_L[k + 1] + UKSim_L[k]) - 0.5F * (UKSim_L[k] + UKSim_L[k - 1])) * DYKDZK;
                                float DVDZN = (0.5F * (VKS_L[k + 1] + VKS_LL) - 0.5F * (VKS_LL + VKS_LL_m)) * DXKDZK;
                                float DVDZS = (0.5F * (VKSjm_L[k + 1] + VKSjm_L[k]) - 0.5F * (VKSjm_L[k] + VKSjm_L[k - 1])) * DXKDZK;
                                float DWDZT = (WK_L[k + 1] - WK_L[k]) * AREAxy_L;
                                float DWDZB = (WK_L[k] - WK_L[k - 1]) * AREAxy_L;
                                float ADD_DIFF = VIS / DZK_K * (DUDZE - DUDZW + DVDZN - DVDZS + DWDZT - DWDZB);
                                DIMW += ADD_DIFF;
                            }

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
                            WK_L[k] += (relax * (PIMW[k] * WK_L[k + 1] + QIMW[k] - WK_L[k]));
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
