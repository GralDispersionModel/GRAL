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
    /// <summary>
    /// Read Point Sources
    /// </summary>
    class ReadPointSources
    {
        /// <summary>
        /// Read the point source data from the file "point.dat" and create and fill the point source arrays
        /// </summary>
        public static void Read()
        {

            List<SourceData> PQ = new List<SourceData>();

            double totalemission = 0;
            int countrealsources = 0;
            double[] emission_sourcegroup = new double[101];

            PQ.Add(new SourceData());

            if (Program.IMQ.Count == 0)
            {
                Program.IMQ.Add(0);
            }

            Deposition Dep = new Deposition();

            StreamReader read = new StreamReader("point.dat");
            try
            {
                string[] text = new string[1];
                string text1;
                text1 = read.ReadLine();
                text1 = read.ReadLine();
                while ((text1 = read.ReadLine()) != null)
                {
                    text = text1.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (text.Length > 9)
                    {
                        double xsi = Convert.ToDouble(text[0].Replace(".", Program.Decsep)) - Program.IKOOAGRAL;
                        double eta = Convert.ToDouble(text[1].Replace(".", Program.Decsep)) - Program.JKOOAGRAL;
                        double dia = Convert.ToDouble(text[8].Replace(".", Program.Decsep));

                        //excluding all point sources outside GRAL domain
                        if (((eta - dia * 0.5) > Program.EtaMinGral) && ((xsi - dia * 0.5) > Program.XsiMinGral) && ((eta + dia * 0.5) < Program.EtaMaxGral) && ((xsi + dia * 0.5) < Program.XsiMaxGral))
                        {
                            //excluding all point sources with undesired source groups
                            {
                                Int16 SG = Convert.ToInt16(text[10]);
                                int SG_index = Program.Get_Internal_SG_Number(SG); // get internal SG number

                                if (SG_index >= 0)
                                {
                                    SourceData sd = new SourceData();
                                    sd.X1 = Convert.ToDouble(text[0].Replace(".", Program.Decsep));
                                    sd.Y1 = Convert.ToDouble(text[1].Replace(".", Program.Decsep));
                                    sd.Z1 = Convert.ToSingle(text[2].Replace(".", Program.Decsep));
                                    sd.ER = Convert.ToDouble(text[3].Replace(".", Program.Decsep));
                                    sd.V = Convert.ToSingle(text[7].Replace(".", Program.Decsep));
                                    sd.D = Convert.ToSingle(text[8].Replace(".", Program.Decsep));
                                    sd.T = Convert.ToSingle(text[9].Replace(".", Program.Decsep));
                                    sd.SG = Convert.ToInt16(text[10]);
                                    sd.Mode = 0; // standard mode = concentration only
                                    totalemission += sd.ER;
                                    emission_sourcegroup[SG_index] += sd.ER;
                                    countrealsources++;

                                    sd.TimeSeriesTemperature = GetTransientTimeSeriesIndex.GetIndex(Program.PS_TimeSerTempValues, "Temp@_", text);
                                    sd.TimeSeriesVelocity = GetTransientTimeSeriesIndex.GetIndex(Program.PS_TimeSerVelValues, "Vel@_", text);

                                    if (text.Length > 15) // deposition data available
                                    {
                                        Dep.Dep_Start_Index = 11; // start index for point sources
                                        Dep.SD = sd;
                                        Dep.SourceData = PQ;
                                        Dep.Text = text;
                                        if (Dep.Compute() == false)
                                        {
                                            throw new IOException();
                                        }
                                    }
                                    else // no depositon
                                    {
                                        PQ.Add(sd);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                string err = "Error when reading file point.dat in line " + (countrealsources + 3).ToString() + " Execution stopped: press ESC to stop";
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
            read.Close();
            read.Dispose();

            int counter = PQ.Count + 1;
            Program.PS_Count += PQ.Count - 1;

            // Copy Lists to global arrays
            Array.Resize(ref Program.PS_ER, counter);
            Array.Resize(ref Program.PS_X, counter);
            Array.Resize(ref Program.PS_Y, counter);
            Array.Resize(ref Program.PS_Z, counter);
            Array.Resize(ref Program.PS_V, counter);
            Array.Resize(ref Program.PS_D, counter);
            Array.Resize(ref Program.PS_T, counter);
            Array.Resize(ref Program.PS_SG, counter);
            Array.Resize(ref Program.PS_PartNumb, counter);
            Array.Resize(ref Program.PS_Mode, counter);
            Array.Resize(ref Program.PS_V_Dep, counter);
            Array.Resize(ref Program.PS_V_sed, counter);
            Array.Resize(ref Program.PS_ER_Dep, counter);
            Array.Resize(ref Program.PS_Absolute_Height, counter);
            Array.Resize(ref Program.PS_TimeSeriesTemperature, counter);
            Array.Resize(ref Program.PS_TimeSeriesVelocity, counter);

            for (int i = 1; i < PQ.Count; i++)
            {
                Program.PS_ER[i] = PQ[i].ER;
                Program.PS_X[i] = PQ[i].X1;
                Program.PS_Y[i] = PQ[i].Y1;

                if (PQ[i].Z1 < 0) // negative value = absolute height
                {
                    Program.PS_Absolute_Height[i] = true;
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
                    Program.PS_Absolute_Height[i] = false;
                }

                Program.PS_Z[i] = (float)Math.Abs(PQ[i].Z1);
                Program.PS_V[i] = PQ[i].V;
                Program.PS_D[i] = PQ[i].D;
                Program.PS_T[i] = PQ[i].T;
                Program.PS_SG[i] = (byte)PQ[i].SG;
                Program.PS_V_Dep[i] = PQ[i].Vdep;
                Program.PS_V_sed[i] = PQ[i].Vsed;
                Program.PS_Mode[i] = PQ[i].Mode;
                Program.PS_ER_Dep[i] = (float)(PQ[i].ER_dep);
                Program.PS_TimeSeriesTemperature[i] = PQ[i].TimeSeriesTemperature;
                Program.PS_TimeSeriesVelocity[i] = PQ[i].TimeSeriesVelocity;
            }

            string info = "Total number of point sources: " + countrealsources.ToString();
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

            Program.PS_effqu = new float[Program.PS_Count + 1];

            PQ = null;
            Dep = null;
        }
    }
}
