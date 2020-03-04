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
using System.Globalization;
using System.IO.Compression;
using System.IO;

namespace GRAL_2001
{
    public class ProgramWriters
    {
        private static int month_old = 0;
        private static int day_old = 0;
        private static int hour_old = 0;
        private static int minute_old = 0;


        /// <summary>
        ///In case of complex terrain and/or the presence of buildings some data is written for usage in the GUI (visualization of vertical slices)
        /// </summary>
        public void WriteGRALGeometries()
        {
            if ((Program.Topo == 1) || (Program.BuildingFlatExist == true) || (Program.BuildingTerrExist == true))
            {
                //write geometry data
                string GRALgeom = "GRAL_geometries.txt";
                try
                {
                    using (BinaryWriter writer = new BinaryWriter(File.Open(GRALgeom, FileMode.Create)))
                    {
                        writer.Write((Int32)Program.NKK);
                        writer.Write((Int32)Program.NJJ);
                        writer.Write((Int32)Program.NII);
                        writer.Write((Int32)Program.IKOOAGRAL);
                        writer.Write((Int32)Program.JKOOAGRAL);
                        writer.Write((float)Program.DZK[1]);
                        writer.Write((float)Program.StretchFF);
                        if (Program.StretchFF < 0.1)
                        {
                            writer.Write((int)Program.StretchFlexible.Count);
                            foreach (float[] stretchflex in Program.StretchFlexible)
                            {
                                writer.Write((float)stretchflex[0]);
                                writer.Write((float)stretchflex[1]);
                            }
                        }
                        writer.Write((float)Program.AHMIN);

                        for (int i = 1; i <= Program.NII + 1; i++)
                        {
                            for (int j = 1; j <= Program.NJJ + 1; j++)
                            {
                                writer.Write((float)Program.AHK[i][j]);
                                writer.Write((Int32)Program.KKART[i][j]);
                                writer.Write((float)Program.BUI_HEIGHT[i][j]);
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when writing file " + GRALgeom;
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                    Environment.Exit(0);
                }
            }
        }//in case of complex terrain and/or the presence of buildings some data is written for usage in the GUI (visualization of vertical slices)

        /// <summary>
        ///optional: write building heights as utilized in GRAL
        /// </summary>
        public void WriteBuildingHeights()
        {
            if ((Math.Abs(Program.IOUTPUT) > 1) && (Program.FlowFieldLevel > 0))
            {
                try
                {
                    CultureInfo ic = CultureInfo.InvariantCulture;
                    using (StreamWriter wt = new StreamWriter("building_heights.txt"))
                    {
                        wt.WriteLine("ncols         " + Program.NII.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("nrows         " + Program.NJJ.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("xllcorner     " + Program.IKOOAGRAL.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("yllcorner     " + Program.JKOOAGRAL.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("cellsize      " + Program.DXK.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("NODATA_value  " + "-9999");

                        StringBuilder SB = new StringBuilder();
                        for (int jj = Program.NJJ; jj >= 1; jj--)
                        {
                            for (int o = 1; o <= Program.NII; o++)
                            {
                                SB.Append(Math.Round(Program.BUI_HEIGHT[o][jj], 1).ToString("0.0", ic));
                                SB.Append(" ");
                            }
                            wt.WriteLine(SB.ToString());
                            SB.Clear();
                        }
                    }
                }
                catch { }
            }
        }//optional: write building heights as utilized in GRAL

        /// <summary>
        ///Output of 2-D concentration files (concentrations, deposition, odour-files)
        /// </summary>
        public void Write2DConcentrations()
        {
            try
            {
                if (Program.ResultFileZipped)
                {
                    using (FileStream zipToOpen = new FileStream(Program.ZippedFile, FileMode.OpenOrCreate))
                    {
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                        {
                            for (int IQ = 0; IQ < Program.SourceGroups.Count; IQ++)
                            {
                                for (int II = 1; II <= Program.NS; II++)
                                {
                                    string fname = Program.IWET.ToString("00000") + "-" + II.ToString("0") + Program.SourceGroups[IQ].ToString("00") + ".con";

                                    if (Program.WriteASCiiResults) // aditional ASCii Output
                                    {
                                        writeConDataAscii(fname, IQ, II);
                                    }

                                    ZipArchiveEntry write_entry = archive.CreateEntry(fname);

                                    using (BinaryWriter sw = new BinaryWriter(write_entry.Open()))
                                    {
                                        if (Program.ResultFileHeader == -2)
                                        {
                                            WriteConcentrationDataStrongCompressed(sw, IQ, II);
                                        }
                                        else if (Program.ResultFileHeader == -3)
                                        {
                                            WriteConcentrationDataAllCompressed(sw, IQ, II);
                                        }
                                        else
                                        {
                                            WriteConcentrationData(sw, IQ, II);
                                        }
                                    }

                                    // Write deposition
                                    if (II == 1 && (Program.DepositionExist || Program.WetDeposition))
                                    {
                                        fname = Program.IWET.ToString("00000") + "-" + Program.SourceGroups[IQ].ToString("00") + ".dep";
                                        write_entry = archive.CreateEntry(fname);

                                        using (BinaryWriter sw = new BinaryWriter(write_entry.Open()))
                                        {
                                            if (Program.ResultFileHeader == -2)
                                            {
                                                WriteDepositionStrongCompressed(sw, IQ);
                                            }
                                            else if (Program.ResultFileHeader == -3)
                                            {
                                                WriteDepositionAllCompressed(sw, IQ);
                                            }
                                            else
                                            {
                                                WriteDepositionData(sw, IQ);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else // not zipped
                {
                    for (int IQ = 0; IQ < Program.SourceGroups.Count; IQ++)
                    {
                        for (int II = 1; II <= Program.NS; II++)
                        {
                            string fname = Program.IWET.ToString("00000") + "-" + II.ToString("0") + Program.SourceGroups[IQ].ToString("00") + ".con";

                            if (Program.WriteASCiiResults) // aditional ASCii Output
                            {
                                writeConDataAscii(fname, IQ, II);
                            }

                            using (BinaryWriter sw = new BinaryWriter(File.Open(fname, FileMode.Create)))
                            {
                                WriteConcentrationData(sw, IQ, II);
                            }

                            // Write deposition
                            if (II == 1 && (Program.DepositionExist || Program.WetDeposition))
                            {
                                fname = Program.IWET.ToString("00000") + "-" + Program.SourceGroups[IQ].ToString("00") + ".dep";
                                using (BinaryWriter sw = new BinaryWriter(File.Open(fname, FileMode.Create)))
                                {
                                    WriteDepositionData(sw, IQ);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                LogfileProblemreportWrite("Situation: " + Program.IWET.ToString() + " Error writing con file: " + exc.Message);
            } //Output of concentration files


            /// <summary>
            ///output of 2-D concentration files above selected layer and some other quantities, needed to compute R90 in odour simulations
            /// </summary>
            if (Program.Odour == true)
            {
                double Nhor = Program.NXL * Program.NYL;

                for (int IQ = 0; IQ < Program.SourceGroups.Count; IQ++)
                {
                    for (int II = 1; II <= Program.NS; II++)
                    {
                        Object thisLock = new Object();

                        Parallel.For(1, Program.NXL + 1, Program.pOptions, i =>
                        {
                            float Q_cv0_L = 0;
                            float td_L = 0;

                            for (int j = 1; j <= Program.NYL; j++)
                            {
                                int IUst = 1;
                                int JUst = 1;
                                int IndexI = 1;
                                int IndexJ = 1;
                                int IndexId = 1;
                                int IndexJd = 1;
                                int IndexK = 1;
                                float UXint = 0, UYint = 0, UZint = 0, U0int = 0, V0int = 0;
                                float W0int = 0;
                                double xco = Convert.ToDouble((float)i * Program.dx - 0.5 * Program.dx + Program.IKOOAGRAL);
                                double yco = Convert.ToDouble((float)j * Program.dy - 0.5 * Program.dy + Program.JKOOAGRAL);

                                //horizontal cell indices
                                double xsi1 = (float)i * Program.dx - 0.5 * Program.dx + Program.IKOOAGRAL - Program.IKOOA;
                                double eta1 = (float)j * Program.dy - 0.5 * Program.dy + Program.JKOOAGRAL - Program.JKOOA;
                                if (Program.Topo == 1)
                                {
                                    double xsi = xco - Program.IKOOAGRAL;
                                    double eta = yco - Program.JKOOAGRAL;
                                    IUst = (int)(xsi1 / Program.DDX[1]) + 1;
                                    JUst = (int)(eta1 / Program.DDY[1]) + 1;
                                    IndexId = (int)(xsi / Program.DXK) + 1;
                                    IndexJd = (int)(eta / Program.DYK) + 1;
                                    IndexI = (int)(xsi / Program.DXK) + 1;
                                    IndexJ = (int)(eta / Program.DYK) + 1;
                                }
                                else
                                {
                                    double xsi = xco - Program.IKOOAGRAL;
                                    double eta = yco - Program.JKOOAGRAL;
                                    IndexId = (int)(xsi / Program.DXK) + 1;
                                    IndexJd = (int)(eta / Program.DYK) + 1;
                                }
                                //surface height
                                float AHint = 0;
                                if (Program.Topo == 1)
                                {
                                    AHint = Program.AHK[IndexId][IndexJd];
                                }
                                //wind speed
                                Zeitschleife.IntWindCalculate(IndexI, IndexJ, IndexId, IndexJd, AHint, ref UXint, ref UYint, ref UZint, ref IndexK, xco, yco, Program.HorSlices[II]);
                                float windge = (float)Math.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint));
                                if (windge <= 0) windge = 0.01F;

                                //horizontal wind standard deviations
                                Zeitschleife.IntStandCalculate(1, IUst, JUst, Program.HorSlices[II], windge, 0, ref U0int, ref V0int);

                                //vertical wind standard deviation
                                if (Program.IStatistics == 3)
                                    W0int = Program.W0int;
                                else
                                {
                                    if (Program.Ob[IUst][JUst] >= 0)
                                    {
                                        W0int = Program.Ustern[IUst][JUst] * Program.StdDeviationW * 1.25F;
                                    }
                                    else
                                    {
                                        W0int = (float)(Program.Ustern[IUst][JUst] * (1.15F + 0.1F * Math.Pow(Program.BdLayHeight / (-Program.Ob[IUst][JUst]), 0.67)) * Program.StdDeviationW);
                                    }
                                }

                                //dissipation
                                float eps = (float)(Program.Pow3(Program.Ustern[IUst][JUst]) / Math.Max(Program.HorSlices[II], Program.Z0Gramm[IUst][JUst]) / 0.4F *
                                                    Math.Pow(1 + 0.5F * Math.Pow(Program.HorSlices[II] / Math.Abs(Program.Ob[IUst][JUst]), 0.8), 1.8));

                                Q_cv0_L += (float)(4 / eps * (Program.Pow4(U0int) / 3 + Program.Pow4(V0int) / 3 + Program.Pow4(W0int) / 4));
                                td_L += (float)(2 * Program.Pow2(W0int) / 4 / eps);
                            } // j - loop

                            lock (thisLock)
                            {
                                //source-term for the concentration variance (the term is probably wrongly calculated -> should be stored separately for each direction and then multiplied with the corresponding concentration gradient
                                Program.Q_cv0[II][IQ] += Q_cv0_L;

                                //dissipation time-scale set equal to the vertical lagrangian time-scale; note the proportionality constant is different in various publications
                                Program.DisConcVar[II][IQ] += td_L;
                            }

                        });

                        thisLock = null;

                        Program.Q_cv0[II][IQ] /= (float)Nhor;
                        Program.DisConcVar[II][IQ] /= (float)Nhor;

                        //output of several quantities needed to run the concentration variance model subsequently
                        string fname = Program.IWET.ToString("00000") + "-" + II.ToString("0") + Program.SourceGroups[IQ].ToString("00") + ".odr";
                        try
                        {
                            if (Program.ResultFileZipped)
                            {
                                using (FileStream zipToOpen = new FileStream(Program.ZippedFile, FileMode.OpenOrCreate))
                                {
                                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                                    {
                                        ZipArchiveEntry write_entry = archive.CreateEntry(fname);

                                        using (BinaryWriter sw = new BinaryWriter(write_entry.Open()))
                                        {
                                            if (Program.ResultFileHeader == -2)
                                            {
                                                WriteOdourDataStrongCompressed(sw, IQ, II);
                                            }
                                            else if (Program.ResultFileHeader == -3)
                                            {
                                                WriteOdourDataAllCompressed(sw, IQ, II);
                                            }
                                            else
                                            {
                                                WriteOdourData(sw, IQ, II);
                                            }
                                        }
                                    }
                                }
                            }
                            else //unzipped files
                            {
                                using (BinaryWriter sw = new BinaryWriter(File.Open(fname, FileMode.Create)))
                                {
                                    WriteOdourData(sw, IQ, II);
                                }
                            }
                        }
                        catch (Exception exc)
                        {
                            if (IQ == 1)
                            {
                                LogfileProblemreportWrite("Situation: " + Program.IWET.ToString() + " Error writing odr file: " + exc.Message);
                            }
                        }
                    }
                }
            }

            //reset concentrations
            for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
            {
                for (int II = 1; II <= Program.NS; II++)
                {
                    for (int i = 1; i <= Program.NXL; i++)
                    {
                        for (int j = 1; j <= Program.NYL; j++)
                        {
                            Program.Conz3d[i][j][II][iq] = 0;
                            Program.Depo_conz[i][j][iq] = 0;
                            if (Program.Odour == true)
                            {
                                Program.Conz3dp[i][j][II][iq] = 0;
                                Program.Conz3dm[i][j][II][iq] = 0;
                                Program.Q_cv0[II][iq] = 0;
                                Program.DisConcVar[II][iq] = 0;
                            }
                        }
                    }
                }
            }
        }//output of 2-D concentration files (concentrations, deposition, odour-files)

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
                    using (FileStream wr = new FileStream("zeitreihe.dat", FileMode.OpenOrCreate))
                    {
                        StreamReader read = new StreamReader(wr);
                        List<string> inhalt = new List<string>();

                        for (int ianz = 1; ianz <= Program.IWET - 1; ianz++)
                        {
                            try
                            {
                                inhalt.Add(read.ReadLine().ToString(ic));
                            }
                            catch
                            {
                                inhalt.Add("0");
                            }
                        }
                        read.Close();
                        read.Dispose();

                        using (StreamWriter write = new StreamWriter("zeitreihe.dat", false))
                        {
                            for (int ianz = 1; ianz <= Program.IWET - 1; ianz++)
                            {
                                write.WriteLine(inhalt[ianz - 1]);
                            }

                            for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
                            {
                                //write.Write("NQI " + NQi[iq].ToString(ic) + " ");
                                for (int ianz = 1; ianz <= Program.ReceptorNumber; ianz++)
                                {
                                    write.Write(Program.ReceptorConc[ianz][iq].ToString(ic) + " ");
                                }
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    LogfileProblemreportWrite("Situation: " + Program.IWET.ToString() + " Error writing zeitreihe.dat file: " + exc.Message);
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

                                for (int ianz = 1; ianz <= Program.IWET - 1; ianz++)
                                {
                                    try
                                    {
                                        inhalt.Add(read.ReadLine().ToString());
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
                                        int I_SC = (int)((Program.ReceptorX[ianz] - Program.IKOOA) / Program.DDX[1]) + 1;
                                        int J_SC = (int)((Program.ReceptorY[ianz] - Program.JKOOA) / Program.DDY[1]) + 1;
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
                                                Ak = Program.StabClassGramm;
                                            else
                                                Ak = Program.StabClass;
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
                                        content.Add(read.ReadLine().ToString(ic)); // add existing data
                                    }
                                    catch
                                    {
                                        content.Add("0"); // add 0 if a user continues with later weater situations
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // create a new header
                        content.Add("U,V,SC,BLH+");
                        string[] headerLine = CreateMeteoHeader(4);
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
                                int I_SC = (int)((Program.ReceptorX[ianz] - Program.IKOOA) / Program.DDX[1]) + 1;
                                int J_SC = (int)((Program.ReceptorY[ianz] - Program.JKOOA) / Program.DDY[1]) + 1;

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
                                        Ak = Program.StabClassGramm;
                                    else
                                        Ak = Program.StabClass;

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
        private string[] CreateMeteoHeader(int mode)
        {
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
                    headerLine[0] += "Rec. " + ianz.ToString() + "/SG: " + tabs;
                }
                headerLine[1] += Math.Round(Program.ReceptorX[ianz], 1).ToString() + tabs;
                headerLine[2] += Math.Round(Program.ReceptorY[ianz], 1).ToString() + tabs;
                headerLine[3] += Math.Round(Program.ReceptorZ[ianz], 1).ToString() + tabs;
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
        ///Output of deposition values
        /// </summary>
        private static bool WriteDepositionData(BinaryWriter sw, int IQ)
        {
            try
            {
                //flag for stream file (not for SOUNDPLAN)
                if (Program.IOUTPUT <= 0) sw.Write(Program.ResultFileHeader);

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        float xp1 = i * Program.dx - Program.dx * 0.5F;
                        float yp1 = j * Program.dy - Program.dy * 0.5F;
                        float xxx = xp1 + Program.IKOOAGRAL;
                        float yyy = yp1 + Program.JKOOAGRAL;

                        if (Program.IOUTPUT <= 0)
                        {
                            if (Program.Depo_conz[i][j][IQ] != 0)
                            {
                                sw.Write(Program.ConvToInt(xxx));
                                sw.Write(Program.ConvToInt(yyy));
                                sw.Write((float)Program.Depo_conz[i][j][IQ]);
                            }
                        }
                        //SOUNDPLAN
                        else if (Program.IOUTPUT > 0)
                        {
                            sw.Write((int)xxx);
                            sw.Write((int)yyy);
                            sw.Write((float)Program.Depo_conz[i][j][IQ]);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///Output of deposition values
        /// </summary>
        private static bool WriteDepositionStrongCompressed(BinaryWriter sw, int IQ)
        {
            try
            {
                //flag for stream file 
                sw.Write(Program.ResultFileHeader);
                sw.Write(Program.IKOOAGRAL);
                sw.Write(Program.JKOOAGRAL);
                sw.Write(Program.dx);
                sw.Write(Program.dy);

                int[] Start = new int[2];
                float[] Remember = new float[Program.NYL + 5];

                for (int i = 1; i <= Program.NXL; i++)
                {
                    int count = 0;
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        double _value = Program.Depo_conz[i][j][IQ];
                        if (_value > 1E-15)
                        {
                            if (count == 0) // write new start
                            {
                                Start[0] = i;
                                Start[1] = j;
                                Remember[count++] = (float)_value;
                            }
                            else
                            {
                                Remember[count++] = (float)_value;
                            }
                        }
                        else
                        {
                            if (count > 0)
                            {
                                if (!WriteStreamStrongCompressed(sw, Remember, Start, count))
                                {
                                    throw new IOException();
                                }
                            }
                            count = 0;
                        }
                    }
                    if (count > 0)
                    {
                        if (!WriteStreamStrongCompressed(sw, Remember, Start, count))
                        {
                            throw new IOException();
                        }
                    }
                }

                int c = -1;
                sw.Write(c);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///Output of deposition values all cells
        /// </summary>
        private static bool WriteDepositionAllCompressed(BinaryWriter sw, int IQ)
        {
            try
            {
                //flag for stream file 
                sw.Write(Program.ResultFileHeader);
                sw.Write(Program.IKOOAGRAL);
                sw.Write(Program.JKOOAGRAL);
                sw.Write(Program.dx);
                sw.Write(Program.dy);
                sw.Write(Program.NXL);
                sw.Write(Program.NYL);

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        sw.Write(Program.Depo_conz[i][j][IQ]);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///Output of concentration values
        /// </summary>
        private static bool WriteConcentrationData(BinaryWriter sw, int IQ, int II)
        {
            try
            {
                //flag for stream file (not for SOUNDPLAN)
                if (Program.IOUTPUT <= 0) sw.Write(Program.ResultFileHeader);

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        float xp1 = i * Program.dx - Program.dx * 0.5F;
                        float yp1 = j * Program.dy - Program.dy * 0.5F;
                        float xxx = xp1 + Program.IKOOAGRAL;
                        float yyy = yp1 + Program.JKOOAGRAL;

                        if (Program.IOUTPUT <= 0)
                        {
                            if (Program.Conz3d[i][j][II][IQ] != 0)
                            {
                                sw.Write(Program.ConvToInt(xxx));
                                sw.Write(Program.ConvToInt(yyy));
                                sw.Write((float)Program.Conz3d[i][j][II][IQ]);
                            }
                        }
                        //SOUNDPLAN
                        else if (Program.IOUTPUT > 0)
                        {
                            sw.Write((int)xxx);
                            sw.Write((int)yyy);
                            sw.Write((float)Program.Conz3d[i][j][II][IQ]);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///Output of concentration values
        /// </summary>
        private static bool WriteConcentrationDataStrongCompressed(BinaryWriter sw, int IQ, int II)
        {
            try
            {
                //flag for stream file 
                sw.Write(Program.ResultFileHeader);
                sw.Write(Program.IKOOAGRAL);
                sw.Write(Program.JKOOAGRAL);
                sw.Write(Program.dx);
                sw.Write(Program.dy);

                int[] Start = new int[2];
                float[] Remember = new float[Program.NYL + 2];

                for (int i = 1; i <= Program.NXL; i++)
                {
                    int count = 0;
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        // float xp1 = i * Program.dx - Program.dx * 0.5F;
                        // float yp1 = j * Program.dy - Program.dy * 0.5F;
                        // double xxx = xp1 + Program.IKOOAGRAL;
                        // double yyy = yp1 + Program.JKOOAGRAL;
                        float _value = Program.Conz3d[i][j][II][IQ];
                        if (_value != 0)
                        {
                            if (count == 0) // write new start
                            {
                                Start[0] = i;
                                Start[1] = j;
                                Remember[count++] = (float)_value;
                            }
                            else
                            {
                                Remember[count++] = (float)_value;
                            }
                        }
                        else
                        {
                            if (count > 0)
                            {
                                if (!WriteStreamStrongCompressed(sw, Remember, Start, count))
                                {
                                    throw new IOException();
                                }
                            }
                            count = 0;
                        }
                    }
                    if (count > 0)
                    {
                        if (!WriteStreamStrongCompressed(sw, Remember, Start, count))
                        {
                            throw new IOException();
                        }
                    }
                }

                int c = -1;
                sw.Write(c);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool WriteStreamStrongCompressed(BinaryWriter sw, float[] array, int[] Start, int lenght)
        {
            try
            {
                sw.Write(lenght);
                sw.Write(Start[0]);
                sw.Write(Start[1]);

                for (int i = 0; i < lenght; i++)
                {
                    sw.Write(array[i]);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///Output of concentration values all cells
        /// </summary>
        private static bool WriteConcentrationDataAllCompressed(BinaryWriter sw, int IQ, int II)
        {
            try
            {
                //flag for stream file 
                sw.Write(Program.ResultFileHeader);
                sw.Write(Program.IKOOAGRAL);
                sw.Write(Program.JKOOAGRAL);
                sw.Write(Program.dx);
                sw.Write(Program.dy);
                sw.Write(Program.NXL);
                sw.Write(Program.NYL);

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        sw.Write(Program.Conz3d[i][j][II][IQ]);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///output of Concentration Data in Ascii format
        /// </summary>
        private static bool writeConDataAscii(string Filename, int IQ, int II)
        {
            CultureInfo ic = CultureInfo.InvariantCulture;
            try
            {
                Filename = Path.GetFileNameWithoutExtension(Filename) + ".txt";
                StringBuilder SB = new StringBuilder();
                using (StreamWriter myWriter = new StreamWriter(Filename))
                {
                    // Header
                    myWriter.WriteLine("ncols         " + Convert.ToString(Program.NXL, ic));
                    myWriter.WriteLine("nrows         " + Convert.ToString(Program.NYL, ic));
                    myWriter.WriteLine("xllcorner     " + Convert.ToString(Program.GralWest, ic));
                    myWriter.WriteLine("yllcorner     " + Convert.ToString(Program.GralSouth, ic));
                    myWriter.WriteLine("cellsize      " + Convert.ToString(Program.dx, ic));
                    myWriter.WriteLine("NODATA_value  -9999");

                    for (int j = Program.NYL; j > 0; j--)
                    {
                        SB.Clear();
                        for (int i = 1; i <= Program.NXL; i++)
                        {
                            SB.Append(Program.Conz3d[i][j][II][IQ].ToString(ic));
                            SB.Append(" ");
                        }
                        myWriter.WriteLine(SB.ToString());
                    }

                }
                SB = null;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool writeDepDataAscii(string Filename, int IQ)
        {
            CultureInfo ic = CultureInfo.InvariantCulture;
            try
            {
                Filename = Path.GetFileNameWithoutExtension(Filename) + "_Dep.txt";
                StringBuilder SB = new StringBuilder();
                using (StreamWriter myWriter = new StreamWriter(Filename))
                {
                    // Header
                    myWriter.WriteLine("ncols         " + Convert.ToString(Program.NXL, ic));
                    myWriter.WriteLine("nrows         " + Convert.ToString(Program.NYL, ic));
                    myWriter.WriteLine("xllcorner     " + Convert.ToString(Program.GralWest, ic));
                    myWriter.WriteLine("yllcorner     " + Convert.ToString(Program.GralSouth, ic));
                    myWriter.WriteLine("cellsize      " + Convert.ToString(Program.dx, ic));
                    myWriter.WriteLine("NODATA_value  -9999");

                    for (int j = Program.NYL; j > 0; j--)
                    {
                        SB.Clear();
                        for (int i = 1; i <= Program.NXL; i++)
                        {
                            SB.Append(Program.Depo_conz[i][j][IQ].ToString(ic));
                            SB.Append(" ");
                        }
                        myWriter.WriteLine(SB.ToString());
                    }
                }
                SB = null;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///output of odour concentrations
        /// </summary>
        private static bool WriteOdourData(BinaryWriter sw, int IQ, int II)
        {
            try
            {
                //flag for stream file (not for SOUNDPLAN)
                if (Program.IOUTPUT <= 0) sw.Write(Program.ResultFileHeader);

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        float xp1 = i * Program.dx - Program.dx * 0.5F;
                        float yp1 = j * Program.dy - Program.dy * 0.5F;
                        float xxx = xp1 + Program.IKOOAGRAL;
                        float yyy = yp1 + Program.JKOOAGRAL;

                        if (Program.IOUTPUT <= 0)
                        {
                            if (Program.Conz3d[i][j][II][IQ] != 0)
                            {
                                sw.Write(Program.ConvToInt(xxx));
                                sw.Write(Program.ConvToInt(yyy));
                                sw.Write((float)Program.Conz3dp[i][j][II][IQ]);
                                sw.Write((float)Program.Conz3dm[i][j][II][IQ]);
                                sw.Write((float)Program.Q_cv0[II][IQ]);
                                sw.Write((float)Program.DisConcVar[II][IQ]);
                            }
                        }
                        //SOUNDPLAN
                        else if (Program.IOUTPUT > 0)
                        {
                            sw.Write((int)xxx);
                            sw.Write((int)yyy);
                            sw.Write((float)Program.Conz3dp[i][j][II][IQ]);
                            sw.Write((float)Program.Conz3dm[i][j][II][IQ]);
                            sw.Write((float)Program.Q_cv0[II][IQ]);
                            sw.Write((float)Program.DisConcVar[II][IQ]);
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///output of odour concentrations
        /// </summary>
        private static bool WriteOdourDataStrongCompressed(BinaryWriter sw, int IQ, int II)
        {
            try
            {
                //flag for stream file 
                sw.Write(Program.ResultFileHeader);
                sw.Write(Program.IKOOAGRAL);
                sw.Write(Program.JKOOAGRAL);
                sw.Write(Program.dx);
                sw.Write(Program.dy);

                int[] Start = new int[2];
                float[] Remember = new float[Program.NYL * 4 + 5];

                for (int i = 1; i <= Program.NXL; i++)
                {
                    int count = 0;
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        if (Program.Conz3d[i][j][II][IQ] != 0)
                        {
                            if (count == 0) // write new start
                            {
                                Start[0] = i;
                                Start[1] = j;
                                Remember[count++] = (float)Program.Conz3dp[i][j][II][IQ];
                                Remember[count++] = (float)Program.Conz3dm[i][j][II][IQ];
                                Remember[count++] = (float)Program.Q_cv0[II][IQ];
                                Remember[count++] = (float)Program.DisConcVar[II][IQ];
                            }
                            else
                            {
                                Remember[count++] = (float)Program.Conz3dp[i][j][II][IQ];
                                Remember[count++] = (float)Program.Conz3dm[i][j][II][IQ];
                                Remember[count++] = (float)Program.Q_cv0[II][IQ];
                                Remember[count++] = (float)Program.DisConcVar[II][IQ];
                            }
                        }
                        else
                        {
                            if (count > 0)
                            {
                                if (!WriteStreamStrongCompressed(sw, Remember, Start, count))
                                {
                                    throw new IOException();
                                }
                            }
                            count = 0;
                        }
                    }
                    if (count > 0)
                    {
                        if (!WriteStreamStrongCompressed(sw, Remember, Start, count))
                        {
                            throw new IOException();
                        }
                    }
                }

                int c = -1;
                sw.Write(c);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///output of odour concentrations all cells
        /// </summary>
        private static bool WriteOdourDataAllCompressed(BinaryWriter sw, int IQ, int II)
        {
            try
            {
                //flag for stream file 
                sw.Write(Program.ResultFileHeader);
                sw.Write(Program.IKOOAGRAL);
                sw.Write(Program.JKOOAGRAL);
                sw.Write(Program.dx);
                sw.Write(Program.dy);
                sw.Write(Program.NXL);
                sw.Write(Program.NYL);

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        if (Program.Conz3d[i][j][II][IQ] != 0)
                        {
                            sw.Write((float)Program.Conz3dp[i][j][II][IQ]);
                            sw.Write((float)Program.Conz3dm[i][j][II][IQ]);
                            sw.Write((float)Program.Q_cv0[II][IQ]);
                            sw.Write((float)Program.DisConcVar[II][IQ]);
                        }
                        else
                        {
                            sw.Write((float)0F);
                            sw.Write((float)0F);
                            sw.Write((float)0F);
                            sw.Write((float)0F);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///Output of 3-D concentration files (concentrations)
        /// </summary>
        public void Write3DConcentrations()
        {
            string fname = Program.IWET.ToString("00000") + ".c3d";
            try
            {
                using (FileStream zipToOpen = new FileStream(fname, FileMode.OpenOrCreate))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        ZipArchiveEntry write_entry = archive.CreateEntry(fname);

                        using (BinaryWriter sw = new BinaryWriter(write_entry.Open()))
                        {
                            sw.Write((Int32)Program.NKK);
                            sw.Write((Int32)Program.NJJ);
                            sw.Write((Int32)Program.NII);
                            sw.Write((float)Program.DXK);

                            for (int i = 1; i <= Program.NII + 1; i++)
                            {
                                float xp1 = i * Program.DXK - Program.DXK * 0.5F;
                                float xxx = xp1 + Program.IKOOAGRAL;
                                sw.Write(xxx);

                                for (int j = 1; j <= Program.NJJ + 1; j++)
                                {
                                    float yp1 = j * Program.DYK - Program.DYK * 0.5F;
                                    float yyy = yp1 + Program.JKOOAGRAL;
                                    sw.Write(yyy);
                                    sw.Write((float)Program.AHK[i][j]);

                                    for (int k = 1; k <= Program.NKK_Transient; k++)
                                    {
                                        float zp1 = Program.AHK[i][j] + Program.HoKartTrans[k] - Program.DZK_Trans[k] * 0.5f;
                                        sw.Write(zp1);

                                        float conz_sum = 0;
                                        for (int IQ = 0; IQ < Program.SourceGroups.Count; IQ++)
                                        {
                                            conz_sum += Program.Conz5d[i][j][k][IQ];
                                        }
                                        //output
                                        sw.Write(conz_sum);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                LogfileProblemreportWrite("Situation: " + Program.IWET.ToString() + " Error writing 3D-concentration file: " + exc.Message);
            }
        }//output of 3-D concentration files (concentrations)


        /// <summary>
        ///Output of 3-D concentration result files (concentrations)
        /// </summary>
        public void Write3DTextConcentrations()
        {
            CultureInfo ic = CultureInfo.InvariantCulture;
            Console.WriteLine("");
            Console.Write("Writing vertical concentration file..");
            try
            {
                using (StreamWriter sw = new StreamWriter("Vertical_Concentrations.txt", false))
                {
                    sw.WriteLine(Program.GralWest.ToString(ic) + "\t" + "  Western border of the GRAL domain");
                    sw.WriteLine(Program.GralEast.ToString(ic) + "\t" + "  Eastern border of the GRAL domain");
                    sw.WriteLine(Program.GralSouth.ToString(ic) + "\t" + "  Southern border of the GRAL domain");
                    sw.WriteLine(Program.GralNorth.ToString(ic) + "\t" + "  Northern border of the GRAL domain");

                    sw.WriteLine(Program.NII.ToString(ic) + "\t" + "  Transient concentration grid cell nr. in x-direction: ");
                    sw.WriteLine(Program.NJJ.ToString(ic) + "\t" + "  Transient concentration grid cell nr. in y-direction: ");
                    sw.WriteLine(Program.NKK_Transient.ToString(ic) + "\t" + "  Transient concentration grid cell nr. in z-direction: ");
                    sw.WriteLine(Program.DXK.ToString(ic) + "\t" + "  Horizontal grid size [m]");
                    sw.WriteLine(Program.AHMIN.ToString(ic) + "\t" + "  Model base height [m]");

                    string h = string.Empty;
                    float[] HOKART_mean = new float[Program.NKK_Transient];
                    for (int k = 1; k < Program.NKK_Transient; k++)
                    {
                        HOKART_mean[k] = (float)Math.Round(Program.HoKartTrans[k] - (Program.HoKartTrans[k] - Program.HoKartTrans[k - 1]) * 0.5F, 1);
                        h += (HOKART_mean[k].ToString(ic)) + "\t";
                    }
                    h += "  Mean height of each layer above ground [m]";
                    sw.WriteLine(h);

                    if (Program.Odour)
                    {
                        sw.WriteLine("Concentration layers [OU/m³]");
                    }
                    else
                    {
                        sw.WriteLine("Concentration layers [µg/m³]");
                    }

                    double val = 0;

                    Console.Write("00 %");
                    System.Text.StringBuilder SB = new StringBuilder();

                    for (int k = 1; k < Program.NKK_Transient; k++)
                    {
                        if (k % 2 == 0)
                        {
                            float p = (float)k / (float)Program.NKK_Transient * 100F;
                            Console.SetCursorPosition(Console.CursorLeft - 4, Console.CursorTop);
                            Console.Write(p.ToString("00") + " %");
                        }

                        sw.WriteLine(HOKART_mean[k].ToString(ic) + "\t  Mean height above ground [m]");

                        for (int j = Program.NJJ; j > 0; j--)
                        {
                            //h = string.Empty;
                            for (int i = 1; i <= Program.NII + 1; i++)
                            {
                                if (i > 1 && j > 1 && i < (Program.NII - 2) && (j < Program.NJJ - 2)) // low pass filter the 3D concentration
                                {
                                    if (Program.ConzSsum[i][j][k] > 0) // concentration != 0?
                                    {
                                        double weighting_factor = 0;
                                        val = 0;

                                        LowPass(ref val, ref weighting_factor, k, i, j, 1.0);
                                        if (k > 1)
                                        {
                                            LowPass(ref val, ref weighting_factor, k - 1, i, j, 0.3);
                                        }
                                        if (k < Program.NKK_Transient - 2)
                                        {
                                            LowPass(ref val, ref weighting_factor, k + 1, i, j, 0.3);
                                        }

                                        val = val / weighting_factor / Program.ConzSumCounter;
                                    }
                                    else // concentration == 0
                                    {
                                        val = 0;
                                    }
                                }
                                else // border cells
                                {
                                    val = Program.ConzSsum[i][j][k] / Program.ConzSumCounter;
                                }

                                SB.Append(val.ToString("e2", ic));
                                SB.Append("\t");
                                //h += val.ToString("e2", ic) + "\t";
                            }
                            sw.WriteLine(SB.ToString());
                            SB.Clear();
                            //sw.WriteLine(h);
                        }
                    } // loop over vertical layers

                    sw.WriteLine("Topography [m]");
                    for (int j = Program.NJJ; j > 0; j--)
                    {
                        //h = string.Empty;
                        SB.Clear();
                        for (int i = 1; i <= Program.NII + 1; i++)
                        {
                            double val__ = Program.AHK[i][j];
                            SB.Append(Math.Round(val__, 1).ToString(ic));
                            SB.Append("\t");
                            //h += Math.Round(val__,1).ToString(ic) + "\t";
                        }
                        sw.WriteLine(SB.ToString());
                        //sw.WriteLine(h);
                    }
                    SB.Clear();
                    SB = null;
                }

                if (File.Exists("Vertical_Concentrations.tmp"))
                {
                    File.Delete("Vertical_Concentrations.tmp");
                }
                Console.WriteLine("");
            }
            catch
            {
                Console.WriteLine("");
            }
        }

        /// <summary>
        ///LowPassFilter
        /// </summary>
        private void LowPass(ref double val, ref double weighting_factor, int k, int i, int j, double f)
        {
            int im2 = i - 2;
            int im1 = i - 1;
            int ip1 = i + 1;
            int ip2 = i + 2;
            int jm2 = j - 2;
            int jm1 = j - 1;
            int jp1 = j + 1;
            int jp2 = j + 2;

            if (Program.ConzSsum[im2][jp2][k] > 0)
            {
                val += 0.354 * Program.ConzSsum[im2][jp2][k] * f;
                weighting_factor += 0.354 * f;
            }
            if (Program.ConzSsum[im2][jm2][k] > 0)
            {
                val += 0.354 * Program.ConzSsum[im2][jm2][k] * f;
                weighting_factor += 0.354 * f;
            }
            if (Program.ConzSsum[ip2][jp2][k] > 0)
            {
                val += 0.354 * Program.ConzSsum[ip2][jp2][k] * f;
                weighting_factor += 0.354 * f;
            }
            if (Program.ConzSsum[ip2][jm2][k] > 0)
            {
                val += 0.354 * Program.ConzSsum[ip2][jm2][k] * f;
                weighting_factor += 0.354 * f;
            }
            if (Program.ConzSsum[im2][jp1][k] > 0)
            {
                val += 0.447 * Program.ConzSsum[im2][jp1][k] * f;
                weighting_factor += 0.447 * f;
            }
            if (Program.ConzSsum[im2][jm1][k] > 0)
            {
                val += 0.447 * Program.ConzSsum[im2][jm1][k] * f;
                weighting_factor += 0.447 * f;
            }
            if (Program.ConzSsum[ip2][jp1][k] > 0)
            {
                val += 0.447 * Program.ConzSsum[ip2][jp1][k] * f;
                weighting_factor += 0.447 * f;
            }
            if (Program.ConzSsum[ip2][jm1][k] > 0)
            {
                val += 0.447 * Program.ConzSsum[ip2][jm1][k] * f;
                weighting_factor += 0.447 * f;
            }
            if (Program.ConzSsum[im1][jp2][k] > 0)
            {
                val += 0.447 * Program.ConzSsum[im1][jp2][k] * f;
                weighting_factor += 0.447 * f;
            }
            if (Program.ConzSsum[im1][jm2][k] > 0)
            {
                val += 0.447 * Program.ConzSsum[im1][jm2][k] * f;
                weighting_factor += 0.447 * f;
            }
            if (Program.ConzSsum[ip1][jp2][k] > 0)
            {
                val += 0.447 * Program.ConzSsum[ip1][jp2][k] * f;
                weighting_factor += 0.447 * f;
            }
            if (Program.ConzSsum[ip1][jm2][k] > 0)
            {
                val += 0.447 * Program.ConzSsum[ip1][jm2][k] * f;
                weighting_factor += 0.447 * f;
            }
            if (Program.ConzSsum[i][jp2][k] > 0)
            {
                val += 0.5 * Program.ConzSsum[i][jp2][k] * f;
                weighting_factor += 0.5 * f;
            }
            if (Program.ConzSsum[i][jm2][k] > 0)
            {
                val += 0.5 * Program.ConzSsum[i][jm2][k] * f;
                weighting_factor += 0.5 * f;
            }
            if (Program.ConzSsum[ip2][j][k] > 0)
            {
                val += 0.5 * Program.ConzSsum[ip2][j][k] * f;
                weighting_factor += 0.5 * f;
            }
            if (Program.ConzSsum[im2][j][k] > 0)
            {
                val += 0.5 * Program.ConzSsum[im2][j][k] * f;
                weighting_factor += 0.5 * f;
            }
            if (Program.ConzSsum[im1][j][k] > 0)
            {
                val += 1.0 * Program.ConzSsum[im1][j][k] * f;
                weighting_factor += 1.0 * f;
            }
            if (Program.ConzSsum[ip1][j][k] > 0)
            {
                val += 1.0 * Program.ConzSsum[ip1][j][k] * f;
                weighting_factor += 1.0 * f;
            }
            if (Program.ConzSsum[i][jp1][k] > 0)
            {
                val += 1.0 * Program.ConzSsum[i][jp1][k] * f;
                weighting_factor += 1.0 * f;
            }
            if (Program.ConzSsum[i][jm1][k] > 0)
            {
                val += 1.0 * Program.ConzSsum[i][jm1][k] * f;
                weighting_factor += 1.0 * f;
            }
            if (Program.ConzSsum[im1][jp1][k] > 0)
            {
                val += 0.707 * Program.ConzSsum[im1][jp1][k] * f;
                weighting_factor += 0.707 * f;
            }
            if (Program.ConzSsum[ip1][jp1][k] > 0)
            {
                val += 0.707 * Program.ConzSsum[ip1][jp1][k] * f;
                weighting_factor += 0.707 * f;
            }
            if (Program.ConzSsum[im1][jm1][k] > 0)
            {
                val += 0.707 * Program.ConzSsum[im1][jm1][k] * f;
                weighting_factor += 0.707 * f;
            }
            if (Program.ConzSsum[ip1][jm1][k] > 0)
            {
                val += 0.707 * Program.ConzSsum[ip1][jm1][k] * f;
                weighting_factor += 0.707 * f;
            }
            if (Program.ConzSsum[i][j][k] > 0)
            {
                val += 2.0 * Program.ConzSsum[i][j][k] * f;
                weighting_factor += 2.0 * f;
            }
        }

        /// <summary>
        ///Output of temporary 3-D concentration files (concentrations)
        /// </summary>
        public void Write3DTempConcentrations()
        {
            try
            {

                string fname = "Vertical_Concentrations.tmp";

                if (File.Exists(fname)) // delete old temp file
                {
                    File.Delete(fname);
                }

                using (FileStream zipToOpen = new FileStream(fname, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry write_entry = archive.CreateEntry(fname);

                        using (BinaryWriter bw = new BinaryWriter(write_entry.Open()))
                        //using (BinaryWriter bw = new BinaryWriter(File.Open("Vertical_Concentrations.tmp", FileMode.Create)))
                        {
                            bw.Write((Int32)Program.NII);
                            bw.Write((Int32)Program.NJJ);
                            bw.Write((Int32)Program.NKK_Transient);
                            bw.Write(Program.GralWest);
                            bw.Write(Program.GralEast);
                            bw.Write(Program.GralSouth);
                            bw.Write(Program.GralNorth);
                            bw.Write((Single)Program.DXK);
                            bw.Write((Int32)Program.IWET);
                            bw.Write((Int32)Program.ConzSumCounter);

                            byte[] writeData = new byte[(Program.NKK_Transient - 1) * sizeof(float)]; // byte-buffer

                            for (int j = 1; j <= Program.NJJ + 1; j++)
                            {
                                for (int i = 1; i <= Program.NII + 1; i++)
                                {
                                    // find lowest and highest index with values > trans_conc_threshold
                                    int maxindex = -1;
                                    int minindex = -1;
                                    float[] conz_sum_L = Program.ConzSsum[i][j];
                                    for (int k = 1; k < Program.NKK_Transient; k++)
                                    {
                                        if (conz_sum_L[k] > float.Epsilon && (minindex == -1))
                                        {
                                            minindex = k;
                                            maxindex = k;
                                        }
                                        if (conz_sum_L[k] > float.Epsilon && (minindex != -1))
                                        {
                                            maxindex = k;
                                        }
                                    }

                                    bw.Write(minindex);
                                    if (minindex > -1 && maxindex >= minindex)
                                    {
                                        bw.Write(maxindex);

                                        for (int k = minindex; k <= maxindex; k++)
                                        {
                                            bw.Write(conz_sum_L[k]);
                                        } // loop over vertical layers
                                    }

                                }
                            }
                        }
                    } // Zip Archiv
                } //File Stream
            }
            catch { }
        }

        /// <summary>
        ///Output of temporary transient concentrations
        /// </summary>
        public void WriteTransientConcentration(int iWet)
        {
            string fname2 = "Transient_Concentrations2.tmp"; // use 2 Files for transient concentration to catch possible write errors!
            try
            {
                string fname = "Transient_Concentrations1.tmp";
                if (File.Exists(fname)) // change write file
                {
                    fname2 = fname; // delete file 1
                    fname = "Transient_Concentrations2.tmp";
                }

                using (FileStream zipToOpen = new FileStream(fname, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry write_entry = archive.CreateEntry(fname);

                        using (BinaryWriter bw = new BinaryWriter(write_entry.Open()))
                        {
                            bw.Write((Int32)Program.NII);
                            bw.Write((Int32)Program.NJJ);
                            bw.Write((Int32)Program.NKK_Transient);
                            bw.Write(Program.GralWest);
                            bw.Write(Program.GralEast);
                            bw.Write(Program.GralSouth);
                            bw.Write(Program.GralNorth);
                            bw.Write((Single)Program.DXK);
                            bw.Write((Int32)iWet);
                            bw.Write((Int32)Program.SourceGroups.Count);

                            for (int j = 1; j <= Program.NJJ + 1; j++)
                            {
                                for (int i = 1; i <= Program.NII + 1; i++)
                                {
                                    for (int IQ = 0; IQ < Program.SourceGroups.Count; IQ++)
                                    {
                                        // find lowest and highest index with values > trans_conc_threshold
                                        int maxindex = -1;
                                        int minindex = -1;
                                        for (int k = 1; k < Program.NKK_Transient; k++)
                                        {
                                            if (Program.Conz4d[i][j][k][IQ] >= Program.TransConcThreshold && (minindex == -1))
                                            {
                                                minindex = k;
                                                maxindex = k;
                                            }
                                            if (Program.Conz4d[i][j][k][IQ] >= Program.TransConcThreshold && (minindex != -1))
                                            {
                                                maxindex = k;
                                            }
                                        }

                                        bw.Write(minindex);
                                        bw.Write(maxindex);

                                        if (minindex > -1 && maxindex >= minindex)
                                        {
                                            for (int k = minindex; k <= maxindex; k++)
                                            {
                                                bw.Write(Program.Conz4d[i][j][k][IQ]);
                                            } // loop over vertical layers
                                        }

                                    }
                                }
                            }
                            // write emission per source group
                            for (int IQ = 0; IQ <= Program.SourceGroups.Count; IQ++)
                            {
                                bw.Write((double)(Program.EmissionPerSG[IQ]));
                            }
                        }
                    } // Zip Archiv
                } //File Stream
            }
            catch
            {
                fname2 = string.Empty; // do not delete old temp file!
            }

            try
            {
                if (File.Exists(fname2)) // delete old temp file
                {
                    File.Delete(fname2);
                }
            }
            catch
            { }
        }

        /// <summary>
        ///Output of GRAL logging file
        /// </summary>
        public static void LogfileGralCoreInfo(bool zipped, bool gff_files)
        {
            // Write additional Data to the Log-File
            LogfileGralCoreWrite("");

            string err = "Particles per second per weather situation: " + Program.TPS.ToString();
            LogfileGralCoreWrite(err);
            err = "Dispersion time [s]: " + Program.TAUS.ToString();
            LogfileGralCoreWrite(err);
            err = "Sum of all particles: " + Program.NTEILMAX.ToString();
            LogfileGralCoreWrite(err);
            LogfileGralCoreWrite("");

            err = "GRAL domain area";
            LogfileGralCoreWrite(err);
            err = "  West  (abs): " + Program.GralWest.ToString() + " (rel): " + Program.XsiMinGral.ToString();
            LogfileGralCoreWrite(err);
            err = "  East  (abs): " + Program.GralEast.ToString() + " (rel): " + Program.XsiMaxGral.ToString();
            LogfileGralCoreWrite(err);
            err = "  North (abs): " + Program.GralNorth.ToString() + " (rel): " + Program.EtaMaxGral.ToString();
            LogfileGralCoreWrite(err);
            err = "  South (abs): " + Program.GralSouth.ToString() + " (rel): " + Program.EtaMinGral.ToString();
            LogfileGralCoreWrite(err);
            err = "  Latitude [°]: " + Program.LatitudeDomain.ToString();
            LogfileGralCoreWrite(err);

            LogfileGralCoreWrite("");
            err = "  Concentration grid cell nr. in x-direction: " + Program.NXL.ToString();
            LogfileGralCoreWrite(err);
            err = "  Concentration grid cell nr. in y-direction: " + Program.NYL.ToString();
            LogfileGralCoreWrite(err);
            err = "  Concentration grid size in x direction  [m]: " + Program.dx.ToString();
            LogfileGralCoreWrite(err);
            err = "  Concentration grid size in y direction  [m]: " + Program.dy.ToString();
            LogfileGralCoreWrite(err);
            err = "  Concentration grid size in z direction  [m]: " + Program.dz.ToString();
            LogfileGralCoreWrite(err);

            err = "  Total number of horizontal slices for the concentration grid: " + Program.NS.ToString();
            LogfileGralCoreWrite(err);

            for (int i = 0; i < Program.NS; i++)
            {
                try
                {
                    err = "    Slice height above ground [m]: " + Program.HorSlices[i + 1].ToString();
                    LogfileGralCoreWrite(err);
                }
                catch
                { }
            }

            float height = Program.ModelTopHeight;
            if (Program.AHMIN < 9999)
                height += Program.AHMIN;

            err = "  Top height of the GRAL domain (abs) [m]: " + (Math.Round(height)).ToString();
            LogfileGralCoreWrite(err);

            LogfileGralCoreWrite("");

            if ((Program.Topo == 1) || (Program.BuildingFlatExist == true) || (Program.BuildingTerrExist == true)) // compute prognostic flow field in these cases
            {
                err = "  Flow field grid cell nr. in x-direction: " + Program.NII.ToString();
                LogfileGralCoreWrite(err);
                err = "  Flow field grid cell nr. in y-direction: " + Program.NJJ.ToString();
                LogfileGralCoreWrite(err);
                err = "  Flow field grid size in x direction  [m]: " + Program.DXK.ToString();
                LogfileGralCoreWrite(err);
                err = "  Flow field grid size in y direction  [m]: " + Program.DYK.ToString();
                LogfileGralCoreWrite(err);

                if (gff_files == false) // KADVMAX is not valid, if gff files are read from file
                {
                    err = "  Flow field grid cell nr. in z-direction: " + Program.KADVMAX.ToString();
                    LogfileGralCoreWrite(err);
                    err = "  Flow field grid height of first layer [m]: " + Math.Round(Program.DZK[1]).ToString();
                    LogfileGralCoreWrite(err);
                    if (Program.StretchFF > 0)
                    {
                        err = "  Flow field vertical stretching factor : " + Program.StretchFF.ToString();
                        LogfileGralCoreWrite(err);
                    }
                    else
                    {
                        foreach (float[] stretchflex in Program.StretchFlexible)
                        {
                            err = "  Flow field vertical stretching factor " + stretchflex[1].ToString() + " Starting at a height of " + Math.Round(stretchflex[0], 1).ToString();
                            LogfileGralCoreWrite(err);
                        }
                    }


                    try
                    {
                        height = 0;
                        for (int i = 1; i < Program.KADVMAX + 1; i++)
                            height += Program.DZK[i];

                        if (Program.AHMIN < 9999)
                            height += Program.AHMIN;

                        err = "  Flow field grid maximum elevation (abs) [m]: " + Math.Round(height).ToString();
                        LogfileGralCoreWrite(err);
                    }
                    catch { }

                    if (File.Exists("Integrationtime.txt") == true)
                    {
                        int INTEGRATIONSSCHRITTE_MIN = 0;
                        int INTEGRATIONSSCHRITTE_MAX = 0;
                        try
                        {
                            using (StreamReader sr = new StreamReader("Integrationtime.txt"))
                            {
                                INTEGRATIONSSCHRITTE_MIN = Convert.ToInt32(sr.ReadLine());
                                INTEGRATIONSSCHRITTE_MAX = Convert.ToInt32(sr.ReadLine());
                            }
                        }
                        catch
                        { }
                        err = "  Flow field minimum iterations : " + INTEGRATIONSSCHRITTE_MIN.ToString();
                        LogfileGralCoreWrite(err);
                        err = "  Flow field maximum iterations : " + INTEGRATIONSSCHRITTE_MAX.ToString();
                        LogfileGralCoreWrite(err);
                    }

                    //roughness length for building walls
                    float building_Z0 = 0.001F;
                    if (File.Exists("building_roughness.txt") == true)
                    {
                        try
                        {
                            using (StreamReader sr = new StreamReader("building_roughness.txt"))
                            {
                                building_Z0 = Convert.ToSingle(sr.ReadLine().Replace(".", Program.Decsep));
                            }
                        }
                        catch
                        { }
                    }
                    err = "  Flow field roughness of building walls : " + building_Z0.ToString();
                    LogfileGralCoreWrite(err);


                }
                else
                {
                    err = "  Reading *.gff files";
                    LogfileGralCoreWrite(err);
                }
                LogfileGralCoreWrite("");
            }

            if (Program.ISTATIONAER == 0)
            {
                err = "";
                LogfileGralCoreWrite(err);
                err = "  Transient concentration grid cell nr. in x-direction: " + Program.NII.ToString();
                LogfileGralCoreWrite(err);
                err = "  Transient concentration grid cell nr. in y-direction: " + Program.NJJ.ToString();
                LogfileGralCoreWrite(err);
                err = "  Transient concentration grid cell nr. in z-direction: " + Program.NKK_Transient.ToString();
                LogfileGralCoreWrite(err);
                err = "  Transient concentration threshold [µg/m³]: " + Program.TransConcThreshold.ToString();
                LogfileGralCoreWrite(err);
                err = "";
                LogfileGralCoreWrite(err);
            }

            if (Program.Topo == 0)
            {
                err = "Flat terrain ";
                LogfileGralCoreWrite(err);
            }
            else if (Program.Topo == 1)
            {
                if (File.Exists("GRAL_topofile.txt") == false || Program.AHKOriMin > 100000) // Gral topofile not available or corrupt (AHK_Ori_Min)
                    err = "Complex terrain - using interpolated GRAMM topography";
                else
                    err = "Complex terrain - using GRAL_topofile.txt";
                LogfileGralCoreWrite(err);

                // get GRAMM Path if available
                err = String.Empty;
                string sclfilename = Convert.ToString(Program.IWET).PadLeft(5, '0') + ".scl";
                if (File.Exists(sclfilename))
                {
                    err = "Reading GRAMM wind fields from: " + Directory.GetCurrentDirectory();
                }
                else if (File.Exists("windfeld.txt") == true)
                {
                    err = "Reading GRAMM wind fields from: " + Program.ReadWindfeldTXT();
                }
                if (err != String.Empty)
                {
                    LogfileGralCoreWrite(err);
                }
            }

            if (Program.AHMAX > -1)
            {
                err = "Minimum elevation of topography [m]: " + Math.Round(Program.AHMIN, 1).ToString();
                LogfileGralCoreWrite(err);
                err = "Maximum elevation of topography [m]: " + Math.Round(Program.AHMAX, 1).ToString();
                LogfileGralCoreWrite(err);
            }

            if (Program.LandUseAvailable == true)
                err = "Roughness length from landuse file available";
            else
            {
                //                if (topo == 1)
                //                    err = "Roughness from GRAMM Windfield available";
                //                else
                err = "Roughness length [m]: " + Program.Z0.ToString();
            }
            LogfileGralCoreWrite(err);


            if (Program.FlowFieldLevel == 0)
                err = "No buildings";
            else if (Program.FlowFieldLevel == 1)
                err = "Diagnostic approach for buildings";
            else
                err = "Prognostic approach for buildings";
            LogfileGralCoreWrite(err);

            err = "Number of receptor points: " + Program.ReceptorNumber.ToString();
            LogfileGralCoreWrite(err);

            if (zipped == true)
            {
                err = "Writing compressed result files *.grz";
                LogfileGralCoreWrite(err);
            }

            if (Program.Odour == true)
            {
                err = "Computing odour";
            }
            else
            {
                err = "Computing pollutant " + Program.PollutantType;
                if (Program.DepositionExist == true)
                    err += " with deposition";
            }
            LogfileGralCoreWrite(err);

            //decay rate
            for (int i = 0; i < Program.SourceGroups.Count; i++)
            {
                int sg = Program.SourceGroups[i];
                if (Program.DecayRate[sg] > 0)
                {
                    err = "  Decay rate " + Program.DecayRate[sg].ToString() + " for source group " + sg.ToString();
                    LogfileGralCoreWrite(err);
                }
            }

            // Wet deposition
            if (Program.WetDeposition)
            {
                double prec = Program.WetDepoPrecipLst.Sum();
                err = "Wet deposition enabled. Sum of precipitation " + Math.Round(prec, 1).ToString() + " mm";
                LogfileGralCoreWrite(err);
                err = "Wet deposition CW " + Program.Wet_Depo_CW.ToString() + " 1/s      AlphaW " + Program.WedDepoAlphaW.ToString();
                LogfileGralCoreWrite(err);
            }

            err = "Starting computation with weather situation: " + Program.IWET.ToString();
            LogfileGralCoreWrite(err);
            LogfileGralCoreWrite("");

        } // Write Logfile

        /// <summary>
        ///Output of Emission rate in transient mode
        /// </summary>
        public static void ShowEmissionRate()
        {
            string Info = "Summarized emission rates per source group in kg";
            Console.WriteLine(Info);
            ProgramWriters.LogfileGralCoreWrite(Info);
            Info = string.Empty;
            string ER = string.Empty;
            for (int i = 0; i < Program.SourceGroups.Count; i++)
            {
                Info += "SG:   " + Program.SourceGroups[i].ToString() + "  \t";
                ER += (Program.EmissionPerSG[i] * Program.TAUS / 1000000000 * Program.GridVolume).ToString("e2") + "\t"; // show ER as eponential number with 2 digits
            }
            Console.WriteLine(Info);
            ProgramWriters.LogfileGralCoreWrite(Info);
            Console.WriteLine(ER);
            ProgramWriters.LogfileGralCoreWrite(ER);

            //            // average emission rate
            //            Info = "Average emission rates per source group in kg/h";
            //            Console.WriteLine(Info);
            //            Prog_Write.Logfile_GralCore_Write(Info);
            //            Info = string.Empty;
            //            ER = string.Empty;
            //            for (int i = 1; i <= Program.SG_MaxNumb; i++)
            //            {
            //                Info += "SG:   " + i.ToString() + "  \t";
            //                ER += (Program.Emission_per_SG[i] * 3600 / 1000000000 * Program.dV / Program.Met_Time_Ser.Count()).ToString("e2") + "\t"; // show ER as eponential number with 2 digits
            //            }
            //            Console.WriteLine(Info);
            //            Prog_Write.Logfile_GralCore_Write(Info);
            //            Console.WriteLine(ER);
            //            Prog_Write.Logfile_GralCore_Write(ER);

        } // show emission rate

        /// <summary>
        ///Output of GRAL logging file
        /// </summary>
        public static void LogfileGralCoreWrite(string a)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter("Logfile_GRALCore.txt", true))
                {
                    sw.WriteLine(a);
                    sw.Flush();
                }
            }
            catch { }
        }

        /// <summary>
        ///Output of GRAL Problem report
        /// </summary>
        public static void LogfileProblemreportWrite(string a)
        {
            a = "GRAL Error: " + a;
            try
            {
                using (StreamWriter sw = new StreamWriter("Problemreport_GRAL.txt", true))
                {
                    sw.WriteLine(a);
                    sw.Flush();
                }
            }
            catch { }
            LogfileGralCoreWrite(a); // Write error to LogfileCore
        }

    }
}
