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
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace GRAL_2001
{
    public class ProgramReaders
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
        ///Read the GRAL domain area
        /// </summary>
        public void ReadGRALGeb()
        {
            int _line = 1;
            
            // The variables GRALWest to GRALSouth have to be assigned at program.cs!
            try
            {
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
                    string[] Text_RComm = myreader.ReadLine().Split(new char[] { '!'});
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
                        
                        for (int i = 2; i < text.Length; i+=2)
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
                            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                        Environment.Exit(0);
                    }

                    _line++;
                    text = myreader.ReadLine().Split(new char[] { '!', ',', ';' });
                    for (int i = 0; i < text.Length; i++)
                    {
                        try
                        {
                            text[i] = text[i].Trim();
                            text[i] = text[i].Replace(".", decsep);
                            Program.SourceGroups.Add(Convert.ToInt32(text[i]));
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
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

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
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                Environment.Exit(0);
            }
        }// Read GRAMM.geb

        /// <summary>
        ///Read max. number of processors to be used
        /// </summary>
        public void ReadMaxNumbProc()
        {
            try
            {
                using (StreamReader myreader = new StreamReader("Max_Proc.txt"))
                {
                    string text = myreader.ReadLine();
                    Program.IPROC = Convert.ToInt32(text);
                    Program.IPROC = Math.Min(Environment.ProcessorCount, Program.IPROC); // limit number of cores to available cores
                    Program.pOptions.MaxDegreeOfParallelism = Program.IPROC;
                }
            }
            catch
            { }
        }// Read max. number processors

        /// <summary>
        ///Check Pollutant.txt and if odour is computed
        /// </summary>
        /// <result>
        /// True -> Odour calculation
        ///</result>
        public bool ReadPollutantTXT()
        {
            bool odour = false;

            string pollutant_file = "Pollutant.txt";
            if (File.Exists(pollutant_file)) // new pollutant.txt since V 18.07 with wet depo data and decay rate
            {
                try
                {
                    using (StreamReader myreader = new StreamReader(pollutant_file))
                    {
                        string text = myreader.ReadLine();
                        string[] txt = new string[10];

                        Program.PollutantType = text;
                        if (text.ToLower().Contains("odour"))
                        {
                            odour = true;
                        }

                        if (myreader.EndOfStream == false)
                        {
                            txt = myreader.ReadLine().Split(new char[] { '\t', '!' });
                            txt[0] = txt[0].Trim();
                            txt[0] = txt[0].Replace(".", decsep);
                            Program.Wet_Depo_CW = Convert.ToDouble(txt[0]);

                            txt = myreader.ReadLine().Split(new char[] { '\t', '!' });
                            txt[0] = txt[0].Trim();
                            txt[0] = txt[0].Replace(".", decsep);
                            Program.WedDepoAlphaW = Convert.ToDouble(txt[0]);

                            if (Program.ISTATIONAER == 0 && Program.Wet_Depo_CW > 0 && Program.WedDepoAlphaW > 0)
                            {
                                Program.WetDeposition = true;
                            }
                            else
                            {
                                Program.WetDeposition = false;
                            }
                        }

                        if (myreader.EndOfStream == false)
                        {
                            txt = myreader.ReadLine().Split(new char[] { '\t', '!' });
                            txt[0] = txt[0].Trim();
                            txt[0] = txt[0].Replace(".", decsep);
                            double decay = Convert.ToDouble(txt[0]);
                            for (int i = 0; i < Program.DecayRate.Length; i++) // set all source groups to this decay rate
                            {
                                Program.DecayRate[i] = decay;
                            }

                            if (myreader.EndOfStream == false) // decay rate for each source group
                            {
                                txt = myreader.ReadLine().Split(new char[] { '\t', '!' }); // split groups

                                for (int i = 0; i < txt.Length; i++)
                                {
                                    string[] _values = txt[i].Split(new char[] { ':' }); // split source group number and decay rate
                                    if (_values.Length > 1)
                                    {
                                        int sg_number = 0;
                                        if (int.TryParse(_values[0], out sg_number))
                                        {
                                            if (sg_number > 0 && sg_number < 100)
                                            {
                                                try
                                                {
                                                    decay = Convert.ToDouble(_values[1], ic);
                                                }
                                                catch 
                                                {
                                                    decay = 0;
                                                }
                                                Program.DecayRate[sg_number] = decay;
                                            }
                                        }
                                    }
                                }


                            }
                        }
                    }
                }
                catch { }
            }
            else // old pollutant.txt at the Settings folder
            {
                pollutant_file = System.IO.Directory.GetCurrentDirectory(); //actual path without / at the end
                DirectoryInfo di = new DirectoryInfo(pollutant_file);
                pollutant_file = Path.Combine(di.Parent.FullName, "Settings", "Pollutant.txt");
                if (File.Exists(pollutant_file) == true)
                {
                    try
                    {
                        using (StreamReader myreader = new StreamReader(pollutant_file))
                        {
                            string txt = myreader.ReadLine();
                            if (txt.ToLower().Contains("odour"))
                                odour = true;
                        }
                    }
                    catch { }
                }
            }
            return odour;
        }//check odour

        //read ggeom.asc
        /// <summary>
        ///Read the GRAMM geometry file ggeom.asc
        /// </summary>
        public void ReadGgeomAsc()
        {
            Program.Topo = 0;
            Console.WriteLine();
            Console.WriteLine("Searching for optional file ggeom.asc...");
            if (File.Exists("ggeom.asc") == true)
            {
                Console.WriteLine("Reading GRAMM orography file ggeom.asc");
                string[] text = new string[1];
                try
                {
                    StreamReader reader = new StreamReader("ggeom.asc");
                    if (Program.RunOnUnix) reader.ReadLine();
                    text[0] = reader.ReadLine();
                    reader.Close();
                    reader.Dispose();
                }
                catch { }

                string path = "ggeom.asc";
                if (File.Exists(text[0]) == true)
                    path = text[0];

                try
                {
                    StreamReader reader = new StreamReader(path);
                    string[] isbin = new string[1];
                    isbin = reader.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    reader.Close();
                    reader.Dispose();

                    if (Convert.ToDouble(isbin[0]) < 0) // binary mode
                    {
                        using (BinaryReader readbin = new BinaryReader(File.Open(path, FileMode.Open))) // read ggeom.asc binary mode
                        {
                            // read 1st line inclusive carriage return and line feed
                            byte[] header;
                            header = readbin.ReadBytes(6);

                            //obtain array size in x,y,z direction
                            Program.NI = readbin.ReadInt32();
                            Program.NJ = readbin.ReadInt32();
                            Program.NK = readbin.ReadInt32();

                            // read AH[] array
                            for (int j = 1; j < Program.NJ + 1; j++)
                            {
                                for (int i = 1; i < Program.NI + 1; i++)
                                {
                                    Program.AH[i][j] = readbin.ReadSingle();
                                }
                            }
                            for (int k = 1; k < Program.NK + 1; k++)
                            {
                                for (int j = 1; j < Program.NJ + 1; j++)
                                {
                                    for (int i = 1; i < Program.NI + 1; i++)
                                    {
                                        Program.ZSP[i][j][k] = readbin.ReadSingle();
                                    }
                                }
                            }

                            for (int i = 1; i < Program.NI + 2; i++)
                            {
                                Program.XKO[i] = readbin.ReadSingle();
                            }
                            for (int i = 1; i < Program.NJ + 2; i++)
                            {
                                Program.YKO[i] = readbin.ReadSingle();
                            }
                            for (int i = 1; i < Program.NK + 2; i++)
                            {
                                Program.ZKO[i] = readbin.ReadSingle();
                            }

                            // not used VOL[] and AREAS[]
                            for (int k = 1; k < Program.NZ + 1; k++)
                            {
                                for (int j = 1; j < Program.NY + 1; j++)
                                {
                                    for (int i = 1; i < Program.NX + 1; i++)
                                    {
                                        readbin.ReadSingle();
                                    }
                                }
                            }

                            for (int k = 1; k < Program.NZ + 1; k++)
                            {
                                for (int j = 1; j < Program.NY + 1; j++)
                                {
                                    for (int i = 1; i < Program.NX + 2; i++)
                                    {
                                        readbin.ReadSingle();
                                    }
                                }
                            }

                            for (int k = 1; k < Program.NZ + 1; k++)
                            {
                                for (int j = 1; j < Program.NY + 2; j++)
                                {
                                    for (int i = 1; i < Program.NX + 1; i++)
                                    {
                                        readbin.ReadSingle();
                                    }
                                }
                            }

                            for (int k = 1; k < Program.NZ + 2; k++)
                            {
                                for (int j = 1; j < Program.NY + 1; j++)
                                {
                                    for (int i = 1; i < Program.NX + 1; i++)
                                    {
                                        readbin.ReadSingle();
                                    }
                                }
                            }

                            for (int k = 1; k < Program.NZ + 2; k++)
                            {
                                for (int j = 1; j < Program.NY + 1; j++)
                                {
                                    for (int i = 1; i < Program.NX + 1; i++)
                                    {
                                        readbin.ReadSingle();
                                    }
                                }
                            }

                            for (int k = 1; k < Program.NZ + 2; k++)
                            {
                                for (int j = 1; j < Program.NY + 1; j++)
                                {
                                    for (int i = 1; i < Program.NX + 1; i++)
                                    {
                                        readbin.ReadSingle();
                                    }
                                }
                            }

                            for (int i = 1; i < Program.NI + 1; i++)
                            {
                                Program.DDX[i] = readbin.ReadSingle();
                            }

                            for (int i = 1; i < Program.NJ + 1; i++)
                            {
                                Program.DDY[i] = readbin.ReadSingle();
                            }

                            for (int i = 1; i < Program.NI + 1; i++)
                            {
                                readbin.ReadSingle();
                            }

                            for (int i = 1; i < Program.NJ + 1; i++)
                            {
                                readbin.ReadSingle();
                            }

                            Program.IKOOA = readbin.ReadInt32();
                            Program.JKOOA = readbin.ReadInt32();
                            readbin.ReadDouble(); // angle not used

                            for (int k = 1; k < Program.NK + 2; k++)
                            {
                                for (int j = 1; j < Program.NJ + 2; j++)
                                {
                                    for (int i = 1; i < Program.NI + 2; i++)
                                    {
                                        Program.AHE[i][j][k] = readbin.ReadSingle();
                                    }
                                }
                            }

                            if (Program.AHE[1][1][Program.NK] < 1) // Poblem reading AHE[]
                            {
                                string err = "Error when reading AHE[] from file ggeom.asc. Execution stopped: press ESC to stop";
                                Console.WriteLine(err);
                                ProgramWriters.LogfileProblemreportWrite(err);

                                if (Program.IOUTPUT <= 0)           // if not a SOUNDPLAN Project
                                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                                Environment.Exit(0);        // Exit console
                            }
                            Program.Topo = 1;
                        }

                    }
                    else // ggeom asc - ascii mode
                    {
                        using (StreamReader read1 = new StreamReader(path))  // read ggeom.asc ascii format
                        {
                            int count = 0;

                            text = read1.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            count = count + text.Length;

                            //obtain array sizes in x,y,z direction
                            Program.NI = Convert.ToInt32(text[0]);
                            Program.NJ = Convert.ToInt32(text[1]);
                            Program.NK = Convert.ToInt32(text[2]);

                            //obtain X, Y, and Z
                            for (int i = 3; i < 3 + Program.NX + 1; i++)
                            {
                                Program.XKO[i - 2] = Convert.ToDouble(text[i].Replace(".", Program.Decsep));
                            }
                            int n = 0;
                            for (int i = 3 + Program.NX + 1; i < 3 + Program.NX + 1 + Program.NY + 1; i++)
                            {
                                n++;
                                Program.YKO[n] = Convert.ToDouble(text[i].Replace(".", Program.Decsep));
                            }
                            n = 0;
                            for (int i = 3 + Program.NX + 1 + Program.NY + 1; i < text.Length; i++)
                            {
                                n++;
                                Program.ZKO[n] = Convert.ToDouble(text[i].Replace(".", Program.Decsep));
                            }

                            //obtain surface heights
                            n = 0;
                            text = read1.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 1; j < Program.NY + 1; j++)
                            {
                                for (int i = 1; i < Program.NX + 1; i++)
                                {
                                    Program.AH[i][j] = Convert.ToSingle(text[n].Replace(".", Program.Decsep));
                                    n++;
                                }
                            }
                            //obtain grid volumes are stored here but not used in GRAL
                            text[0] = read1.ReadLine();
                            //obtain areas in x-direction are stored here but not used in GRAL
                            text[0] = read1.ReadLine();
                            //obtain areas in y-direction are stored here but not used in GRAL
                            text[0] = read1.ReadLine();
                            //obtain projection of z-area in x-direction are stored here but not used in GRAL
                            text[0] = read1.ReadLine();
                            //obtain projection of z-area in y-direction are stored here but not used in GRAL
                            text[0] = read1.ReadLine();
                            //obtain area in z-direction are stored here but not used in GRAL
                            text[0] = read1.ReadLine();
                            //obtain cell-heights
                            n = 0;
                            text = read1.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int k = 1; k < Program.NZ + 1; k++)
                            {
                                for (int j = 1; j < Program.NY + 1; j++)
                                {
                                    for (int i = 1; i < Program.NX + 1; i++)
                                    {
                                        Program.ZSP[i][j][k] = Convert.ToSingle(text[n].Replace(".", Program.Decsep));
                                        n++;
                                    }
                                }
                            }
                            //obtain grid cells sizes in x-direction
                            n = 0;
                            text = read1.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 1; i < Program.NX + 1; i++)
                            {
                                Program.DDX[i] = Convert.ToSingle(text[n].Replace(".", Program.Decsep));
                                n++;
                            }
                            //obtain grid cells sizes in y-direction
                            n = 0;
                            text = read1.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 1; i < Program.NY + 1; i++)
                            {
                                Program.DDY[i] = Convert.ToSingle(text[n].Replace(".", Program.Decsep));
                                n++;
                            }
                            //obtain distances of neighbouring grid cells in x-direction are stored here but not used in GRAL
                            text[0] = read1.ReadLine();
                            //obtain distances of neighbouring grid cells in y-direction are stored here but not used in GRAL
                            text[0] = read1.ReadLine();
                            //obtain western and southern borders of the model domain and the angle (not used anymore) between the main model domain axis and north
                            text = read1.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            Program.IKOOA = Convert.ToInt32(text[0]);
                            Program.JKOOA = Convert.ToInt32(text[1]);

                            //obtain heights of the cell corners
                            n = 0;
                            text = read1.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int k = 1; k < Program.NZ + 2; k++)
                            {
                                for (int j = 1; j < Program.NY + 2; j++)
                                {
                                    for (int i = 1; i < Program.NX + 2; i++)
                                    {
                                        Program.AHE[i][j][k] = Convert.ToSingle(text[n].Replace(".", Program.Decsep));
                                        n++;
                                    }
                                }
                            }

                            if (Program.AHE[1][1][Program.NK] < 1) // Poblem reading AHE[]
                            {
                                string err = "Error when reading AHE[] from file ggeom.asc. Execution stopped: press ESC to stop";
                                Console.WriteLine(err);
                                ProgramWriters.LogfileProblemreportWrite(err);

                                if (Program.IOUTPUT <= 0)           // if not a SOUNDPLAN Project
                                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                                Environment.Exit(0);        // Exit console
                            }

                            Program.Topo = 1;
                        } // Read ggeom.asc ascii format
                    }
                }
                catch
                {
                    string err = "Error when reading file ggeom.asc. Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                    Environment.Exit(0);
                }

            }
            else
            {
                Console.WriteLine("ggeom.asc does not exist. Simulation is carried out for flat terrain.");
            }
        }//read ggeom.asc

        /// <summary>
        ///Read the buildings in case of terrain
        /// </summary>
        public void ReadBuildingsTerrain()
        {
            if (File.Exists("buildings.dat") == true)
            {
                Program.BuildingTerrExist = true;
                int block = 0;
                Console.WriteLine();
                Console.WriteLine("Reading building file buildings.dat");

                try
                {
                    using (StreamReader read = new StreamReader("buildings.dat"))
                    {
                        string[] text = new string[1];
                        string text1;
                        Int32 IXCUT;
                        Int32 IYCUT;
                        while ((text1 = read.ReadLine()) != null)
                        {
                            block++;
                            text = text1.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            double cic = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                            double cjc = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                            double cz = Convert.ToDouble(text[2].Replace(".", Program.Decsep));
                            double czo = Convert.ToDouble(text[3].Replace(".", Program.Decsep));
                            IXCUT = (int)((cic - Program.XsiMinGral) / Program.DXK) + 1;
                            IYCUT = (int)((cjc - Program.EtaMinGral) / Program.DYK) + 1;
                            if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                            {
                                if (czo < 0) // absolute height of building
                                {
                                    if (Program.GralTopofile)
                                    {
                                        if (Math.Abs(czo) > Program.AHKOri[IXCUT][IYCUT]) // Building height > topography?
                                        {
                                            czo = Math.Abs(czo) - Program.AHKOri[IXCUT][IYCUT]; // compute relative building height for this cell
                                            czo = Math.Max(0, czo); // if czo < AHK -> set building height to 0
                                        }
                                        else
                                            czo = 0; // building height = 0
                                    }
                                    else
                                    {
                                        string err = "You are using absolute building coordinates but no original GRAL terrain  - ESC = Exit";
                                        Console.WriteLine(err);
                                        ProgramWriters.LogfileProblemreportWrite(err);

                                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                                        Environment.Exit(0);
                                    }
                                }
                                Program.CUTK[IXCUT][IYCUT] = (float)Math.Max(czo, Program.CUTK[IXCUT][IYCUT]);
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when reading file building.dat in line " + block.ToString() + " Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                    Environment.Exit(0);
                }

                string Info = "Total number of blocked cells in 2D: " + block.ToString();
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
                ProgramWriters.LogfileGralCoreWrite(" ");

                CalculateSubDomainsForPrognosticWindSimulation();
            }
        }//read buildings in case of terrain

        /// <summary>
        ///Read the buildings in case of flat terrain
        /// </summary>
        public void ReadBuildingsFlat()
        {
            string Info;
            if (File.Exists("buildings.dat") == true)
            {
                Program.BuildingFlatExist = true;
                int block = 0;
                Console.WriteLine();
                Console.WriteLine("Reading building file buildings.dat");

                try
                {
                    using (StreamReader read = new StreamReader("buildings.dat"))
                    {
                        string[] text = new string[1];
                        string text1;
                        Int32 IXCUT;
                        Int32 IYCUT;
                        while ((text1 = read.ReadLine()) != null)
                        {
                            block++;
                            text = text1.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            double cic = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                            double cjc = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                            double cz = Convert.ToDouble(text[2].Replace(".", Program.Decsep));
                            double czo = Convert.ToDouble(text[3].Replace(".", Program.Decsep));
                            IXCUT = (int)((cic - Program.XsiMinGral) / Program.DXK) + 1;
                            IYCUT = (int)((cjc - Program.EtaMinGral) / Program.DYK) + 1;
                            if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                            {
                                if (czo < 0) // absolute height of building
                                {
                                    string err = "You are using absolute building coordinates but flat terrain  - ESC = Exit";
                                    Console.WriteLine(err);
                                    ProgramWriters.LogfileProblemreportWrite(err);

                                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                                    Environment.Exit(0);
                                }
                                Program.CUTK[IXCUT][IYCUT] = (float)Math.Max(czo, Program.CUTK[IXCUT][IYCUT]);
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when reading file building.dat in line " + block.ToString() + " Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                    Environment.Exit(0);
                }

                Info = "Total number of blocked cells in 2D: " + block.ToString();
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
                ProgramWriters.LogfileGralCoreWrite(" ");

                CalculateSubDomainsForPrognosticWindSimulation();                
            }
        }//read buildings in case of flat terrain

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
                                    Program.ADVDOM[i1][j1] = 1;
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        ///Read the main control file in.dat
        /// </summary>
        public void ReadInDat()
        {
            if (File.Exists("in.dat") == true)
            {
                int _line = 1;
                try
                {
                    using (StreamReader sr = new StreamReader("in.dat"))
                    {
                        string[] text = new string[1];
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.TPS = Convert.ToSingle(text[0].Replace(".", Program.Decsep));

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.TAUS = Convert.ToSingle(text[0].Replace(".", Program.Decsep));

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.ISTATIONAER = Convert.ToInt32(text[0].Replace(".", Program.Decsep));

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.IStatistics = Convert.ToInt32(text[0].Replace(".", Program.Decsep));
                        if (Program.IStatistics == 1)
                        {
                            Console.WriteLine();
                            string err = "The meteorological input file meteo.all is not supported anymore. Check flag in file in.dat. -> Execution stopped: press ESC to stop";
                            Console.WriteLine(err);
                            ProgramWriters.LogfileProblemreportWrite(err);

                            if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                                while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                            Environment.Exit(0);
                        }

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.ReceptorsAvailable = Convert.ToInt32(text[0].Replace(".", Program.Decsep));

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.Z0 = Convert.ToSingle(text[0].Replace(".", Program.Decsep));

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.LatitudeDomain = Convert.ToSingle(text[0].Replace(".", Program.Decsep));

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);

                        if (text[0].ToUpper().Contains("J") || text[0].ToUpper().Contains("Y"))
                        {
                            Program.MeanderingOff = true;
                        }
                        else
                        {
                            Program.MeanderingOff = false;
                        }

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.Pollutant = text[0];

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i <= Program.NS; i++)
                        {
                            try
                            {
                                text[i] = text[i].Trim();
                                text[i] = text[i].Replace(".", decsep);
                                Program.HorSlices[i + 1] = Convert.ToSingle(text[i]);
                            }
                            catch { }
                        }

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.dz = Convert.ToSingle(text[0].Replace(".", Program.Decsep));

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.IWETstart = Convert.ToInt16(text[0]);

                        _line++;
                        text = sr.ReadLine().Split(new char[] {'!' }, StringSplitOptions.RemoveEmptyEntries); // Remove comment
                        text = text[0].Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.FlowFieldLevel = Convert.ToInt32(text[0].Replace(".", Program.Decsep));
                        if (text.Length > 1)
                        {
                            try
                            {
                                Program.PrognosticSubDomainFactor = Convert.ToInt32(text[1].Replace(".", Program.Decsep));
                            }
                            catch{}
                            Program.PrognosticSubDomainFactor = Math.Max(15, Program.PrognosticSubDomainFactor);
                        }

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.IOUTPUT = Convert.ToInt32(text[0].Replace(".", Program.Decsep));

                        if (sr.EndOfStream == false)
                        {
                           _line++;
                            text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                            if (text[0].Trim().ToLower() == "compressed") 
                            {
                                Program.ResultFileZipped = true; // zipped output
                                if (text.Length > 1 && text[1].Equals("V02"))
                                {
                                    Program.ResultFileHeader = -2; // strong compressed output
                                }
                                if (text.Length > 1 && text[1].Equals("V03"))
                                {
                                    Program.ResultFileHeader = -3; // all cells output
                                }
                            }
                            
                            if (sr.EndOfStream == false)
                            {
                                try
                                {
                                    _line++;
                                    text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (text[0].Trim().ToLower() == "nokeystroke")
                                    {
                                        Program.WaitForConsoleKey = false;
                                    }
                                    else
                                    {
                                        Program.WaitForConsoleKey = true;
                                    }
                                }
                                catch
                                {
                                    Program.WaitForConsoleKey = true;
                                }
                            }
                            
                            Program.WriteASCiiResults = false;
                            if (sr.EndOfStream == false)
                            {
                                try
                                {
                                    _line++;
                                    text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (text[0].Trim().Equals("ASCiiResults"))
                                    {
                                        if (text.Length > 1 && text[1].Equals("1"))
                                        {
                                            Program.WriteASCiiResults = true;
                                            Console.WriteLine("Write ASCii Results");
                                        }
                                    }
                                }
                                catch
                                {
                                 
                                }
                            }

                        }
                    }
                }
                catch
                {
                    Console.WriteLine();
                    string err = "Error reading file in.dat. in line " + _line.ToString() + " -> Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                    Environment.Exit(0);
                }
            }
            else
            {
                Console.WriteLine();
                string err = "Main control file in.dat is missing. -> Execution stopped: press ESC to stop";
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                Environment.Exit(0);
            }
        }//read main control file in.dat

        /// <summary>
        ///Read the Receptor file Receptor.dat
        /// </summary>
        public void ReadReceptors()
        {
            if (Program.ReceptorsAvailable == 1)
            {
                if (File.Exists("Receptor.dat") == true)
                {
                    try
                    {
                        using (StreamReader sr = new StreamReader("Receptor.dat"))
                        {
                            string[] text = new string[1];
                            text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                            Program.ReceptorNumber = Convert.ToInt32(text[0]);

                            //array declarations
                            Program.ReceptorConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count + 1]);
                            Program.ReceptorParticleMaxConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count + 1]);
                            Program.ReceptorTotalConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count + 1]);
                            Program.ReceptorX = new double[Program.ReceptorNumber + 1];
                            Program.ReceptorY = new double[Program.ReceptorNumber + 1];
                            Program.ReceptorZ = new float[Program.ReceptorNumber + 1];
                            Program.ReceptorIInd = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorJInd = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorKInd = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorIIndFF = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorJIndFF = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorKIndFF = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorNearbyBuilding = new bool [Program.ReceptorNumber + 1];
                        }
                    }
                    catch
                    {
                        Console.WriteLine();
                        string err = "Error reading file Receptor.dat";
                        Console.WriteLine(err);
                        ProgramWriters.LogfileProblemreportWrite(err);
                        Program.ReceptorNumber = 0;
                        Program.ReceptorsAvailable = 0;
                        // if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        // 	while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                        // Environment.Exit(0);
                    }
                }
                else
                {
                    Console.WriteLine();
                    string err = "Receptor file Receptor.dat is missing";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);
                    Program.ReceptorNumber = 0;

                    // if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    // 	while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                    // Environment.Exit(0);
                }
            }
        }//read receptors

        /// <summary>
        ///Read the emission modulation for transient mode
        /// </summary>
        public void ReadEmissionTimeseries()
        {
            if (Program.ISTATIONAER == 0)
            {
                if (File.Exists("emissions_timeseries.txt") == true)
                {
                    Program.EmissionTimeseriesExist = true;
                    try
                    {
                        var lineCount = File.ReadLines("emissions_timeseries.txt").Count();
                        Program.EmFacTimeSeries = new float[lineCount, Program.SourceGroups.Count];

                        using (StreamReader sr = new StreamReader("emissions_timeseries.txt"))
                        {
                            //read timeseries of emissions
                            string[] text10 = new string[1];
                            //get source group numbers
                            text10 = sr.ReadLine().Split(new char[] { ' ', ':', '-', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            int SG_Time_Series_Count = text10.Length - 2; // number of Source groups in emissions_timeseries.txt
                            int[] SG_Time_Series = new int[SG_Time_Series_Count];

                            for (int ii = 2; ii < text10.Length; ii++)
                            {
                                //get the column corresponding with the source group number stored in sg_numbers
                                int sg_temp = Convert.ToInt16(text10[ii]);
                                SG_Time_Series[ii - 2] = sg_temp; // remember the real SG Number for each column in emissions_timeseries.txt
                            }

                            int i = 0;
                            while (sr.EndOfStream == false)
                            {
                                text10 = sr.ReadLine().Split(new char[] { ' ', ':', '-', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);

                                for (int sg_i = 0; sg_i < Program.SourceGroups.Count; sg_i++) // set emission factors to 1 by default
                                {
                                    Program.EmFacTimeSeries[i, sg_i] = 1;
                                }

                                for (int n = 0; n < SG_Time_Series_Count; n++) // check each source group defined in emissions_timeseries.txt
                                {
                                    int SG_internal = Program.Get_Internal_SG_Number(SG_Time_Series[n]); // get the internal SG number

                                    if (SG_internal >= 0 && (n + 2) < text10.Length) // otherwise this source group in emissions_timeseries.txt does not exist internal in GRAL
                                    {
                                        Program.EmFacTimeSeries[i, SG_internal] = Convert.ToSingle(text10[n + 2].Replace(".", decsep));
                                    }
                                    else
                                    {
                                        Program.EmFacTimeSeries[i, SG_internal] = 1;
                                    }
                                }
                                i++;
                            }
                        }
                    }
                    catch
                    { }
                }
            }
        }//emission modulation for transient mode

        /// <summary>
        ///Read the file precipitation.txt
        /// </summary>
        public void ReadPrecipitationTXT()
        {
            CultureInfo ic = CultureInfo.InvariantCulture;
            float Precipitation_Sum = 0;
            string Info;

            if (Program.ISTATIONAER == 0)
            {
                if (File.Exists("Precipitation.txt") == true && Program.WetDeposition == true)
                {
                    try
                    {

                        using (StreamReader sr = new StreamReader("Precipitation.txt"))
                        {
                            //read "Precipitation.txt"
                            string[] text10 = new string[1];

                            // Read header
                            text10 = sr.ReadLine().Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            while (sr.EndOfStream == false)
                            {
                                text10 = sr.ReadLine().Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                                if (text10.Length > 2)
                                {
                                    float prec = Convert.ToSingle(text10[2], ic);
                                    Precipitation_Sum += prec;
                                    Program.WetDepoPrecipLst.Add(prec);
                                }
                            }
                        }
                    }
                    catch
                    { }

                    // plausibility checks for precipitation
                    if (Precipitation_Sum < 0.1)
                    {
                        Program.WetDeposition = false;
                        Info = "Precipitation sum = 0 - wet deposition is not computed";
                        Console.WriteLine(Info);
                    }
                    else if (Program.WetDepoPrecipLst.Count != Program.MeteoTimeSer.Count)
                    {
                        Program.WetDeposition = false;
                        Info = "The number of situations in the file Precipitation.txt does not match the number in the file mettimeseries.txt - wet deposition is not computed";
                        Console.WriteLine(Info);
                        ProgramWriters.LogfileGralCoreWrite(Info);
                        ProgramWriters.LogfileGralCoreWrite(" ");
                    }
                    else
                    {
                        Info = "Wet deposition is computed, the precipitation sum = " + Precipitation_Sum.ToString() + " mm";
                        Console.WriteLine(Info);

                        Info = "Wet deposition parameters cW = " + Program.Wet_Depo_CW.ToString() + " 1/s    AlphaW = " + Program.WedDepoAlphaW.ToString();
                        Console.WriteLine(Info);

                        ProgramWriters.LogfileGralCoreWrite(" ");
                        Console.WriteLine("");

                        Program.WetDeposition = true;
                    }

                }
            }
        } // Read Precipitation.txt

        /// <summary>
        ///Reading optional files used to define areas where either the tunnel jet stream is destroyed due to traffic
        ///on the opposite lanes of a highway or where pollutants are sucked into a tunnel portal
        /// </summary>
        public void ReadTunnelfilesOptional()
        {
            if (Program.TS_Count > 0)
            {
                Program.TUN_ENTR = Program.CreateArray<Int16[]>(Program.NII + 2, () => new Int16[Program.NJJ + 2]);
                Program.OPP_LANE = Program.CreateArray<Int16[]>(Program.NII + 2, () => new Int16[Program.NJJ + 2]);
                if (File.Exists("Opposite_lane.txt") == true)
                {
                    Program.TunnelOppLane = true;
                    Console.WriteLine();
                    Console.WriteLine("Reading file Opposite_lane.txt: ");
                    int block = 0;

                    try
                    {
                        using (StreamReader read = new StreamReader("Opposite_lane.txt"))
                        {
                            string[] text = new string[1];
                            string text1;
                            Int32 IXCUT;
                            Int32 IYCUT;
                            while ((text1 = read.ReadLine()) != null)
                            {
                                block++;
                                text = text1.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                double ci = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                                double cj = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                                IXCUT = (int)((ci - Program.XsiMinGral) / Program.DXK) + 1;
                                IYCUT = (int)((cj - Program.EtaMinGral) / Program.DYK) + 1;
                                if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                                    Program.OPP_LANE[IXCUT][IYCUT] = 1;
                            }
                        }
                    }
                    catch
                    {
                        string err = "Error when reading fileOpposite_lane.txt in line " + block.ToString() + " Execution stopped: press ESC to stop";
                        Console.WriteLine(err);
                        ProgramWriters.LogfileProblemreportWrite(err);

                        if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                        Environment.Exit(0);
                    }
                }

                if (File.Exists("tunnel_entrance.txt") == true)
                {
                    Program.TunnelEntr = true;
                    Console.WriteLine();
                    Console.WriteLine("Reading file tunnel_entrance.txt: ");
                    int block = 0;

                    try
                    {
                        using (StreamReader read = new StreamReader("tunnel_entrance.txt"))
                        {
                            string[] text = new string[1];
                            string text1;
                            Int32 IXCUT;
                            Int32 IYCUT;
                            while ((text1 = read.ReadLine()) != null)
                            {
                                block++;
                                text = text1.Split(new char[] { ' ', ',', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
                                double ci = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                                double cj = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                                IXCUT = (int)((ci - Program.XsiMinGral) / Program.DXK) + 1;
                                IYCUT = (int)((cj - Program.EtaMinGral) / Program.DYK) + 1;
                                if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                                    Program.TUN_ENTR[IXCUT][IYCUT] = 1;
                            }
                        }
                    }
                    catch
                    {
                        string err = "Error when reading file tunnel_entrance.txt in line " + block.ToString() + " Execution stopped: press ESC to stop";
                        Console.WriteLine(err);
                        ProgramWriters.LogfileProblemreportWrite(err);

                        if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                        Environment.Exit(0);
                    }
                }
            }
        }//reading optional files used to define areas where either the tunnel jet stream is destroyed due to traffic
         //on the opposite lanes of a highway or where pollutants are sucked into a tunnel portal

        //read landuse file
        /// <summary>
        /// Read the landuse.asc file
        /// </summary>
        public void ReadLanduseFile()
        {
            if (File.Exists("landuse.asc") == true)
                Program.LandUseAvailable = true;

            if ((Program.Topo == 1) && (Program.LandUseAvailable == true))
            {
                string[] text = new string[(Program.NI + 2) * (Program.NJ + 2)];

                try
                {
                    using (StreamReader r = new StreamReader("landuse.asc"))
                    {
                        text = Convert.ToString(r.ReadLine()).Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        text = Convert.ToString(r.ReadLine()).Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        text = Convert.ToString(r.ReadLine()).Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        int n = 0;
                        for (int j = 1; j < Program.NJ + 1; j++)
                        {
                            for (int i = 1; i < Program.NI + 1; i++)
                            {
                                Program.Z0Gramm[i][j] = Convert.ToSingle(text[n].Replace(".", Program.Decsep));
                                n++;
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when reading file 'landuse.asc' - Execution stopped";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                    Environment.Exit(0);
                }
            }
            else
            {
                for (int ix = 1; ix <= Program.NX; ix++)
                {
                    for (int iy = 1; iy <= Program.NY; iy++)
                    {
                        Program.Z0Gramm[ix][iy] = Program.Z0;
                    }
                }
            }
        }//read landuse

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
                            throw new ArgumentOutOfRangeException("Microscale terrain grid is not equal to flow field grid");

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

        //read vegetation
        /// <summary>
        /// Read the vegetation.dat file
        /// </summary>	
        public void ReadVegetation()
        {
            CultureInfo ic = CultureInfo.InvariantCulture;

            if (File.Exists("vegetation.dat") == true)
            {
                int block = 0;
                Program.VegetationExist = true;
                Console.WriteLine();
                Console.WriteLine("Reading building file vegetation.dat");

                try
                {
                    using (StreamReader read = new StreamReader("vegetation.dat"))
                    {
                        double czu = 0;
                        double czo = 0;
                        double COV = 0;
                        double LAD = 0;
                        double trunk_height = 0;
                        double crown_height = 0;
                        double trunk_LAD = 0;
                        double crown_LAD = 0;
                        //Vegetation influence
                        float veg_fac = 0;

                        string[] text = new string[1];
                        string text1;
                        Int32 IXCUT;
                        Int32 IYCUT;
                        Program.VEG[0][0][0] = 0;

                        while (read.EndOfStream == false)
                        {
                            text1 = read.ReadLine();
                            block++;
                            text = text1.Split(new char[] { '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

                            //Vegetation influence
                            if (text[0].Contains("D") && text.Length > 4) // Data line
                            {
                                crown_height = Convert.ToDouble(text[1], ic);
                                trunk_height = Convert.ToDouble(text[2], ic) * crown_height * 0.01;
                                trunk_LAD = Convert.ToDouble(text[3], ic);
                                crown_LAD = Convert.ToDouble(text[4], ic);
                                COV = Convert.ToDouble(text[5], ic) * 0.01;
                            }
                            else if (text.Length > 1) // Coordinates
                            {
                                double cic = Convert.ToDouble(text[0], ic);
                                double cjc = Convert.ToDouble(text[1], ic);
                                IXCUT = (int)((cic - Program.GralWest) / Program.DXK) + 1;
                                IYCUT = (int)((cjc - Program.GralSouth) / Program.DYK) + 1;

                                if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                                {
                                    //separation between trunk anc crown zone
                                    for (int n = 1; n < 3; n++)
                                    {
                                        //Vegetation influence
                                        if (n == 1)
                                        {
                                            LAD = trunk_LAD;
                                            czu = 0;
                                            czo = trunk_height;
                                        }
                                        else
                                        {
                                            LAD = crown_LAD;
                                            czu = trunk_height;
                                            czo = crown_height;
                                        }

                                        veg_fac = (float)(0.3 * Math.Pow(COV, 3) * LAD);

                                        for (int k = Program.KKART[IXCUT][IYCUT]; k < Program.NKK; k++)
                                        {
                                            if (Program.HOKART[k] - Program.HOKART[Program.KKART[IXCUT][IYCUT]] + Program.DZK[k] * 0.5 >= czu && Program.HOKART[k + 1] - Program.HOKART[Program.KKART[IXCUT][IYCUT]] < czo)
                                            {
                                                Program.VEG[IXCUT][IYCUT][k] = veg_fac;
                                            }
                                            else if (Program.HOKART[k] >= czu && Program.HOKART[k] + Program.DZK[k] * 0.5 <= czo)
                                            {
                                                Program.VEG[IXCUT][IYCUT][k] = veg_fac;
                                            }

                                        }
                                    }
                                    //coverage
                                    Program.COV[IXCUT][IYCUT] = (float)COV;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when reading file vegetation.dat in line " + block.ToString() + " Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                    Environment.Exit(0);
                }

                string Info = "Total number of vegetation cells in 2D: " + block.ToString();
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
                ProgramWriters.LogfileGralCoreWrite(" ");
            }
        }//read vegetation


        /// <summary>
        /// Read the vegetation.dat domain
        /// </summary>	
        public void ReadVegetationDomain()
        {
            CultureInfo ic = CultureInfo.InvariantCulture;

            if (File.Exists("vegetation.dat") == true)
            {
                int block = 0;
                Program.VegetationExist = true;
                Console.WriteLine();
                Console.WriteLine("Reading file vegetation.dat");

                try
                {
                    using (StreamReader read = new StreamReader("vegetation.dat"))
                    {
                        double czo = 0;

                        string[] text = new string[1];
                        string text1;
                        Int32 IXCUT;
                        Int32 IYCUT;
                        while (read.EndOfStream == false)
                        {
                            text1 = read.ReadLine();
                            block++;
                            text = text1.Split(new char[] { '\t', ',' });

                            if (text[0].Contains("D") && text.Length > 5) // Data line
                            {
                                czo = Convert.ToDouble(text[1], ic);
                            }
                            else if (text.Length > 1) // Coordinates
                            {
                                double cic = Convert.ToDouble(text[0], ic);
                                double cjc = Convert.ToDouble(text[1], ic);

                                IXCUT = (int)((cic - Program.GralWest) / Program.DXK) + 1;
                                IYCUT = (int)((cjc - Program.GralSouth) / Program.DYK) + 1;

                                if ((IXCUT <= Program.NII) && (IXCUT >= 1) && (IYCUT <= Program.NJJ) && (IYCUT >= 1))
                                {
                                    //compute sub-domains of GRAL for the prognostic wind-field simulations
                                    int iplus = (int)(Program.PrognosticSubDomainFactor * czo / Program.DXK);
                                    int jplus = (int)(Program.PrognosticSubDomainFactor * czo / Program.DYK);
                                    //for (int i1 = IXCUT - iplus; i1 <= IXCUT + iplus; i1++)
                                    Parallel.For(IXCUT - iplus, IXCUT + iplus + 1, Program.pOptions, i1 =>
                                    {
                                        for (int j1 = IYCUT - jplus; j1 <= IYCUT + jplus; j1++)
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
                }
                catch
                {
                    string err = "Error when reading file vegetation.dat in line " + block.ToString() + " Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                    Environment.Exit(0);
                }

                string Info = "Total number of vegetation cells in 2D: " + block.ToString();
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
                ProgramWriters.LogfileGralCoreWrite(" ");
            }
        }//read vegetation_Domain

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

        public string ReadGFFFilePath()
        {
            string  gff_filepath = string.Empty;
            try
            {
                if (File.Exists("GFF_FilePath.txt"))
                {
                    using (StreamReader reader = new StreamReader("GFF_FilePath.txt"))
                    {
                        if (Program.RunOnUnix) reader.ReadLine(); //19.05.25 Ku: read 1st line -> reserved for windows, 2nd line for LINUX
                        string filepath = reader.ReadLine();
                        if (Directory.Exists(filepath))
                        {
                            gff_filepath = filepath;
                        }
                    }
                }
            }
            catch{}
            return gff_filepath;
        }
    }
}