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
using System.IO.Compression;

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
        /// <summary>
        /// Read the Transient Threshold value from file
        /// </summary>
        public float ReadTransientThreshold()
        {
            float trans_conc_threshold = 0;

            if (File.Exists("GRAL_Trans_Conc_Threshold.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("GRAL_Trans_Conc_Threshold.txt"))
                    {
                        trans_conc_threshold = Convert.ToSingle(sr.ReadLine().Replace(".", Program.Decsep));
                    }
                }
                catch
                { }
            }

            trans_conc_threshold = Math.Max(trans_conc_threshold, float.Epsilon); // threshold > 0!

            return trans_conc_threshold;
        }

        /// <summary>
        /// Read the temporarily stored vertical concentration file
        /// </summary>
        public void Read3DTempConcentrations()
        {
            bool ok = true;
            try
            {
                string fname = "Vertical_Concentrations.tmp";

                using (ZipArchive archive = ZipFile.OpenRead(fname))
                {
                    string filename = archive.Entries[0].FullName;
                    using (BinaryReader rb = new BinaryReader(archive.Entries[0].Open()))
                    //using (BinaryReader rb = new BinaryReader(File.Open("Vertical_Concentrations.tmp", FileMode.Open))) // read ggeom.asc binary mode
                    {
                        int ff = 0;
                        // check, if temp field matches to the recent computation
                        if (rb.ReadInt32() != Program.NII)
                        {
                            ff = 1;
                            ok = false;
                        }
                        if (rb.ReadInt32() != Program.NJJ)
                        {
                            ff = 2;
                            ok = false;
                        }
                        if (rb.ReadInt32() != Program.NKK_Transient)
                        {
                            ff = 3;
                            ok = false;
                        }
                        if (rb.ReadDouble() != Program.GralWest)
                        {
                            ff = 4;
                            ok = false;
                        }
                        if (rb.ReadDouble() != Program.GralEast)
                        {
                            ff = 5;
                            ok = false;
                        }
                        if (rb.ReadDouble() != Program.GralSouth)
                        {
                            ff = 5;
                            ok = false;
                        }
                        if (rb.ReadDouble() != Program.GralNorth)
                        {
                            ff = 6;
                            ok = false;
                        }

                        if (rb.ReadSingle() != Program.DXK)
                        {
                            ff = 7;
                            ok = false;
                        }

                        int LastIWET = rb.ReadInt32(); // check if actual starting disp. situation and temp disp. situation match
                        int tempCounter = rb.ReadInt32(); // the number of summarized situations

                        if (Math.Abs(LastIWET - Program.IWETstart) > 24)
                        {
                            if (Program.TransientTempFileDelete) //continue, if transient files are not deleted
                            {
                                ff = 8;
                                ok = false;
                            }
                        }

                        if (ok) // then read the data
                        {
                            for (int j = 1; j <= Program.NJJ + 1; j++)
                            {
                                for (int i = 1; i <= Program.NII + 1; i++)
                                {
                                    int minindex = rb.ReadInt32();
                                    if (minindex > -1)
                                    {
                                        int maxindex = rb.ReadInt32();
                                        if (maxindex >= minindex)
                                        {
                                            for (int k = minindex; k <= maxindex; k++)
                                            {
                                                Program.ConzSsum[i][j][k] = rb.ReadSingle();
                                            } // loop over vertical layers with concentration values
                                        }
                                    }
                                }
                            }

                            Program.ConzSumCounter = tempCounter;
                            string err = "Reading Vertical_Concentrations.tmp successful";
                            Console.WriteLine(err);
                            ProgramWriters.LogfileGralCoreWrite(err);
                        }
                        else
                        {
                            Program.ConzSumCounter = 0;
                            if (ff == 8)
                            {
                                string err = "Reading Vertical_Concentrations.tmp failed nr. " + ff.ToString();
                                Console.WriteLine(err);
                                ProgramWriters.LogfileGralCoreWrite(err);
                                err = "Saved disp. situation: " + tempCounter.ToString() +
                                    "  recent disp. situation: " + Program.IWETstart.ToString();
                                Console.WriteLine(err);
                                ProgramWriters.LogfileGralCoreWrite(err);
                            }

                        }

                    } // binary reader
                } // ZIP

            }
            catch
            {
                // if an error occurs, delete the field and set counter to 0
                for (int k = 1; k < Program.NKK_Transient; k++)
                {
                    for (int j = 1; j <= Program.NJJ + 1; j++)
                    {
                        for (int i = 1; i <= Program.NII + 1; i++)
                        {
                            Program.ConzSsum[i][j][k] = 0;
                        }
                    }
                } // loop over vertical layers
                Program.ConzSumCounter = 0;
            } // catch
        }

        /// <summary>
        /// Read the temporarily saved transient concentration file
        /// </summary>	
        public int ReadTransientConcentrations(string fname)
        {
            bool ok = true;
            int LastIWET = 0;
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(fname))
                {
                    string filename = archive.Entries[0].FullName;
                    using (BinaryReader rb = new BinaryReader(archive.Entries[0].Open()))
                    {
                        int ff = 0;
                        // check, if temp field matches to the recent computation
                        if (rb.ReadInt32() != Program.NII)
                        {
                            ff = 1;
                            ok = false;
                        }
                        if (rb.ReadInt32() != Program.NJJ)
                        {
                            ff = 2;
                            ok = false;
                        }
                        if (rb.ReadInt32() != Program.NKK_Transient)
                        {
                            ff = 3;
                            ok = false;
                        }
                        if (rb.ReadDouble() != Program.GralWest)
                        {
                            ff = 4;
                            ok = false;
                        }
                        if (rb.ReadDouble() != Program.GralEast)
                        {
                            ff = 5;
                            ok = false;
                        }
                        if (rb.ReadDouble() != Program.GralSouth)
                        {
                            ff = 5;
                            ok = false;
                        }
                        if (rb.ReadDouble() != Program.GralNorth)
                        {
                            ff = 6;
                            ok = false;
                        }

                        if (rb.ReadSingle() != Program.DXK)
                        {
                            ff = 7;
                            ok = false;
                        }

                        LastIWET = rb.ReadInt32(); // check if actual starting disp. situation and temp disp. situation match
                        int SG_COUNT = rb.ReadInt32(); // the number of Source groups

                        if (SG_COUNT != Program.SourceGroups.Count)
                        {
                            ff = 8;
                            ok = false;
                        }

                        if (ok) // then read the data
                        {
                            for (int j = 1; j <= Program.NJJ + 1; j++)
                            {
                                for (int i = 1; i <= Program.NII + 1; i++)
                                {
                                    for (int IQ = 0; IQ < Program.SourceGroups.Count; IQ++)
                                    {
                                        int minindex = rb.ReadInt32();
                                        int maxindex = rb.ReadInt32();
                                        if (minindex > -1 && maxindex >= minindex)
                                        {
                                            for (int k = minindex; k <= maxindex; k++)
                                            {
                                                Program.Conz4d[i][j][k][IQ] = rb.ReadSingle();
                                            } // loop over vertical layers with concentration values
                                        }
                                    }
                                }
                            }

                            // read emission per source group
                            for (int IQ = 0; IQ <= Program.SourceGroups.Count; IQ++)
                            {
                                Program.EmissionPerSG[IQ] = rb.ReadDouble();
                            }

                            string err = "Reading Transient_Concentrations.tmp successful";
                            Console.WriteLine(err);
                            ProgramWriters.LogfileGralCoreWrite(err);
                        }
                        else
                        {
                        }

                    } // binary reader
                } // ZIP

            }
            catch
            {
                ok = false;
            } // catch

            if (ok == false)
            {
                LastIWET = 0;
            }
            else
            {
                LastIWET++; // continue with next situation
            }
            return LastIWET;
        }

        /// <summary>
        /// Read all Time Series files for Source Parameters
        /// </summary> 
        public void ReadSourceTimeSeries()
        {
            ReadSourceTimeSeries RSTS = new ReadSourceTimeSeries();
            Console.WriteLine("");

            string Filename = "TimeSeriesPointSourceVel.txt";
            if (RSTS.ReadTimeSeries(ref Program.PS_TimeSerVelValues, Filename))
            {
                string Info = "Reading TimeSeriesPointSourceVel.txt successful";
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
            }

            Filename = "TimeSeriesPointSourceTemp.txt";
            if (RSTS.ReadTimeSeries(ref Program.PS_TimeSerTempValues, Filename))
            {
                string Info = "Reading TimeSeriesPointSourceTemp.txt successful";
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
            }

            Filename = "TimeSeriesPortalSourceVel.txt";
            if (RSTS.ReadTimeSeries(ref Program.TS_TimeSerVelValues, Filename))
            {
                string Info = "Reading TimeSeriesPortalSourceVel.txt successful";
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
            }

            Filename = "TimeSeriesPortalSourceTemp.txt";
            if (RSTS.ReadTimeSeries(ref Program.TS_TimeSerTempValues, Filename))
            {
                string Info = "Reading TimeSeriesPortalSourceTemp.txt successful";
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
            }
        }

        /// <summary>
        /// Check, if the file "KeepAndReadTransientTempFiles.dat" exist and read TempFileInterval
        /// </summary> 
        public void ReadKeepAndDeleteTransientTempFiles()
        {
            if (File.Exists("KeepAndReadTransientTempFiles.dat"))
            {
                //do not delete transient temp files if this file exist and force to read transient temp files and do not override the first weather situation 
                Program.TransientTempFileDelete = false;
                try
                {
                    using (StreamReader reader = new StreamReader("KeepAndReadTransientTempFiles.dat"))
                    {
                        string text = reader.ReadLine();
                        int temp = 24;
                        if (int.TryParse(text, out temp))
                        {
                            Program.TransientTempFileInterval = temp;
                            Console.WriteLine("Reading KeepAndReadTransientTempFiles.dat successful");
                        }
                    }
                }
                catch { }
            }
        }
    }
}