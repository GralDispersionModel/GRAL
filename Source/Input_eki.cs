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

namespace GRAL_2001
{
    /// <summary>
    ///Read meteorological input data (friction velocity, Obukhov length, standard deviation of horizontal wind fluctuations, u- and v-wind components)
    /// </summary>
    class Input_Eki
    {
        /// <summary>
    	/// Read the meteo input file elimakidata.prn
    	/// </summary> 
        public static void Read()
        {
            try
            {
                StreamReader sr = new StreamReader("elimakidata.prn");
                try
                {
                    string[] text = new string[1];

                    if (Program.IDISP == Program.IWETstart)
                    {
                        //number of observations of the vertical profile                    
                        Program.MetProfileNumb = 3;
                        Program.MeasurementHeight[1] = 7 / 2;
                        Program.MeasurementHeight[2] = 6;
                        Program.MeasurementHeight[3] = 10;

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
                    }

                    Program.Ob[1][1] = Convert.ToSingle(text[0].Replace(".", Program.Decsep));

                    //read wind components along the vertical profile
                    int n1 = 0;
                    for (int k = 1; k <= Program.MetProfileNumb; k++)
                    {
                        n1 = (k - 1) * 2;
                        Program.UX[k] = Convert.ToSingle(text[1 + n1].Replace(".", Program.Decsep));
                        Program.UY[k] = Convert.ToSingle(text[2 + n1].Replace(".", Program.Decsep));
                    }

                    int ianzi = 0;
                    for (int iprof = 1; iprof <= Program.MetProfileNumb;iprof++)
                    {
                        ianzi++;
                        if((Program.UX[ianzi]==0)&&(Program.UY[ianzi]==0))
                        {
                            Program.MeasurementHeight[ianzi] = Program.MeasurementHeight[ianzi + 1];
                            Program.MeasurementHeight[ianzi + 1] = Program.MeasurementHeight[ianzi + 2];
                            Program.MeasurementHeight[ianzi + 2] = Program.MeasurementHeight[ianzi + 3];
                            Program.UX[ianzi] = Program.UX[ianzi + 1];
                            Program.UX[ianzi + 1] = Program.UX[ianzi + 2];
                            Program.UX[ianzi + 2] = Program.UX[ianzi + 3];
                            Program.UY[ianzi] = Program.UY[ianzi + 1];
                            Program.UY[ianzi + 1] = Program.UY[ianzi + 2];
                            Program.UY[ianzi + 2] = Program.UY[ianzi + 3];
                            ianzi--;
                        }
                    }

                    Program.MetProfileNumb = ianzi;

                    //friction velocity
                    Program.WindVelGral = (float)Math.Sqrt(Math.Pow(Program.UX[Program.MetProfileNumb], 2) + Math.Pow(Program.UY[Program.MetProfileNumb], 2));

                    for (int ix = 1; ix <= Program.NX; ix++)
                    {
                        for (int iy = 1; iy <= Program.NY; iy++)
                        {
                            if ((Program.Ob[ix][iy] >= 0) && (Program.Ob[ix][iy] < 100))
                            {
                                double phim = 1 + 5 * Program.MeasurementHeight[Program.MetProfileNumb] / Program.Ob[ix][iy];
                                double psim = -5 * Program.MeasurementHeight[Program.MetProfileNumb] / Program.Ob[ix][iy];
                                Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(Program.MeasurementHeight[Program.MetProfileNumb] / Program.Z0Gramm[ix][iy]) - psim * (Program.MeasurementHeight[Program.MetProfileNumb] / Program.Ob[ix][iy])));
                            }
                            else if (Program.Ob[ix][iy] >= 100)
                            {
                                Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(Program.MeasurementHeight[Program.MetProfileNumb] / Program.Z0Gramm[ix][iy])));
                            }
                            else
                            {
                                double phim = Math.Pow(1 - 16 * Program.MeasurementHeight[Program.MetProfileNumb] / Program.Ob[ix][iy], 0.25);
                                double psim = Math.Log((1 + Math.Pow(phim, -2)) * 0.5 * Math.Pow((1 + Math.Pow(phim, -1)) * 0.5, 2)) - 2 * Math.Atan(Math.Pow(phim, -1)) + 1.57;
                                Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(Program.MeasurementHeight[Program.MetProfileNumb] / Program.Z0Gramm[ix][iy]) - psim * (Program.MeasurementHeight[Program.MetProfileNumb] / Program.Ob[ix][iy])));
                            }

                            Program.Ustern[ix][iy] = (float)Math.Max(Program.Ustern[ix][iy], 0.02);
                        }
                    }

                    Program.IWETstart = Program.IDISP + 1;
                }
                catch
                {
                    string err = "Error when reading file elimakidata.prn in line " + (Program.IWETstart).ToString() + " Execution stopped: press ESC to stop";
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
            	string err = "Error when reading file elimakidata.prn. -> Execution stopped: press ESC to stop";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);
                
                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                
                Environment.Exit(0);
            }
        }
    }
}
