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

namespace GRAL_2001
{
    class ReadWndFile
    {
        public static bool Read(string pfad)
        {
            bool GRAMMwfhere = true;
            try
            {
                string wndfilename = String.Empty;

                if (pfad != String.Empty)
                {
                    //case 2: wind fields are imported from a different project
                    wndfilename = Path.Combine(pfad, Convert.ToString(Program.IDISP).PadLeft(5, '0') + ".wnd");
                }
                else
                {
                    //case 1: wind fields are located in the same sub-directory as the GRAL executable
                    wndfilename = Convert.ToString(Program.IDISP).PadLeft(5, '0') + ".wnd";
                }

                Console.WriteLine(wndfilename);

                //check whether wind-field exists
                if (File.Exists(wndfilename) == false)
                {
                    string err = "The following wind field does not exist: " + wndfilename;
                    Console.WriteLine(err);

                    //in transient mode GRAL should continue the simulation
                    if (Program.ISTATIONAER != 0)
                    {
                        Console.WriteLine("Simulation interrupted.");
                        ProgramWriters.LogfileProblemreportWrite(err);

                        if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;

                        Environment.Exit(0);
                    }
                    else
                    {
                        GRAMMwfhere = false;
                    }
                }

                //there are two different output formats of GRAMM
                if (Program.IOUTPUT <= 0) // default
                {
                    WindfieldReader Reader = new WindfieldReader();
                    if (Reader.WindfieldRead(wndfilename, Program.NX, Program.NY, Program.NZ, ref Program.UWIN, ref Program.VWIN, ref Program.WWIN) == false)
                    {
                        throw new IOException();
                    }
                }
                else // soundplan
                {
                    WindfieldReaderSoundplan Reader = new WindfieldReaderSoundplan();
                    if (Reader.WindfieldRead(wndfilename, Program.NX, Program.NY, Program.NZ, ref Program.UWIN, ref Program.VWIN, ref Program.WWIN) == false)
                    {
                        throw new IOException();
                    }
                }

            }
            catch
            { }
            return GRAMMwfhere;
        }
    }
}
