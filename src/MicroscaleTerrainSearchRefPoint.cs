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
        /// <summary>
        /// Search a reference point within the sub domain mask
        /// </summary>
        /// <param name="subDomainMask">Array defining the prognostic sub domains</param>
        /// <returns></returns>
        public IntPoint SearchReferencePoint(byte[][] subDomainMask)
        {
            int xmax = subDomainMask.Length;
            int ymax = subDomainMask[1].Length;
            int searchmax = Math.Min(xmax, ymax) / 2;

            int delta = 1;
            int i = 1; int j = 1;
            bool found = false;

            // search from border of the domain to the center in a spiral
            while (!found && delta < searchmax)
            {
                i = delta;
                j = delta;
                
                while (j < (ymax - delta) && !found)
                {
                    //Console.Write(i.ToString()+"/"+j.ToString()+":");
                    if (subDomainMask[i][j] == 1)
                    {
                        found = true;
                    }
                    else
                    {
                        j ++;
                    }
                }
                while (i < (xmax - delta) && !found)
                {
                    //Console.Write(i.ToString()+"/"+j.ToString()+":");
                    if (subDomainMask[i][j] == 1)
                    {
                        found = true;
                    }
                    else
                    {
                        i ++;
                    }
                }
                while (j > delta && !found)
                {
                    //Console.Write(i.ToString()+"/"+j.ToString()+":");
                    if (subDomainMask[i][j] == 1)
                    {
                        found = true;
                    }
                    else
                    {
                        j --;
                    }
                }
                while (i > delta && !found)
                {
                    //Console.Write(i.ToString()+"/"+j.ToString()+":");
                    if (subDomainMask[i][j] == 1)
                    {
                        found = true;
                    }
                    else
                    {
                        i --;
                    }
                }
                // take the next ring to search a sub domain
                delta++; 
            }

            //if no prognostic sub domain was found -> set an invalid position for the reference point
            if (!found)
            {
                i = 0;
                j = 0;
            }
            //Console.WriteLine("Reference Point X:" + i.ToString() + "Y:" + j.ToString());
            return new IntPoint(i, j);
        }
    }
}