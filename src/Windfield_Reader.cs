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

/*
 * Created by SharpDevelop.
 * User: Markus Kuntner
 * Date: 11.08.2015
 * Time: 08:59
 */

using System;
using System.Globalization;
using System.IO;

namespace GRAL_2001
{
    /// <summary>
    ///Read GRAMM Wind Fields
    /// </summary>
    public class WindfieldReader
    {
        /// <summary>
        ///Read GRAMM Wind Fields
        /// </summary>
        public bool WindfieldRead(string filename, int NX, int NY, int NZ, ref Single[][][] UWI, ref Single[][][] VWI, ref Single[][][] WWI)
        {
            try
            {
                int dummy = 0;
                using (BinaryReader windfieldb = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    dummy = windfieldb.ReadInt32(); // read 4 bytes from stream = "Header"
                }

                if (dummy == -1) // Compact wnd File-format
                {
                    using (FileStream str_windfield = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (BufferedStream buf_windfield = new BufferedStream(str_windfield, 32768))
                        {
                            using (BinaryReader windfieldb = new BinaryReader(buf_windfield))
                            {
                                dummy = windfieldb.ReadInt32(); // read 4 bytes from stream = "Header"
                                dummy = windfieldb.ReadInt32(); // read 4 bytes from stream = Nx
                                dummy = windfieldb.ReadInt32(); // read 4 bytes from stream = Ny
                                dummy = windfieldb.ReadInt32(); // read 4 bytes from stream = Nz
                                float temp = windfieldb.ReadInt32(); // read 4 bytes from stream = DXX
                                for (int i = 1; i <= NX; i++)
                                {
                                    for (int j = 1; j <= NY; j++)
                                    {
                                        for (int k = 1; k <= NZ; k++)
                                        {
                                            UWI[i][j][k] = (float)windfieldb.ReadInt16() / 100; // 2 Bytes  = word integer value;
                                            VWI[i][j][k] = (float)windfieldb.ReadInt16() / 100;
                                            WWI[i][j][k] = (float)windfieldb.ReadInt16() / 100;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else // classic windfield file format
                {
                    string decsep = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;
                    string[] text = new string[1];
                    using (StreamReader windfield = new StreamReader(filename))
                    {
                        for (int i = 1; i <= NX; i++)
                        {
                            for (int j = 1; j <= NY; j++)
                            {
                                for (int k = 1; k <= NZ; k++)
                                {
                                    text = windfield.ReadLine().Split(new char[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                    UWI[i][j][k] = (float)Convert.ToDouble(text[0].Replace(".", decsep));
                                    VWI[i][j][k] = (float)Convert.ToDouble(text[1].Replace(".", decsep));
                                    WWI[i][j][k] = (float)Convert.ToDouble(text[2].Replace(".", decsep));
                                }
                            }
                        }
                    }
                }
                return true; // Reader OK
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                return false; // Reader Error
            }
        }
    }
}
