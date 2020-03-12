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
using System.IO.Compression;

namespace GRAL_2001
{
    public partial class ProgramWriters
    {
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
    }
}
