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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Globalization;

namespace GRAL_2001
{
    class WindfieldReaderSoundplan
    {
        // methode to read windfield-data 
        public bool WindfieldRead(string filename, int NX, int NY, int NZ, ref Single[][][] UWI, ref Single[][][] VWI, ref Single[][][] WWI)
        {
            try
            {
                string decsep = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;

                using (BinaryReader windfieldb = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    for (int i = 1; i <= NX; i++)
                        for (int j = 1; j <= NY; j++)
                            for (int k = 1; k <= NZ; k++)
                            {
                                UWI[i][j][k] = windfieldb.ReadSingle();
                                VWI[i][j][k] = windfieldb.ReadSingle();
                                WWI[i][j][k] = windfieldb.ReadSingle();
                            }
                }
                
                return true; // Reader OK
            }
            catch
            {
                return false; // Reader Error
            }
        }
    }
}
