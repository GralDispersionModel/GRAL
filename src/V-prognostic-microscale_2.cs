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
    class V_PrognosticMicroscaleV2
    {
        public static float l_infinitive = 90;

        /// <summary>
    	/// Momentum equations for the v wind component - k-epsilon model
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
                        float Z0 = 0.1F;
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

                        int KSTART = 1;
                        if (CUTK_L == 0) // building heigth == 0 m
                        {
                            KSTART = KKART_LL + 1;
                        }

                        for (int k = KSTART; k <= Vert_Index_LL; k++)
                        {
                            float DZK_K = Program.DZK[k];
                            float DXKDZK = DXK * DZK_K;
                            float DYKDZK = DYK * DZK_K;

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
                            {
                                DB = DT;
                            }

                            //BOUNDARY CONDITIONS FOR DIFFUSION TERMS
                            if ((Program.ADVDOM[i][j - 1] < 1) || (j == 2))
                            {
                                DS = 0;
                            }

                            if ((Program.ADVDOM[i][j + 1] < 1) || (j == Program.NJJ - 1))
                            {
                                DN = 0;
                            }

                            if ((Program.ADVDOM[i - 1][j] < 1) || (i == 2))
                            {
                                DW = 0;
                            }

                            if ((Program.ADVDOM[i + 1][j] < 1) || (i == Program.NII - 1))
                            {
                                DE = 0;
                            }


                            //ADVECTION TERMS
                            float FE = 0.25F * (UKS_L[k] + UKSip_L[k] + UKSjm_L[k] + UKSipjm_L[k]) * DYKDZK;
                            float FW = 0.25F * (UKS_L[k] + UKSim_L[k] + UKSjm_L[k] + UKSimjm_L[k]) * DYKDZK;
                            float FS = VKSjm_L[k] * DXKDZK;
                            float FN = VKS_L[k] * DXKDZK;
                            float FT = 0.25F * (WKSjm_L[k] + WKS_L[k] + WKSjm_L[k + 1] + WKS_L[k + 1]) * AREAxy_L;
                            float FB = 0;
                            if (k > KKART_LL + 1)
                            {
                                FB = 0.25F * (WKSjm_L[k] + WKS_L[k] + WKSjm_L[k - 1] + WKS_L[k - 1]) * AREAxy_L;
                            }

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
                            double BIM = DT + Program.fl_max(-FT, 0);
                            double CIM = DB + Program.fl_max(FB, 0);
                            double AE1 = DE + Program.fl_max(-FE, 0);
                            double AW1 = DW + Program.fl_max(FW, 0);
                            double AS1 = DS + Program.fl_max(FS, 0);
                            double AN1 = DN + Program.fl_max(-FN, 0);
                             */

                            float AIM = BIM + CIM + AW1 + AS1 + AE1 + AN1 + PrognosticFlowfield.AP0[k];

                            //SOURCE TERMS
                            float DDPY = DPMNEWjm_L[k] - DPMNEW_L[k];
                            float DIMV = (float)(AW1 * VKim_L[k] + AS1 * VKjm_L[k] + AE1 * VKip_L[k] + AN1 * VKjp_L[k] +
                                PrognosticFlowfield.AP0[k] * 0.5 * (VKSjm_L[k] + VKS_L[k]) + DDPY * DXKDZK + Program.CorolisParam * (VG - VK_L[k]) * AREAxy_L * DZK_K);

                            //BOUNDARY CONDITION AT SURFACES (OBSTACLES AND TERRAIN)
                            if (k == KKART_LL + 1)
                            {
                                if (CUTK_L < 1) // building heigth < 1 m
                                {
                                    //above terrain
                                    float xhilf = (float)((float)i * (DXK - DXK * 0.5));
                                    float yhilf = (float)((float)j * (DYK - DYK * 0.5));
                                    float windhilf = Program.FloatMax((float)Math.Sqrt(Program.Pow2(0.5 * ((UKSim_L[k]) + UKS_L[k])) + Program.Pow2(0.5 * ((VKSim_L[k]) + VKS_L[k]))), 0.01F);
                                    int IUstern = (int)(xhilf / Program.DDX[1]) + 1;
                                    int JUstern = (int)(yhilf / Program.DDY[1]) + 1;
                                    float Ustern_Buildings = Ustern_terrain_helpterm * windhilf;
                                    if (Z0 >= DZK_K * 0.1)
                                    {
                                        Ustern_Buildings = Ustern_terrain_helpterm * (float)Math.Sqrt(Program.Pow2(0.5 * ((UKSim_L[k + 1]) + UKS_L[k + 1])) + Program.Pow2(0.5 * ((VKSim_L[k + 1]) + VKS_L[k + 1])));
                                    }

                                    DIMV -= (float)(VK_L[k] / windhilf * Program.Pow2(Ustern_Buildings) * AREAxy_L);
                                }
                                else
                                {
                                    //above building
                                    float windhilf = Program.FloatMax((float)Math.Sqrt(Program.Pow2(0.5 * ((UKSim_L[k]) + UKS_L[k])) + Program.Pow2(0.5 * ((VKSim_L[k]) + VKS_L[k]))), 0.01F);
                                    float Ustern_Buildings = Ustern_obstacles_helpterm * windhilf;
                                    DIMV -= (float)(VK_L[k] / windhilf * Program.Pow2(Ustern_Buildings) * AREAxy_L);
                                }
                            }

                            //additional terms of the eddy-viscosity model
                            if (k > KKART_LL + 1)
                            {
                                float DVDYN = (VKjp_L[k] - VK_L[k]) * DXKDZK;
                                float DVDYS = (VK_L[k] - VKjm_L[k]) * DXKDZK;
                                float DUDYE = (0.5F * (UKSipjp_L[k] + UKSip_L[k]) - 0.5F * (UKSip_L[k] + UKSipjm_L[k])) * DXKDZK;
                                float DUDYW = (0.5F * (UKSimjp_L[k] + UKSim_L[k]) - 0.5F * (UKSim_L[k] + UKSimjm_L[k])) * DXKDZK;
                                float DWDYT = (0.5F * (WKSjp_L[k] + WKS_L[k]) - 0.5F * (WKS_L[k] + WKSjm_L[k])) * AREAxy_L;
                                float DWDYB = (0.5F * (WKSjp_L[k - 1] + WKS_L[k - 1]) - 0.5F * (WKS_L[k - 1] + WKSjm_L[k - 1])) * AREAxy_L;
                                float ADD_DIFF = VIS / DXK * (DUDYE - DUDYW + DVDYN - DVDYS + DWDYT - DWDYB);
                                DIMV += ADD_DIFF;
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
    }
}
