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

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
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
                    using (StreamReader reader = new StreamReader("ggeom.asc"))
                    {
                        text[0] = reader.ReadLine(); // read path
                        if (Program.RunOnUnix)       // it is possible to use a 2nd line for compatibility to Windows
                        {
                            if (!File.Exists(text[0])) 
                            {
                                text[0] = reader.ReadLine();
                            }
                        }
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(text[0])) // if ReadLine() was at the end of the stream when running on LINUX
                {
                    text[0] = string.Empty;
                }

                string path = "ggeom.asc";
                try
                {
                    if (File.Exists(text[0]) == true)
                    {
                        path = text[0];
                    }
                    else if (!File.Exists(path))
                    {
                        throw new IOException();
                    }

                    string[] isbin = new string[1];
                    using (StreamReader reader = new StreamReader(path))
                    {
                        isbin = reader.ReadLine().Split(new char[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    }

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

                            Program.GrammWest = readbin.ReadInt32();
                            Program.GrammSouth = readbin.ReadInt32();
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
                                {
                                    Console.ReadKey(true); 	// wait for a key input
                                }

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
                            Program.GrammWest = Convert.ToInt32(text[0]);
                            Program.GrammSouth = Convert.ToInt32(text[1]);

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
                                {
                                    Console.ReadKey(true); 	// wait for a key input
                                }

                                Environment.Exit(0);        // Exit console
                            }

                            Program.Topo = 1;
                        } // Read ggeom.asc ascii format
                    }
                }
                catch
                {
                    string err = "Execution stopped: ggeom.asc not available ot path in ggeom.asc not valid: " + path;
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);
                    
                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    {
                        Console.ReadKey(true); 	// wait for a key input
                    }

                    Environment.Exit(0);
                }

            }
            else
            {
                Console.WriteLine("ggeom.asc does not exist. Simulation is carried out for flat terrain.");
            }
        }//read ggeom.asc
    }
}