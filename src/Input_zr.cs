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
    /// <summary>
    ///Read meteorological input data (friction velocity, Obukhov length, boundary-layer height, standard deviation of horizontal wind fluctuations, u- and v-wind components)
    /// </summary>
    class Input_zr
    {
        public static void Read()
        {
            try
            {
                StreamReader sr = new StreamReader("inputzr.dat");
                try
                {
                    string[] text = new string[1];
                    text = sr.ReadLine().Split(new char[] { ' ', ',', '\t', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);

                    //number of observations of the vertical profile
                    Program.MetProfileNumb = Convert.ToInt16(text[0].Replace(".", Program.Decsep));

                    //height of these observations above ground in m
                    text = sr.ReadLine().Split(new char[] { ' ', ',', '\t', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine();
                    Console.Write("Observational heights: ");
                    for (int n = 1; n <= Program.MetProfileNumb; n++)
                    {
                        Program.MeasurementHeight[n] = Convert.ToSingle(text[n - 1].Replace(".", Program.Decsep));
                        Console.Write(Program.MeasurementHeight[n].ToString("0.00") + "m  ");
                    }
                    Console.WriteLine();

                    for (int n = 1; n <= Program.IWETstart; n++)
                    {
                        string text1 = sr.ReadLine();
                        if (text1 == null)
                        {
                            Program.IEND = Consts.CalculationFinished;
                            return;
                        }
                        text = text1.Split(new char[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    Program.Ustern[1][1] = Convert.ToSingle(text[1].Replace(".", Program.Decsep));
                    Program.Ob[1][1] = Convert.ToSingle(text[2].Replace(".", Program.Decsep));
                    if (Program.Ob[1][1] == 0)
                    {
                        Program.Ob[1][1] = 1.0F;
                    }

                    Program.BdLayHeight = Convert.ToSingle(text[3].Replace(".", Program.Decsep));

                    //read additional data for tunnel portals if existent
                    int n1 = 0;
                    for (int k = 1; k <= Program.MetProfileNumb; k++)
                    {
                        n1 = (k - 1) * 3;
                        Program.U0[k] = Convert.ToSingle(text[4 + n1].Replace(".", Program.Decsep));
                        Program.ObsWindU[k] = Convert.ToSingle(text[5 + n1].Replace(".", Program.Decsep));
                        Program.ObsWindV[k] = Convert.ToSingle(text[6 + n1].Replace(".", Program.Decsep));

                        //just for wind-speed output
                        if (k == 1)
                        {
                            Program.WindVelGral = (float)Math.Sqrt(Math.Pow(Program.ObsWindU[k], 2) + Math.Pow(Program.ObsWindV[k], 2));
                        }
                    }

                    //read additional data for tunnel portals if existent //Removed Kuntner 1.4.2019 - Read these values in ReadTunnelportals
                    // for (int k = 1; k <= Program.TS_Count; k++)
                    // {
                    //     int s = n1 + (k - 1) * 2;
                    //     Program.TS_V[k] = Convert.ToSingle(text[7 + s].Replace(".", Program.decsep));
                    //     Program.TS_T[k] = Convert.ToSingle(text[8 + s].Replace(".", Program.decsep));
                    // }

                    //friction velocity and boundary-layer height
                    for (int ix = 1; ix <= Program.NX; ix++)
                    {
                        for (int iy = 1; iy <= Program.NY; iy++)
                        {
                            Program.Ustern[ix][iy] = Program.Ustern[1][1];
                            Program.Ob[ix][iy] = Program.Ob[1][1];
                            if (Program.BdLayHeight <= 0)
                            {
                                if (Program.Ob[ix][iy] >= 0)
                                {
                                    Program.BdLayHeight = (float)Math.Min(0.4F * Math.Sqrt(Program.Ustern[1][1] * Program.Ob[1][1] / Program.CorolisParam), 1000);
                                }
                                else
                                {
                                    Program.BdLayHeight = (float)(Math.Min(0.4 * Math.Sqrt(Program.Ustern[1][1] * 1000 / Program.CorolisParam), 800) + 300 * Math.Pow(2.72, Program.Ob[1][1] * 0.01F));
                                }
                            }
                        }
                    }

                    Program.BdLayFlat = Program.BdLayHeight;
                    for (int k = 1; k <= Program.MetProfileNumb; k++)
                    {
                        Program.V0[k] = Program.U0[k];
                    }

                    Program.IWETstart = Program.IDISP + 1;
                }
                catch
                {
                    Console.WriteLine("Error when reading file inputzr.dat in line " + (Program.IWETstart + 2).ToString() + " Execution stopped: press ESC to stop");

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    {
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                        {
                            ;
                        }
                    }
                }
                sr.Close();
                sr.Dispose();
            }
            catch
            {
                string err = "Error when reading file inputzr.dat. -> Execution stopped: press ESC to stop";
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
}
