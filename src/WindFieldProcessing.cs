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
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Threading;
using System.Threading.Tasks;

namespace GRAL_2001
{
    partial class Program
    {
        /// <summary>
        /// Calculate a GRAL flow field
        /// </summary>
        /// <param name="IDISP"></param>
        /// <param name="MetfileSituation"></param>
        /// <param name="CalculationFinished"></param>
        /// <param name="FirstLoop"></param>
        /// <param name="ThreadWriteGffFiles"></param>
        /// <param name="WindfieldPath"></param>
        /// <param name="Synchron">false: startet before the next situation has been started</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<(int, bool)> WindfieldProcessing(int IDISP, int MetfileSituation, int CalculationFinished, bool FirstLoop, Thread ThreadWriteGffFiles, string WindfieldPath, bool Synchron, CancellationToken token)
        {
            try
            {
                // if GFF writing thread has been started -> wait until GFF--WriteThread has been finished
                if (ThreadWriteGffFiles != null)
                {
                    ThreadWriteGffFiles.Join(5000); // wait up to 5s or until thread has been finished
                    if (ThreadWriteGffFiles.IsAlive)
                    {
                        Console.Write("Writing *.gff file..");
                        while (ThreadWriteGffFiles.IsAlive)
                        {
                            ThreadWriteGffFiles.Join(30000); // wait up to 30s or until thread has been finished
                            Console.Write(".");
                        }
                        Console.WriteLine();
                    }
                    ThreadWriteGffFiles = null; // Release ressources				
                }
                if (! Synchron) { Console.WriteLine("Flow field calculation for the next situation starts in parallel"); }
                //Topography mode -> read GRAMM stability classes
                if (Topo == Consts.TerrainAvailable)
                {
                    string SclFileName = String.Empty;
                    if (WindfieldPath == String.Empty)
                    {
                        //case 1: stability fields are located in the same sub-directory as the GRAL executable
                        SclFileName = Convert.ToString(IDISP).PadLeft(5, '0') + ".scl";
                    }
                    else
                    {
                        //case 2: wind fields are imported from a different project
                        SclFileName = Path.Combine(WindfieldPath, Convert.ToString(IDISP).PadLeft(5, '0') + ".scl");
                    }

                    if (File.Exists(SclFileName))
                    {
                        Console.WriteLine("Reading GRAMM stability classes: " + SclFileName);

                        ReadSclUstOblClasses Reader = new ReadSclUstOblClasses
                        {
                            FileName = SclFileName,
                            Stabclasses = AKL_GRAMM
                        };
                        Reader.ReadSclFile();
                        AKL_GRAMM = Reader.Stabclasses;
                        Reader.Close();
                    }
                }

                //Read meteorological input data depending on the input type IStatisitcs
                (IDISP, CalculationFinished) = ReadMeteoData(IDISP);

                //potential temperature gradient
                CalculateTemperatureGradient();

                //optional: read pre-computed GRAL flow fields
                bool GffFiles = ReadGralFlowFields.Read(IDISP);

                if (GffFiles == false) // Reading of GRAL Flowfields not successful
                {
                    //microscale flow field: complex terrain
                    if (Topo == Consts.TerrainAvailable)
                    {
                        MicroscaleTerrain.Calculate(FirstLoop, IDISP, Synchron);
                    }
                    //microscale flow field: flat terrain
                    else if ((Topo == Consts.TerrainFlat) && ((BuildingsExist == true) || (File.Exists("vegetation.dat") == true)))
                    {
                        MicroscaleFlat.Calculate(IDISP, Synchron);
                    }
                    if (Topo == Consts.TerrainFlat)
                    {
                        Program.AHMIN = 0;
                    }

                    //optional: write GRAL flow fields
                    ThreadWriteGffFiles = new Thread(() => WriteGRALFlowFields.Write(IDISP));
                    ThreadWriteGffFiles.Start(); // start writing thread

                    if (FirstLoop)
                    {
                        WriteGRALFlowFields.WriteGRALGeometries();
                    }
                }
                else
                {
                    //Read Vegetation areas
                    if ((Program.FlowFieldLevel == Consts.FlowFieldProg) && (File.Exists("vegetation.dat") == true) && Program.VEG[0][0][1] < 0)
                    {
                        //read vegetation only once
                        ProgramReaders Readclass = new ProgramReaders();
                        Readclass.ReadVegetation();
                        Program.VEG[0][0][1] = 0;
                        Program.VEG[0][0][0] = -0.001f; //reset marker, that vegetation is available
                    }
                }
                return (CalculationFinished, GffFiles);
            }
            catch (OperationCanceledException)
            {
                return (Consts.CalculationFinished, false);
            }
        }
    }
}