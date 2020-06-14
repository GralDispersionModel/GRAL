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
        ///Read the main control file in.dat - Gral.geb must be read before this routine!
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
                            {
                                while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                                {
                                    ;
                                }
                            }

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
                        for (int i = 0; i < Program.NS; i++)
                        {
                            try
                            {
                                text[i] = text[i].Trim();
                                text[i] = text[i].Replace(".", decsep);
                                Program.HorSlices[i] = Convert.ToSingle(text[i]);
                            }
                            catch { }
                        }

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.GralDz = Convert.ToSingle(text[0].Replace(".", Program.Decsep));

                        _line++;
                        text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.IWETstart = Convert.ToInt16(text[0]);

                        _line++;
                        text = sr.ReadLine().Split(new char[] { '!' }, StringSplitOptions.RemoveEmptyEntries); // Remove comment
                        text = text[0].Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.FlowFieldLevel = Convert.ToInt32(text[0].Replace(".", Program.Decsep));
                        if (text.Length > 1)
                        {
                            try
                            {
                                Program.PrognosticSubDomainFactor = Convert.ToInt32(text[1].Replace(".", Program.Decsep));
                            }
                            catch { }
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

                            Program.AdaptiveRoughnessMax = 0;
                            if (sr.EndOfStream == false)
                            {
                                try
                                {
                                    _line++;
                                    text = sr.ReadLine().Split(new char[] { ' ', ',', '\r', '\n', ';', '!' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (text.Length > 0 && float.TryParse(text[0], System.Globalization.NumberStyles.Any, ic, out float r) && Program.IStatistics == 4)
                                    {
                                        Program.AdaptiveRoughnessMax = r;
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
                    {
                        while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                        {
                            ;
                        }
                    }

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
                {
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                    {
                        ;
                    }
                }

                Environment.Exit(0);
            }
        } //read main control file in.dat
    }
}