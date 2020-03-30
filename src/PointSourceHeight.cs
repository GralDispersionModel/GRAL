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

namespace GRAL_2001
{
    class PointSourceHeight
    {
        /// <summary>
    	/// Precalculate values for the Hurley Algorithm
    	/// </summary>
        public static void CalculatePointSourceHeight()
        {
            for (int i = 1; i <= Program.PS_Count; i++)
            {
                double xsi = Program.PS_X[i] - Program.IKOOAGRAL;
                double eta = Program.PS_Y[i] - Program.JKOOAGRAL;
                double xsi1 = Program.PS_X[i] - Program.IKOOA;
                double eta1 = Program.PS_Y[i] - Program.JKOOA;
                if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                {
                    Console.WriteLine("Point source " + i.ToString() + " out of GRAL domain.");
                }

                int IndexI = 1;
                int IndexJ = 1;
                int IndexId = 1;
                int IndexJd = 1;
                if (Program.Topo == 1)
                {
                    IndexI = (int)(xsi / Program.DXK) + 1;
                    IndexJ = (int)(eta / Program.DYK) + 1;
                }
                else
                {
                    IndexI = (int)(xsi1 / Program.DDX[1]) + 1;
                    IndexJ = (int)(eta1 / Program.DDY[1]) + 1;
                }
                IndexId = (int)(xsi / Program.DXK) + 1;
                IndexJd = (int)(eta / Program.DYK) + 1;

                float AHint = 0;
                if (Program.Topo == 1)
                {
                    AHint = Program.AHK[IndexI][IndexJ];
                }

                // input = absolute source-height -> compute relative source height
                if (Program.PS_Absolute_Height[i])
                {
                    Program.PS_Z[i] -= AHint;
                    if (Program.PS_Z[i] < 0) // if relative height < 0
                    {
                        Program.PS_Z[i] = 0.1F;
                    }

                    Program.PS_Absolute_Height[i] = false; // correction just one times!
                }

                //source height
                Program.PS_effqu[i] = Program.PS_Z[i] + AHint;

                //check if point source is within buildings
                if ((Program.BuildingTerrExist == true) || (Program.BuildingFlatExist == true))
                {
                    while (Program.PS_Z[i] <= Program.CUTK[IndexId][IndexJd])
                    {
                        Program.PS_Z[i] += 0.1f;
                        Program.PS_effqu[i] = Program.PS_Z[i] + AHint;
                    }
                }

                //stability parameter for plume rise calculation according to Hurley, 2005
                if ((Program.Ob[1][1] >= 0) && (Program.Ob[1][1] < 100))
                {
                    Program.PlumeStab = 0.004F;
                }
                else if ((Program.Ob[1][1] < 0) && (Program.Ob[1][1] > -100))
                {
                    Program.PlumeStab = 0.0001F;
                }
                else
                {
                    Program.PlumeStab = 0.00015F;
                }
            }
        }
    }
}
