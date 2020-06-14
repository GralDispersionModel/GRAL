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
        ///Read a user define path for the *.gff files
        /// </summary>
        public string ReadGFFFilePath()
        {
            string gff_filepath = string.Empty;
            try
            {
                if (File.Exists("GFF_FilePath.txt"))
                {
                    using (StreamReader reader = new StreamReader("GFF_FilePath.txt"))
                    {
                        if (Program.RunOnUnix)
                        {
                            reader.ReadLine(); //19.05.25 Ku: read 1st line -> reserved for windows, 2nd line for LINUX
                        }

                        string filepath = reader.ReadLine();
                        if (Directory.Exists(filepath))
                        {
                            gff_filepath = filepath;
                        }
                    }
                }
            }
            catch { }
            return gff_filepath;
        }
    }
}