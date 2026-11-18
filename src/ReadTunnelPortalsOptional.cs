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
using System.IO;

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
        /// <summary>
        ///Reading optional files used to define areas where either the tunnel jet stream is destroyed due to traffic
        ///on the opposite lanes of a highway or where pollutants are sucked into a tunnel portal
        /// </summary>
        public void ReadTunnelfilesOptional()
        {
            if (Program.TS_Count > 0)
            {
                Program.TUN_ENTR = new byte [Program.NII + 2][];
                Program.OPP_LANE = new byte [Program.NII + 2][];
                for (int i = 0; i < Program.NII + 2; ++i)
                {
                    Program.TUN_ENTR[i] = GC.AllocateArray<byte>(Program.NJJ + 2);
                    Program.OPP_LANE[i] = GC.AllocateArray<byte>(Program.NJJ + 2);
                }
                //Program.TUN_ENTR = Program.CreateArray<byte[]>(Program.NII + 2, () => new byte[Program.NJJ + 2]);
                //Program.OPP_LANE = Program.CreateArray<byte[]>(Program.NII + 2, () => new byte[Program.NJJ + 2]);

                if (File.Exists("Opposite_lane.txt") == true)
                {
                    Program.TunnelOppLane = true;
                    Console.WriteLine();
                    Console.WriteLine("Reading file Opposite_lane.txt: ");
                    ReadCellFlagFile("Opposite_lane.txt", Program.OPP_LANE);
                }

                if (File.Exists("tunnel_entrance.txt") == true)
                {
                    Program.TunnelEntr = true;
                    Console.WriteLine();
                    Console.WriteLine("Reading file tunnel_entrance.txt: ");
                    ReadCellFlagFile("tunnel_entrance.txt", Program.TUN_ENTR);
                }
            }
        }//reading optional files used to define areas where either the tunnel jet stream is destroyed due to traffic
         //on the opposite lanes of a highway or where pollutants are sucked into a tunnel portal

        /// <summary>
        /// Mark the flow-field cells listed in a coordinate file (x, y per line).
        /// Blank lines, comment lines ('#' or '//') and other lines that do not start with a number are skipped;
        /// points outside the GRAL domain are ignored.
        /// </summary>
        private static void ReadCellFlagFile(string fileName, byte[][] cells)
        {
            int block = 0;
            try
            {
                using (StreamReader read = new StreamReader(fileName))
                {
                    string text1;
                    while ((text1 = read.ReadLine()) != null)
                    {
                        block++;
                        string line = text1.Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//") ||
                            !(char.IsDigit(line[0]) || line[0] == '-' || line[0] == '+' || line[0] == '.'))
                        {
                            continue;
                        }
                        string[] text = line.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        double ci = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                        double cj = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                        // Floor, so points west or south of the domain are rejected instead of truncated into cell 1.
                        double fx = Math.Floor((ci - Program.XsiMinGral) / Program.DXK) + 1;
                        double fy = Math.Floor((cj - Program.EtaMinGral) / Program.DYK) + 1;
                        if ((fx <= Program.NII) && (fx >= 1) && (fy <= Program.NJJ) && (fy >= 1))
                        {
                            cells[(int)fx][(int)fy] = 1;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                string err = "Error when reading file " + fileName + " in line " + block.ToString() + ": " + exc.Message + " Execution stopped: press ESC to stop";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                {
                    Program.CleanUpMemory();
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                    {
                        ;
                    }
                }

                Environment.Exit(1);
            }
        }
    }
}
