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

using FilterArray;
using System;
using System.IO;

namespace GRAL_2001
{
    public partial class Program
    {
        /// <summary>
        /// Load or calculate the roughness lenght
        /// </summary>
        /// <param name="ReaderClass">Program Reader Class</param>
        private static void InitAdaptiveRoughnessLenght(ProgramReaders ReaderClass)
        {
            Program.Z0Gral = CreateArray<float[]>(NII + 2, () => new float[NJJ + 2]);
            Program.OLGral = CreateArray<float[]>(NII + 2, () => new float[NJJ + 2]);
            Program.USternGral = CreateArray<float[]>(NII + 2, () => new float[NJJ + 2]);

            // Read roughness from file
            bool readRoughnessFromFile = false;
            if (File.Exists("RoughnessLengthsGral.dat"))
            {
                readRoughnessFromFile = ReaderClass.ReadRoughnessGral(Program.Z0Gral);
                if (readRoughnessFromFile)
                {
                    Console.WriteLine("Reading local roughness lenghts from RoughnessLenghtsGral.dat successful");
                }
            }      
            // or create adaptive roughness lenghts 
            if (readRoughnessFromFile == false)
            {
                CreateAdaptiveRoughnessLenght(ReaderClass);
            }
        }
        /// <summary>
        /// Calculate the roughness lenght based on objects like buildings and vegetation areas
        /// </summary>
        /// <param name="ReaderClass">Program Reader Class</param>
        private static void CreateAdaptiveRoughnessLenght(ProgramReaders ReaderClass)
        {
            //read buildings into a local array
            float[][] TempArray = CreateArray<float[]>(NII + 2, () => new float[NJJ + 2]);
            
            if (Topo == 1)
            {
                ReaderClass.ReadBuildingsTerrain(TempArray);
            }
            else
            {
                ReaderClass.ReadBuildingsFlat(TempArray);
            }
            
            ArrayFilter fil = new ArrayFilter();
            
            //Find building outlines
            //TempArray = fil.FindOutline(TempArray);
            
            // Take building height into Z0
            float max = 0;
            for (int i = 0; i <= NII; i++)
            {
                for (int j = 0; j <= NJJ; j++)
                {
                    if (TempArray[i][j] > 1)
                    {
                        float val = MathF.Log10(TempArray[i][j]); //roughness from building height
                        max = MathF.Max(max, val);
                        Program.Z0Gral[i][j] = MathF.Min(AdaptiveRoughnessMax, val);
                    }
                }
            }

            max = Math.Min(max, Program.AdaptiveRoughnessMax);
            //Filter Roughness
            Program.Z0Gral = fil.LowPassGaussian(Program.Z0Gral, Program.DXK, 40, max); // scale with max
            
            //roughness length for building walls
            float buildingZ0 = 0.01F;
            if (File.Exists("building_roughness.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("building_roughness.txt"))
                    {
                        buildingZ0 = Convert.ToSingle(sr.ReadLine().Replace(".", Program.Decsep));
                    }
                }
                catch
                { }
            }
            
            //Set Roughness within buildings to building wall value and clear TempArray
            for (int i = 0; i <= NII; i++)
            {
                for (int j = 0; j <= NJJ; j++)
                {
                    if (TempArray[i][j] > Program.Z0Gral[i][j] * 2)
                    {
                        Program.Z0Gral[i][j] = buildingZ0;
                    } 
                    TempArray[i][j] = 0;
                }
            }

            // Read vegetation heigth, limit vegetation roughness to 1.3 m and combine with building roughness
            ReaderClass.ReadVegetationDomain(TempArray);
            //Limit vegetation to 1.5
            for (int i = 0; i <= NII; i++)
            {
                for (int j = 0; j <= NJJ; j++)
                {
                    TempArray[i][j] = MathF.Min(1.3F, TempArray[i][j]);
                }
            }

            // Filter vegetation array
            TempArray = fil.LowPassGaussian(TempArray, Program.DXK, 15, 0); // no scale
            fil = null;

            for (int i = 0; i <= NII; i++)
            {
                for (int j = 0; j <= NJJ; j++)
                {
                    Program.Z0Gral[i][j] = MathF.Max(MathF.Min(1.5F, TempArray[i][j]), Program.Z0Gral[i][j]);
                }
            }

            if ((Program.Topo == 1) && (Program.LandUseAvailable == true))
            {
                float DDX1 = Program.DDX[1];
                float DDY1 = Program.DDY[1];
                int Delta_IKOO = (int) XsiMinGral - Program.GrammWest;
                int Delta_JKOO = (int) EtaMinGral - Program.GrammSouth;
                //With topography and LandUse File for Roughness lenght -> Compare with Z0Gramm and take larger value
                for (int i = 0; i <= NII; i++)
                {
                    for (int j = 0; j <= NJJ; j++)
                    {
                        int GrammCellX = Math.Clamp((int)((Delta_IKOO + DXK * i - DXK * 0.5) / DDX1) + 1, 1, Program.NX);
                        int GrammCellY = Math.Clamp((int)((Delta_JKOO + DYK * j - DYK * 0.5) / DDY1) + 1, 1, Program.NY);
                        Program.Z0Gral[i][j] = MathF.Max(Program.Z0Gramm[GrammCellX][GrammCellY], 
                                               MathF.Min(AdaptiveRoughnessMax, Program.Z0Gral[i][j]));      
                    }
                }
            }
            else
            {
                // limit roughness between min and max
                for (int i = 0; i <= NII; i++)
                {
                    for (int j = 0; j <= NJJ; j++)
                    {
                        Program.Z0Gral[i][j] = MathF.Max(Z0, MathF.Min(AdaptiveRoughnessMax, Program.Z0Gral[i][j]));
                    }
                }
            }
            
            max = 0;
            float min = 20000;
            for (int i = 0; i <= NII; i++)
            {
                for (int j = 0; j <= NJJ; j++)
                {
                    max = MathF.Max(max, Program.Z0Gral[i][j]);
                    min = MathF.Min(min, Program.Z0Gral[i][j]);
                }
            }

            string Info = "Using adaptive roughness length. User minimum: " + Z0.ToString("0.00") + " m  User maximum: " + AdaptiveRoughnessMax.ToString("0.00") + " m";
            Console.WriteLine(Info);
            ProgramWriters.LogfileGralCoreWrite(Info);
            Info = "                                 Used minimum: " + min.ToString("0.00") + " m  Used maximum: " + max.ToString("0.00") + " m";
            Console.WriteLine(Info);
            ProgramWriters.LogfileGralCoreWrite(Info);

            // ProgramWriters WriteClass = new ProgramWriters();
            ProgramWriters WriteClass = new ProgramWriters();
            WriteClass.WriteBuildingHeights("RoughnessLengthsGral.txt", Program.Z0Gral, "0.00", 2, Program.GralWest, Program.GralSouth);
            
            WriteClass = null;
            TempArray = null;
        }
    }
}