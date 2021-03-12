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

namespace GRAL_2001
{
    /// <summary>
    ///Get the index of a source time series in time series file
    /// </summary> 
    class GetTransientTimeSeriesIndex
    {
        /// <summary>
        ///Search the index of the recent weather situation in the time series file
        /// </summary> 
        /// <param name="TSColList">List with time series values and column names</param>
        /// <param name="CompareString">Search string: Temp@_ or Vel@_</param>
        /// <param name="SourceData">Array with splitted source data</param>
        ///<returns>The index of the TSColList or -1 if no match found</returns>  
        public static int GetIndex(List<TimeSeriesColumn> TSColList, string CompareString, string[] SourceData)
        {
            int columnIndex = -1;

            // transient mode acitvated?
            if (Program.ISTATIONAER == Consts.TransientMode && TSColList != null)
            {
                //search the CompareString within the SourceData
                string columnName = String.Empty;
                for (int i = 2; i < SourceData.Length; i++)
                {
                    if (SourceData[i].StartsWith(CompareString))
                    {
                        columnName = SourceData[i].Substring(CompareString.Length);
                        break;
                    }
                }

                //get index within the TimeSeriesList
                if (!string.IsNullOrEmpty(columnName))
                {
                    int i = 0;
                    foreach (TimeSeriesColumn tscol in TSColList)
                    {
                        if (tscol.Name.Equals(columnName))
                        {
                            columnIndex = i;
                        }
                        i++;
                    }
                }
            }

            return columnIndex;
        }
    }
}