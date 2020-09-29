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
using System.Threading.Tasks;

namespace GRAL_2001
{
    public partial class ProgramWriters
    {
        /// <summary>
        ///Output of 2-D concentration files (concentrations, deposition, odour-files)
        /// </summary>
        public void Write2DConcentrations()
        {
            try
            {
                Console.Write("Writing result files.");
                if (Program.ResultFileZipped)
                {
                    using (FileStream zipToOpen = new FileStream(Program.ZippedFile, FileMode.OpenOrCreate))
                    {
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                        {
                            for (int IQ = 0; IQ < Program.SourceGroups.Count; IQ++)
                            {
                                for (int II = 0; II < Program.NS; II++)
                                {
                                    if ((IQ + II) % 2 == 0)
                                    {
                                        Console.Write(".");
                                    }
                                    string fname = Program.IWET.ToString("00000") + "-" + (II + 1).ToString("0") + Program.SourceGroups[IQ].ToString("00") + ".con";

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
                                    if (II == 0 && (Program.DepositionExist || Program.WetDeposition))
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
                        for (int II = 0; II < Program.NS; II++)
                        {
                            if ((IQ + II) % 2 == 0)
                            {
                                Console.Write(".");
                            }

                            string fname = Program.IWET.ToString("00000") + "-" + (II + 1).ToString("0") + Program.SourceGroups[IQ].ToString("00") + ".con";
                            if (Program.WriteASCiiResults) // aditional ASCii Output
                            {
                                writeConDataAscii(fname, IQ, II);
                            }

                            using (BinaryWriter sw = new BinaryWriter(File.Open(fname, FileMode.Create)))
                            {
                                WriteConcentrationData(sw, IQ, II);
                            }

                            // Write deposition
                            if (II == 0 && (Program.DepositionExist || Program.WetDeposition))
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
                Console.WriteLine();
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
                    for (int II = 0; II < Program.NS; II++)
                    {
                        if ((IQ + II) % 2 == 0)
                        {
                            Console.Write(".");
                        }

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

                                int IndexK = 1;
                                float UXint = 0, UYint = 0, UZint = 0, U0int = 0, V0int = 0;
                                float W0int = 0;
                                double xco = Convert.ToDouble((float)i * Program.GralDx - 0.5 * Program.GralDx + Program.IKOOAGRAL);
                                double yco = Convert.ToDouble((float)j * Program.GralDy - 0.5 * Program.GralDy + Program.JKOOAGRAL);

                                //horizontal cell indices
                                double xsi1 = (float)i * Program.GralDx - 0.5 * Program.GralDx + Program.IKOOAGRAL - Program.GrammWest;
                                double eta1 = (float)j * Program.GralDy - 0.5 * Program.GralDy + Program.JKOOAGRAL - Program.GrammSouth;
                                if (Program.Topo == 1)
                                {
                                    double xsi = xco - Program.IKOOAGRAL;
                                    double eta = yco - Program.JKOOAGRAL;
                                    IUst = (int)(xsi1 / Program.DDX[1]) + 1;
                                    JUst = (int)(eta1 / Program.DDY[1]) + 1;
                                    IUst = Math.Clamp(IUst, 1, Program.NX);
                                    JUst = Math.Clamp(JUst, 1, Program.NY);

                                    IndexI = (int)(xsi / Program.DXK) + 1;
                                    IndexJ = (int)(eta / Program.DYK) + 1;
                                }
                                else
                                {
                                    double xsi = xco - Program.IKOOAGRAL;
                                    double eta = yco - Program.JKOOAGRAL;
                                    IndexI = (int)(xsi / Program.DXK) + 1;
                                    IndexJ = (int)(eta / Program.DYK) + 1;
                                }
                                //surface height
                                float AHint = 0;
                                if (Program.Topo == 1)
                                {
                                    AHint = Program.AHK[IndexI][IndexJ];
                                }
                                //wind speed
                                (UXint, UYint, UZint, IndexK) = Zeitschleife.IntWindCalculate(IndexI, IndexJ, AHint, xco, yco, Program.HorSlices[II]);
                                float windge = (float)Math.Sqrt(Program.Pow2(UXint) + Program.Pow2(UYint));
                                if (windge <= 0)
                                {
                                    windge = 0.01F;
                                }

                                float ObLength = Program.Ob[IUst][JUst];
                                float Z0 = Program.Z0Gramm[IUst][JUst];
                                float ust = Program.Ustern[IUst][JUst];
                                if (Program.AdaptiveRoughnessMax > 0)
                                {
                                    ObLength = Program.OLGral[i][j];
                                    Z0 = Program.Z0Gral[i][j];
                                    ust = Program.USternGral[i][j];
                                }

                                //horizontal wind standard deviations
                                (U0int, V0int) = Zeitschleife.IntStandCalculate(1, Z0, Program.HorSlices[II], windge, 0);

                                //vertical wind standard deviation
                                if (Program.IStatistics == 3)
                                {
                                    W0int = Program.W0int;
                                }
                                else
                                {
                                    if (ObLength >= 0)
                                    {
                                        W0int = ust * Program.StdDeviationW * 1.25F;
                                    }
                                    else
                                    {
                                        W0int = (float)(ust * (1.15F + 0.1F * Math.Pow(Program.BdLayHeight / (-ObLength), 0.67)) * Program.StdDeviationW);
                                    }
                                }

                                //dissipation
                                float eps = (float)(Program.Pow3(ust) / Math.Max(Program.HorSlices[II], Z0) / 0.4F *
                                                    Math.Pow(1 + 0.5F * Math.Pow(Program.HorSlices[II] / Math.Abs(ObLength), 0.8), 1.8));

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
                        string fname = Program.IWET.ToString("00000") + "-" + (II + 1).ToString("0") + Program.SourceGroups[IQ].ToString("00") + ".odr";
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
                                Console.WriteLine();
                                LogfileProblemreportWrite("Situation: " + Program.IWET.ToString() + " Error writing odr file: " + exc.Message);
                            }
                        }
                    }
                }
            }

            //reset concentrations
            Console.WriteLine(".");
            for (int iq = 0; iq < Program.SourceGroups.Count; iq++)
            {
                for (int II = 0; II < Program.NS; II++)
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
            Console.WriteLine();
        }//output of 2-D concentration files (concentrations, deposition, odour-files)

        /// <summary>
        ///Output of deposition values
        /// </summary>
        private static bool WriteDepositionData(BinaryWriter sw, int IQ)
        {
            try
            {
                //flag for stream file (not for SOUNDPLAN)
                if (Program.IOUTPUT <= 0)
                {
                    sw.Write(Program.ResultFileHeader);
                }

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        float xp1 = i * Program.GralDx - Program.GralDx * 0.5F;
                        float yp1 = j * Program.GralDy - Program.GralDy * 0.5F;
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
                sw.Write(Program.GralDx);
                sw.Write(Program.GralDy);

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
                sw.Write(Program.GralDx);
                sw.Write(Program.GralDy);
                sw.Write(Program.NXL);
                sw.Write(Program.NYL);

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        sw.Write((float)Program.Depo_conz[i][j][IQ]);
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
                if (Program.IOUTPUT <= 0)
                {
                    sw.Write(Program.ResultFileHeader);
                }

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        float xp1 = i * Program.GralDx - Program.GralDx * 0.5F;
                        float yp1 = j * Program.GralDy - Program.GralDy * 0.5F;
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
                sw.Write(Program.GralDx);
                sw.Write(Program.GralDy);

                int[] Start = new int[2];
                float[] Remember = new float[Program.NYL + 2];

                for (int i = 1; i <= Program.NXL; i++)
                {
                    int count = 0;
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        // float xp1 = i * Program.GralDx - Program.GralDx * 0.5F;
                        // float yp1 = j * Program.GralDy - Program.GralDy * 0.5F;
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
                sw.Write(Program.GralDx);
                sw.Write(Program.GralDy);
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
                    myWriter.WriteLine("cellsize      " + Convert.ToString(Program.GralDx, ic));
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
                    myWriter.WriteLine("cellsize      " + Convert.ToString(Program.GralDx, ic));
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
                if (Program.IOUTPUT <= 0)
                {
                    sw.Write(Program.ResultFileHeader);
                }

                for (int i = 1; i <= Program.NXL; i++)
                {
                    for (int j = 1; j <= Program.NYL; j++)
                    {
                        float xp1 = i * Program.GralDx - Program.GralDx * 0.5F;
                        float yp1 = j * Program.GralDy - Program.GralDy * 0.5F;
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
                sw.Write(Program.GralDx);
                sw.Write(Program.GralDy);

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
                sw.Write(Program.GralDx);
                sw.Write(Program.GralDy);
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
    }
}
