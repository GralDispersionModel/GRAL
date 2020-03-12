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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GRAL_2001
{
    public partial class ProgramWriters
    {
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
    }
}
