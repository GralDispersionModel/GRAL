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
    ///Read classified meteorological input data (wind speed, wind direction, stability class)
    /// </summary> 
    class Input_MeteopgtAll
    {
        /// <summary>
    	/// Read the current line of the variable Program.IWETstart from the classified meteo data file meteopgt.all
    	/// </summary> 
        public static int Read()
        {
            try
            {
                using (StreamReader sr = new StreamReader("meteopgt.all"))
                {
                    string[] text = new string[1];
                    text = sr.ReadLine().Split(new char[] { ' ', ',', '\t', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                    float ahoehe = Convert.ToSingle(text[0].Replace(".", Program.Decsep));
                    string TIMESERIES = text[1].Trim();
                    double SECTORWIDTH = Convert.ToDouble(text[2].Replace(".", Program.Decsep));
                    double akla_sum = 0; int akla_count = 0;

                    text = sr.ReadLine().Split(new char[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int n = 1; n <= Program.IWETstart; n++)
                    {
                        if (sr.EndOfStream == true) // at the end of the stream -> computation finished
                        {
                            return 1;
                        }

                        string text1 = sr.ReadLine();

                        text = text1.Split(new char[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    Program.MetProfileNumb = 1;
                    Program.MeasurementHeight[1] = ahoehe;
                    Program.WindDirGral = Convert.ToSingle(text[0].Replace(".", Program.Decsep));
                    Program.WindVelGral = Convert.ToSingle(text[1].Replace(".", Program.Decsep));
                    Program.StabClass = Convert.ToInt16(text[2]);

                    //random wind direction taken from the defined sector width
                    if ((TIMESERIES == "0") && (Program.Topo != 1))
                    {
                        Random rnd = new Random();
                        Program.WindDirGral = Program.WindDirGral - (float)(SECTORWIDTH / 20) + (float)(rnd.NextDouble() * SECTORWIDTH / 10);
                    }

                    //horizontal wind components
                    Program.WindDirGral *= 10;
                    Program.WindDirGral = (float)((270 - Program.WindDirGral) * Math.PI / 180);
                    Program.UX[1] = (float)(Program.WindVelGral * Math.Cos(Program.WindDirGral));
                    Program.UY[1] = (float)(Program.WindVelGral * Math.Sin(Program.WindDirGral));

                    //Obukhov length and friction velocity
                    for (int ix = 1; ix <= Program.NX; ix++)
                    {
                        for (int iy = 1; iy <= Program.NY; iy++)
                        {
                            Program.Z0Gramm[ix][iy] = (float)Math.Max(Program.Z0Gramm[ix][iy], 0.00001);

                            if (Program.Topo == 1 && Program.AKL_GRAMM[0, 0] != 0)
                            {
                                Program.StabClass = (int)Program.AKL_GRAMM[ix - 1, iy - 1];
                            }

                            // sum up the SC values inside the GRAL domain area
                            if (Program.IKOOA + Program.DDX[1] * ix > Program.IKOOAGRAL &&
                            Program.IKOOA + Program.DDX[1] * ix < Program.IKOOAGRAL + Program.GralDx * Program.NXL &&
                            Program.JKOOA + Program.DDY[1] * iy > Program.JKOOAGRAL &&
                            Program.JKOOA + Program.DDY[1] * iy < Program.JKOOAGRAL + Program.GralDy * Program.NYL)
                            {
                                akla_sum += Program.StabClass;
                                akla_count++;
                            }

                            Program.SC_Gral[ix][iy] = (byte)Math.Max(0, Math.Min(7, Program.StabClass)); // 11.9.2017 Kuntner remember the Stability class for the receptor concenterations

                            if (Program.StabClass == 1)
                            {
                                Program.Ob[ix][iy] = (float)Math.Min(1 / (-0.37 * Math.Pow(Program.Z0Gramm[ix][iy] * 100, -0.55)), -4);
                                double phim = Math.Pow(1 - 16 * ahoehe / Program.Ob[ix][iy], 0.25);
                                double psim = Math.Log((1 + Math.Pow(phim, -2)) * 0.5 * Math.Pow((1 + Math.Pow(phim, -1)) * 0.5, 2)) - 2 * Math.Atan(Math.Pow(phim, -1)) + 1.57;
                                if (Program.Topo != 1)
                                    Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(ahoehe / Program.Z0Gramm[ix][iy]) - psim * (ahoehe / Program.Ob[ix][iy])));
                                else
                                {
                                    int izz = 1;
                                    for (int iz = 1; iz <= Program.NK; iz++)
                                        if ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) < 5)
                                            izz = iz + 1;
                                    double windig = Math.Sqrt(Math.Pow(Program.UWIN[ix][iy][izz], 2) + Math.Pow(Program.VWIN[ix][iy][izz], 2));
                                    Program.Ustern[ix][iy] = (float)((windig + 0.15) * 0.4 / (Math.Log((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Z0Gramm[ix][iy]) -
                                        psim * ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Ob[ix][iy])));
                                }
                            }
                            if (Program.StabClass == 2)
                            {
                                Program.Ob[ix][iy] = (float)Math.Min(1 / (-0.12 * Math.Pow(Program.Z0Gramm[ix][iy] * 100, -0.50)), -4);
                                double phim = Math.Pow(1 - 16 * ahoehe / Program.Ob[ix][iy], 0.25);
                                double psim = Math.Log((1 + Math.Pow(phim, -2)) * 0.5 * Math.Pow((1 + Math.Pow(phim, -1)) * 0.5, 2)) - 2 * Math.Atan(Math.Pow(phim, -1)) + 1.57;
                                if (Program.Topo != 1)
                                    Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(ahoehe / Program.Z0Gramm[ix][iy]) - psim * (ahoehe / Program.Ob[ix][iy])));
                                else
                                {
                                    int izz = 1;
                                    for (int iz = 1; iz <= Program.NK; iz++)
                                        if ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) < 5)
                                            izz = iz + 1;
                                    double windig = Math.Sqrt(Math.Pow(Program.UWIN[ix][iy][izz], 2) + Math.Pow(Program.VWIN[ix][iy][izz], 2));
                                    Program.Ustern[ix][iy] = (float)((windig + 0.15) * 0.4 / (Math.Log((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Z0Gramm[ix][iy]) -
                                        psim * ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Ob[ix][iy])));
                                }
                            }
                            if (Program.StabClass == 3)
                            {
                                Program.Ob[ix][iy] = (float)Math.Min(1 / (-0.067 * Math.Pow(Program.Z0Gramm[ix][iy] * 100, -0.56)), -4);
                                double phim = Math.Pow(1 - 16 * ahoehe / Program.Ob[ix][iy], 0.25);
                                double psim = Math.Log((1 + Math.Pow(phim, -2)) * 0.5 * Math.Pow((1 + Math.Pow(phim, -1)) * 0.5, 2)) - 2 * Math.Atan(Math.Pow(phim, -1)) + 1.57;
                                if (Program.Topo != 1)
                                    Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(ahoehe / Program.Z0Gramm[ix][iy]) - psim * (ahoehe / Program.Ob[ix][iy])));
                                else
                                {
                                    int izz = 1;
                                    for (int iz = 1; iz <= Program.NK; iz++)
                                        if ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) < 5)
                                            izz = iz + 1;
                                    double windig = Math.Sqrt(Math.Pow(Program.UWIN[ix][iy][izz], 2) + Math.Pow(Program.VWIN[ix][iy][izz], 2));
                                    Program.Ustern[ix][iy] = (float)((windig + 0.15) * 0.4 / (Math.Log((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Z0Gramm[ix][iy]) -
                                        psim * ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Ob[ix][iy])));
                                }
                            }
                            if (Program.StabClass == 4)
                            {
                                Program.Ob[ix][iy] = 1000;
                                double phim = 1 + 5 * ahoehe / Program.Ob[ix][iy];
                                double psim = -5 * ahoehe / Program.Ob[ix][iy];
                                if (Program.Topo != 1)
                                    Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(ahoehe / Program.Z0Gramm[ix][iy]) - psim * (ahoehe / Program.Ob[ix][iy])));
                                else
                                {
                                    int izz = 1;
                                    for (int iz = 1; iz <= Program.NK; iz++)
                                        if ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) < 5)
                                            izz = iz + 1;
                                    double windig = Math.Sqrt(Math.Pow(Program.UWIN[ix][iy][izz], 2) + Math.Pow(Program.VWIN[ix][iy][izz], 2));
                                    Program.Ustern[ix][iy] = (float)((windig + 0.15) * 0.4 / (Math.Log((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Z0Gramm[ix][iy]) -
                                        psim * ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Ob[ix][iy])));
                                }
                            }
                            if (Program.StabClass == 5)
                            {
                                Program.Ob[ix][iy] = (float)Math.Min(1 / (0.01 * Math.Pow(Program.Z0Gramm[ix][iy] * 100, -0.5)), 1000);
                                double phim = 1 + 5 * ahoehe / Program.Ob[ix][iy];
                                double psim = -5 * ahoehe / Program.Ob[ix][iy];
                                if (Program.Topo != 1)
                                    Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(ahoehe / Program.Z0Gramm[ix][iy]) - psim * (ahoehe / Program.Ob[ix][iy])));
                                else
                                {
                                    int izz = 1;
                                    for (int iz = 1; iz <= Program.NK; iz++)
                                        if ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) < 5)
                                            izz = iz + 1;
                                    double windig = Math.Sqrt(Math.Pow(Program.UWIN[ix][iy][izz], 2) + Math.Pow(Program.VWIN[ix][iy][izz], 2));
                                    Program.Ustern[ix][iy] = (float)((windig + 0.15) * 0.4 / (Math.Log((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Z0Gramm[ix][iy]) -
                                        psim * ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Ob[ix][iy])));
                                }
                            }
                            if (Program.StabClass == 6)
                            {
                                Program.Ob[ix][iy] = (float)Math.Max(1 / (0.05 * Math.Pow(Program.Z0Gramm[ix][iy] * 100, -0.5)), 4);
                                double phim = 1 + 5 * ahoehe / Program.Ob[ix][iy];
                                double psim = -5 * ahoehe / Program.Ob[ix][iy];
                                if (Program.Topo != 1)
                                    Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(ahoehe / Program.Z0Gramm[ix][iy]) - psim * (ahoehe / Program.Ob[ix][iy])));
                                else
                                {
                                    int izz = 1;
                                    for (int iz = 1; iz <= Program.NK; iz++)
                                        if ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) < 5)
                                            izz = iz + 1;
                                    double windig = Math.Sqrt(Math.Pow(Program.UWIN[ix][iy][izz], 2) + Math.Pow(Program.VWIN[ix][iy][izz], 2));
                                    Program.Ustern[ix][iy] = (float)((windig + 0.15) * 0.4 / (Math.Log((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Z0Gramm[ix][iy]) -
                                        psim * ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Ob[ix][iy])));
                                }
                            }
                            if (Program.StabClass == 7)
                            {
                                Program.Ob[ix][iy] = (float)Math.Max(1 / (0.2 * Math.Pow(Program.Z0Gramm[ix][iy] * 100, -0.55)), 4);
                                double phim = 1 + 5 * ahoehe / Program.Ob[ix][iy];
                                double psim = -5 * ahoehe / Program.Ob[ix][iy];
                                if (Program.Topo != 1)
                                    Program.Ustern[ix][iy] = (float)((Program.WindVelGral + 0.15) * 0.4 / (Math.Log(ahoehe / Program.Z0Gramm[ix][iy]) - psim * (ahoehe / Program.Ob[ix][iy])));
                                else
                                {
                                    int izz = 1;
                                    for (int iz = 1; iz <= Program.NK; iz++)
                                        if ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) < 5)
                                            izz = iz + 1;
                                    double windig = Math.Sqrt(Math.Pow(Program.UWIN[ix][iy][izz], 2) + Math.Pow(Program.VWIN[ix][iy][izz], 2));
                                    Program.Ustern[ix][iy] = (float)((windig + 0.15) * 0.4 / (Math.Log((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Z0Gramm[ix][iy]) -
                                        psim * ((Program.ZSP[ix][iy][izz] - Program.AH[ix][iy]) / Program.Ob[ix][iy])));
                                }
                            }
                            Program.Ustern[ix][iy] = (float)Math.Max(Program.Ustern[ix][iy], 0.02);
                        }
                    }

                    Program.IWETstart = Program.IDISP + 1;
                    if (akla_count > 0)
                        Program.StabClassGramm = Convert.ToInt32(akla_sum / akla_count);
                }

                return 0; // read meteopgt.all OK
            }
            catch
            {
                string err = "Error when reading file meteopgt.all in line " + (Program.IWETstart + 2).ToString() + " Execution stopped: press ESC to stop";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                Environment.Exit(0);
                return 1; // read meteopgt.all Error
            }
        }
    }
}
