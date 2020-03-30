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
    class ReadReceptors
    {
        /// <summary>
    	///Read receptor data from file "Receptor.dat"; flow field terrain is needed
    	/// </summary>
        public static void ReadReceptor()
        {
            //read file Receptor.dat
            if (File.Exists("Receptor.dat") == true)
            {
                int block = 1;
                using (StreamReader sr = new StreamReader("Receptor.dat"))
                {
                    try
                    {
                        string[] text = new string[1];
                        text = sr.ReadLine().Split(new char[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        Program.ReceptorNumber = Convert.ToInt16(text[0]);
                        string text1;

                        // read entire receptor file and adjust Program.irec if needed
                        while (sr.EndOfStream == false)
                        {
                            text1 = sr.ReadLine();

                            text = text1.Split(new char[] { ',', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                            if (text.Length > 3) // at least 4 entries available?
                            {
                                // if number of receptors is higher than receptor lines
                                if (block > Program.ReceptorNumber)
                                {
                                    Program.ReceptorNumber = block;
                                    Program.ReceptorConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count + 1]);
                                    Program.ReceptorParticleMaxConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count + 1]);
                                    Program.ReceptorTotalConc = Program.CreateArray<double[]>(Program.ReceptorNumber + 1, () => new double[Program.SourceGroups.Count + 1]);
                                    Array.Resize(ref Program.ReceptorX, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorX, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorY, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorZ, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorIInd, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorJInd, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorKInd, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorIIndFF, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorJIndFF, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorKIndFF, Program.ReceptorNumber + 1);
                                    Array.Resize(ref Program.ReceptorNearbyBuilding, Program.ReceptorNumber + 1);
                                }

                                Program.ReceptorX[block] = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                                Program.ReceptorY[block] = Convert.ToDouble(text[2].Replace(".", Program.Decsep));
                                Program.ReceptorZ[block] = Convert.ToSingle(text[3].Replace(".", Program.Decsep));
                                if (text.Length > 4)
                                {
                                    Program.ReceptorName.Add(text[4]);
                                }
                                else
                                {
                                    Program.ReceptorName.Add("Rec." + block.ToString());
                                }

                                if (Program.ReceptorZ[block] < Program.GralDz * 0.5F)
                                {
                                    Program.ReceptorZ[block] = Program.GralDz * 0.5F;
                                }

                                double xsi = Program.ReceptorX[block] - Program.IKOOAGRAL;
                                double eta = Program.ReceptorY[block] - Program.JKOOAGRAL;

                                //receptor indices in the GRAL concentration grid
                                Program.ReceptorIInd[block] = (int)(xsi / Program.GralDx) + 1;
                                Program.ReceptorJInd[block] = (int)(eta / Program.GralDy) + 1;
                                Program.ReceptorKInd[block] = (int)(Program.ReceptorZ[block] / Program.GralDz);

                                if (Program.ReceptorIInd[block] > Program.NXL) Program.ReceptorIInd[block] = Program.NXL;
                                if (Program.ReceptorJInd[block] > Program.NYL) Program.ReceptorJInd[block] = Program.NYL;
                                if (Program.ReceptorIInd[block] < 1) Program.ReceptorIInd[block] = 1;
                                if (Program.ReceptorJInd[block] < 1) Program.ReceptorJInd[block] = 1;

                                //receptor indices in the GRAL flow-field grid
                                if (Program.FlowFieldLevel > 0)
                                {
                                    Program.ReceptorIIndFF[block] = (int)(xsi / Program.DXK) + 1;
                                    Program.ReceptorJIndFF[block] = (int)(eta / Program.DYK) + 1;
                                    Program.ReceptorKIndFF[block] = 1;

                                    for (int k = Program.NKK; k >= 1; k--)
                                    {
                                        if (Program.HOKART[k] + Program.AHMIN > Program.ReceptorZ[block] + Program.AHK[Program.ReceptorIIndFF[block]][Program.ReceptorJIndFF[block]])
                                            Program.ReceptorKIndFF[block] = k;
                                    }

                                    if (Program.LogLevel > 0) // additional log output
                                    {
                                        Console.WriteLine("Vertical index of receptor " + block.ToString() + " = " + Program.ReceptorKIndFF[block].ToString());
                                    }

                                    if (Program.ReceptorIIndFF[block] > Program.NII) Program.ReceptorIIndFF[block] = Program.NII;
                                    if (Program.ReceptorJIndFF[block] > Program.NJJ) Program.ReceptorJIndFF[block] = Program.NJJ;
                                    if (Program.ReceptorIIndFF[block] < 1) Program.ReceptorIIndFF[block] = 1;
                                    if (Program.ReceptorJIndFF[block] < 1) Program.ReceptorJIndFF[block] = 1;

                                    // check, if a building higher than the receptor is nearby the receptor or the receptor is at the border
                                    int dx = (int)(Program.GralDx / Program.DXK);
                                    int dy = (int)(Program.GralDy / Program.DYK);
                                    for (int i = Program.ReceptorIIndFF[block] - dx; i <= Program.ReceptorIIndFF[block] + dx; i++)
                                    {
                                        for (int j = Program.ReceptorJIndFF[block] - dy; j <= Program.ReceptorJIndFF[block] + dy; j++)
                                        {
                                            if (i > 1 && j > 1 && i < Program.NII && j < Program.NJJ)
                                            {
                                                if (Program.CUTK[i][j] > Program.ReceptorZ[block])
                                                {
                                                    // in case of equal flow field and concetration raster, otherwise a volume correction is applied
                                                    if (Program.GralDx == Program.DXK && Program.GralDy == Program.DYK)
                                                    {
                                                        Program.ReceptorNearbyBuilding[block] = true;
                                                    }
                                                }
                                            }
                                            else // at the border 
                                            {
                                                Program.ReceptorNearbyBuilding[block] = true;
                                            }
                                        }
                                    }
                                }
                                block++;
                            }
                        }
                    }
                    catch
                    {
                        string err = "Error when reading file 'Receptor.dat' in line " + (block + 1).ToString();
                        Console.WriteLine(err);
                        ProgramWriters.LogfileProblemreportWrite(err);
                        Program.ReceptorNumber = 0;
                        Program.ReceptorsAvailable = 0;
                        return;

                        // if (Program.IOUTPUT <= 0) // Soundplan
                        //     while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                        // Environment.Exit(0);
                    }
                } // using

                if (block < Program.ReceptorNumber)
                {
                    Program.ReceptorNumber = block;
                }

                Console.WriteLine();
                // info about receptors
                for (int i = 1; i <= Program.ReceptorNumber; i++)
                {
                    string info = string.Empty;
                    if ((i - 1) < Program.ReceptorName.Count)
                    {
                        info = "Rec " + i.ToString() + "\t" + Program.ReceptorName[i - 1];
                    }
                    else
                    {
                        info = "Receptor " + i.ToString() + "\t";
                    }
                    info += " : x = " + Math.Round(Program.ReceptorX[i], 1).ToString() + "\t y = " + Math.Round(Program.ReceptorY[i], 1).ToString() + "\t z = " + Math.Round(Program.ReceptorZ[i], 1).ToString();

                    if (Program.ReceptorNearbyBuilding[i])
                    {
                        info += "\t gridded";
                    }
                    ProgramWriters.LogfileGralCoreWrite(info);
                    Console.WriteLine(info);
                }

                ReceptorResetConcentration();
            }
        }

        /// <summary>
    	///Reset the concentration to 0
    	/// </summary>
        public static void ReceptorResetConcentration()
        {
            //set previous concentrations at receptor points to zero
            for (int i = 1; i <= Program.ReceptorNumber; i++)
            {
                for (int k = 0; k < Program.SourceGroups.Count; k++)
                {
                    Program.ReceptorConc[i][k] = 0;
                }
            }
        }
    }
}
