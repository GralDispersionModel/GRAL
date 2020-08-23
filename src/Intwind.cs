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

namespace GRAL_2001
{
    partial class Zeitschleife
    {
        /// <summary>
        /// Interpolation of the microscale wind field in horizontal and vertical direction on the staggered grid 
        /// </summary>
        /// <param name="IndexI">X pos. of particle in the GRAL flow field grid</param>
        /// <param name="IndexJ">Y pos. of particle in the GRAL flow field grid</param>
        /// <param name="AHint">Height of the surface of the GRAL grid</param>
        /// <param name="IndexK">Vertical index of the particle within the flow field grid</param>
        /// <param name="xcoord">Particle x position</param>
        /// <param name="ycoord">Particle y position</param>
        /// <param name="zcoord">Particle z position</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static (float, float, float, int) IntWindCalculate(int IndexI, int IndexJ, float AHint, double xcoord, double ycoord, float zcoord)
        {
            float DXK = Program.DXK;
            float DYK = Program.DYK;
            float UXint = 0;
            float UYint = 0;
            float UZint = 0;
            int IndexK = 0;

            if (Program.Topo == 1)
            {
                float f1 = zcoord - Program.AHMIN;
                IndexK = BinarySearch(f1); //19.05.25 Ku

                float u1 = Program.UK[IndexI][IndexJ][IndexK];
                float u2 = Program.UK[IndexI + 1][IndexJ][IndexK];
                UXint = (float)(u1 + (u2 - u1) / DXK * (xcoord - (Program.IKOOAGRAL + (IndexI - 1) * DXK)));

                float v1 = Program.VK[IndexI][IndexJ][IndexK];
                float v2 = Program.VK[IndexI][IndexJ + 1][IndexK];
                UYint = (float)(v1 + (v2 - v1) / DYK * (ycoord - (Program.JKOOAGRAL + (IndexJ - 1) * DYK)));

                float[] WK_L = Program.WK[IndexI][IndexJ]; //19.05.25 Ku: use a reference -> performance
                float w1 = WK_L[IndexK];
                float w2 = WK_L[IndexK + 1];
                UZint = w1 + (w2 - w1) / Program.DZK[IndexK] * (f1 - Program.HOKART[IndexK - 1]); //19.05.25 Ku: use f1 instead of zcoord - (Program.AHMIN + Program.HOKART[IndexK - 1])
                //mean wind-speed component gradients are not computed as there is no significant change in computed concentrations, even though the well-mixed criterion (Anfossi et al., 2009) is not met
            }
            else if (Program.Topo == 0)
            {
                int inumm = Program.MetProfileNumb;
                int NKK = Program.NKK;

                //no obstacles
                if (Program.BuildingsExist == false)
                {
                    //interpolation between observations
                    float exponent;
                    float dumfac;
                    if (zcoord <= Program.MeasurementHeight[1])
                    {
                        if (Program.Ob[1][1] <= 0)
                        {
                            exponent = MathF.Max(0.35F - 0.4F * MathF.Pow(Math.Abs(Program.Ob[1][1]), -0.15F), 0.05F);
                        }
                        else
                        {
                            exponent = 0.56F * MathF.Pow(Program.Ob[1][1], -0.15F);
                        }
                        dumfac = MathF.Pow(zcoord / Program.MeasurementHeight[1], exponent);
                        UXint = Program.ObsWindU[1] * dumfac;
                        UYint = Program.ObsWindV[1] * dumfac;
                    }
                    else if (zcoord >= Program.MeasurementHeight[inumm])
                    {
                        if (inumm == 1)
                        {
                            if (Program.Ob[1][1] <= 0)
                            {
                                exponent = MathF.Max(0.35F - 0.4F * MathF.Pow(Math.Abs(Program.Ob[1][1]), -0.15F), 0.05F);
                            }
                            else
                            {
                                exponent = 0.56F * MathF.Pow(Program.Ob[1][1], -0.15F);
                            }

                            dumfac = MathF.Pow(zcoord / Program.MeasurementHeight[inumm], exponent);
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
                            if (zcoord > Program.MeasurementHeight[iprof])
                            {
                                ipo = iprof + 1;
                            }
                        }
                        float help = 1 / (Program.MeasurementHeight[ipo] - Program.MeasurementHeight[ipo - 1]) * (zcoord - Program.MeasurementHeight[ipo - 1]);
                        UXint = Program.ObsWindU[ipo - 1] + (Program.ObsWindU[ipo] - Program.ObsWindU[ipo - 1]) * help;
                        UYint = Program.ObsWindV[ipo - 1] + (Program.ObsWindV[ipo] - Program.ObsWindV[ipo - 1]) * help;
                    }
                }
                //with obstacles
                else if (Program.BuildingsExist == true)
                {
                    for (IndexK = 1; IndexK <= NKK; IndexK++)
                    {
                        if (zcoord <= Program.HOKART[IndexK])
                        {
                            break;
                        }
                    }
                    if (IndexK > NKK)
                    {
                        IndexK = NKK;
                    }

                    float u1 = Program.UK[IndexI][IndexJ][IndexK];
                    float u2 = Program.UK[IndexI + 1][IndexJ][IndexK];
                    UXint = (float)(u1 + (u2 - u1) / DXK * (xcoord - (Program.IKOOAGRAL + (IndexI - 1) * DXK)));

                    float v1 = Program.VK[IndexI][IndexJ][IndexK];
                    float v2 = Program.VK[IndexI][IndexJ + 1][IndexK];
                    UYint = (float)(v1 + (v2 - v1) / DYK * (ycoord - (Program.JKOOAGRAL + (IndexJ - 1) * DYK)));

                    float[] WK_L = Program.WK[IndexI][IndexJ]; //19.05.25 Ku: use a reference -> performance
                    float w1 = WK_L[IndexK];
                    float w2 = WK_L[IndexK + 1];
                    UZint = w1 + (w2 - w1) / Program.DZK[IndexK] * (zcoord + AHint - Program.HOKART[IndexK - 1]);
                    //mean wind-speed component gradients are not computed as there is no significant change in computed concentrations, even though the well-mixed criterion (Anfossi et al., 2009) is not met
                }
            }
            return (UXint, UYint, UZint, IndexK);
        }


        /// <summary>
        /// Find the index of a value in HOKART[] that exceeds the value Height - 19.05.25 Ku
        /// </summary>
        /// <param name="Height">Height to compare with HOKART[]</param> 
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static int BinarySearch(float Height)
        {
            int nkk = Program.NKK;
            int low = 1;
            int high = nkk;
            int aQuarter = high >> 3;
            float[] hokart = Program.HOKART;

            if (hokart[aQuarter] >= Height)
            {
                high = aQuarter;
            }
            else
            {
                low = aQuarter;
            }

            int mid = 0;
            while (low <= high)
            {
                mid = (low + high) >> 1;
                if (hokart[mid] >= Height)
                {
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            if (low > nkk)
            {
                low = nkk;
            }
            return low;
        }
    }
}
