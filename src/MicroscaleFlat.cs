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
                           double vertk;
                           double exponent;
                           double dumfac;

                           for (int k = Program.NKK; k >= 1; k--)
                           {
                               vertk = Program.HOKART[k - 1] + Program.DZK[k] * 0.5;
                               if (vertk > Program.CUTK[i][j])
                               {
                                //interpolation between observations
                                if (vertk <= Program.MeasurementHeight[1])
                                   {
                                       if (Program.Ob[1][1] <= 0)
                                           exponent = Math.Max(0.35 - 0.4 * Math.Pow(Math.Abs(Program.Ob[1][1]), -0.15), 0.05);
                                       else
                                           exponent = 0.56 * Math.Pow(Program.Ob[1][1], -0.15);

                                       dumfac = Math.Pow(vertk / Program.MeasurementHeight[1], exponent);
                                       UXint = Program.UX[1] * dumfac;
                                       UYint = Program.UY[1] * dumfac;
                                   }
                                   else if (vertk > Program.MeasurementHeight[inumm])
                                   {
                                       if (inumm == 1)
                                       {
                                           if (Program.Ob[1][1] <= 0)
                                               exponent = Math.Max(0.35 - 0.4 * Math.Pow(Math.Abs(Program.Ob[1][1]), -0.15), 0.05);
                                           else
                                               exponent = 0.56 * Math.Pow(Program.Ob[1][1], -0.15);

                                           dumfac = Math.Pow(vertk / Program.MeasurementHeight[inumm], exponent);
                                           UXint = Program.UX[inumm] * dumfac;
                                           UYint = Program.UY[inumm] * dumfac;
                                       }
                                       else
                                       {
                                           UXint = Program.UX[inumm];
                                           UYint = Program.UY[inumm];
                                       }
                                   }
                                   else
                                   {
                                       int ipo = 1;
                                       for (int iprof = 1; iprof <= inumm; iprof++)
                                       {
                                           if (vertk > Program.MeasurementHeight[iprof])
                                               ipo = iprof + 1;
                                       }
                                       UXint = Program.UX[ipo - 1] + (Program.UX[ipo] - Program.UX[ipo - 1]) / (Program.MeasurementHeight[ipo] - Program.MeasurementHeight[ipo - 1]) *
                                        (vertk - Program.MeasurementHeight[ipo - 1]);
                                       UYint = Program.UY[ipo - 1] + (Program.UY[ipo] - Program.UY[ipo - 1]) / (Program.MeasurementHeight[ipo] - Program.MeasurementHeight[ipo - 1]) *
                                        (vertk - Program.MeasurementHeight[ipo - 1]);
                                   }

                                //finally get wind speeds outside obstacles
                                UK_L[k] = (float)UXint;
                                   VK_L[k] = (float)UYint;
                                   if (i > 1)
                                   {
                                       if (vertk <= Program.CUTK[i - 1][j])
                                           UK_L[k] = 0;
                                   }
                                   if (j > 1)
                                   {
                                       if (vertk <= Program.CUTK[i][j - 1])
                                           VK_L[k] = 0;
                                   }
                                   Program.UK[Program.NII + 1][j][k] = Program.UK[Program.NII][j][k];
                                   Program.VK[i][Program.NJJ + 1][k] = Program.VK[i][Program.NJJ][k];
                               }
                               else
                               {
                                //set wind speed zero inside obstacles
                                UK_L[k] = 0;
                                   if (i < Program.NII) UKi_L[k] = 0;
                                   VK_L[k] = 0;
                                   if (j < Program.NJJ) VKj_L[k] = 0;
                                   Program.AHK[i][j] = Math.Max(Program.HOKART[k], Program.AHK[i][j]);
                                   Program.BUI_HEIGHT[i][j] = Program.AHK[i][j];
                                   Program.KKART[i][j] = Convert.ToInt16(Math.Max(k, Program.KKART[i][j]));
                                   if (Program.CUTK[i][j] > 0)
                                       KADVMAX1 = Math.Max(Program.KKART[i][j], KADVMAX1);
                               }
                           }
                       }
                   }
                   if (KADVMAX1 > Program.KADVMAX)
                       lock (obj) { Program.KADVMAX = Math.Max(Program.KADVMAX, KADVMAX1); }
               });
            obj = null;
            //maximum z-index up to which the microscale flow field is being computed
            Program.KADVMAX = Math.Min(Program.NKK, Program.KADVMAX + Program.VertCellsFF);

            Program.AHMIN = 0;

            //in case of the diagnostic approach, a boundary layer is established near the obstacle's walls
            if (Program.FlowFieldLevel <= 1)
            {
                Parallel.For((1 + Program.IGEB), (Program.NII - Program.IGEB + 1), Program.pOptions, i =>
                {
                    for (int j = 1 + Program.IGEB; j <= Program.NJJ - Program.IGEB; j++)
                    {
                        double entf = 0;
                        double vertk;
                        double abmind;
                        for (int k = 1; k < Program.NKK; k++)
                        {
                            abmind = 1;
                            vertk = Program.HOKART[k - 1] + Program.DZK[k] * 0.5;

                            //search towards west for obstacles
                            for (int ig = i - Program.IGEB; ig < i; ig++)
                            {
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][j]) && (Program.CUTK[ig][j] > 0) && (k > Program.KKART[i][j]))
                                    entf = Math.Abs((i - ig) * Program.DXK);
                            }
                            if (entf <= 20)
                                abmind *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards east for obstacles
                            for (int ig = i + Program.IGEB; ig >= i + 1; ig--)
                            {
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][j]) && (Program.CUTK[ig][j] > 0) && (k > Program.KKART[i][j]))
                                    entf = Math.Abs((i - ig) * Program.DXK);
                            }
                            if (entf <= 20)
                                abmind *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards south for obstacles
                            for (int jg = j - Program.IGEB; jg < j; jg++)
                            {
                                entf = 21;
                                if ((vertk <= Program.AHK[i][jg]) && (Program.CUTK[i][jg] > 0) && (k > Program.KKART[i][j]))
                                    entf = Math.Abs((j - jg) * Program.DYK);
                            }
                            if (entf <= 20)
                                abmind *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards north for obstacles
                            for (int jg = j + Program.IGEB; jg >= j + 1; jg--)
                            {
                                entf = 21;
                                if ((vertk <= Program.AHK[i][jg]) && (Program.CUTK[i][jg] > 0) && (k > Program.KKART[i][j]))
                                    entf = Math.Abs((j - jg) * Program.DYK);
                            }
                            if (entf <= 20)
                                abmind *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards north/east for obstacles
                            for (int ig = i + Program.IGEB; ig >= i + 1; ig--)
                            {
                                int jg = j + ig - i;
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][jg]) && (Program.CUTK[ig][jg] > 0) && (k > Program.KKART[i][j]))
                                    entf = Math.Sqrt(Program.Pow2((i - ig) * Program.DXK) + Program.Pow2((j - jg) * Program.DYK));
                            }
                            if (entf <= 20)
                                abmind *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards south/west for obstacles
                            for (int ig = i - Program.IGEB; ig < i; ig++)
                            {
                                int jg = j + ig - i;
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][jg]) && (Program.CUTK[ig][jg] > 0) && (k > Program.KKART[i][j]))
                                    entf = Math.Sqrt(Program.Pow2((i - ig) * Program.DXK) + Program.Pow2((j - jg) * Program.DYK));
                            }
                            if (entf <= 20)
                                abmind *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards south/east for obstacles
                            for (int ig = i + Program.IGEB; ig >= i + 1; ig--)
                            {
                                int jg = j - ig + i;
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][jg]) && (Program.CUTK[ig][jg] > 0) && (k > Program.KKART[i][j]))
                                    entf = Math.Sqrt(Program.Pow2((i - ig) * Program.DXK) + Program.Pow2((j - jg) * Program.DYK));
                            }
                            if (entf <= 20)
                                abmind *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards north/west for obstacles
                            for (int ig = i - Program.IGEB; ig < i; ig++)
                            {
                                int jg = j - ig + i;
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][jg]) && (Program.CUTK[ig][jg] > 0) && (k > Program.KKART[i][j]))
                                    entf = Math.Sqrt(Program.Pow2((i - ig) * Program.DXK) + Program.Pow2((j - jg) * Program.DYK));
                            }
                            if (entf <= 20)
                                abmind *= 0.19 * Math.Log((entf + 0.5) * 10);

                            Program.UK[i][j][k] *= (float)abmind;
                            Program.VK[i][j][k] *= (float)abmind;
                        }
                    }
                });
            }

            //computing flow field either with diagnostic or prognostic approach
            if (Program.FlowFieldLevel > 0)
            {
                if (Program.FlowFieldLevel == 1)
                {
                    Console.WriteLine("DIAGNOSTIC WIND FIELD AROUND OBSTACLES");
                    DiagnosticFlowfield.Calculate();
                }
                if (Program.FlowFieldLevel == 2)
                {
                    //read vegetation only once
                    if (Program.VEG[0][0][0] < 0)
                    {
                        ProgramReaders Readclass = new ProgramReaders();
                        Readclass.ReadVegetationDomain();
                        Readclass.ReadVegetation();
                    }

                    Console.WriteLine("PROGNOSTIC WIND FIELD AROUND OBSTACLES");
                    PrognosticFlowfield.Calculate();
                }

                //Final mass conservation using poisson equation for pressure after advection has been computed with level 2
                if (Program.FlowFieldLevel == 2)
                    DiagnosticFlowfield.Calculate();
            }

            //in diagnostic mode mass-conservation is finally achieved by adjusting the vertical velocity in each cell
            if (Program.FlowFieldLevel < 2)
            {
                Parallel.For(2, Program.NII, Program.pOptions, i =>
                {
                    for (int j = 2; j < Program.NJJ; j++)
                    {
                        //Pointers (to speed up the computations)
                        float[] UK_L = Program.UK[i][j];
                        float[] UKi_L = Program.UK[i + 1][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] VKj_L = Program.VK[i][j + 1];
                        float[] WK_L = Program.WK[i][j];
                        double fwo1 = 0;
                        double fwo2 = 0;
                        double fsn1 = 0;
                        double fsn2 = 0;
                        double fbt1 = 0;
                        int KKART = Program.KKART[i][j];

                        for (int k = 1; k < Program.NKK; k++)
                        {
                            if (k > KKART)
                            {
                                if (k <= Program.KKART[i - 1][j])
                                    fwo1 = 0;
                                else
                                    fwo1 = Program.DZK[k] * Program.DYK * UK_L[k];

                                if (k <= Program.KKART[i + 1][j])
                                    fwo2 = 0;
                                else
                                    fwo2 = Program.DZK[k] * Program.DYK * UKi_L[k];

                                if (k <= Program.KKART[i][j - 1])
                                    fsn1 = 0;
                                else
                                    fsn1 = Program.DZK[k] * Program.DXK * VK_L[k];

                                if (k <= Program.KKART[i][j + 1])
                                    fsn2 = 0;
                                else
                                    fsn2 = Program.DZK[k] * Program.DXK * VKj_L[k];

                                if ((k <= KKART + 1) || (k == 1))
                                    fbt1 = 0;
                                else
                                    fbt1 = Program.DXK * Program.DYK * WK_L[k];

                                WK_L[k + 1] = (float)((fwo1 - fwo2 + fsn1 - fsn2 + fbt1) / (Program.DXK * Program.DYK));
                            }
                        }
                    }
                });
            }
        }

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
