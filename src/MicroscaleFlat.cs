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
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace GRAL_2001
{
    class MicroscaleFlat
    {
        /// <summary>
        /// Pre calculations for flat microscale terrain
        /// </summary>
        public static void Calculate()
        {
            Console.WriteLine();
            Console.WriteLine("FLOW FIELD COMPUTATION WITHOUT TOPOGRAPHY");

            Program.KADVMAX = 1;
            
            // Search the reference point within the prognostic sub domains
            Program.SubDomainRefPos = MicroscaleTerrain.GetSubDomainPoint(Program.SubDomainRefPos);

            int inumm = Program.MetProfileNumb;

            //set wind-speed components equal to zero
            Parallel.For(1, Program.NII + 2, Program.pOptions, i =>
            {
                for (int j = 1; j <= Program.NJJ + 1; j++)
                {
                    float[] UK_L = Program.UK[i][j];
                    float[] VK_L = Program.VK[i][j];
                    float[] WK_L = Program.WK[i][j];
                    for (int k = 1; k <= Program.NKK; k++)
                    {
                        UK_L[k] = 0;
                        VK_L[k] = 0;
                        WK_L[k] = 0;
                    }
                }
            });

            //Set initial wind components of the GRAL wind field
            object obj = new object();
            //Parallel.For(1, Program.NII + 1, Program.pOptions, i =>
            Parallel.ForEach(Partitioner.Create(1, Program.NII + 1, Math.Max(4, (int)(Program.NII / Program.pOptions.MaxDegreeOfParallelism))), range =>
            {
                int KADVMAX1 = 1;

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    for (int j = 1; j <= Program.NJJ; j++)
                    {
                        Program.AHK[i][j] = 0;
                        Program.KKART[i][j] = 0;

                        //Pointers (to speed up the computations)
                        float[] UK_L = Program.UK[i][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] UKi_L = Program.UK[i + 1][j];
                        float[] VKj_L = Program.VK[i][j + 1];
                        double UXint;
                        double UYint;
                        float vertk;
                        float exponent;
                        float dumfac;
                        float ObLength = Program.Ob[1][1];
                        if (Program.AdaptiveRoughnessMax > 0)
                        {
                            ObLength = Program.OLGral[i][j];
                        }

                        for (int k = Program.NKK; k >= 1; k--)
                        {
                            vertk = Program.HOKART[k - 1] + Program.DZK[k] * 0.5F;
                            if (vertk > Program.CUTK[i][j])
                            {
                                //interpolation between observations
                                if (vertk <= Program.MeasurementHeight[1])
                                {
                                    if (ObLength <= 0)
                                    {
                                        exponent = MathF.Max(0.35F - 0.4F * MathF.Pow(MathF.Abs(ObLength), -0.15F), 0.05F);
                                    }
                                    else
                                    {
                                        exponent = 0.56F * MathF.Pow(ObLength, -0.15F);
                                    }

                                    dumfac = MathF.Pow(vertk / Program.MeasurementHeight[1], exponent);
                                    UXint = Program.ObsWindU[1] * dumfac;
                                    UYint = Program.ObsWindV[1] * dumfac;
                                }
                                else if (vertk > Program.MeasurementHeight[inumm])
                                {
                                    if (inumm == 1)
                                    {
                                        if (ObLength <= 0)
                                        {
                                            exponent = MathF.Max(0.35F - 0.4F * MathF.Pow(MathF.Abs(ObLength), -0.15F), 0.05F);
                                        }
                                        else
                                        {
                                            exponent = 0.56F * MathF.Pow(ObLength, -0.15F);
                                        }

                                        dumfac = MathF.Pow(vertk / Program.MeasurementHeight[inumm], exponent);
                                        UXint = Program.ObsWindU[inumm] * dumfac;
                                        UYint = Program.ObsWindV[inumm] * dumfac;
                                    }
                                    else
                                    {
                                        UXint = Program.ObsWindU[inumm];
                                        UYint = Program.ObsWindV[inumm];
                                    }
                                }
                                else
                                {
                                    int ipo = 1;
                                    for (int iprof = 1; iprof <= inumm; iprof++)
                                    {
                                        if (vertk > Program.MeasurementHeight[iprof])
                                        {
                                            ipo = iprof + 1;
                                        }
                                    }
                                    UXint = Program.ObsWindU[ipo - 1] + (Program.ObsWindU[ipo] - Program.ObsWindU[ipo - 1]) / (Program.MeasurementHeight[ipo] - Program.MeasurementHeight[ipo - 1]) *
                                     (vertk - Program.MeasurementHeight[ipo - 1]);
                                    UYint = Program.ObsWindV[ipo - 1] + (Program.ObsWindV[ipo] - Program.ObsWindV[ipo - 1]) / (Program.MeasurementHeight[ipo] - Program.MeasurementHeight[ipo - 1]) *
                                     (vertk - Program.MeasurementHeight[ipo - 1]);
                                }

                                //finally get wind speeds outside obstacles
                                UK_L[k] = (float)UXint;
                                VK_L[k] = (float)UYint;
                                if (i > 1)
                                {
                                    if (vertk <= Program.CUTK[i - 1][j])
                                    {
                                        UK_L[k] = 0;
                                    }
                                }
                                if (j > 1)
                                {
                                    if (vertk <= Program.CUTK[i][j - 1])
                                    {
                                        VK_L[k] = 0;
                                    }
                                }
                                Program.UK[Program.NII + 1][j][k] = Program.UK[Program.NII][j][k];
                                Program.VK[i][Program.NJJ + 1][k] = Program.VK[i][Program.NJJ][k];
                            }
                            else
                            {
                                //set wind speed zero inside obstacles
                                UK_L[k] = 0;
                                if (i < Program.NII)
                                {
                                    UKi_L[k] = 0;
                                }

                                VK_L[k] = 0;
                                if (j < Program.NJJ)
                                {
                                    VKj_L[k] = 0;
                                }

                                Program.AHK[i][j] = Math.Max(Program.HOKART[k], Program.AHK[i][j]);
                                Program.BUI_HEIGHT[i][j] = Program.AHK[i][j];
                                Program.KKART[i][j] = Convert.ToInt16(Math.Max(k, Program.KKART[i][j]));
                                if (Program.CUTK[i][j] > 0)
                                {
                                    KADVMAX1 = Math.Max(Program.KKART[i][j], KADVMAX1);
                                }
                            }
                        }
                    }
                }
                if (KADVMAX1 > Program.KADVMAX)
                {
                    lock (obj) { Program.KADVMAX = Math.Max(Program.KADVMAX, KADVMAX1); }
                }
            });
            obj = null;

            //maximum z-index up to which the microscale flow field is being computed
            Program.KADVMAX = Math.Min(Program.NKK, Program.KADVMAX + Program.VertCellsFF);

            Program.AHMIN = 0;

            //in case of the diagnostic approach or the prognostic approach with reduced SubDomains, a boundary layer is established near the obstacle's walls
            if ((Program.FlowFieldLevel <= Consts.FlowFieldDiag) || 
                (Program.FlowFieldLevel == Consts.FlowFieldProg && Program.SubDomainDistance < 10000))
            {
                Parallel.For((1 + Program.IGEB), (Program.NII - Program.IGEB + 1), Program.pOptions, i =>
                {
                    int IGEB = Program.IGEB;
                    float DXK = Program.DXK;
                    float DYK = Program.DYK;
                    for (int j = 1 + Program.IGEB; j <= Program.NJJ - Program.IGEB; j++)
                    {
                        //in case of diagnostic approach or outside prognostic sub domain area
                        if ((Program.FlowFieldLevel <= Consts.FlowFieldDiag) || (Program.ADVDOM[i][j] == 0))
                        {
                            float entf = 0;
                            float vertk;
                            float abmind;
                            for (int k = 1; k < Program.NKK; k++)
                            {
                                abmind = 1;
                                if (k > Program.KKART[i][j])
                                {
                                    vertk = Program.HOKART[k - 1] + Program.DZK[k] * 0.5F;

                                    //search towards west for obstacles
                                    entf = 21;
                                    for (int ig = i - 1; ig >= i - IGEB; ig--)
                                    {
                                        if ((Program.CUTK[ig][j] > 1) && (vertk <= Program.AHK[ig][j]))
                                        {
                                            entf = Math.Abs((i - ig) * DXK);
                                            break; //break the loop
                                        }
                                    }
                                    if (entf <= 20)
                                    {
                                        abmind *= 0.19F * MathF.Log((entf + 0.5F) * 10);
                                    }

                                    //search towards east for obstacles
                                    entf = 21;
                                    for (int ig = i + 1; ig <= i + IGEB; ig++)
                                    {
                                        if ((Program.CUTK[ig][j] > 1) && (vertk <= Program.AHK[ig][j]))
                                        {
                                            entf = MathF.Abs((i - ig) * DXK);
                                            break; //break the loop
                                        }
                                    }
                                    if (entf <= 20)
                                    {
                                        abmind *= 0.19F * MathF.Log((entf + 0.5F) * 10);
                                    }

                                    //search towards north for obstacles
                                    entf = 21;
                                    for (int jg = j + 1; jg <= j + IGEB; jg++)
                                    {
                                        if ((Program.CUTK[i][jg] > 1) && (vertk <= Program.AHK[i][jg]))
                                        {
                                            entf = MathF.Abs((j - jg) * DYK);
                                            break; //break the loop
                                        }
                                    }
                                    if (entf <= 20)
                                    {
                                        abmind *= 0.19F * MathF.Log((entf + 0.5F) * 10);
                                    }

                                    //search towards south for obstacles
                                    entf = 21;
                                    for (int jg = j - 1; jg >= j - IGEB; jg--)
                                    {
                                        if ((Program.CUTK[i][jg] > 1) && (vertk <= Program.AHK[i][jg]))
                                        {
                                            entf = MathF.Abs((j - jg) * DYK);
                                            break; //break the loop
                                        }
                                    }
                                    if (entf <= 20)
                                    {
                                        abmind *= 0.19F * MathF.Log((entf + 0.5F) * 10);
                                    }

                                    //search towards south/east for obstacles
                                    entf = 21;
                                    for (int ig = i + 1; ig <= i + IGEB; ig++)
                                    {
                                        int jg = j + ig - i;
                                        if ((Program.CUTK[ig][jg] > 1) && (vertk <= Program.AHK[ig][jg]))
                                        {
                                            entf = MathF.Sqrt(Program.Pow2((i - ig) * DXK) + Program.Pow2((j - jg) * DYK));
                                            break; //break the loop
                                        }
                                    }
                                    if (entf <= 20)
                                    {
                                        abmind *= 0.19F * MathF.Log((entf + 0.5F) * 10);
                                    }

                                    //search towards north/west for obstacles
                                    entf = 21;
                                    for (int ig = i - 1; ig >= i - IGEB; ig--)
                                    {
                                        int jg = j + ig - i;
                                        if ((Program.CUTK[ig][jg] > 1) && (vertk <= Program.AHK[ig][jg]))
                                        {
                                            entf = MathF.Sqrt(Program.Pow2((i - ig) * DXK) + Program.Pow2((j - jg) * DYK));
                                            break; //break the loop
                                        }
                                    }
                                    if (entf <= 20)
                                    {
                                        abmind *= 0.19F * MathF.Log((entf + 0.5F) * 10);
                                    }

                                    //search towards north/east for obstacles
                                    entf = 21;
                                    for (int ig = i + 1; ig <= i + IGEB; ig++)
                                    {
                                        int jg = j + i - ig;
                                        if ((Program.CUTK[ig][jg] > 1) && (vertk <= Program.AHK[ig][jg]))
                                        {
                                            entf = MathF.Sqrt(Program.Pow2((i - ig) * DXK) + Program.Pow2((j - jg) * DYK));
                                            break; //break the loop
                                        }
                                    }
                                    if (entf <= 20)
                                    {
                                        abmind *= 0.19F * MathF.Log((entf + 0.5F) * 10);
                                    }

                                    //search towards south/west for obstacles
                                    entf = 21;
                                    for (int ig = i - 1; ig >= i - IGEB; ig--)
                                    {
                                        int jg = j + i - ig;
                                        if ((Program.CUTK[ig][jg] > 1) && (vertk <= Program.AHK[ig][jg]))
                                        {
                                            entf = MathF.Sqrt(Program.Pow2((i - ig) * DXK) + Program.Pow2((j - jg) * DYK));
                                            break; //break the loop
                                        }
                                    }
                                    if (entf <= 20)
                                    {
                                        abmind *= 0.19F * MathF.Log((entf + 0.5F) * 10);
                                    }


                                    Program.UK[i][j][k] *= abmind;
                                    Program.VK[i][j][k] *= abmind;
                                }
                            }
                        }
                    }
                });
            }
            
            //computing flow field either with diagnostic or prognostic approach
            if (Program.FlowFieldLevel > Consts.FlowFieldNoBuildings)
            {
                //diagnostic approach
                if (Program.FlowFieldLevel == Consts.FlowFieldDiag)
                {
                    Console.WriteLine("DIAGNOSTIC WIND FIELD AROUND OBSTACLES");
                    DiagnosticFlowfield.Calculate();
                }

                //prognostic approach
                if (Program.FlowFieldLevel == Consts.FlowFieldProg)
                {
                    //read vegetation only one times
                    if (Program.VEG[0][0][0] < 0)
                    {
                        ProgramReaders Readclass = new ProgramReaders();
                        Readclass.ReadVegetationDomain(null);
                        Readclass.ReadVegetation();
                    }

                    Console.WriteLine("PROGNOSTIC WIND FIELD AROUND OBSTACLES");
                    PrognosticFlowfield.Calculate();
                
                    //Final mass conservation using poisson equation for pressure after advection has been computed with level 2
                    DiagnosticFlowfield.Calculate();
                }
            }

            //in diagnostic mode mass-conservation is finally achieved by adjusting the vertical velocity in each cell
            Parallel.For(2, Program.NII, Program.pOptions, i =>
            {
                for (int j = 2; j < Program.NJJ; j++)
                {
                    if ((Program.FlowFieldLevel <= Consts.FlowFieldDiag) || (Program.ADVDOM[i][j] == 0))
                    {
                        //Pointers (to speed up the computations)
                        float[] UK_L = Program.UK[i][j];
                        float[] UKi_L = Program.UK[i + 1][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] VKj_L = Program.VK[i][j + 1];
                        float[] WK_L = Program.WK[i][j];
                        float fwo1 = 0;
                        float fwo2 = 0;
                        float fsn1 = 0;
                        float fsn2 = 0;
                        float fbt1 = 0;
                        int KKART = Program.KKART[i][j];

                        for (int k = 1; k < Program.NKK; k++)
                        {
                            if (k > KKART)
                            {
                                if (k <= Program.KKART[i - 1][j])
                                {
                                    fwo1 = 0;
                                }
                                else
                                {
                                    fwo1 = Program.DZK[k] * Program.DYK * UK_L[k];
                                }

                                if (k <= Program.KKART[i + 1][j])
                                {
                                    fwo2 = 0;
                                }
                                else
                                {
                                    fwo2 = Program.DZK[k] * Program.DYK * UKi_L[k];
                                }

                                if (k <= Program.KKART[i][j - 1])
                                {
                                    fsn1 = 0;
                                }
                                else
                                {
                                    fsn1 = Program.DZK[k] * Program.DXK * VK_L[k];
                                }

                                if (k <= Program.KKART[i][j + 1])
                                {
                                    fsn2 = 0;
                                }
                                else
                                {
                                    fsn2 = Program.DZK[k] * Program.DXK * VKj_L[k];
                                }

                                if ((k <= KKART + 1) || (k == 1))
                                {
                                    fbt1 = 0;
                                }
                                else
                                {
                                    fbt1 = Program.DXK * Program.DYK * WK_L[k];
                                }

                                WK_L[k + 1] = (fwo1 - fwo2 + fsn1 - fsn2 + fbt1) / (Program.DXK * Program.DYK);
                            }
                        }
                    }
                }
            });
            
        }
    }
}
