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
                            Program.ReceptorConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count]);
                            Program.ReceptorParticleMaxConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count]);
                            Program.ReceptorTotalConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count]);
                            Program.ReceptorX = new double[Program.ReceptorNumber + 1];
                            Program.ReceptorY = new double[Program.ReceptorNumber + 1];
                            Program.ReceptorZ = new float[Program.ReceptorNumber + 1];
                            Program.ReceptorIInd = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorJInd = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorKInd = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorIIndFF = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorJIndFF = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorKIndFF = new Int32[Program.ReceptorNumber + 1];
                            Program.ReceptorNearbyBuilding = new bool[Program.ReceptorNumber + 1];
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
        } //read receptors
    }
}