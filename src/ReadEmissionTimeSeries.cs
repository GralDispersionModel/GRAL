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
using System.Linq;

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
        /// <summary>
        ///Read the emission modulation factors for the transient GRAL mode
        /// </summary>
        public void ReadEmissionTimeseries()
        {
            if (Program.ISTATIONAER == Consts.TransientMode)
            {
                if (File.Exists("emissions_timeseries.txt") == true)
                {
                    Program.EmissionTimeseriesExist = true;
                    try
                    {
                        int lineCount = File.ReadLines("emissions_timeseries.txt").Count();
                        Program.EmFacTimeSeries = new float[lineCount, Program.SourceGroups.Count];
                        // Header columns can include groups not selected for this run.
                        
                        using (StreamReader sr = new StreamReader("emissions_timeseries.txt"))
                        {
                            //read timeseries of emissions
                            string[] text10 = new string[1];
                            //get source group numbers
                            text10 = sr.ReadLine().Split(new char[] { ',',':', '-', '\t', ';' });
                            
                            int SG_Time_Series_Count = Math.Max(text10.Length - 2, 1); // number of Source groups in emissions_timeseries.txt
                            int[] SG_Time_Series = new int[SG_Time_Series_Count];
                            double[] mean = new double[SG_Time_Series_Count];
                            var seenGroups = new System.Collections.Generic.HashSet<int>();
 
                            for (int ii = 2; ii < text10.Length; ii++)
                            {
                                //get the column corresponding with the source group number stored in sg_numbers
                                int sg_temp = Convert.ToInt32(text10[ii]);
                                if (!SourceGroupFileName.IsSupported(sg_temp) || !seenGroups.Add(sg_temp))
                                    throw new InvalidDataException("Invalid or duplicate source group in emissions_timeseries.txt");
                                SG_Time_Series[ii - 2] = sg_temp; // remember the real SG Number for each column in emissions_timeseries.txt
                            }
        
                            int i = 0;
      
                            while (sr.EndOfStream == false)
                            {
                                text10 = sr.ReadLine().Split(new char[] { ',', ':', '-', '\t', ';' });

                                for (int sg_i = 0; sg_i < Program.SourceGroups.Count; sg_i++) // set emission factors to 1 by default
                                {
                                    Program.EmFacTimeSeries[i, sg_i] = 1;
                                }

                                for (int n = 0; n < SG_Time_Series_Count; n++) // check each source group defined in emissions_timeseries.txt
                                {
                                    int SG_internal = Program.Get_Internal_SG_Number(SG_Time_Series[n]); // get the internal SG number
                                    if (SG_internal < 0) continue; // Unselected groups do not affect this run.

                                    if (SG_internal >= 0 && (n + 2) < text10.Length) // otherwise this source group in emissions_timeseries.txt does not exist internal in GRAL
                                    {
                                        Program.EmFacTimeSeries[i, SG_internal] = Convert.ToSingle(text10[n + 2].Replace(".", decsep));
                                    }
                                    else
                                    {
                                        Program.EmFacTimeSeries[i, SG_internal] = 1;
                                    }
                                    if (!float.IsFinite(Program.EmFacTimeSeries[i, SG_internal]) || Program.EmFacTimeSeries[i, SG_internal] < 0)
                                        throw new InvalidDataException("Emission factors must be finite and nonnegative");
                                    mean[n] += Program.EmFacTimeSeries[i, SG_internal];
                                }
                                i++;
                            }

                            string info = "Reading emissions_timeseries.txt successful - mean factors for each source group: ";
                            Console.WriteLine(info);
                            ProgramWriters.LogfileGralCoreWrite(info);
                            for (int n = 0; n < SG_Time_Series_Count; n++) // check each source group defined in emissions_timeseries.txt
                            {
                                info = "  Source group: " + SG_Time_Series[n].ToString() + " Mean emission factor: " + Math.Round(mean[n] / i, 2);
                                Console.WriteLine(info);
                                ProgramWriters.LogfileGralCoreWrite(info);
                            }
                            ProgramWriters.LogfileGralCoreWrite(" ");
                            Console.WriteLine();
                        }
                    }
                    catch(Exception ex)
                    { 
                        ProgramWriters.LogfileProblemreportWrite("Invalid emissions_timeseries.txt: " + ex.Message);
                        throw new InvalidDataException("Invalid emissions_timeseries.txt; simulation stopped", ex);
                    }
                }
            }
        }  //emission modulation for transient mode
    }
}