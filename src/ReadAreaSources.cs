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
using System.Collections.Generic;
using System.IO;

namespace GRAL_2001
{
    class ReadAreaSources
    {
        /// <summary>
        /// Read Area Sources from the file "cadestre.dat"
        /// </summary>
    	public static void Read()
        {

            List<SourceData> AQ = new List<SourceData>();

            double totalemission = 0;
            int countrealsources = 0;
            double[] emission_sourcegroup = new double[101];

            AQ.Add(new SourceData());

            if (Program.IMQ.Count == 0)
            {
                Program.IMQ.Add(0);
            }

            Deposition Dep = new Deposition();

            StreamReader read = new StreamReader("cadastre.dat");
            try
            {
                string[] text = new string[1];
                string text1;
                text1 = read.ReadLine();
                while ((text1 = read.ReadLine()) != null)
                {
                    text = text1.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                    double xsi1 = (Convert.ToDouble(text[0].Replace(".", Program.Decsep)) + Convert.ToDouble(text[3].Replace(".", Program.Decsep)) * 0.5) - Program.IKOOAGRAL;
                    double xsi2 = (Convert.ToDouble(text[0].Replace(".", Program.Decsep)) - Convert.ToDouble(text[3].Replace(".", Program.Decsep)) * 0.5) - Program.IKOOAGRAL;
                    double xsi3 = Math.Min(xsi1, xsi2);
                    double xsi4 = Math.Max(xsi1, xsi2);
                    double eta1 = (Convert.ToDouble(text[1].Replace(".", Program.Decsep)) + Convert.ToDouble(text[4].Replace(".", Program.Decsep)) * 0.5) - Program.JKOOAGRAL;
                    double eta2 = (Convert.ToDouble(text[1].Replace(".", Program.Decsep)) - Convert.ToDouble(text[4].Replace(".", Program.Decsep)) * 0.5) - Program.JKOOAGRAL;
                    double eta3 = Math.Min(eta1, eta2);
                    double eta4 = Math.Max(eta1, eta2);

                    //excluding all area sources outside GRAL domain
                    if ((eta3 > Program.EtaMinGral) && (xsi3 > Program.XsiMinGral) && (eta4 < Program.EtaMaxGral) && (xsi4 < Program.XsiMaxGral))
                    {
                        //excluding all area sources with undesired source groups
                        {
                            Int16 SG = Convert.ToInt16(text[10]);
                            int SG_index = Program.Get_Internal_SG_Number(SG); // get internal SG number

                            if (SG_index >= 0)
                            {
                                SourceData sd = new SourceData();
                                sd.X1 = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                                sd.Y1 = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                                sd.Z1 = Convert.ToSingle(text[2].Replace(".", Program.Decsep));
                                sd.X2 = Convert.ToDouble(text[3].Replace(".", Program.Decsep));
                                sd.Y2 = Convert.ToDouble(text[4].Replace(".", Program.Decsep));
                                sd.Z2 = Convert.ToSingle(text[5].Replace(".", Program.Decsep));
                                sd.ER = Convert.ToDouble(text[6].Replace(".", Program.Decsep));
                                sd.SG = Convert.ToInt16(text[10]);
                                sd.Mode = 0; // standard mode = concentration only
                                totalemission += sd.ER;
                                emission_sourcegroup[SG_index] += sd.ER;
                                countrealsources++;

                                if (text.Length > 16) // deposition data available
                                {
                                    Dep.Dep_Start_Index = 11; // start index for area sources
                                    Dep.SD = sd;
                                    Dep.SourceData = AQ;
                                    Dep.Text = text;
                                    if (Dep.Compute() == false)
                                    {
                                        throw new IOException();
                                    }
                                }
                                else // no depositon
                                {
                                    AQ.Add(sd);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                string err = "Error when reading file cadastre.dat in line " + (Program.AS_Count + 3).ToString();
                Console.WriteLine(err + " Execution stopped: press ESC to stop");
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
            read.Close();
            read.Dispose();

            int counter = AQ.Count + 1;
            Program.AS_Count = AQ.Count - 1;
            // Copy Lists to global arrays
            Array.Resize(ref Program.AS_X, counter);
            Array.Resize(ref Program.AS_Y, counter);
            Array.Resize(ref Program.AS_Z, counter);
            Array.Resize(ref Program.AS_dX, counter);
            Array.Resize(ref Program.AS_dY, counter);
            Array.Resize(ref Program.AS_dZ, counter);
            Array.Resize(ref Program.AS_ER, counter);
            Array.Resize(ref Program.AS_SG, counter);
            Array.Resize(ref Program.AS_PartNumb, counter);
            Array.Resize(ref Program.AS_Mode, counter);
            Array.Resize(ref Program.AS_V_Dep, counter);
            Array.Resize(ref Program.AS_V_sed, counter);
            Array.Resize(ref Program.AS_ER_Dep, counter);
            Array.Resize(ref Program.AS_Absolute_Height, counter);

            for (int i = 1; i < AQ.Count; i++)
            {
                Program.AS_X[i] = AQ[i].X1;
                Program.AS_Y[i] = AQ[i].Y1;

                if (AQ[i].Z1 < 0) // negative value = absolute height
                {
                    Program.AS_Absolute_Height[i] = true;
                    if (Program.Topo != 1)
                    {
                        string err = "You are using absolute coordinates but flat terrain  - ESC = Exit";
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
                    Program.AS_Absolute_Height[i] = false;
                }

                Program.AS_Z[i] = (float)Math.Abs(AQ[i].Z1);
                Program.AS_dX[i] = AQ[i].X2;
                Program.AS_dY[i] = AQ[i].Y2;
                Program.AS_dZ[i] = AQ[i].Z2;
                Program.AS_ER[i] = AQ[i].ER;
                Program.AS_SG[i] = (byte)AQ[i].SG;
                Program.AS_V_Dep[i] = AQ[i].Vdep;
                Program.AS_V_sed[i] = AQ[i].Vsed;
                Program.AS_Mode[i] = AQ[i].Mode;
                Program.AS_ER_Dep[i] = (float)(AQ[i].ER_dep);
            }

            string info = "Total number of area source partitions: " + countrealsources.ToString();
            Console.WriteLine(info);
            ProgramWriters.LogfileGralCoreWrite(info);

            string unit = "[kg/h]: ";
            if (Program.Odour == true)
            {
                unit = "[MOU/h]: ";
            }

            info = "Total emission " + unit + (totalemission).ToString("0.000");
            Console.Write(info);
            ProgramWriters.LogfileGralCoreWrite(info);

            Console.Write(" (");
            for (int im = 0; im < Program.SourceGroups.Count; im++)
            {
                info = "  SG " + Program.SourceGroups[im] + unit + emission_sourcegroup[im].ToString("0.000");
                Console.Write(info);
                ProgramWriters.LogfileGralCoreWrite(info);
            }
            Console.WriteLine(" )");

        }
    }
}
