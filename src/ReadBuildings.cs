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
        ///Read the buildings in case of terrain
        /// </summary>
        public void ReadBuildingsTerrain(float[][] BuildingHeights)
        {
            if (File.Exists("buildings.dat") == true)
            {
                Program.BuildingsExist = true;
                int block = 0;
                Console.WriteLine();
                Console.WriteLine("Reading building file buildings.dat");

                try
                {
                    using (StreamReader read = new StreamReader("buildings.dat"))
                    {
                        string[] text = new string[1];
                        string text1;
                        Int32 IXCUT;
                        Int32 IYCUT;
                        while ((text1 = read.ReadLine()) != null)
                        {
                            text = text1.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            double cic = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                            double cjc = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                            double cz = Convert.ToDouble(text[2].Replace(".", Program.Decsep));
                            double czo = Convert.ToDouble(text[3].Replace(".", Program.Decsep));
                            IXCUT = (int)((cic - Program.XsiMinGral) / Program.DXK) + 1;
                            IYCUT = (int)((cjc - Program.EtaMinGral) / Program.DYK) + 1;
                            if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                            {
                                if (czo < 0) // absolute height of building
                                {
                                    if (Program.GralTopofile)
                                    {
                                        if (Math.Abs(czo) > Program.AHKOri[IXCUT][IYCUT]) // Building height > topography?
                                        {
                                            czo = Math.Abs(czo) - Program.AHKOri[IXCUT][IYCUT]; // compute relative building height for this cell
                                            czo = Math.Max(0, czo); // if czo < AHK -> set building height to 0
                                            block++;
                                        }
                                        else
                                        {
                                            czo = 0; // building height = 0
                                        }
                                    }
                                    else
                                    {
                                        string err = "You are using absolute building coordinates but no original GRAL terrain  - ESC = Exit";
                                        Console.WriteLine(err);
                                        ProgramWriters.LogfileProblemreportWrite(err);

                                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                                        {
                                            ;
                                        }

                                        Environment.Exit(0);
                                    }
                                }
                                else
                                {
                                    block++;
                                }
                                BuildingHeights[IXCUT][IYCUT] = (float)Math.Max(czo, BuildingHeights[IXCUT][IYCUT]);
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when reading file building.dat in line " + block.ToString() + " Execution stopped: press ESC to stop";
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

                string Info = "Total number of blocked cells in 2D: " + block.ToString();
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
                ProgramWriters.LogfileGralCoreWrite(" ");

                CalculateSubDomainsForPrognosticWindSimulation();
            }
        } //read buildings in case of terrain

        /// <summary>
        ///Read the buildings in case of flat terrain
        /// </summary>
        public void ReadBuildingsFlat(float[][] BuildingHeights)
        {
            string Info;
            if (File.Exists("buildings.dat") == true)
            {
                Program.BuildingsExist = true;
                int block = 0;
                Console.WriteLine();
                Console.WriteLine("Reading building file buildings.dat");

                try
                {
                    using (StreamReader read = new StreamReader("buildings.dat"))
                    {
                        string[] text = new string[1];
                        string text1;
                        Int32 IXCUT;
                        Int32 IYCUT;
                        while ((text1 = read.ReadLine()) != null)
                        {
                            block++;
                            text = text1.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            double cic = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                            double cjc = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                            double cz = Convert.ToDouble(text[2].Replace(".", Program.Decsep));
                            double czo = Convert.ToDouble(text[3].Replace(".", Program.Decsep));
                            IXCUT = (int)((cic - Program.XsiMinGral) / Program.DXK) + 1;
                            IYCUT = (int)((cjc - Program.EtaMinGral) / Program.DYK) + 1;

                            if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                            {
                                if (czo < 0) // absolute height of building
                                {
                                    string err = "You are using absolute building coordinates but flat terrain  - ESC = Exit";
                                    Console.WriteLine(err);
                                    ProgramWriters.LogfileProblemreportWrite(err);

                                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                                    {
                                        ;
                                    }

                                    Environment.Exit(0);
                                }
                                BuildingHeights[IXCUT][IYCUT] = (float)Math.Max(czo, BuildingHeights[IXCUT][IYCUT]);
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when reading file building.dat in line " + block.ToString() + " Execution stopped: press ESC to stop";
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

                Info = "Total number of blocked cells in 2D: " + block.ToString();
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
                ProgramWriters.LogfileGralCoreWrite(" ");

                CalculateSubDomainsForPrognosticWindSimulation();
            }
        } //read buildings in case of flat terrain
    }
}