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
        public static bool Read(string pfad, bool speculative = false)
        {
            string filename = Path.Combine(pfad, Program.IDISP.ToString("00000") + ".wnd");
            Console.WriteLine(filename);
            bool exists = File.Exists(filename);
            if (exists)
            {
                bool complete = Program.IOUTPUT <= 0
                    ? new WindfieldReader().WindfieldRead(filename, Program.NX, Program.NY, Program.NZ, ref Program.UWIN, ref Program.VWIN, ref Program.WWIN)
                    : new WindfieldReaderSoundplan().WindfieldRead(filename, Program.NX, Program.NY, Program.NZ, ref Program.UWIN, ref Program.VWIN, ref Program.WWIN);
                if (complete) return true;
            }
            if (speculative) return false; // Let the ordered read report failures after the last result is written.

            string error = exists ? "Cannot read the complete wind field: " + filename
                                  : "The following wind field does not exist: " + filename;
            Console.WriteLine(error);
            if (exists || Program.ISTATIONAER != Consts.TransientMode)
            {
                ProgramWriters.LogfileProblemreportWrite(error);
                Program.MeteoInputFailed = true;
                Program.IEND = Consts.CalculationFinished;
            }
            return false;
        }
    }
}
