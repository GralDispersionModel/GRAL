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
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GRAL_2001
{
    ///<summary>
    ///Search a reference point within the sub domain mask
    ///</summary>
    public class MicroscaleTerrainSearchRefPoint
    {
        public IntPoint SearchReferencePoint(byte[][] subDomainMask)
        {
            int xmax = subDomainMask.Length;
            int ymax = subDomainMask[1].Length;

            int delta = 1;
            int i = 1; int j = 1;
            bool found = false;

            // search from border of the domain to the center in a spiral
            while (!found && delta < xmax / 2)
            {
                i = delta;
                j = delta;
                while (j < (ymax - delta) && !found)
                {
                    if (subDomainMask[i][j] == 1)
                    {
                        found = true;
                    }
                    else
                    {
                        j += delta;
                    }
                }
                while (i < (xmax - delta) && !found)
                {
                    if (subDomainMask[i][j] == 1)
                    {
                        found = true;
                    }
                    else
                    {
                        i += delta;
                    }
                }
                while (j > delta && !found)
                {
                    if (subDomainMask[i][j] == 1)
                    {
                        found = true;
                    }
                    else
                    {
                        j -= delta;
                    }
                }
                while (i > delta && !found)
                {
                    if (subDomainMask[i][j] == 1)
                    {
                        found = true;
                    }
                    else
                    {
                        i -= delta;
                    }
                }
                delta++;
            }
            //Console.WriteLine("Reference Point X:" + i.ToString() + "Y:" + j.ToString());
            return new IntPoint(i, j);
        }
    }
}