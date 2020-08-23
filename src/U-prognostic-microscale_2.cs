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
    class U_PrognosticMicroscaleV2
    {
        public static float l_infinitive = 90;

        /// <summary>
    	/// Momentum equations for the u wind component - k-epsilon model
    	/// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, Single UG, float building_Z0, float relax)
        {
            Parallel.For(3, Program.NII, Program.pOptions, i1 =>
            {
                float DXK = Program.DXK; float DYK = Program.DYK;
                int KKART_LL, Vert_Index_LL;
                float AREAxy_L = AREAxy;
                Single[] PIMU = new Single[Program.KADVMAX + 1];
                Single[] QIMU = new Single[Program.KADVMAX + 1];

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
                        Single[] UK_L = Program.UK[i][j];
                        Single[] UKS_L = Program.UKS[i][j];
                        Single[] VKS_L = Program.VKS[i][j];
                        Single[] WKS_L = Program.WKS[i][j];
                        Single[] DPMNEW_L = Program.DPMNEW[i][j];
                        Single[] UKim_L = Program.UK[i - 1][j];
                        Single[] UKip_L = Program.UK[i + 1][j];
                        Single[] UKjm_L = Program.UK[i][j - 1];
                        Single[] UKjp_L = Program.UK[i][j + 1];
                        Single[] UKSim_L = Program.UKS[i - 1][j];
                        Single[] VKSim_L = Program.VKS[i - 1][j];
                        Single[] VKSimjm_L = Program.VKS[i - 1][j - 1];
                        Single[] VKSjm_L = Program.VKS[i][j - 1];
                        Single[] VKSimjp_L = Program.VKS[i - 1][j + 1];
                        Single[] VKSjp_L = Program.VKS[i][j + 1];
                        Single[] WKSim_L = Program.WKS[i - 1][j];
                        Single[] DPMNEWim_L = Program.DPMNEW[i - 1][j];

                        Single[] VKSipjp_L = Program.VKS[i + 1][j + 1];
                        Single[] VKSipjm_L = Program.VKS[i + 1][j - 1];
                        Single[] WKSip_L = Program.WK[i + 1][j];
                        Single[] VK_L = Program.VK[i][j];
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
                            float FE = UKS_L[k] * DYKDZK;
                            float FW = UKSim_L[k] * DYKDZK;
                            float FS = 0.25F * (VKSim_L[k] + VKS_L[k] + VKSimjm_L[k] + VKSjm_L[k]) * DXKDZK;
                            float FN = 0.25F * (VKSim_L[k] + VKS_L[k] + VKSimjp_L[k] + VKSjp_L[k]) * DXKDZK;
                            float FT = 0.25F * (WKSim_L[k] + WKS_L[k] + WKSim_L[k + 1] + WKS_L[k + 1]) * AREAxy_L;
                            float FB = 0;
                            if (k > KKART_LL + 1)
                            {
                                FB = 0.25F * (WKSim_L[k] + WKS_L[k] + WKSim_L[k - 1] + WKS_L[k - 1]) * AREAxy_L;
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
                            float DDPX = DPMNEWim_L[k] - DPMNEW_L[k];
                            float DIMU = (float)(AW1 * UKim_L[k] + AS1 * UKjm_L[k] + AE1 * UKip_L[k] + AN1 * UKjp_L[k] +
                                PrognosticFlowfield.AP0[k] * 0.5 * (UKSim_L[k] + UKS_L[k]) + DDPX * DYKDZK + Program.CorolisParam * (UG - UK_L[k]) * AREAxy_L * DZK_K);


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

                                    DIMU -= (float)(UK_L[k] / windhilf * Program.Pow2(Ustern_Buildings) * AREAxy_L);
                                }
                                else
                                {
                                    //above building
                                    float windhilf = Program.FloatMax((float)Math.Sqrt(Program.Pow2(0.5 * ((UKSim_L[k]) + UKS_L[k])) + Program.Pow2(0.5 * ((VKSim_L[k]) + VKS_L[k]))), 0.01F);
                                    float Ustern_Buildings = Ustern_obstacles_helpterm * windhilf;
                                    DIMU -= (float)(UK_L[k] / windhilf * Program.Pow2(Ustern_Buildings) * AREAxy_L);
                                }
                            }

                            //additional terms of the eddy-viscosity model
                            if (k > KKART_LL + 1)
                            {
                                float DUDXE = (UKip_L[k] - UK_L[k]) * DYKDZK;
                                float DUDXW = (UK_L[k] - UKim_L[k]) * DYKDZK;
                                float DVDXN = (0.5F * (VKSipjp_L[k] + VKSjp_L[k]) - 0.5F * (VKSjp_L[k] + VKSimjp_L[k])) * DXKDZK;
                                float DVDXS = (0.5F * (VKSipjm_L[k] + VKSjm_L[k]) - 0.5F * (VKSjm_L[k] + VKSimjm_L[k])) * DXKDZK;
                                float DWDXT = (0.5F * (WKSip_L[k] + WKS_L[k]) - 0.5F * (WKS_L[k] + WKSim_L[k])) * AREAxy_L;
                                float DWDXB = (0.5F * (WKSip_L[k - 1] + WKS_L[k - 1]) - 0.5F * (WKS_L[k - 1] + WKSim_L[k - 1])) * AREAxy_L;
                                float ADD_DIFF = VIS / DXK * (DUDXE - DUDXW + DVDXN - DVDXS + DWDXT - DWDXB);
                                DIMU += ADD_DIFF;
                            }

                            //RECURRENCE FORMULA
                            if (k > KKART_LL + 1)
                            {
                                PIMU[k] = (BIM / (AIM - CIM * PIMU[k - 1]));
                                QIMU[k] = ((DIMU + CIM * QIMU[k - 1]) / (AIM - CIM * PIMU[k - 1]));
                            }
                            else
                            {
                                PIMU[k] = (BIM / AIM);
                                QIMU[k] = (DIMU / AIM);
                            }
                        }
                        //OBTAIN NEW U-COMPONENTS
                        for (int k = Vert_Index_LL; k >= KSTART; k--)
                        {
                            if ((KKART_LL < k) && (Program.KKART[i - 1][j] < k))
                            {
                                UK_L[k] += (relax * (PIMU[k] * UK_L[k + 1] + QIMU[k] - UK_L[k]));
                            }
                        }
                    }
                }
            });
        }
    }
}
