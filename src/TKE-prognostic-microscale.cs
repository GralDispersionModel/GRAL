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
    class TKE_PrognosticMicroscale
    {
        /// <summary>
        /// Momentum equations for the turbulent kinetic energy - k-epsilon model
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Calculate(int IS, int JS, float Cmueh, float Ceps, float Ceps1, float Ceps2, float VISHMIN, float VISVMIN, float AREAxy, float building_Z0, float relax, float Ustern_factorX)
        {
            Parallel.For(2, Program.NII, Program.pOptions, i1 =>
            {
                float[] PIMTURB = new float[Program.KADVMAX + 1];
                float[] QIMTURB = new float[Program.KADVMAX + 1];
                float[] PIMTDISS = new float[Program.KADVMAX + 1];
                float[] QIMTDISS = new float[Program.KADVMAX + 1];
                float DXK = Program.DXK;
                float DYK = Program.DYK;
                float DXK_Rez = 1 / DXK;
                float DYK_Rez = 1 / DYK;

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
                        float[] TURB_L = Program.TURB[i][j];
                        float[] TURBim_L = Program.TURB[i - 1][j];
                        float[] TURBip_L = Program.TURB[i + 1][j];
                        float[] TURBjm_L = Program.TURB[i][j - 1];
                        float[] TURBjp_L = Program.TURB[i][j + 1];
                        float[] TDISS_L = Program.TDISS[i][j];
                        float[] TDISSim_L = Program.TDISS[i - 1][j];
                        float[] TDISSip_L = Program.TDISS[i + 1][j];
                        float[] TDISSjm_L = Program.TDISS[i][j - 1];
                        float[] TDISSjp_L = Program.TDISS[i][j + 1];
                        float[] UK_L = Program.UK[i][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] WK_L = Program.WK[i][j];
                        float[] UKip_L = Program.UK[i + 1][j];
                        float[] UKjp_L = Program.UK[i][j + 1];
                        float[] UKipjp_L = Program.UK[i + 1][j + 1];
                        float[] UKjm_L = Program.UK[i][j - 1];
                        float[] UKipjm_L = Program.UK[i + 1][j - 1];
                        float[] VKjp_L = Program.VK[i][j + 1];
                        float[] VKip_L = Program.VK[i + 1][j];
                        float[] VKipjp_L = Program.VK[i + 1][j + 1];
                        float[] VKim_L = Program.VK[i - 1][j];
                        float[] VKimjp_L = Program.VK[i - 1][j + 1];
                        float[] WKip_L = Program.WK[i + 1][j];
                        float[] WKim_L = Program.WK[i - 1][j];
                        float[] WKjp_L = Program.WK[i][j + 1];
                        float[] WKjm_L = Program.WK[i][j - 1];

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

                        int KKART = Program.KKART[i][j];

                        int KSTART = 1;
                        if (Program.CUTK[i][j] == 0)
                        {
                            KSTART = KKART + 1;
                        }

                        for (int k = KSTART; k <= Program.VerticalIndex[i][j]; k++)
                        {
                            float DXKDZK = DXK * Program.DZK[k];
                            float DYKDZK = DYK * Program.DZK[k];
                            float DZK_Rez = 1 / Program.DZK[k];

                            float zs = Program.HOKART[k] - Program.HOKART[KKART] - Program.DZK[k] * 0.5F;
                            float VIS = (float)Math.Sqrt(TURB_L[k]) * zs * Cmueh;
                            VIS = Math.Min(VIS, 15);


                            float DE = Math.Max(VIS, VISHMIN) * DYKDZK * DXK_Rez;
                            float DW = DE;
                            float DS = Math.Max(VIS, VISHMIN) * DXKDZK * DYK_Rez;
                            float DN = DS;
                            float DT = Math.Max(VIS, VISHMIN) * AREAxy * DZK_Rez;
                            float DB = 0;
                            float DTEPS = Math.Max(VIS / Ceps, VISVMIN) * AREAxy * DZK_Rez;
                            float DBEPS = 0;
                            if (k > KKART + 1)
                            {
                                DB = DT;
                                DBEPS = DTEPS;
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
                            float FE = UKip_L[k] * DYKDZK;
                            float FW = UK_L[k] * DYKDZK;
                            float FS = VK_L[k] * DXKDZK;
                            float FN = VKjp_L[k] * DXKDZK;
                            float FT = WK_L[k + 1] * AREAxy;
                            float FB = 0;
                            if (k > KKART + 1)
                            {
                                FB = WK_L[k] * AREAxy;
                            }

                            //PECLET NUMBERS
                            DE = Math.Max(DE, 0.0001F);
                            DB = Math.Max(DB, 0.0001F);
                            DW = Math.Max(DW, 0.0001F);
                            DS = Math.Max(DS, 0.0001F);
                            DN = Math.Max(DN, 0.0001F);
                            DT = Math.Max(DT, 0.0001F);
                            DBEPS = Math.Max(DBEPS, 0.0001F);
                            DTEPS = Math.Max(DTEPS, 0.0001F);

                            float PE = Math.Abs(FE / DE);
                            float PB = Math.Abs(FB / DB);
                            float PW = Math.Abs(FW / DW);
                            float PS = Math.Abs(FS / DS);
                            float PN = Math.Abs(FN / DN);
                            float PT = Math.Abs(FT / DT);
                            float PBEPS = Math.Abs(FN / DBEPS);
                            float PTEPS = Math.Abs(FT / DTEPS);

                            //POWER LAW ADVECTION SCHEME
                            float BIM = DT * Math.Max(0, Program.Pow5(1 - 0.1F * PT)) + Math.Max(-FT, 0);
                            float CIM = DB * Math.Max(0, Program.Pow5(1 - 0.1F * PB)) + Math.Max(FB, 0);
                            float AE1 = DE * Math.Max(0, Program.Pow5(1 - 0.1F * PE)) + Math.Max(-FE, 0);
                            float AW1 = DW * Math.Max(0, Program.Pow5(1 - 0.1F * PW)) + Math.Max(FW, 0);
                            float AS1 = DS * Math.Max(0, Program.Pow5(1 - 0.1F * PS)) + Math.Max(FS, 0);
                            float AN1 = DN * Math.Max(0, Program.Pow5(1 - 0.1F * PN)) + Math.Max(-FN, 0);
                            float BIMEPS = DT * Math.Max(0, Program.Pow5(1 - 0.1F * PTEPS)) + Math.Max(-FT, 0);
                            float CIMEPS = DB * Math.Max(0, Program.Pow5(1 - 0.1F * PBEPS)) + Math.Max(FB, 0);

                            //UPWIND SCHEME
                            /*
                            double BIM = DT + Math.Max(-FT, 0);
                            double CIM = DB + Math.Max(FB, 0);
                            double AE1 = DE + Math.Max(-FE, 0);
                            double AW1 = DW + Math.Max(FW, 0);
                            double AS1 = DS + Math.Max(FS, 0);
                            double AN1 = DN + Math.Max(-FN, 0);
                            double BIMEPS = DT + Math.Max(-FT, 0);
                            double CIMEPS = DB + Math.Max(FB, 0);
                             */

                            float AIM = BIM + CIM + AW1 + AS1 + AE1 + AN1 + PrognosticFlowfield.AP0[k];

                            //PRODUCTION TERMS OF TURBULENT KINETIC ENERGY
                            float UX1 = UKip_L[k];
                            float UX2 = UK_L[k];
                            float VX1 = 0.5F * (VKip_L[k] + VKipjp_L[k]);
                            float VX2 = 0.5F * (VKim_L[k] + VKimjp_L[k]);
                            float WX1 = 0.5F * (WKip_L[k] + WKip_L[k + 1]);
                            float WX2 = 0.5F * (WKim_L[k] + WKim_L[k + 1]);
                            float UY1 = 0.5F * (UKjp_L[k] + UKipjp_L[k]);
                            float UY2 = 0.5F * (UKjm_L[k] + UKipjm_L[k]);
                            float VY1 = VKjp_L[k];
                            float VY2 = VK_L[k];
                            float WY1 = 0.5F * (WKjp_L[k] + WKjp_L[k + 1]);
                            float WY2 = 0.5F * (WKjm_L[k] + WKjm_L[k + 1]);

                            float DUDX = (UX1 - UX2) * DXK_Rez;
                            float DVDX = (VX1 - VX2) * DXK_Rez * 0.5F;
                            float DWDX = (WX1 - WX2) * DXK_Rez * 0.5F;
                            float DUDY = (UY1 - UY2) * DYK_Rez * 0.5F;
                            float DVDY = (VY1 - VY2) * DYK_Rez;
                            float DWDY = (WY1 - WY2) * DYK_Rez * 0.5F;
                            float DUDZ = UK_L[k + 1] * DZK_Rez * 0.5F;
                            float DVDZ = VK_L[k + 1] * DZK_Rez * 0.5F;
                            float DWDZ = WK_L[k + 1] * DZK_Rez * 0.5F;
                            if (k > KKART + 1)
                            {
                                DUDZ = (UK_L[k + 1] - UK_L[k - 1]) * DZK_Rez * 0.5F;
                                DVDZ = (VK_L[k + 1] - VK_L[k - 1]) * DZK_Rez * 0.5F;
                                DWDZ = (WK_L[k + 1] - WK_L[k - 1]) * DZK_Rez * 0.5F;
                            }

                            //SOURCE TERMS
                            float PROTURB = (float)(VIS * AREAxy * Program.DZK[k] *
                                (2 * Program.Pow2(DUDX) + 2 * Program.Pow2(DVDY) + 2 * Program.Pow2(DWDZ) +
                                Program.Pow2(DUDY + DVDX) + Program.Pow2(DUDZ + DWDX) + Program.Pow2(DVDZ + DWDY) - TDISS_L[k]));
                            float PROBOU = (float)(-Program.PotdTdz * 9.81 / 283 * VIS * 1.35 * AREAxy * Program.DZK[k]);

                            float DIMTURB = AW1 * TURBim_L[k] + AS1 * TURBjm_L[k] + AE1 * TURBip_L[k] + AN1 * TURBjp_L[k] +
                                PrognosticFlowfield.AP0[k] * TURB_L[k] + PROTURB + PROBOU;
                            float DIMTDISS = AW1 * TDISSim_L[k] + AS1 * TDISSjm_L[k] + AE1 * TDISSip_L[k] + AN1 * TDISSjp_L[k] +
                                PrognosticFlowfield.AP0[k] * TDISS_L[k] + (TDISS_L[k] / TURB_L[k] * (Ceps1 * PROTURB - Ceps2 * TDISS_L[k]));

                            //RECURRENCE FORMULA
                            if (k > KKART + 1)
                            {
                                PIMTURB[k] = (BIM / (AIM - CIM * PIMTURB[k - 1]));
                                QIMTURB[k] = ((DIMTURB + CIM * QIMTURB[k - 1]) / (AIM - CIM * PIMTURB[k - 1]));
                                PIMTDISS[k] = (BIMEPS / (AIM - CIMEPS * PIMTDISS[k - 1]));
                                QIMTDISS[k] = ((DIMTDISS + CIMEPS * QIMTDISS[k - 1]) / (AIM - CIMEPS * PIMTDISS[k - 1]));
                            }
                            else
                            {
                                PIMTURB[k] = (BIM / AIM);
                                QIMTURB[k] = (DIMTURB / AIM);
                                PIMTDISS[k] = (BIMEPS / AIM);
                                QIMTDISS[k] = (DIMTDISS / AIM);
                            }
                        }
                        //OBTAIN NEW TKE/EPS-COMPONENTS
                        for (int k = Program.VerticalIndex[i][j]; k >= KSTART; k--)
                        {
                            if (KKART < k)
                            {
                                float Ustern_Buildings = 0.01F;

                                if ((k == KKART + 1) && (Program.CUTK[i][j] == 0))
                                {
                                    double x = i * Program.DXK + Program.GralWest;
                                    double y = j * Program.DYK + Program.GralSouth;
                                    double xsi1 = x - Program.GrammWest;
                                    double eta1 = y - Program.GrammSouth;
                                    int IUstern = Math.Clamp((int)(xsi1 / Program.DDX[1]) + 1, 1, Program.NX);
                                    int JUstern = Math.Clamp((int)(eta1 / Program.DDY[1]) + 1, 1, Program.NY);

                                    //surface-layer similarity is not applicable within the roughness layer
                                    //orographical surface
                                    if (Z0 >= Program.DZK[k] * 0.5)
                                    {
                                        Ustern_Buildings = (float)(0.4 / Math.Log((Program.DZK[k] + Program.DZK[k + 1] * 0.5) / Z0) *
                                            Math.Sqrt(Program.Pow2(0.5 * (UK_L[k + 1] + UKip_L[k + 1])) + Program.Pow2(0.5 * (VK_L[k + 1] + VKjp_L[k + 1]))));
                                        TURB_L[k] = (float)(3.33 * Program.Pow2(Ustern_Buildings));
                                        TDISS_L[k] = (float)(Program.Pow3(Ustern_Buildings) / ((Program.DZK[k] + Program.DZK[k + 1] * 0.5) * 0.4));
                                    }
                                    else
                                    {
                                        Ustern_Buildings = (float)(0.4 / Math.Log(Program.DZK[k] * 0.5 / Z0) *
                                            Math.Sqrt(Program.Pow2(0.5 * ((UK_L[k + 1]) + UKip_L[k])) + Program.Pow2(0.5 * ((VK_L[k]) + VKjp_L[k]))));
                                        TURB_L[k] = (float)(3.33 * Program.Pow2(Ustern_Buildings));
                                        TDISS_L[k] = (float)(Program.Pow3(Ustern_Buildings) / (Program.DZK[k] * 0.5 * 0.4));
                                    }

                                }
                                //Top of obstacles
                                else if ((k == KKART + 1) && (Program.CUTK[i][j] > 0))
                                {
                                    Ustern_Buildings = Program.UsternObstaclesHelpterm[i][j] *
                                                (float)Math.Sqrt(Program.Pow2(0.5 * (UK_L[k] + UKip_L[k])) + Program.Pow2(0.5 * (VK_L[k] + VKjp_L[k])));
                                    TURB_L[k] = (float)(3.33 * Program.Pow2(Ustern_Buildings));
                                    TDISS_L[k] = (float)(Program.Pow3(Ustern_Buildings) / (Program.DZK[k] * 0.5 * 0.4));
                                }
                                //near wall-flows
                                else if ((k == Program.KKART[i + 1][j] + 1) && (Program.CUTK[i + 1][j] > 0))
                                {
                                    Ustern_Buildings = (float)(Ustern_factorX * Math.Sqrt(Program.Pow2(0.5 * (WK_L[k] + WK_L[k + 1])) + Program.Pow2(0.5 * (VK_L[k] + VKjp_L[k]))));
                                    TURB_L[k] = (float)(3.33 * Program.Pow2(Ustern_Buildings));
                                    TDISS_L[k] = (float)(Program.Pow3(Ustern_Buildings) / (building_Z0 * 0.4));
                                }
                                else if ((k == Program.KKART[i - 1][j] + 1) && (Program.CUTK[i - 1][j] > 0))
                                {
                                    Ustern_Buildings = (float)(Ustern_factorX * Math.Sqrt(Program.Pow2(0.5 * (WK_L[k] + WK_L[k + 1])) + Program.Pow2(0.5 * (VK_L[k] + VKjp_L[k]))));
                                    TURB_L[k] = (float)(3.33 * Program.Pow2(Ustern_Buildings));
                                    TDISS_L[k] = (float)(Program.Pow3(Ustern_Buildings) / (building_Z0 * 0.4));
                                }
                                else if ((k == Program.KKART[i][j + 1] + 1) && (Program.CUTK[i][j + 1] > 0))
                                {
                                    Ustern_Buildings = (float)(Ustern_factorX * Math.Sqrt(Program.Pow2(0.5 * (WK_L[k] + WK_L[k + 1])) + Program.Pow2(0.5 * (UK_L[k] + UKip_L[k]))));
                                    TURB_L[k] = (float)(3.33 * Program.Pow2(Ustern_Buildings));
                                    TDISS_L[k] = (float)(Program.Pow3(Ustern_Buildings) / (building_Z0 * 0.4));
                                }
                                else if ((k == Program.KKART[i][j - 1] + 1) && (Program.CUTK[i][j - 1] > 0))
                                {
                                    Ustern_Buildings = (float)(Ustern_factorX * Math.Sqrt(Program.Pow2(0.5 * (WK_L[k] + WK_L[k + 1])) + Program.Pow2(0.5 * (UK_L[k] + UKip_L[k]))));
                                    TURB_L[k] = (float)(3.33 * Program.Pow2(Ustern_Buildings));
                                    TDISS_L[k] = (float)(Program.Pow3(Ustern_Buildings) / (building_Z0 * 0.4));
                                }
                                //free flow conditions
                                else
                                {
                                    TURB_L[k] = PIMTURB[k] * TURB_L[k + 1] + QIMTURB[k];
                                    TDISS_L[k] = PIMTDISS[k] * TDISS_L[k + 1] + QIMTDISS[k];
                                }

                                TURB_L[k] = (float)Math.Max(TURB_L[k], 0.001);
                                TDISS_L[k] = (float)Math.Max(TDISS_L[k], 0.0000001);
                            }
                        }
                    }
                }
            });
        }
    }
}
