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
using System.Globalization;
using System.IO;

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
        /// <summary>
        ///Read the file precipitation.txt
        /// </summary>
        public void ReadPrecipitationTXT()
        {
            CultureInfo ic = CultureInfo.InvariantCulture;
            float Precipitation_Sum = 0;
            string Info;

            if (Program.ISTATIONAER == 0)
            {
                if (File.Exists("Precipitation.txt") == true && Program.WetDeposition == true)
                {
                    try
                    {

                        using (StreamReader sr = new StreamReader("Precipitation.txt"))
                        {
                            //read "Precipitation.txt"
                            string[] text10 = new string[1];

                            // Read header
                            text10 = sr.ReadLine().Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            while (sr.EndOfStream == false)
                            {
                                text10 = sr.ReadLine().Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                                if (text10.Length > 2)
                                {
                                    float prec = Convert.ToSingle(text10[2], ic);
                                    Precipitation_Sum += prec;
                                    Program.WetDepoPrecipLst.Add(prec);
                                }
                            }
                        }
                    }
                    catch
                    { }

                    // plausibility checks for precipitation
                    if (Precipitation_Sum < 0.1)
                    {
                        Program.WetDeposition = false;
                        Info = "Precipitation sum = 0 - wet deposition is not computed";
                        Console.WriteLine(Info);
                    }
                    else if (Program.WetDepoPrecipLst.Count != Program.MeteoTimeSer.Count)
                    {
                        Program.WetDeposition = false;
                        Info = "The number of situations in the file Precipitation.txt does not match the number in the file mettimeseries.txt - wet deposition is not computed";
                        Console.WriteLine(Info);
                        ProgramWriters.LogfileGralCoreWrite(Info);
                        ProgramWriters.LogfileGralCoreWrite(" ");
                    }
                    else
                    {
                        Info = "Wet deposition is computed, the precipitation sum = " + Precipitation_Sum.ToString() + " mm";
                        Console.WriteLine(Info);

                        Info = "Wet deposition parameters cW = " + Program.Wet_Depo_CW.ToString() + " 1/s    AlphaW = " + Program.WedDepoAlphaW.ToString();
                        Console.WriteLine(Info);

                        ProgramWriters.LogfileGralCoreWrite(" ");
                        Console.WriteLine("");

                        Program.WetDeposition = true;
                    }

                }
            }
        } // Read Precipitation.txt
    }
}