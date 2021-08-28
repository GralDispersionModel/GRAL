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
                    float anemoHeight = Convert.ToSingle(text[0].Replace(".", Program.Decsep));
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
                    Program.MeasurementHeight[1] = anemoHeight;
                    Program.WindDirGral = Convert.ToSingle(text[0].Replace(".", Program.Decsep));
                    Program.WindVelGral = Convert.ToSingle(text[1].Replace(".", Program.Decsep));
                    int stabilityclass = Convert.ToInt16(text[2]);

                    //random wind direction taken from the defined sector width
                    if ((TIMESERIES == "0") && (Program.Topo != Consts.TerrainAvailable))
                    {
                        Random rnd = new Random();
                        Program.WindDirGral = Program.WindDirGral - (float)(SECTORWIDTH / 20) + (float)(rnd.NextDouble() * SECTORWIDTH / 10);
                    }

                    //horizontal wind components
                    Program.WindDirGral *= 10;
                    Program.WindDirGral = (float)((270 - Program.WindDirGral) * Math.PI / 180);
                    Program.ObsWindU[1] = (float)(Program.WindVelGral * Math.Cos(Program.WindDirGral));
                    Program.ObsWindV[1] = (float)(Program.WindVelGral * Math.Sin(Program.WindDirGral));

                    //Calculate Obukhov length and friction velocity for GRAMM grid
                    for (int ix = 1; ix <= Program.NX; ix++)
                    {
                        for (int iy = 1; iy <= Program.NY; iy++)
                        {
                            Program.Z0Gramm[ix][iy] = MathF.Max(Program.Z0Gramm[ix][iy], 0.00001F);

                            if (Program.Topo == Consts.TerrainAvailable && Program.AKL_GRAMM[0, 0] != 0)
                            {
                                stabilityclass = (int)Program.AKL_GRAMM[ix - 1, iy - 1];
                            }

                            // sum up the SC values inside the GRAL domain area
                            if (Program.GrammWest + Program.DDX[1] * ix > Program.IKOOAGRAL &&
                            Program.GrammWest + Program.DDX[1] * ix < Program.IKOOAGRAL + Program.GralDx * Program.NXL &&
                            Program.GrammSouth + Program.DDY[1] * iy > Program.JKOOAGRAL &&
                            Program.GrammSouth + Program.DDY[1] * iy < Program.JKOOAGRAL + Program.GralDy * Program.NYL)
                            {
                                akla_sum += stabilityclass;
                                akla_count++;
                            }

                            Program.SC_Gral[ix][iy] = (byte)Math.Max(0, Math.Min(7, stabilityclass)); // 11.9.2017 Kuntner remember the Stability class for the receptor concenterations
                            (Program.Ob[ix][iy], Program.Ustern[ix][iy]) = CalcMetParams(stabilityclass, anemoHeight, ix, iy, Program.Z0Gramm[ix][iy]);
                        }
                    }

                    //if GRAL Roughness is used (with buildings only) -> calculate OL and Ustar for each FF cell
                    if (Program.AdaptiveRoughnessMax > 0)
                    {
                        for (int ix = 1; ix <= Program.NII; ix++)
                        {
                            for (int iy = 1; iy <= Program.NJJ; iy++)
                            {
                                //index for flat terrain = 1
                                int IUstern = 1;
                                int JUstern = 1;
                                if (Program.Topo == Consts.TerrainAvailable && Program.AKL_GRAMM[0, 0] != 0)
                                {
                                    double x = ix * Program.DXK + Program.GralWest;
                                    double y = iy * Program.DYK + Program.GralSouth;
                                    double xsi1 = x - Program.GrammWest;
                                    double eta1 = y - Program.GrammSouth;
                                    IUstern = Math.Clamp((int)(xsi1 / Program.DDX[1]) + 1, 1, Program.NX - 1);
                                    JUstern = Math.Clamp((int)(eta1 / Program.DDY[1]) + 1, 1, Program.NY - 1);
                                    stabilityclass = Program.AKL_GRAMM[IUstern, JUstern];
                                }

                                (Program.OLGral[ix][iy], Program.USternGral[ix][iy]) = CalcMetParams(stabilityclass, anemoHeight, IUstern, JUstern, Program.Z0Gral[ix][iy]);
                            }
                        }

                    }

                    Program.StabClass = stabilityclass;
                    Program.IWETstart = Program.IDISP + 1;
                    if (akla_count > 0)
                    {
                        Program.StabClassGramm = Convert.ToInt32(akla_sum / akla_count);
                    }
                }

                return Consts.CalculationRunning; // read meteopgt.all OK
            }
            catch (Exception ex)
            {
                string err = "Error when reading file meteopgt.all in line " + (Program.IWETstart + 2).ToString() + " Execution stopped: press ESC to stop";
                Console.WriteLine(ex.Message + Environment.NewLine + err);
                ProgramWriters.LogfileProblemreportWrite(err);

                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                {
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) {; }
                }
                Environment.Exit(0);
                return Consts.CalculationFinished; // read meteopgt.all Error
            }
        }

        /// <summary>
        /// Calculate Ustar and the Obukhov Lenght
        /// </summary>
        /// <param name="stabilityclass">Stability class between 1 (unstable) and 7 (stable) </param>
        /// <param name="AnemoHeight">Height of the wind measurement</param>
        /// <param name="ix">Index X in the GRAMM wind field</param>
        /// <param name="iy">Index Y in the GRAMM wind field</param>
        /// <param name="Z0">Roughness lenght for this cell</param>
        /// <returns></returns>
        private static (float, float) CalcMetParams(int stabilityclass, float AnemoHeight, int ix, int iy, float Z0)
        {
            float ObL = 1000;
            float UStar = 0.02F;

            if (stabilityclass == 1)
            {
                ObL = MathF.Min(1 / (-0.37F * MathF.Pow(Z0 * 100, -0.55F)), -4);
                float phim = MathF.Pow(1 - 16 * AnemoHeight / ObL, 0.25F);
                float psim = MathF.Log((1 + MathF.Pow(phim, -2)) * 0.5F * MathF.Pow((1 + MathF.Pow(phim, -1)) * 0.5F, 2)) - 2 * MathF.Atan(MathF.Pow(phim, -1)) + 1.57F;

                if (Program.Topo != Consts.TerrainAvailable)
                {
                    // flat terrain -> use the wind velocity from meteopgt.all
                    UStar = (Program.WindVelGral + 0.15F) * 0.4F / (MathF.Log(AnemoHeight / Z0) - psim * (AnemoHeight / ObL));
                }
                else
                {
                    //with terrain -> use the wind velocity from the GRAMM wind field
                    int izz = 1;
                    float surfHeight = Program.AH[ix][iy];
                    for (int iz = 1; iz <= Program.NK; iz++)
                    {
                        if ((Program.ZSP[ix][iy][izz] - surfHeight) < 5)
                        {
                            izz = iz + 1;
                        }
                    }

                    float windig = MathF.Sqrt(MathF.Pow(Program.UWIN[ix][iy][izz], 2) + MathF.Pow(Program.VWIN[ix][iy][izz], 2));
                    UStar = (windig + 0.15F) * 0.4F / (MathF.Log((Program.ZSP[ix][iy][izz] - surfHeight) / Z0) - psim * ((Program.ZSP[ix][iy][izz] - surfHeight) / ObL));
                }
            }
            else if (stabilityclass == 2)
            {
                ObL = MathF.Min(1 / (-0.12F * MathF.Pow(Z0 * 100, -0.50F)), -4);
                float phim = MathF.Pow(1 - 16 * AnemoHeight / ObL, 0.25F);
                float psim = MathF.Log((1 + MathF.Pow(phim, -2)) * 0.5F * MathF.Pow((1 + MathF.Pow(phim, -1)) * 0.5F, 2)) - 2 * MathF.Atan(MathF.Pow(phim, -1)) + 1.57F;
                if (Program.Topo != Consts.TerrainAvailable)
                {
                    UStar = (Program.WindVelGral + 0.15F) * 0.4F / (MathF.Log(AnemoHeight / Z0) - psim * (AnemoHeight / ObL));
                }
                else
                {
                    int izz = 1;
                    float surfHeight = Program.AH[ix][iy];
                    for (int iz = 1; iz <= Program.NK; iz++)
                    {
                        if ((Program.ZSP[ix][iy][izz] - surfHeight) < 5)
                        {
                            izz = iz + 1;
                        }
                    }

                    float windig = MathF.Sqrt(MathF.Pow(Program.UWIN[ix][iy][izz], 2) + MathF.Pow(Program.VWIN[ix][iy][izz], 2));
                    UStar = (windig + 0.15F) * 0.4F / (MathF.Log((Program.ZSP[ix][iy][izz] - surfHeight) / Z0) - psim * ((Program.ZSP[ix][iy][izz] - surfHeight) / ObL));
                }
            }
            else if (stabilityclass == 3)
            {
                ObL = MathF.Min(1 / (-0.067F * MathF.Pow(Z0 * 100, -0.56F)), -4);
                float phim = MathF.Pow(1 - 16 * AnemoHeight / ObL, 0.25F);
                float psim = MathF.Log((1 + MathF.Pow(phim, -2)) * 0.5F * MathF.Pow((1 + MathF.Pow(phim, -1)) * 0.5F, 2)) - 2 * MathF.Atan(MathF.Pow(phim, -1)) + 1.57F;
                if (Program.Topo != Consts.TerrainAvailable)
                {
                    UStar = (Program.WindVelGral + 0.15F) * 0.4F / (MathF.Log(AnemoHeight / Z0) - psim * (AnemoHeight / ObL));
                }
                else
                {
                    int izz = 1;
                    float surfHeight = Program.AH[ix][iy];
                    for (int iz = 1; iz <= Program.NK; iz++)
                    {
                        if ((Program.ZSP[ix][iy][izz] - surfHeight) < 5)
                        {
                            izz = iz + 1;
                        }
                    }

                    float windig = MathF.Sqrt(MathF.Pow(Program.UWIN[ix][iy][izz], 2) + MathF.Pow(Program.VWIN[ix][iy][izz], 2));
                    UStar = (windig + 0.15F) * 0.4F / (MathF.Log((Program.ZSP[ix][iy][izz] - surfHeight) / Z0) - psim * ((Program.ZSP[ix][iy][izz] - surfHeight) / ObL));
                }
            }
            else if (stabilityclass == 5)
            {
                ObL = MathF.Min(1 / (0.01F * MathF.Pow(Z0 * 100, -0.5F)), 1000);
                //float phim = 1 + 5 * AnemoHeight / ObL;
                float psim = -5 * AnemoHeight / ObL;
                if (Program.Topo != Consts.TerrainAvailable)
                {
                    UStar = (Program.WindVelGral + 0.15F) * 0.4F / (MathF.Log(AnemoHeight / Z0) - psim * (AnemoHeight / ObL));
                }
                else
                {
                    int izz = 1;
                    float surfHeight = Program.AH[ix][iy];
                    for (int iz = 1; iz <= Program.NK; iz++)
                    {
                        if ((Program.ZSP[ix][iy][izz] - surfHeight) < 5)
                        {
                            izz = iz + 1;
                        }
                    }

                    float windig = MathF.Sqrt(MathF.Pow(Program.UWIN[ix][iy][izz], 2) + MathF.Pow(Program.VWIN[ix][iy][izz], 2));
                    UStar = (windig + 0.15F) * 0.4F / (MathF.Log((Program.ZSP[ix][iy][izz] - surfHeight) / Z0) - psim * ((Program.ZSP[ix][iy][izz] - surfHeight) / ObL));
                }
            }
            else if (stabilityclass == 6)
            {
                ObL = MathF.Max(1 / (0.05F * MathF.Pow(Z0 * 100, -0.5F)), 4);
                //float phim = 1 + 5 * AnemoHeight / ObL;
                float psim = -5 * AnemoHeight / ObL;
                if (Program.Topo != Consts.TerrainAvailable)
                {
                    UStar = (Program.WindVelGral + 0.15F) * 0.4F / (MathF.Log(AnemoHeight / Z0) - psim * (AnemoHeight / ObL));
                }
                else
                {
                    int izz = 1;
                    float surfHeight = Program.AH[ix][iy];
                    for (int iz = 1; iz <= Program.NK; iz++)
                    {
                        if ((Program.ZSP[ix][iy][izz] - surfHeight) < 5)
                        {
                            izz = iz + 1;
                        }
                    }

                    float windig = MathF.Sqrt(MathF.Pow(Program.UWIN[ix][iy][izz], 2) + MathF.Pow(Program.VWIN[ix][iy][izz], 2));
                    UStar = (windig + 0.15F) * 0.4F / (MathF.Log((Program.ZSP[ix][iy][izz] - surfHeight) / Z0) - psim * ((Program.ZSP[ix][iy][izz] - surfHeight) / ObL));
                }
            }
            else if (stabilityclass == 7)
            {
                ObL = (float)Math.Max(1 / (0.2 * Math.Pow(Z0 * 100, -0.55)), 4);
                //float phim = 1 + 5 * AnemoHeight / ObL;
                float psim = -5 * AnemoHeight / ObL;
                if (Program.Topo != Consts.TerrainAvailable)
                {
                    UStar = (Program.WindVelGral + 0.15F) * 0.4F / (MathF.Log(AnemoHeight / Z0) - psim * (AnemoHeight / ObL));
                }
                else
                {
                    int izz = 1;
                    float surfHeight = Program.AH[ix][iy];
                    for (int iz = 1; iz <= Program.NK; iz++)
                    {
                        if ((Program.ZSP[ix][iy][izz] - surfHeight) < 5)
                        {
                            izz = iz + 1;
                        }
                    }

                    float windig = MathF.Sqrt(MathF.Pow(Program.UWIN[ix][iy][izz], 2) + MathF.Pow(Program.VWIN[ix][iy][izz], 2));
                    UStar = (windig + 0.15F) * 0.4F / (MathF.Log((Program.ZSP[ix][iy][izz] - surfHeight) / Z0) - psim * ((Program.ZSP[ix][iy][izz] - surfHeight) / ObL));
                }
            }
            else // if (stabilityclass == 4) // Fallback - take stab class 4 if stabilityclass is not valid
            {
                ObL = 1000;
                //float phim = 1 + 5 * AnemoHeight / ObL;
                float psim = -5 * AnemoHeight / ObL;
                if (Program.Topo != Consts.TerrainAvailable)
                {
                    UStar = (Program.WindVelGral + 0.15F) * 0.4F / (MathF.Log(AnemoHeight / Z0) - psim * (AnemoHeight / ObL));
                }
                else
                {
                    int izz = 1;
                    float surfHeight = Program.AH[ix][iy];
                    for (int iz = 1; iz <= Program.NK; iz++)
                    {
                        if ((Program.ZSP[ix][iy][izz] - surfHeight) < 5)
                        {
                            izz = iz + 1;
                        }
                    }

                    float windig = MathF.Sqrt(MathF.Pow(Program.UWIN[ix][iy][izz], 2) + MathF.Pow(Program.VWIN[ix][iy][izz], 2));
                    UStar = (windig + 0.15F) * 0.4F / (MathF.Log((Program.ZSP[ix][iy][izz] - surfHeight) / Z0) - psim * ((Program.ZSP[ix][iy][izz] - surfHeight) / ObL));
                }
            }

            UStar = MathF.Max(UStar, 0.02F);
            return (ObL, UStar);
        }
    }
}
