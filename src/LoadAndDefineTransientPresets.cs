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
    /// <summary>
    ///Load and define the presets for the transient GRAL mode
    /// </summary> 
    class TransientPresets
    {
        /// <summary>
        ///Define the vertical grid heights and the vertical stretching
        ///Define the transient arrays
        ///Load transient temp.async files with saved particle positions within the transient grid
        ///Load the 3D concentration file
        /// </summary> 
        public static void LoadAndDefine()
        {
            ProgramReaders Readclass = new ProgramReaders();
            Readclass.ReadKeepAndDeleteTransientTempFiles();

            Program.HoKartTrans[0] = 0;
            Program.DZK_Trans[1] = Program.DZK[1];
            Program.HoKartTrans[1] = Program.DZK_Trans[1];
            float stretching = 1;
            double max = 10;
            for (int i = 2; i < Program.VerticalCellMaxBound; i++)
            {
                Program.DZK_Trans[i] = (float)Math.Min(Program.DZK[1] * stretching, max);
                Program.HoKartTrans[i] = Program.HoKartTrans[i - 1] + Program.DZK_Trans[i];

                if ((Program.HoKartTrans[i] >= (Program.AHMAX - Program.AHMIN + 300)) && (Program.HoKartTrans[i] >= 800))
                {
                    Program.NKK_Transient = i;
                    break;
                }

                if (Program.HoKartTrans[i] > 400)
                {
                    stretching = 20F;
                    max = 30;
                }
                else if (Program.HoKartTrans[i] > 250)
                {
                    stretching = 15F;
                    max = 20;
                }
                else if (Program.HoKartTrans[i] > 150)
                {
                    stretching = 10F;
                    max = 15;
                }
                else if (Program.HoKartTrans[i] > 100)
                {
                    stretching = 2F;
                    max = 10;
                }
                else if (Program.HoKartTrans[i] > 60)
                {
                    stretching = 1.5F;
                    max = 10;
                }
                else if (Program.HoKartTrans[i] > 30)
                {
                    stretching = 1.2F;
                    max = 10;
                }
            }
            Program.NKK_Transient = Math.Min(Program.NKK_Transient, Program.VerticalCellMaxBound - 2);

            Program.Conz4d = Program.CreateArray<float[][][]>(Program.NII + 2, () => Program.CreateArray<float[][]>(Program.NJJ + 2, () =>
                Program.CreateArray<float[]>(Program.NKK_Transient + 2, () => new float[Program.SourceGroups.Count + 1])));
            Console.Write(".");
            Program.Conz5d = Program.CreateArray<float[][][]>(Program.NII + 2, () => Program.CreateArray<float[][]>(Program.NJJ + 2, () =>
                Program.CreateArray<float[]>(Program.NKK_Transient + 2, () => new float[Program.SourceGroups.Count + 1])));
            Console.Write(".");

            if (File.Exists("GRAL_Vert_Conc.txt"))
            {
                Program.WriteVerticalConcentration = true;
                Program.ConzSsum = Program.CreateArray<float[][]>(Program.NII + 2, () => Program.CreateArray<float[]>(Program.NJJ + 2, () => new float[Program.NKK_Transient + 2]));
            }

            Program.EmissionPerSG = new double[Program.SourceGroups.Count + 1];

            if (Program.IWETstart == 1) // search and read an exising temporarily conz_sum() file and conz4d file in transient mode
            {
                int temp = Readclass.ReadTransientConcentrations("Transient_Concentrations1.tmp");
                if (temp == 0) // error reading this file, try 2nd file
                {
                    temp = Readclass.ReadTransientConcentrations("Transient_Concentrations2.tmp");
                }
                if (temp > 1) // reading of one of these files was successful
                {
                    // if transient files are deletet (default) -> continue with the weather situation saved in the transient field
                    // otherwise continue with the user defined weather situation (overrides the default behaviour)
                    if (Program.TransientTempFileDelete)
                    {
                        Program.IWETstart = temp;
                    }
                }
                if (Program.WriteVerticalConcentration)
                {
                    Readclass.Read3DTempConcentrations();
                }
            }
            Readclass.ReadSourceTimeSeries();
        }
    }
}