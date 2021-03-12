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
 * Date: 15.01.2018
 * Time: 13:58
*/

using System;
using System.IO;

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
        /// <summary>
        /// Read the landuse.asc file
        /// </summary>
        public void ReadLanduseFile()
        {
            if (File.Exists("landuse.asc") == true)
            {
                Program.LandUseAvailable = true;
            }

            if ((Program.Topo == Consts.TerrainAvailable) && (Program.LandUseAvailable == true))
            {
                //Read surface roughness length from the GRAMM landuse file
                string[] text = new string[(Program.NI + 2) * (Program.NJ + 2)];

                try
                {
                    using (StreamReader r = new StreamReader("landuse.asc"))
                    {
                        text = Convert.ToString(r.ReadLine()).Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        text = Convert.ToString(r.ReadLine()).Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        text = Convert.ToString(r.ReadLine()).Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        int n = 0;
                        for (int j = 1; j < Program.NJ + 1; j++)
                        {
                            for (int i = 1; i < Program.NI + 1; i++)
                            {
                                Program.Z0Gramm[i][j] = Convert.ToSingle(text[n].Replace(".", Program.Decsep));
                                n++;
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when reading file 'landuse.asc' - Execution stopped";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    {
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                        {
                            ;
                        }
                    }

                    Environment.Exit(0);
                }
            }
            else
            {
                //set default surface roughness length at the whole array
                for (int ix = 1; ix <= Program.NX; ix++)
                {
                    for (int iy = 1; iy <= Program.NY; iy++)
                    {
                        Program.Z0Gramm[ix][iy] = Program.Z0;
                    }
                }
            }
        }//read landuse
    }
}