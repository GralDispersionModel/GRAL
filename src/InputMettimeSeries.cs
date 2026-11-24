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
using System.IO;
using System.Linq;

namespace GRAL_2001
{
    public class InputMettimeSeries
    {
        private List<WindData> _winddata;
        /// <summary>
        /// Wind data class
        /// </summary>
        public List<WindData> WindData { set { _winddata = value; } get { return _winddata; } }

        /// <summary>
        /// Read the time series meteo data file mettimeseries.dat
    	/// </summary> 
        public bool ReadMetTimeSeries()
        {
            string filename = "mettimeseries.dat";
            int lineNumber = 0;
            string[] text2;
            string[] text3;

            try
            {
                using (FileStream fs_mettimeseries = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader mettimeseries = new StreamReader(fs_mettimeseries))
                    {
                        string text;

                        // read data
                        while (true)
                        {
                            lineNumber++;
                            text = MeteoRows.ReadDataLine(mettimeseries);
                            if (text == null) break;
                            WindData wd = new WindData();
                            text2 = text.Split(new char[] { ' ', ';', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            text3 = text2[0].Split(new char[] { '.', ':', '-' }, StringSplitOptions.RemoveEmptyEntries);

                            wd.Month = text3[1];
                            wd.Day = text3[0];
                            wd.Hour = text2[1];
                            wd.Date = wd.Day + wd.Month;
                            wd.Vel = Convert.ToSingle(text2[2].Replace(".", Program.Decsep));
                            wd.Dir = Convert.ToSingle(text2[3].Replace(".", Program.Decsep));
                            wd.Stab_Class = Convert.ToInt16(text2[4]);
                            wd.IWet = lineNumber;

                            _winddata.Add(wd);
                        }
                        if (_winddata.Count == 0)
                        {
                            throw new FormatException("no weather situations");
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _winddata.Clear();
                string err = $"Error when reading file {filename} in line {Math.Max(1, lineNumber)}: {ex.Message}";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);
                return false;
            }
        }//read mettimeseries

        /// <summary>
    	/// Read the classified meteo data file meteopgt.all
    	/// </summary> 
        public bool ReadMeteopgtAll()
        {
            string filename = "meteopgt.all";
            int lineNumber = 1;
            string[] text2;

            try
            {
                using (FileStream fs_meteopgt = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader meteopgt = new StreamReader(fs_meteopgt))
                    {
                        //header (not used)
                        if (meteopgt.ReadLine() == null)
                        {
                            throw new FormatException("missing first header line");
                        }
                        lineNumber = 2;
                        if (meteopgt.ReadLine() == null)
                        {
                            throw new FormatException("missing second header line");
                        }

                        // read data
                        string text;
                        while (true)
                        {
                            lineNumber++;
                            text = MeteoRows.ReadDataLine(meteopgt);
                            if (text == null) break;
                            WindData wd = new WindData();
                            text2 = text.Split(new char[] { ' ', ';', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            wd.Dir = Convert.ToSingle(text2[0].Replace(".", Program.Decsep));
                            wd.Vel = Convert.ToSingle(text2[1].Replace(".", Program.Decsep));
                            wd.Stab_Class = Convert.ToInt16(text2[2]);
                            wd.IWet = lineNumber - 2;

                            _winddata.Add(wd);
                        }
                        if (_winddata.Count == 0)
                        {
                            throw new FormatException("no weather situations");
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _winddata.Clear();
                string err = $"Error when reading file {filename} in line {lineNumber}: {ex.Message}";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);
                return false;
            }
        }//read meteopgt.all


        /// <summary>
    	/// Search the corresponding meteo situation for one line in mettimeseries.txt in meteopgt.all
    	/// </summary> 
        /// <returns>
        /// Returns the line number in Meteopgt[], -1 if last line in MetTimeSer[] has been reached, -3 if no match could be found
        /// </returns>
        public int SearchWeatherSituation(int Weathersituation)
        {
            if (Program.MeteoTimeSer.Count() <= (Weathersituation - 1)) // if last line in Met_Time_Ser[] has been reached -> exit with -1
            {
                return -1;
            }

            int iwet = -3; // if no best match can be found -> throw an ecxeption in Program.cs() and try the next situation
            for (int n = 0; n < Program.MeteopgtLst.Count; n++)
            {
                //if (Program.Met_Time_Ser.Count() > (Weathersituation - 1))
                //{
                if (MathF.Abs(Program.MeteopgtLst[n].Vel - Program.MeteoTimeSer[Weathersituation - 1].Vel) < 0.01F
                   && MathF.Abs(Program.MeteopgtLst[n].Dir - Program.MeteoTimeSer[Weathersituation - 1].Dir) < 0.01F
                   && (Program.MeteopgtLst[n].Stab_Class == Program.MeteoTimeSer[Weathersituation - 1].Stab_Class))
                {
                    iwet = n;
                    break;
                }
                //}
            }

            if (iwet == -3) // found no exact match -> try to find the best fitting match
            {
                float richtung = (270F - Program.MeteoTimeSer[Weathersituation - 1].Dir) * MathF.PI / 180F;
                float u_Mettime = Program.MeteoTimeSer[Weathersituation - 1].Vel * MathF.Cos(richtung);
                float v_Mettime = Program.MeteoTimeSer[Weathersituation - 1].Vel * MathF.Sin(richtung);
                float error_min = 1000000;

                for (int n = 0; n < Program.MeteopgtLst.Count; n++)
                {
                    if (Program.MeteopgtLst[n].Stab_Class == Program.MeteoTimeSer[Weathersituation - 1].Stab_Class) // use exact stability class
                    {
                        richtung = (270 - Program.MeteopgtLst[n].Dir) * MathF.PI / 180F;
                        float u_Meteopgt = Program.MeteopgtLst[n].Vel * MathF.Cos(richtung);
                        float v_Meteopgt = Program.MeteopgtLst[n].Vel * MathF.Sin(richtung);

                        float err = MathF.Sqrt(MathF.Pow((u_Meteopgt - u_Mettime), 2) + MathF.Pow((v_Meteopgt - v_Mettime), 2));

                        if (err < error_min) // found a situation with a lower error value
                        {
                            error_min = err;
                            iwet = n;
                        }
                    }
                }
            }

            return iwet;

        }//search in meteopgt.all for the corresponding in mettimeseries
    }
}
