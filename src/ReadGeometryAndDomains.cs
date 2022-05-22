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
using System.Threading.Tasks;

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
        private static string decsep = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;    //global decimal separator of the system
        private CultureInfo ic = CultureInfo.InvariantCulture;

        /// <summary>
        ///Read the number of vertical layers
        /// </summary>
        public void ReadMicroVertLayers()
        {
            if (File.Exists("micro_vert_layers.txt") == true)
            {
                try
                {
                    using (StreamReader reader = new StreamReader("micro_vert_layers.txt"))
                    {
                        Program.VertCellsFF = Convert.ToInt32(reader.ReadLine());
                    }
                }
                catch
                {
                    string err = "Error when reading file 'micro_vert_layers.txt' - Default value of 40 vertical cells is used";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);
                }
            }
        }

        /// <summary>
        ///Read the GRAL domain area and the source group numbers used for this simulation
        /// </summary>
        public void ReadGRALGeb()
        {
            int _line = 1;

            // The variables GRALWest to GRALSouth have to be assigned at program.cs!
            try
            {
                if (!File.Exists("GRAL.geb"))
                {
                    string error = "File GRAL.geb not available in the project folder " + Directory.GetCurrentDirectory() + Environment.NewLine + "Execution stopped: press ESC to exit";
                    Console.WriteLine(error);
                    ProgramWriters.LogfileProblemreportWrite(error);
                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    {
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) {; }
                    }
                    Environment.Exit(0);
                }

                using (StreamReader myreader = new StreamReader("GRAL.geb"))
                {
                    string[] text = new string[201];
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.DXK = Convert.ToSingle(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.DYK = Convert.ToSingle(text[0]);

                    _line++;
                    string[] Text_RComm = myreader.ReadLine().Split(new char[] { '!' });
                    text = Text_RComm[0].Split(new char[] { ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.DZK[1] = Convert.ToSingle(text[0]);

                    text[1] = text[1].Trim();
                    text[1] = text[1].Replace(".", decsep);
                    Program.StretchFF = Convert.ToSingle(text[1]);

                    if (text.Length > 2)
                    {
                        Program.StretchFF = 0;
                        Program.StretchFlexible.Add(new float[2]);
                        Program.StretchFlexible[0][1] = Convert.ToSingle(text[1]);

                        for (int i = 2; i < text.Length; i += 2)
                        {
                            Program.StretchFlexible.Add(new float[2]);
                            text[i] = text[i].Trim();
                            text[i] = text[i].Replace(".", decsep);
                            Program.StretchFlexible[Program.StretchFlexible.Count - 1][0] = Convert.ToSingle(text[i]); // Height
                            // Console.WriteLine("Read Height:" + Program.StretchFlexible[Program.StretchFlexible.Count - 1][0]);
                            text[i + 1] = text[i + 1].Trim();
                            text[i + 1] = text[i + 1].Replace(".", decsep);
                            Program.StretchFlexible[Program.StretchFlexible.Count - 1][1] = Convert.ToSingle(text[i + 1]); //Stretch
                            // Console.WriteLine("Read Fact:" + Program.StretchFlexible[Program.StretchFlexible.Count - 1][1]);
                        }
                    }

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.NXL = Convert.ToInt32(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.NYL = Convert.ToInt32(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.NS = Convert.ToInt32(text[0]);
                    if (Program.NS > 9)
                    {
                        string err = "Number of horizontal concentration grids exceeds maximum number of 9 - Execution stopped: press ESC to stop";
                        Console.WriteLine(err);
                        ProgramWriters.LogfileProblemreportWrite(err);

                        if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        {
                            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                            {
                                ;
                            }
                        }
                        Environment.Exit(0);
                    }

                    // read the used source groups for this simulation
                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    for (int i = 0; i < text.Length; i++)
                    {
                        try
                        {
                            text[i] = text[i].Trim();
                            text[i] = text[i].Replace(".", decsep);
                            Program.SourceGroups.Add(Convert.ToInt32(text[i], ic));
                        }
                        catch { }
                    }

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.XsiMinGral = Convert.ToDouble(text[0]);
                    Program.GralWest = Program.XsiMinGral;

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.XsiMaxGral = Convert.ToDouble(text[0]);
                    Program.GralEast = Program.XsiMaxGral;

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.EtaMinGral = Convert.ToDouble(text[0]);
                    Program.GralSouth = Program.EtaMinGral;

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.EtaMaxGral = Convert.ToDouble(text[0]);
                    Program.GralNorth = Program.EtaMaxGral;

                    //calculation of the highest and maximum number of source groups
                    /*
                    int ncount=0;
                    Console.Write("Compute source-group numbers: ");
                    for (int i = 1; i <= 100; i++)
                    {
                        if (Program.NQi[i] != 0)
                        {
                            ncount++;
                            Console.Write("<" + Program.NQi[i] + "> ");
                            Program.SG_MaxNumb = Math.Max(Program.NQi[i], Program.SG_MaxNumb);
                        }
                    }
                    Program.SG_COUNT = Math.Max(ncount, 1);
                     */

                    Console.WriteLine();
                    string Info = "Source group count: " + Convert.ToString(Program.SourceGroups.Count);
                    Console.WriteLine(Info);
                    ProgramWriters.LogfileGralCoreWrite(Info);
                }
            }
            catch
            {
                if (_line == 8)
                {
                    string errx = "No GRAL domain area defined";
                    Console.WriteLine(errx);
                    ProgramWriters.LogfileProblemreportWrite(errx);
                }
                string err = "Error when reading file 'GRAL.geb' in line " + _line.ToString() + " - Execution stopped: press ESC to stop";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                {
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                    {
                        ;
                    }
                }

                Environment.Exit(0);
            }
        } // Read GRAL.geb

        /// <summary>
        ///Read the GRAMM domain area
        /// </summary>
        public void ReadGRAMMGeb()
        {
            int _line = 1;
            try
            {
                using (StreamReader myreader = new StreamReader("GRAMM.geb"))
                {
                    string[] text = new string[10];
                    text = myreader.ReadLine().Split(new char[] { '!' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.NX = Convert.ToInt32(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.NY = Convert.ToInt32(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.NZ = Convert.ToInt32(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.XsiMinGramm = Convert.ToDouble(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.XsiMaxGramm = Convert.ToDouble(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.EtaMinGramm = Convert.ToDouble(text[0]);

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    text[0] = text[0].Trim();
                    text[0] = text[0].Replace(".", decsep);
                    Program.EtaMaxGramm = Convert.ToDouble(text[0]);
                }
            }
            catch
            {
                string err = "Error when reading 'GRAMM.geb' in line " + _line.ToString() + " - Execution stopped: press ESC to stop";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                {
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                    {
                        ;
                    }
                }

                Environment.Exit(0);
            }
        }// Read GRAMM.geb

        /// <summary>
        ///Define sub domains to be calculated by the prognostic wind-field model
        /// </summary>    
        private void CalculateSubDomainsForPrognosticWindSimulation()
        {
            //compute sub-domains of GRAL for the prognostic wind-field simulations
            int iplus;
            int jplus;
            for (int i = 1; i <= Program.NII; i++)
            {
                for (int j = 1; j <= Program.NJJ; j++)
                {
                    if (Program.CUTK[i][j] > 0)
                    {
                        iplus = (int)(Program.PrognosticSubDomainFactor * Program.CUTK[i][j] / Program.DXK);
                        jplus = (int)(Program.PrognosticSubDomainFactor * Program.CUTK[i][j] / Program.DYK);

                        //for (int i1 = i - iplus; i1 <= i + iplus; i1++)
                        Parallel.For(i - iplus, i + iplus + 1, Program.pOptions, i1 =>
                        {
                            for (int j1 = j - jplus; j1 <= j + jplus; j1++)
                            {
                                if ((i1 <= Program.NII) && (i1 >= 1) && (j1 <= Program.NJJ) && (j1 >= 1))
                                {
                                    Program.ADVDOM[i1][j1] = 1;
                                }
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Read the GRAL_topofile.txt
        /// </summary>
        public bool ReadGRALTopography(int NII, int NJJ)
        {
            bool GRAL_Topofile = false;
            if (File.Exists("GRAL_topofile.txt"))
            {
                Console.WriteLine("Reading GRAL_topofile.txt");
                string[] data = new string[100];
                Program.AHKOriMin = 1000000; // Flag, that GRAL_topofile is corrupt

                try
                {
                    using (StreamReader myReader = new StreamReader("GRAL_topofile.txt"))
                    {
                        data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        int nx = Convert.ToInt32(data[1]);
                        data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        int ny = Convert.ToInt32(data[1]);
                        data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        double x11 = Convert.ToDouble(data[1].Replace(".", Program.Decsep));
                        data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        double y11 = Convert.ToDouble(data[1].Replace(".", Program.Decsep));
                        data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        double dx = Convert.ToDouble(data[1].Replace(".", Program.Decsep));
                        data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        int nodata = Convert.ToInt32(data[1]);
                        data = new string[nx + 1];
                        if (nx != NII || ny != NJJ)
                        {
                            throw new ArgumentOutOfRangeException("Microscale terrain grid is not equal to flow field grid");
                        }

                        for (int i = ny; i > 0; i--)
                        {
                            data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 1; j <= nx; j++)
                            {
                                Program.AHKOri[j][i] = Convert.ToSingle(data[j - 1].Replace(".", Program.Decsep));
                                if (Convert.ToInt32(Program.AHKOri[j][i]) != nodata)
                                {
                                    Program.AHKOriMin = Math.Min(Program.AHKOriMin, Program.AHKOri[j][i]);
                                }
                            }
                        }
                    }
                    GRAL_Topofile = true; // Reading GRAL Topofile = OK;
                    Console.WriteLine("...finished");

                }
                catch (Exception ex)
                {
                    Console.Write(ex.Message.ToString());
                    Console.WriteLine("...reading error - finished");
                    ProgramWriters.LogfileProblemreportWrite(ex.Message.ToString() + "...reading error - finished");
                    Program.AHKOriMin = 1000000; // Flag, that GRAL_topofile is corrupt
                }
            }
            return GRAL_Topofile;
        } // Read GRAL_Topography.txt

        /// <summary>
        /// Read the file RoughnessLengthsGral.dat
        /// </summary>
        public bool ReadRoughnessGral(float[][] RoughnessArray)
        {
            bool fileReadingOK = false;

            Console.WriteLine("Reading RoughnessLengthsGral.dat");
            string[] data;

            try
            {
                using (StreamReader myReader = new StreamReader("RoughnessLengthsGral.dat"))
                {
                    data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    int nx = Convert.ToInt32(data[1]);
                    data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    int ny = Convert.ToInt32(data[1]);
                    data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    double x11 = Convert.ToDouble(data[1].Replace(".", Program.Decsep));
                    data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    double y11 = Convert.ToDouble(data[1].Replace(".", Program.Decsep));
                    data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    double dx = Convert.ToDouble(data[1].Replace(".", Program.Decsep));
                    data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    int nodata = Convert.ToInt32(data[1]);
                    data = new string[nx + 1];
                    if (nx != Program.NII || ny != Program.NJJ)
                    {
                        throw new ArgumentOutOfRangeException("Microscale Roughness grid cell count is not equal to the flow field grid cell count");
                    }
                    if (Math.Abs(x11 - Program.GralWest) > 0.1 || Math.Abs(y11 - Program.GralSouth) > 0.1)
                    {
                        throw new ArgumentOutOfRangeException("Microscale Roughness grid does not match the GRAL Domain grid");
                    }
                    if (Math.Abs(dx - Program.DXK) > 0.1)
                    {
                        throw new ArgumentOutOfRangeException("Microscale Roughness grid size does not match the GRAL Domain grid size");
                    }

                    double sum = 0;
                    int count = 0;
                    for (int j = ny; j > 0; j--)
                    {
                        data = myReader.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 1; i <= nx; i++)
                        {
                            float _val = Convert.ToSingle(data[j - 1], ic);
                            if (Convert.ToInt32(_val) != nodata)
                            {
                                RoughnessArray[i][j] = _val;
                            }
                            else
                            {
                                RoughnessArray[i][j] = Program.Z0;
                            }
                            count++;
                            sum += RoughnessArray[i][j];
                        }
                    }
                    if (count > 0)
                    {
                        Console.WriteLine("...finished - mean roughness lenght: " + Math.Round(sum / count, 2).ToString() + " m");
                    }
                }
                fileReadingOK = true;
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message.ToString());
                Console.WriteLine("...reading error - finished");
                ProgramWriters.LogfileProblemreportWrite(ex.Message.ToString() + "...reading error - finished");
            }
            return fileReadingOK;
        } // Read file RoughnessLenghtsGral.dat 
    }
}