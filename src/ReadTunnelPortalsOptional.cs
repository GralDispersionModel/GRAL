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
                    int block = 0;

                    try
                    {
                        using (StreamReader read = new StreamReader("Opposite_lane.txt"))
                        {
                            string[] text = new string[1];
                            string text1;
                            Int32 IXCUT;
                            Int32 IYCUT;
                            while ((text1 = read.ReadLine()) != null)
                            {
                                block++;
                                text = text1.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                double ci = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                                double cj = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                                IXCUT = (int)((ci - Program.XsiMinGral) / Program.DXK) + 1;
                                IYCUT = (int)((cj - Program.EtaMinGral) / Program.DYK) + 1;
                                if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                                {
                                    Program.OPP_LANE[IXCUT][IYCUT] = 1;
                                }
                            }
                        }
                    }
                    catch
                    {
                        string err = "Error when reading fileOpposite_lane.txt in line " + block.ToString() + " Execution stopped: press ESC to stop";
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

                        Environment.Exit(0);
                    }
                }

                if (File.Exists("tunnel_entrance.txt") == true)
                {
                    Program.TunnelEntr = true;
                    Console.WriteLine();
                    Console.WriteLine("Reading file tunnel_entrance.txt: ");
                    int block = 0;

                    try
                    {
                        using (StreamReader read = new StreamReader("tunnel_entrance.txt"))
                        {
                            string[] text = new string[1];
                            string text1;
                            Int32 IXCUT;
                            Int32 IYCUT;
                            while ((text1 = read.ReadLine()) != null)
                            {
                                block++;
                                text = text1.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                double ci = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                                double cj = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                                IXCUT = (int)((ci - Program.XsiMinGral) / Program.DXK) + 1;
                                IYCUT = (int)((cj - Program.EtaMinGral) / Program.DYK) + 1;
                                if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                                {
                                    Program.TUN_ENTR[IXCUT][IYCUT] = 1;
                                }
                            }
                        }
                    }
                    catch
                    {
                        string err = "Error when reading file tunnel_entrance.txt in line " + block.ToString() + " Execution stopped: press ESC to stop";
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

                        Environment.Exit(0);
                    }
                }
            }
        }//reading optional files used to define areas where either the tunnel jet stream is destroyed due to traffic
         //on the opposite lanes of a highway or where pollutants are sucked into a tunnel portal
    }
}