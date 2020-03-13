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
    ///Read meteorological input data (velocity, direction, friction velocity, standard deviation of horizontal and vertical wind fluctuations, Obukhov length, meandering parameters)
    /// </summary>
    class Input_Sonic
    {
        public static void Read()
        {
            try
            {
                StreamReader sr = new StreamReader("sonic.dat");
                try
                {
                    string[] text = new string[1];
                    text = sr.ReadLine().Split(new char[] { ' ', ',', '\t', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);

                    //number of observations of the vertical profile
                    Program.MetProfileNumb = 1;
                    //height of observation above ground in m                    
                    Program.MeasurementHeight[1] = Convert.ToSingle(text[0].Replace(".", Program.Decsep));
                    Console.WriteLine();
                    Console.Write("Observational heights: " + Program.MeasurementHeight[1].ToString("0.00"));
                    Console.WriteLine();

                    for (int n = 1; n <= Program.IWETstart; n++)
                    {
                        string text1 = sr.ReadLine();
                        if (text1 == null)
                        {
                            Program.IEND = 1;
                            return;
                        }
                        text = text1.Split(new char[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    Program.WindVelGral = Convert.ToSingle(text[0].Replace(".", Program.Decsep));
                    Program.WindDirGral = Convert.ToSingle(text[1].Replace(".", Program.Decsep));
                    Program.Ustern[1][1] = Convert.ToSingle(text[2].Replace(".", Program.Decsep));
                    Program.U0[1] = Convert.ToSingle(text[3].Replace(".", Program.Decsep));
                    Program.V0[1] = Convert.ToSingle(text[4].Replace(".", Program.Decsep));
                    Program.W0int = Convert.ToSingle(text[5].Replace(".", Program.Decsep));
                    Program.Ob[1][1] = Convert.ToSingle(text[6].Replace(".", Program.Decsep));
                    if (Program.Ob[1][1] == 0) Program.Ob[1][1] = 1.0F;
                    Program.MWindMEander = Convert.ToSingle(text[8].Replace(".", Program.Decsep));
                    Program.TWindMeander = Convert.ToSingle(text[9].Replace(".", Program.Decsep));

                    //read additional data for tunnel portals if existent //Removed Kuntner 1.4.2019 - Read these values in ReadTunnelportals
                    // for (int k = 1; k <= Program.TS_Count; k++)
                    // {
                    //     int s = (k - 1) * 2;
                    //     Program.TS_V[k] = Convert.ToSingle(text[10 + s].Replace(".", Program.decsep));
                    //     Program.TS_T[k] = Convert.ToSingle(text[11 + s].Replace(".", Program.decsep));
                    // }

                    Program.WindDirGral = (float)((270 - Program.WindDirGral) * Math.PI / 180);
                    Program.UX[1] = (float)(Program.WindVelGral * Math.Cos(Program.WindDirGral));
                    Program.UY[1] = (float)(Program.WindVelGral * Math.Sin(Program.WindDirGral));

                    //friction velocity and boundary-layer height
                    for (int ix = 1; ix <= Program.NX; ix++)
                    {
                        for (int iy = 1; iy <= Program.NY; iy++)
                        {
                            Program.Ustern[ix][iy] = Program.Ustern[1][1];
                            Program.Ob[ix][iy] = Program.Ob[1][1];
                        }
                    }

                    Program.IWETstart = Program.IDISP + 1;
                }
                catch
                {
                    string err = "Error when reading file sonic.dat in line " + (Program.IWETstart + 1).ToString() + " Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                    Environment.Exit(0);
                }
                sr.Close();
                sr.Dispose();
            }
            catch
            {
                string err = "Error when reading file sonic.dat. -> Execution stopped: press ESC to stop";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                Environment.Exit(0);
            }
        }
    }
}
