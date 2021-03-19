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
using System.Globalization;
using System.IO;

namespace GRAL_2001
{
    class ReadTunnelPortals
    {
        /// <summary>
        /// Read Portal Sources from the file "portal.dat"
        /// </summary>
        public static void Read()
        {
            List<SourceData> TQ = new List<SourceData>();
            CultureInfo ic = CultureInfo.InvariantCulture;

            double totalemission = 0;
            int countrealsources = 0;
            double[] emission_sourcegroup = new double[101];

            TQ.Add(new SourceData());

            Deposition Dep = new Deposition();

            StreamReader read = new StreamReader("portals.dat");
            try
            {
                string[] text = new string[1];
                string text1;
                text1 = read.ReadLine();
                text1 = read.ReadLine();
                while ((text1 = read.ReadLine()) != null)
                {
                    text = text1.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                    double xsi1 = Convert.ToDouble(text[0], ic) - Program.IKOOAGRAL;
                    double eta1 = Convert.ToDouble(text[1], ic) - Program.JKOOAGRAL;
                    double xsi2 = Convert.ToDouble(text[2], ic) - Program.IKOOAGRAL;
                    double eta2 = Convert.ToDouble(text[3], ic) - Program.JKOOAGRAL;

                    //excluding all tunnel portals outside GRAL domain
                    if ((eta1 > Program.EtaMinGral) && (xsi1 > Program.XsiMinGral) && (eta1 < Program.EtaMaxGral) && (xsi1 < Program.XsiMaxGral) &&
                        (eta2 > Program.EtaMinGral) && (xsi2 > Program.XsiMinGral) && (eta2 < Program.EtaMaxGral) && (xsi2 < Program.XsiMaxGral))
                    {
                        //excluding all tunnel portals with undesired source groups
                        if (text.Length > 9)
                        {
                            Int16 SG = Convert.ToInt16(text[10]);
                            int SG_index = Program.Get_Internal_SG_Number(SG); // get internal SG number

                            if (SG_index >= 0)
                            {
                                SourceData sd = new SourceData();
                                sd.X1 = Convert.ToDouble(text[0], ic);
                                sd.Y1 = Convert.ToDouble(text[1], ic);
                                sd.X2 = Convert.ToDouble(text[2], ic);
                                sd.Y2 = Convert.ToDouble(text[3], ic);
                                sd.Z1 = Convert.ToSingle(text[4], ic);
                                sd.Z2 = Convert.ToSingle(text[5], ic);
                                sd.ER = Convert.ToDouble(text[6], ic);
                                sd.SG = Convert.ToInt16(text[10]);
                                sd.Mode = 0; // standard mode

                                totalemission += sd.ER;
                                emission_sourcegroup[SG_index] += sd.ER;
                                countrealsources++;

                                sd.TimeSeriesTemperature = GetTransientTimeSeriesIndex.GetIndex(Program.TS_TimeSerTempValues, "Temp@_", text);
                                sd.TimeSeriesVelocity = GetTransientTimeSeriesIndex.GetIndex(Program.TS_TimeSerVelValues, "Vel@_", text);

                                if (text.Length > 17) // deposition data available
                                {
                                    Dep.Dep_Start_Index = 11; // start index for portal sources
                                    if (text.Length > 20)
                                    {
                                        sd.T = Convert.ToSingle(text[19], ic);
                                        sd.V = Convert.ToSingle(text[20], ic);
                                    }
                                    Dep.SD = sd;
                                    Dep.SourceData = TQ;
                                    Dep.Text = text;
                                    if (Dep.Compute() == false)
                                    {
                                        throw new IOException();
                                    }
                                }
                                else // no depositon
                                {
                                    if (text.Length > 12) // DeltaT and ExitVel available
                                    {
                                        sd.T = Convert.ToSingle(text[11], ic);
                                        sd.V = Convert.ToSingle(text[12], ic);
                                    }
                                    TQ.Add(sd);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                string err = "Error when reading file Portals.dat in line " + (countrealsources + 3).ToString() + " Execution stopped: press ESC to stop";
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

            int counter = TQ.Count + 1;
            Program.TS_Count = TQ.Count - 1;

            // Copy Lists to global arrays
            Program.TS_ER = GC.AllocateUninitializedArray<double>(counter);
            Program.TS_X1 = GC.AllocateUninitializedArray<double>(counter);
            Program.TS_Y1 = GC.AllocateUninitializedArray<double>(counter);
            Program.TS_Z1 = GC.AllocateUninitializedArray<float>(counter);
            Program.TS_X2 = GC.AllocateUninitializedArray<double>(counter);
            Program.TS_Y2 = GC.AllocateUninitializedArray<double>(counter);
            Program.TS_Z2 = GC.AllocateUninitializedArray<float>(counter);
            Program.TS_SG = GC.AllocateUninitializedArray<byte>(counter);
            Program.TS_PartNumb = GC.AllocateUninitializedArray<int>(counter);
            Program.TS_Mode = GC.AllocateUninitializedArray<byte>(counter);
            Program.TS_V_Dep = GC.AllocateUninitializedArray<float>(counter);
            Program.TS_V_sed = GC.AllocateUninitializedArray<float>(counter);
            Program.TS_ER_Dep = GC.AllocateUninitializedArray<float>(counter);
            Program.TS_Absolute_Height = GC.AllocateUninitializedArray<bool>(counter);
            Program.TS_T = GC.AllocateUninitializedArray<float>(counter);
            Program.TS_V = GC.AllocateUninitializedArray<float>(counter);
            Program.TS_TimeSeriesTemperature = GC.AllocateUninitializedArray<int>(counter);
            Program.TS_TimeSeriesVelocity = GC.AllocateUninitializedArray<int>(counter);

            for (int i = 1; i < TQ.Count; i++)
            {
                Program.TS_X1[i] = TQ[i].X1;
                Program.TS_Y1[i] = TQ[i].Y1;
                Program.TS_X2[i] = TQ[i].X2;
                Program.TS_Y2[i] = TQ[i].Y2;

                if (TQ[i].Z1 < 0 && TQ[i].Z2 < 0) // negative values = absolute heights
                {
                    Program.TS_Absolute_Height[i] = true;
                    if (Program.Topo != Consts.TerrainAvailable)
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
                    Program.TS_Absolute_Height[i] = false;
                }

                Program.TS_Z1[i] = (float)Math.Abs(TQ[i].Z1);
                Program.TS_Z2[i] = (float)Math.Abs(TQ[i].Z2);
                Program.TS_ER[i] = TQ[i].ER;
                Program.TS_SG[i] = (byte)TQ[i].SG;
                Program.TS_V_Dep[i] = TQ[i].Vdep;
                Program.TS_V_sed[i] = TQ[i].Vsed;
                Program.TS_Mode[i] = TQ[i].Mode;
                Program.TS_ER_Dep[i] = (float)(TQ[i].ER_dep);
                Program.TS_T[i] = TQ[i].T;
                Program.TS_V[i] = TQ[i].V;
                Program.TS_TimeSeriesTemperature[i] = TQ[i].TimeSeriesTemperature;
                Program.TS_TimeSeriesVelocity[i] = TQ[i].TimeSeriesVelocity;
            }

            string info = "Total number of tunnel portals: " + countrealsources.ToString();
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

            Program.TS_Area = new float[Program.TS_Count + 1];
            Program.TS_Width = new float[Program.TS_Count + 1];
            Program.TS_Height = new float[Program.TS_Count + 1];
            Program.TS_cosalpha = new float[Program.TS_Count + 1];
            Program.TS_sinalpha = new float[Program.TS_Count + 1];

            TQ.Clear();
            TQ.TrimExcess();
            Dep = null;
        }
    }
}
