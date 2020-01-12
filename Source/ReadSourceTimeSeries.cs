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
using System.Collections.Generic;
using System.Globalization;

namespace GRAL_2001
{
	public class ReadSourceTimeSeries
    {
        /// <summary>
	    /// Read Time Series of Source Parameters at Transient Calculations
	    /// </summary>
        public bool ReadTimeSeries(ref List<TimeSeriesColumn> TSColList, string Filename)
        {
            bool isOK = false;
            try
            {
                if (File.Exists(Filename))
                {
                    int lines = Program.CountLinesInFile(Filename); 
                    string header = string.Empty;
                    using (StreamReader read = new StreamReader(Filename))
                    {
                        header = read.ReadLine();
                    }
                    // Get number of columns without Day.Month and Hour
                    string[] columns = header.Split(new char[] {'\t'}, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length > 1) // Index 0= Day.Month, 1 = Hour
                    {
                        TSColList = new List<TimeSeriesColumn>();
                        
                        // Create Columns
                        for (int i = 2; i < columns.Length; i++)
                        {
                            TimeSeriesColumn TS = new TimeSeriesColumn();
                            TS.Name = columns[i];
                            TS.Value = new float[lines];
                            TSColList.Add(TS);
                        }

                        //Read Column Values
                        using (StreamReader read = new StreamReader(Filename))
                        {
                            int lineCount = 0;
                            header = read.ReadLine();
                            while(!read.EndOfStream)
                            {
                                string linestring = read.ReadLine();
                                columns = linestring.Split(new char[] {'\t'}, StringSplitOptions.RemoveEmptyEntries);                           
                                for (int col = 2; col < columns.Length; col++)
                                {
                                    if (lineCount < TSColList[col - 2].Value.Length)
                                    {
                                        TSColList[col - 2].Value[lineCount] = Convert.ToSingle(columns[col], CultureInfo.InvariantCulture);
                                    }
                                }
                                lineCount++;
                            }
                        }
                        isOK = true;
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                TSColList.Clear();
                TSColList.TrimExcess();
                TSColList = null;
                return false;
            }
            return isOK;
        }
    }
}