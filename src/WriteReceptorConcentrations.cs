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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GRAL_2001
{
    public partial class ProgramWriters
    {
        /// <summary>
        ///Output of receptor concentrations
        /// </summary>
        public void WriteReceptorConcentrations()
        {
            if (Program.ReceptorsAvailable > 0)
            {
                try
                {
                    CultureInfo ic = CultureInfo.InvariantCulture;
                    List<string> content = new List<string>();

                    // read existing header and concentration data
                    if (Program.IWET > 1 && File.Exists("ReceptorConcentrations.dat"))
                    {
                        using (FileStream wr = new FileStream("ReceptorConcentrations.dat", FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            using (StreamReader read = new StreamReader(wr))
                            {
                                // Read header
                                for (int ianz = 1; ianz < 6; ianz++)
                                {
                                    try
                                    {
                                        content.Add(read.ReadLine()); // add existing data
                                    }
                                    catch
                                    {
                                        content.Add("0"); // add 0 in the case of an error
                                    }
                                }

                                for (int ianz = 1; ianz <= Program.IWET; ianz++)
                                {
                                    try
                                    {
                                        if (read.EndOfStream)
                                        {
                                            content.Add("0"); // IWET > than data lines in the existing file -> fill with 0 values
                                        }
                                        else
                                        {
                                            content.Add(read.ReadLine()); // add existing data
                                        }
                                    }
                                    catch
                                    {
                                        content.Add("0");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // create a new header
                        string[] headerLine = ReceptorConcentrationCreateMeteoHeader();
                        for (int i = 0; i < 6; i++)
                        {
                            content.Add(headerLine[i]);
                        }
                    }

                    using (StreamWriter write = new StreamWriter("ReceptorConcentrations.dat", false))
                    {
                        for (int ianz = 0; ianz < content.Count; ianz++)
                        {
                            write.WriteLine(content[ianz]);
                        }

                        for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
                        {
                            //write.Write("NQI " + NQi[iq].ToString(ic) + " ");
                            for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                            {
                                write.Write(Program.ReceptorConc[ianz][iq].ToString(ic) + "\t");
                            }
                        }
                    }

                }
                catch (Exception exc)
                {
                    LogfileProblemreportWrite("Situation: " + Program.IWET.ToString() + " Error writing ReceptorConcentrations.dat file: " + exc.Message);
                }

                for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
                {
                    for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                    {
                        Program.ReceptorTotalConc[ianz][iq] += Program.ReceptorConc[ianz][iq];
                        Program.ReceptorConc[ianz][iq] = 0;
                    }
                }
            }
        }//receptor concentrations

        /// <summary>
        ///Output of receptor timeseries concentrations in the transient GRAL mode
        /// </summary>
        ///<param name="mode">0: new line of concentration, 1: new line with statistical error</param> 
        public void WriteReceptorTimeseries(int mode)
        {
            if (Program.ReceptorsAvailable > 0)
            {
                StringBuilder StB = new StringBuilder();
                List<string> inhalt = new List<string>();
                try
                {
                    CultureInfo ic = CultureInfo.InvariantCulture;
                    string[] header = new string[5];

                    // Read the file except at the 1st weather situation up to the recent weather situation (if a calculation has been restarted)
                    if (File.Exists("Receptor_Timeseries_Transient.txt") && Program.IWET > 1)
                    {
                        try
                        {
                            using (StreamReader read = new StreamReader("Receptor_Timeseries_Transient.txt"))
                            {
                                for (int i = 0; i < header.Length; i++)
                                {
                                    header[i] = read.ReadLine(); // read header
                                }

                                for (int ianz = 1; ianz <= Program.IWET; ianz++)
                                {
                                    try
                                    {
                                        inhalt.Add(read.ReadLine());
                                    }
                                    catch
                                    {
                                        inhalt.Add("0");
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    // write the new time series 
                    using (StreamWriter write = new StreamWriter("Receptor_Timeseries_Transient.txt", false, System.Text.Encoding.Unicode))
                    {
                        // write header
                        if (string.IsNullOrEmpty(header[0]) || string.IsNullOrEmpty(header[4])) //create new header
                        {
                            header[0] = "Rec/SourceGroup" + "\t";
                            header[1] = "X \t";
                            header[2] = "Y \t";
                            header[3] = "Z \t";
                            header[4] = "Date/time \t";
                            // write header
                            for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                            {
                                for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
                                {
                                    if ((ianz - 1) < Program.ReceptorName.Count)
                                    {
                                        header[0] += Program.ReceptorName[ianz - 1] + "/SG: " + Program.SourceGroups[iq].ToString() + "\t";
                                    }
                                    else
                                    {
                                        header[0] += "Rec. " + ianz.ToString() + "/SG: " + Program.SourceGroups[iq].ToString() + "\t";
                                    }
                                    header[1] += Math.Round(Program.ReceptorX[ianz], 1).ToString() + "\t";
                                    header[2] += Math.Round(Program.ReceptorY[ianz], 1).ToString() + "\t";
                                    header[3] += Math.Round(Program.ReceptorZ[ianz], 1).ToString() + "\t";

                                    if (Program.Odour)
                                    {
                                        header[4] += "[OU/m³] " + "\t";
                                    }
                                    else
                                    {
                                        header[4] += "[µg/m³] " + "\t";
                                    }
                                }
                            }

                            if (Program.FlowFieldLevel > 0)
                            {
                                try
                                {
                                    //Add header information for meteo data
                                    for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                                    {
                                        header[0] += "Meteo\t" + Program.ReceptorName[ianz - 1] + "\t \t \t";
                                        header[1] += "\t" + Math.Round(Program.ReceptorX[ianz], 1).ToString() + "\t \t \t";
                                        header[2] += "\t" + Math.Round(Program.ReceptorY[ianz], 1).ToString() + "\t \t \t";
                                        header[3] += "\t" + Math.Round(Program.ReceptorZ[ianz], 1).ToString() + "\t \t \t";

                                        header[4] += "U [m/s]\tV [m/s]\tSC\tBLH\t";
                                    }
                                }
                                catch { }
                            }
                        }

                        for (int i = 0; i < header.Length; i++)
                        {
                            write.WriteLine(header[i]);
                        }

                        // restore file
                        for (int ianz = 1; ianz <= Program.IWET - 1; ianz++)
                        {
                            if (ianz <= inhalt.Count())
                            {
                                write.WriteLine(inhalt[ianz - 1]);
                            }
                        }

                        // write new line
                        if (mode == 0)
                        {
                            WindData wd = Program.MeteoTimeSer[Program.IWET - 1];
                            int month = Convert.ToInt32(wd.Month);
                            int day = Convert.ToInt32(wd.Day);
                            int hour = Convert.ToInt32(wd.Hour);
                            int minute = 0;

                            // check if minutes are != 0
                            if (month == month_old && day == day_old && hour == hour_old && Program.TAUS < 3600)
                            {
                                minute = minute_old + Convert.ToInt32(Program.TAUS / 60);
                                minute_old = minute;
                            }
                            else
                            {
                                minute_old = 0; // reset minute counter
                                hour_old = hour;
                                day_old = day;
                                month_old = month;
                            }

                            // use the user specific time format
                            try
                            {
                                DateTime rdate = new DateTime(2020, month, day, hour, minute, 0);
                                StB.Append(rdate.ToString());
                                StB.Append("\t");
                            }
                            catch
                            {
                                StB.Append(day.ToString());
                                StB.Append(":");
                                StB.Append(month.ToString());
                                StB.Append(".");
                                StB.Append("2020");
                                StB.Append(hour.ToString());
                                StB.Append(":");
                                StB.Append(minute.ToString());
                            }

                            // write concentrations
                            for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                            {
                                for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
                                {
                                    StB.Append(Program.ReceptorConc[ianz][iq].ToString("e4"));
                                    StB.Append("\t");
                                }
                            }

                            if (Program.FlowFieldLevel > 0)
                            {
                                try
                                {
                                    for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                                    {
                                        // write receptor meteo data
                                        int I_SC = (int)((Program.ReceptorX[ianz] - Program.GrammWest) / Program.DDX[1]) + 1;
                                        int J_SC = (int)((Program.ReceptorY[ianz] - Program.GrammSouth) / Program.DDY[1]) + 1;
                                        StB.Append(Program.UK[Program.ReceptorIIndFF[ianz]][Program.ReceptorJIndFF[ianz]][Program.ReceptorKIndFF[ianz]].ToString("0.00"));
                                        StB.Append("\t");
                                        StB.Append(Program.VK[Program.ReceptorIIndFF[ianz]][Program.ReceptorJIndFF[ianz]][Program.ReceptorKIndFF[ianz]].ToString("0.00"));
                                        StB.Append("\t");
                                        int Ak = 0;
                                        if (I_SC >= 0 && I_SC <= Program.NX1 && J_SC >= 0 && J_SC <= Program.NY1 && Program.Topo == 1 && Program.AKL_GRAMM[0, 0] != 0)
                                        {
                                            Ak = Program.SC_Gral[I_SC][J_SC];
                                        }
                                        else
                                        {
                                            if (Program.StabClassGramm > 0)
                                            {
                                                Ak = Program.StabClassGramm;
                                            }
                                            else
                                            {
                                                Ak = Program.StabClass;
                                            }
                                        }
                                        StB.Append(Ak.ToString());
                                        StB.Append("\t");

                                        StB.Append(Math.Round(Program.BdLayHeight).ToString());
                                        StB.Append("\t");
                                    }
                                }
                                catch { }
                            }
                        }
                        else if (mode == 1) // write statistical error
                        {
                            // write empty line 
                            write.WriteLine("");

                            StB.Clear();
                            // write new line with statistical error
                            StB.Append("Est. statistical error [%]");
                            StB.Append("\t");

                            for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                            {
                                for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
                                {
                                    if (Program.ReceptorTotalConc[ianz][iq] > 0)
                                    {
                                        double err = 100d * Program.ReceptorParticleMaxConc[ianz][iq] / Program.ReceptorTotalConc[ianz][iq];  // estimation based on the maximum concentration of one particle and the resulted concentration in the cell	
                                        StB.Append(Math.Round(err, 1).ToString());
                                        StB.Append("%");
                                    }
                                    else
                                    {
                                        StB.Append("NA");
                                    }
                                    StB.Append("\t");
                                }
                            }
                        }

                        write.WriteLine(StB.ToString());
                    }
                }
                catch (Exception exc)
                {
                    LogfileProblemreportWrite("Situation: " + Program.IWET.ToString() + " Error writing Receptor_Timeseries_Transient.txt file: " + exc.Message);
                }
                finally
                {
                    StB = null;
                    inhalt.Clear();
                    inhalt.TrimExcess();
                }
            }
        }//receptor timeseries

        /// <summary>
        ///Output of microscale flow-field at receptors points into GRAL_Meteozeitreihe.dat
        /// </summary>
        public void WriteMicroscaleFlowfieldReceptors()
        {
            if ((Program.ReceptorsAvailable > 0) && (Program.FlowFieldLevel > 0))
            {
                try
                {
                    CultureInfo ic = CultureInfo.InvariantCulture;

                    List<string> content = new List<string>();

                    //read the existing header and content up to the recent Program.IWET - prevent errors if a user restarts at a previous weather situation
                    if (Program.IWET > 1 && File.Exists("GRAL_Meteozeitreihe.dat"))
                    {
                        using (FileStream wr = new FileStream("GRAL_Meteozeitreihe.dat", FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            using (StreamReader read = new StreamReader(wr))
                            {
                                // read Header lines
                                try
                                {
                                    string header = read.ReadLine();
                                    if (header.EndsWith("+")) // additional header lines for all receptors
                                    {
                                        content.Add(header);
                                        content.Add(read.ReadLine()); // Name
                                        content.Add(read.ReadLine()); // X
                                        content.Add(read.ReadLine()); // Y
                                        content.Add(read.ReadLine()); // Z
                                    }
                                    else if (header.Contains("U")) // old one line header, otherwise very old and no header
                                    {
                                        content.Add(header);
                                    }
                                }
                                catch
                                {
                                    //content.Add("0");
                                }

                                for (int ianz = 1; ianz <= Program.IWET; ianz++)
                                {
                                    try
                                    {
                                        content.Add(read.ReadLine()); // add existing data
                                    }
                                    catch
                                    {
                                        content.Add("0"); // add 0 if a user continues with a later weather situations
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // create a new header
                        content.Add("U,V,SC,BLH+");
                        string[] headerLine = ReceptorMeteoCreateMeteoHeader(4);
                        foreach (string h in headerLine)
                        {
                            content.Add(h);
                        }
                    }

                    using (StreamWriter write = new StreamWriter("GRAL_Meteozeitreihe.dat", false))
                    {
                        // write header and entire file up to (Program.IWET - 1)
                        for (int ianz = 0; ianz < content.Count; ianz++)
                        {
                            write.WriteLine(content[ianz]);
                        }

                        for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                        {
                            if (Program.Topo == 1 && Program.AKL_GRAMM[0, 0] != 0) // 11.9.2017 Kuntner Write Local SCL
                            {
                                int I_SC = (int)((Program.ReceptorX[ianz] - Program.GrammWest) / Program.DDX[1]) + 1;
                                int J_SC = (int)((Program.ReceptorY[ianz] - Program.GrammSouth) / Program.DDY[1]) + 1;

                                if (I_SC >= 0 && I_SC <= Program.NX1 && J_SC >= 0 && J_SC <= Program.NY1)
                                {
                                    write.Write(Program.UK[Program.ReceptorIIndFF[ianz]][Program.ReceptorJIndFF[ianz]][Program.ReceptorKIndFF[ianz]].ToString("0.00", ic) +
                                                "\t" + Program.VK[Program.ReceptorIIndFF[ianz]][Program.ReceptorJIndFF[ianz]][Program.ReceptorKIndFF[ianz]].ToString("0.00", ic) +
                                                "\t" + Program.SC_Gral[I_SC][J_SC].ToString(ic) +
                                                "\t" + Convert.ToInt32(Program.BdLayHeight).ToString(ic) + "\t");
                                }
                                else // invalid indices -> write mean SCL or meteogpt SCL
                                {
                                    int Ak;
                                    if (Program.StabClassGramm > 0)
                                    {
                                        Ak = Program.StabClassGramm;
                                    }
                                    else
                                    {
                                        Ak = Program.StabClass;
                                    }

                                    write.Write(Program.UK[Program.ReceptorIIndFF[ianz]][Program.ReceptorJIndFF[ianz]][Program.ReceptorKIndFF[ianz]].ToString("0.00", ic) +
                                                "\t" + Program.VK[Program.ReceptorIIndFF[ianz]][Program.ReceptorJIndFF[ianz]][Program.ReceptorKIndFF[ianz]].ToString("0.00", ic) +
                                                "\t" + Ak.ToString(ic) +
                                                "\t" + Convert.ToInt32(Program.BdLayHeight).ToString(ic) + "\t");
                                }
                            }
                            else // original format without SCL
                            {
                                int Ak = Program.StabClass;
                                write.Write(Program.UK[Program.ReceptorIIndFF[ianz]][Program.ReceptorJIndFF[ianz]][Program.ReceptorKIndFF[ianz]].ToString("0.00", ic) +
                                      "\t" + Program.VK[Program.ReceptorIIndFF[ianz]][Program.ReceptorJIndFF[ianz]][Program.ReceptorKIndFF[ianz]].ToString("0.00", ic) +
                                      "\t" + Ak.ToString(ic) +
                                      "\t" + Convert.ToInt32(Program.BdLayHeight).ToString(ic) + "\t");
                            }
                        }
                    }
                }
                catch { }

                for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
                {
                    for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                    {
                        Program.ReceptorConc[ianz][iq] = 0;
                    }
                }
            }
        }//microscale flow-field at receptors

        /// <summary>
        ///Create a header for the file GRAL_Meteozeitreihe
        /// </summary>
        private string[] ReceptorMeteoCreateMeteoHeader(int mode)
        {
            CultureInfo ic = CultureInfo.InvariantCulture;

            string[] headerLine = new string[5];
            string tabs = new string('\t', mode);

            for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
            {
                if ((ianz - 1) < Program.ReceptorName.Count)
                {
                    headerLine[0] += Program.ReceptorName[ianz - 1] + tabs;
                }
                else
                {
                    headerLine[0] += "Rec. " + ianz.ToString() + tabs;
                }
                headerLine[1] += Math.Round(Program.ReceptorX[ianz], 1).ToString(ic) + tabs;
                headerLine[2] += Math.Round(Program.ReceptorY[ianz], 1).ToString(ic) + tabs;
                headerLine[3] += Math.Round(Program.ReceptorZ[ianz], 1).ToString(ic) + tabs;
                if (mode == 3)
                {
                    headerLine[4] += "U\tV\tBLH\t";
                }
                else if (mode == 4)
                {
                    headerLine[4] += "U\tV\tSC\tBLH\t";
                }
            }
            return headerLine;
        }

        /// <summary>
        ///Create a header for the file ReceptorConcetration.dat
        /// </summary>
        private string[] ReceptorConcentrationCreateMeteoHeader()
        {
            CultureInfo ic = CultureInfo.InvariantCulture;

            string[] headerLine = new string[6];
            string tabs = "\t";

            for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
            {
                for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                {
                    if ((ianz - 1) < Program.ReceptorName.Count)
                    {
                        headerLine[0] += Program.ReceptorName[ianz - 1] + tabs;
                    }
                    else
                    {
                        headerLine[0] += "Rec. " + ianz.ToString(ic) + tabs;
                    }
                    headerLine[1] += Program.SourceGroups[iq].ToString(ic) + tabs;
                    headerLine[2] += Math.Round(Program.ReceptorX[ianz], 1).ToString(ic) + tabs;
                    headerLine[3] += Math.Round(Program.ReceptorY[ianz], 1).ToString(ic) + tabs;
                    headerLine[4] += Math.Round(Program.ReceptorZ[ianz], 1).ToString(ic) + tabs;
                    headerLine[5] += "-----" + tabs;
                }
            }
            return headerLine;
        }
    }
}
