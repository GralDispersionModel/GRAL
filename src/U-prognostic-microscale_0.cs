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
    class U_PrognosticMicroscaleV0
    {
        public static float l_infinitive = 90;

        /// <summary>
        /// Momentum equations for the u wind component - no diffusion
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
                            if ((Program.Topo == Consts.TerrainAvailable) && (Program.LandUseAvailable == true))
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

                            //POWER LAW ADVECTION SCHEME
                            float BIM = Program.FloatMax(-FT, 0F);
                            float CIM = Program.FloatMax(FB, 0F);
                            float AE1 = Program.FloatMax(-FE, 0F);
                            float AW1 = Program.FloatMax(FW, 0F);
                            float AS1 = Program.FloatMax(FS, 0F);
                            float AN1 = Program.FloatMax(-FN, 0F);

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
                                    float windhilf = Program.FloatMax((float)Math.Sqrt(Program.Pow2(0.5 * ((UKSim_L[k]) + UKS_L[k])) + Program.Pow2(0.5 * ((VKSim_L[k]) + VKS_L[k]))), 0.01F);
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
