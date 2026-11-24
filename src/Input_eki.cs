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
    ///Read meteorological input data (friction velocity, Obukhov length, standard deviation of horizontal wind fluctuations, u- and v-wind components)
    /// </summary>
    class Input_Eki
    {
        /// <summary>
    	/// Read the meteo input file elimakidata.prn
    	/// </summary> 
        public static MeteoReadStatus Read(int situation)
        {
            int lineNumber = 1;
            try
            {
                using (StreamReader sr = new StreamReader("elimakidata.prn"))
                {
                    string line = null;
                    for (int n = 1; n <= situation; n++)
                    {
                        lineNumber = n;
                        line = MeteoRows.ReadDataLine(sr);
                        if (line == null)
                        {
                            return MeteoReadStatus.EndOfInput;
                        }
                    }
                    string[] text = line.Split(new char[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);

                    Program.MetProfileNumb = 3;
                    Program.MeasurementHeight[1] = 7 / 2;
                    Program.MeasurementHeight[2] = 6;
                    Program.MeasurementHeight[3] = 10;

                    float observedObukhovLength = Convert.ToSingle(text[0].Replace(".", Program.Decsep));
                    if (!float.IsFinite(observedObukhovLength))
                    {
                        throw new FormatException("Obukhov length must be finite");
                    }
                    // Ob = 0 would give infinite dissipation and a zero time step.
                    if (observedObukhovLength == 0)
                    {
                        observedObukhovLength = 1.0F;
                    }

                    //read wind components along the vertical profile
                    int n1 = 0;
                    for (int k = 1; k <= Program.MetProfileNumb; k++)
                    {
                        n1 = (k - 1) * 2;
                        Program.ObsWindU[k] = Convert.ToSingle(text[1 + n1].Replace(".", Program.Decsep));
                        Program.ObsWindV[k] = Convert.ToSingle(text[2 + n1].Replace(".", Program.Decsep));
                        if (!float.IsFinite(Program.ObsWindU[k]) || !float.IsFinite(Program.ObsWindV[k]))
                        {
                            throw new FormatException("wind components must be finite");
                        }
                    }

                    int ianzi = 0;
                    for (int iprof = 1; iprof <= Program.MetProfileNumb; iprof++)
                    {
                        ianzi++;
                        if ((Program.ObsWindU[ianzi] == 0) && (Program.ObsWindV[ianzi] == 0))
                        {
                            Program.MeasurementHeight[ianzi] = Program.MeasurementHeight[ianzi + 1];
                            Program.MeasurementHeight[ianzi + 1] = Program.MeasurementHeight[ianzi + 2];
                            Program.MeasurementHeight[ianzi + 2] = Program.MeasurementHeight[ianzi + 3];
                            Program.ObsWindU[ianzi] = Program.ObsWindU[ianzi + 1];
                            Program.ObsWindU[ianzi + 1] = Program.ObsWindU[ianzi + 2];
                            Program.ObsWindU[ianzi + 2] = Program.ObsWindU[ianzi + 3];
                            Program.ObsWindV[ianzi] = Program.ObsWindV[ianzi + 1];
                            Program.ObsWindV[ianzi + 1] = Program.ObsWindV[ianzi + 2];
                            Program.ObsWindV[ianzi + 2] = Program.ObsWindV[ianzi + 3];
                            ianzi--;
                        }
                    }

                    Program.MetProfileNumb = ianzi;

                    //friction velocity
                    Program.WindVelGral = (float)Math.Sqrt(Math.Pow(Program.ObsWindU[Program.MetProfileNumb], 2) + Math.Pow(Program.ObsWindV[Program.MetProfileNumb], 2));

                    for (int ix = 1; ix <= Program.NX; ix++)
                    {
                        for (int iy = 1; iy <= Program.NY; iy++)
                        {
                            Program.Ob[ix][iy] = observedObukhovLength;
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
                }
                return MeteoReadStatus.Success;
            }
            catch (Exception ex)
            {
                string error = $"Error when reading file elimakidata.prn in line {lineNumber}: {ex.Message}";
                Console.WriteLine(error);
                ProgramWriters.LogfileProblemreportWrite(error);
                return MeteoReadStatus.InvalidInput;
            }
        }
    }
}
