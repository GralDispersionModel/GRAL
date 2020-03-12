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
    }
}