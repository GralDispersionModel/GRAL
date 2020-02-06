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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GRAL_2001
{
    class ReadLineSources
    {
        /// <summary>
        /// Read Line Sources from the file "line.dat"
        /// </summary>
        public static void Read()
        {
        	List<SourceData> LQ = new List<SourceData>();
        	
        	LQ.Add(new SourceData());
        	
            double totalemission = 0;
            int countrealsources = 0;
            double[] emission_sourcegroup = new double[101];
        	
            if (Program.IMQ.Count == 0)
                Program.IMQ.Add(0);
            
            Deposition Dep = new Deposition();
            
            StreamReader read = new StreamReader("line.dat");
            try
            {
                string[] text = new string[1];
                string text1;
                text1 = read.ReadLine();
                text1 = read.ReadLine();
                text1 = read.ReadLine();
                text1 = read.ReadLine();
                text1 = read.ReadLine();
                while ((text1 = read.ReadLine()) != null)
                {
                    text = text1.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    double xsi1 = Convert.ToDouble(text[3].Replace(".", Program.Decsep)) - Program.IKOOAGRAL;
                    double eta1 = Convert.ToDouble(text[4].Replace(".", Program.Decsep)) - Program.JKOOAGRAL;
                    double xsi2 = Convert.ToDouble(text[6].Replace(".", Program.Decsep)) - Program.IKOOAGRAL;
                    double eta2 = Convert.ToDouble(text[7].Replace(".", Program.Decsep)) - Program.JKOOAGRAL;

                    //excluding all line sources outside GRAL domain
                    if ((eta1 > Program.EtaMinGral) && (xsi1 > Program.XsiMinGral) && (eta1 < Program.EtaMaxGral) && (xsi1 < Program.XsiMaxGral)&&
                        (eta2 > Program.EtaMinGral) && (xsi2 > Program.XsiMinGral) && (eta2 < Program.EtaMaxGral) && (xsi2 < Program.XsiMaxGral))
                    {
                        //excluding all line sources with undesired source groups
                        {
                            Int16 SG = Convert.ToInt16(text[2]);
                            int SG_index = Program.Get_Internal_SG_Number(SG); // get internal SG number
                            
                            if (SG_index >= 0)
                            {
                            	SourceData sd = new SourceData();
                            	
                                sd.SG = Convert.ToInt16(text[2]);
                                sd.X1 = Convert.ToDouble(text[3].Replace(".", Program.Decsep));
                                sd.Y1 = Convert.ToDouble(text[4].Replace(".", Program.Decsep));
                                sd.Z1 = Convert.ToSingle(text[5].Replace(".", Program.Decsep));
                                sd.X2 = Convert.ToDouble(text[6].Replace(".", Program.Decsep));
                                sd.Y2 = Convert.ToDouble(text[7].Replace(".", Program.Decsep));
                                sd.Z2 = Convert.ToSingle(text[8].Replace(".", Program.Decsep));
                                sd.Width = Convert.ToSingle(text[9].Replace(".", Program.Decsep));
                                sd.Laerm = Convert.ToSingle(text[10].Replace(".", Program.Decsep));
								sd.Mode = 0; // standard mode = concentration only
								
                                //Conversion of emissions given in kg/h/km in kg/h
                                float length = (float)Math.Sqrt(Math.Pow(sd.X1 - sd.X2, 2) + 
                                                                Math.Pow(sd.Y1 - sd.Y2, 2) +
                                                                Math.Pow(sd.Z1 - sd.Z2, 2));
                                double emission = Convert.ToDouble(text[13].Replace(".", Program.Decsep));
                                double emission_kg_h = length * emission * 0.001F;
                                
                                if (length > 0.001) // Kuntner 4.10.2017 Filter sources with length < 0.001
                                {
                                	sd.ER =  emission_kg_h;
                                	
                                	totalemission += sd.ER;
                                	emission_sourcegroup[SG_index] += sd.ER;
                                	countrealsources ++;
                                	
                                	if (text.Length > 24) // deposition data available
                                	{
                                		Dep.Dep_Start_Index = 19; // start index for line sources
                                		Dep.SD = sd;
                                		Dep.SourceData = LQ;
                                		Dep.Text = text;
                                		if (Dep.Compute() == false) throw new IOException();
                                	}
                                	else // no depositon
                                	{
                                		LQ.Add(sd);
                                	}
                                } // Filter sources
                            }
                        }
                    }
                }
            }
            catch
            {
                string err = "Error when reading file line.dat in line " + (countrealsources + 3).ToString() + " Execution stopped: press ESC to stop";
            	Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);
                
                if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                
                Environment.Exit(0);
            }
            read.Close();
            read.Dispose();
            
            int counter = LQ.Count + 1;
            Program.LS_Count = LQ.Count - 1;
            // Copy Lists to global arrays
            Array.Resize(ref Program.LS_ER, counter);
            Array.Resize(ref Program.LS_X1, counter);
            Array.Resize(ref Program.LS_Y1, counter);
            Array.Resize(ref Program.LS_Z1, counter);
            Array.Resize(ref Program.LS_X2, counter);
            Array.Resize(ref Program.LS_Y2, counter);
            Array.Resize(ref Program.LS_Z2, counter);
            Array.Resize(ref Program.LS_Width, counter);
            Array.Resize(ref Program.LS_Laerm, counter);
            Array.Resize(ref Program.LS_SG, counter);
            Array.Resize(ref Program.LS_PartNumb, counter);
            Array.Resize(ref Program.LS_Mode, counter);
			Array.Resize(ref Program.LS_V_Dep, counter);
			Array.Resize(ref Program.LS_V_sed, counter);
			Array.Resize(ref Program.LS_ER_Dep, counter);
            Array.Resize(ref Program.LS_Absolute_Height, counter);
            
            for (int i = 1; i < LQ.Count; i++)
            {
            	Program.LS_ER[i] = LQ[i].ER;
            	Program.LS_X1[i] = LQ[i].X1;
            	Program.LS_Y1[i] = LQ[i].Y1;
            	
            	if (LQ[i].Z1 < 0 && LQ[i].Z2 < 0) // negative values = absolute heights
            	{
					Program.LS_Absolute_Height[i] = true;
					if (Program.Topo != 1)
					{
						string err = "You are using absolute coordinates but flat terrain  - ESC = Exit";
						Console.WriteLine(err);
						ProgramWriters.LogfileProblemreportWrite(err);
						
                        if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                        {   
					    	while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                        }
                        
						Environment.Exit(0);
                    }
            	}
				else
					Program.LS_Absolute_Height[i] = false;
            	
				Program.LS_Z1[i] = (float) Math.Abs(LQ[i].Z1);
            	Program.LS_X2[i] = LQ[i].X2;
            	Program.LS_Y2[i] = LQ[i].Y2;
            	Program.LS_Z2[i] = (float) Math.Abs(LQ[i].Z2);
            	Program.LS_Width[i] = LQ[i].Width;
            	Program.LS_Laerm[i] = LQ[i].Laerm;
            	Program.LS_SG[i]    = (byte) LQ[i].SG;
            	Program.LS_V_Dep[i] = LQ[i].Vdep;
				Program.LS_V_sed[i] = LQ[i].Vsed;
				Program.LS_Mode[i]  = LQ[i].Mode;
				Program.LS_ER_Dep[i]= (float) (LQ[i].ER_dep);
            }
            
            string info = "Total number of line source segments: " + countrealsources.ToString();
            Console.WriteLine(info);
            ProgramWriters.LogfileGralCoreWrite(info);
            
            string unit = "[kg/h]: ";
            if (Program.Odour == true)
            	unit = "[MOU/h]: ";
            
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
			
			LQ = null;
			Dep = null;
        }
    }
}
