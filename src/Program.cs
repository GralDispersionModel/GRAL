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
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GRAL_2001
{
    /*  GRAZ LAGRANGIAN PARTICLE MODELL GRAL
        COMPREHENSIVE DESCRIPTION CAN BE FOUND IN OETTL, 2016
        THE GRAL MODEL HAS BEEN  DEVELOPED BY DIETMAR OETTL SINCE AROUND 1999.
     */
    partial class Program
    {
        /*INPUT FILES :
          BASIC DOMAIN INFORMATION              GRAL.geb
          MAIN CONTROL PARAM FILEs              in.dat
          GEOMETRY DATA                         ggeom.asc
          LANDUSE DATA                          landuse.asc
          METEOROLOGICAL DATA                   meteopgt.all, or inputzr.dat, or sonic.dat
          MAX. NUMBER OF CPUs                   Max_Proc.txt
          EMISSION SOURCES                      line.dat, point.dat, cadastre.dat, portals.dat
          TUNNEL JET DESTRUCTION
          BY TRAFFIC ON OPPOSITE LANE           oppsite_lane.txt
          POLLUTANTS SUCKED IN BY
          TUNNEL PORTAL AT OPPOSITE LANE        tunnel_entrance.txt
          LOCATIONS AND HEIGHTS OF BUILDINGS    buildings.dat
          LOCATIONS AND HEIGHTS OF VEGETATION   vegetation.dat
          LOCATIONS AND HEIGHTS OF RECEPTORS    Receptor.dat
          NUMBER OF VERTICAL LAYERS FOR
          PROGNOSTIC FLOW FIELD MODEL           micro_vert_layers.txt
          RELAXATION FACTORS FOR
          PROGNOSTIC FLOW FIELD MODEL           relaxation_factors.txt
          MINIMUM ANd MAXIMUM INTEGRATION TIMES
          FOR PROGNOSTIC FLOW FIELD MODEL       Integrationtime.txt
          ROUGHNESS LENGTH FOR OBSTACLES
          FOR PROGNOSTIC FLOW FIELD MODEL       building_roughness.txt
          TRANSIENT GRAL MODE CONC.THRESHOLD	GRAL_Trans_Conc_Threshold.txt
          POLLUTANT & WET DEPOSITION SETTINGS	Pollutant.txt
          WET DEPOSITION PRECIPITATION DATA		Precipitation.txt
          CALCULATE 3D CONCENTRATION DATA IN
          TRANSIENT GRAL MODE					GRAL_Vert_Conc.txt
         */

        static void Main(string[] args)
        {
            int p = (int)Environment.OSVersion.Platform;
            if ((p == 4) || (p == 6) || (p == 128))
            {
                //Console.WriteLine ("Running on Unix");
                RunOnUnix = true;
            }

            //WRITE GRAL VERSION INFORMATION TO SCREEN
            Console.WriteLine("");
            Console.WriteLine("+------------------------------------------------------+");
            Console.WriteLine("|                                                      |");
            string Info =     "+  > >         G R A L VERSION: 20.09            < <   +";
            Console.WriteLine(Info);
            if (RunOnUnix)
            {
                Console.WriteLine("|                     L I N U X                        |");
            }
            Console.WriteLine("|                 .Net Core Version                    |");
            Console.WriteLine("|                                                      |");
            Console.WriteLine("+------------------------------------------------------+");
            Console.WriteLine("");

            ShowCopyright(args);

            // write zipped files?
            ResultFileZipped = false;
            //User defined decimal seperator
            Decsep = NumberFormatInfo.CurrentInfo.NumberDecimalSeparator;

            int SIMD = CheckSIMD();
            LogLevel = CheckCommandLineArguments(args);

            //Delete file Problemreport_GRAL.txt
            if (File.Exists("Problemreport_GRAL.txt") == true)
            {
                try
                {
                    File.Delete("Problemreport_GRAL.txt");
                }
                catch { }
            }
            // Write to "Logfile_GRALCore"
            try
            {
                ProgramWriters.LogfileGralCoreWrite(new String('-', 80));
                ProgramWriters.LogfileGralCoreWrite(Info);
                ProgramWriters.LogfileGralCoreWrite("Computation started at: " + DateTime.Now.ToString());
                ProgramWriters.LogfileGralCoreWrite("Computation folder:     " + Directory.GetCurrentDirectory());
                Info = "Application hash code:  " + GetAppHashCode();
                ProgramWriters.LogfileGralCoreWrite(Info);
            }
            catch { }

            ProgramReaders ReaderClass = new ProgramReaders();

            //Read number of user defined vertical layers
            ReaderClass.ReadMicroVertLayers();
            GFFFilePath = ReaderClass.ReadGFFFilePath();

            //Lowest grid level of the prognostic flow field model grid
            HOKART[0] = 0;

            //read main GRAL domain file "GRAL.geb" and check if the file "UseOrigTopography.txt" exists -> needed to read In.Dat!
            ReaderClass.ReadGRALGeb();

            //optional: read GRAMM file ggeom.asc
            if (File.Exists("ggeom.asc") == true)
            {
                ReaderClass.ReadGRAMMGeb();
            }

            //horizontal grid sizes of the GRAL concentration grid
            GralDx = (float)((XsiMaxGral - XsiMinGral) / (float)NXL);
            GralDy = (float)((EtaMaxGral - EtaMinGral) / (float)NYL);

            //Set the maximum number of threads to be used in each parallelized region
            ReaderClass.ReadMaxNumbProc();

            //sets the number of cells near the walls of obstacles, where a boundary-layer is computed in the diagnostic flow field approach
            IGEB = Math.Max((int)(20 / DXK), 1);

            //Read Pollutant and Wet deposition data
            Odour = ReaderClass.ReadPollutantTXT();

            //Create large arrays
            CreateLargeArrays();

            //optional: reading GRAMM orography file ggeom.asc -> there are two ways to read ggeom.asc (1) the files contains all information or (2) the file just provides the path to the original ggeom.asc
            ReaderClass.ReadGgeomAsc();

            //number of grid cells of the GRAL microscale flow field
            NII = (int)((XsiMaxGral - XsiMinGral) / DXK);
            NJJ = (int)((EtaMaxGral - EtaMinGral) / DYK);
            AHKOri = CreateArray<float[]>(NII + 2, () => new float[NJJ + 2]);
            GralTopofile = ReaderClass.ReadGRALTopography(NII, NJJ); // GRAL Topofile OK?

            //reading main control file in.dat
            ReaderClass.ReadInDat();
            //total number of particles released for each weather situation
            NTEILMAX = (int)(TAUS * TPS);
            //Volume of the GRAL concentration grid
            GridVolume = GralDx * GralDy * GralDz;

            //Reading building data
            //case 1: complex terrain
            if (Topo == 1)
            {
                InitGralTopography(SIMD);
                ReaderClass.ReadBuildingsTerrain(Program.CUTK); //define buildings in GRAL
            }
            //flat terrain application
            else
            {
                InitGralFlat();
                ReaderClass.ReadBuildingsFlat(Program.CUTK); //define buildings in GRAL
            }

            // array declarations for prognostic and diagnostic flow field
            if ((FlowFieldLevel > 0) || (Topo == 1))
            {
                // create jagged arrays manually to keep memory areas of similar indices togehter -> reduce false sharing & 
                // save memory because of the unused index 0  
                DIV = new float[NII + 1][][];
                DPM = new float[NII + 1][][];
                for (int i = 1; i < NII + 1; ++i)
                {
                    DIV[i] = new float[NJJ + 1][];
                    DPM[i] = new float[NJJ + 1][];
                    for (int j = 1; j < NJJ + 1; ++j)
                    {
                        DIV[i][j] = new float[NKK + 1];
                        DPM[i][j] = new float[NKK + 2];
                    }
                    if (i % 100 == 0)
                    {
                        Console.Write(".");
                    }
                }
            }

            //In case of transient simulations: load presets and define arrays
            if (ISTATIONAER == 0)
            {
                TransientPresets.LoadAndDefine();
            }

            Console.WriteLine(".");
            //reading receptors from file Receptor.dat
            ReaderClass.ReadReceptors();

            //the horizontal standard deviations of wind component fluctuations are dependent on the averaging time (dispersion time)
            if ((IStatistics == 4))
            {
                StdDeviationV = (float)Math.Pow(TAUS / 3600, 0.2);
            }

            //for applications in flat terrain, the roughness length is homogenous as defined in the file in.dat
            if (Topo != 1)
            {
                Z0Gramm[1][1] = Z0;
            }

            //checking source files
            if (File.Exists("line.dat") == true)
            {
                LS_Count = 1;
            }

            if (File.Exists("portals.dat") == true)
            {
                TS_Count = 1;
            }

            if (File.Exists("point.dat") == true)
            {
                PS_Count = 1;
            }

            if (File.Exists("cadastre.dat") == true)
            {
                AS_Count = 1;
            }

            Console.WriteLine();
            Info = "Total number of horizontal slices for concentration grid: " + NS.ToString();
            Console.WriteLine(Info);

            for (int i = 0; i < NS; i++)
            {
                try
                {
                    Info = "  Slice height above ground [m]: " + HorSlices[i].ToString();
                    Console.WriteLine(Info);
                }
                catch
                { }
            }

            //emission modulation for transient mode
            ReaderClass.ReadEmissionTimeseries();

            //vegetation array
            VEG = CreateArray<float[][]>(NII + 2, () => CreateArray<float[]>(NJJ + 2, () => new float[NKK + 1]));
            Program.VEG[0][0][0] = -0.00001f;
            COV = CreateArray<float[]>(NII + 2, () => new float[NJJ + 2]);

            //reading optional files used to define areas where either the tunnel jet stream is destroyed due to traffic
            //on the opposite lanes of a highway or where pollutants are sucked into a tunnel portal
            ReaderClass.ReadTunnelfilesOptional();

            //optional: reading land-use data from file landuse.asc
            ReaderClass.ReadLanduseFile();

            //Use the adaptive roughness lenght mode?
            if (AdaptiveRoughnessMax > 0 && BuildingsExist)
            {
                //define FlowField dependend Z0, UStar and OL, generate Z0GRAL[][] array and write RoghnessGRAL file
                InitAdaptiveRoughnessLenght(ReaderClass);
            }
            else
            {
                AdaptiveRoughnessMax = 0;
            }

            //setting the lateral borders of the domain -> Attention: changes the GRAL borders from absolute to reletive values!
            InitGralBorders();

            //reading point source data
            if (PS_Count > 0)
            {
                PS_Count = 0;
                Console.WriteLine();
                Console.WriteLine("Reading file point.dat");
                ReadPointSources.Read();
            }
            //reading line source data
            if (LS_Count > 0)
            {
                LS_Count = 0;
                Console.WriteLine();
                Console.WriteLine("Reading file line.dat");
                ReadLineSources.Read();
            }
            //reading tunnel portal data
            if (TS_Count > 0)
            {
                TS_Count = 0;
                Console.WriteLine();
                Console.WriteLine("Reading file portals.dat");
                ReadTunnelPortals.Read();
            }
            //reading area source data
            if (AS_Count > 0)
            {
                AS_Count = 0;
                Console.WriteLine();
                Console.WriteLine("Reading file cadastre.dat");
                ReadAreaSources.Read();
            }

            //distribution of all particles over all sources according to their source strengths (the higher the emission rate the larger the number of particles)
            NTEILMAX = ParticleManagement.Calculate();
            //Coriolis parameter
            CorolisParam = (float)(2 * 7.29 * 0.00001 * Math.Sin(Math.Abs(LatitudeDomain) * 3.1415 / 180));

            //weather situation to start with
            IWET = IWETstart - 1;

            //read mettimeseries.dat, meteopgt.all and Precipitation.txt in case of transient simulations
            InputMettimeSeries InputMetTimeSeries = new InputMettimeSeries();

            if ((ISTATIONAER == 0) && (IStatistics == 4))
            {
                InputMetTimeSeries.WindData = MeteoTimeSer;
                InputMetTimeSeries.ReadMetTimeSeries();
                MeteoTimeSer = InputMetTimeSeries.WindData;
                if (MeteoTimeSer.Count == 0) // no data available
                {
                    string err = "Error when reading file mettimeseries.dat -> Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);
                }
                InputMetTimeSeries.WindData = MeteopgtLst;
                InputMetTimeSeries.ReadMeteopgtAll();
                MeteopgtLst = InputMetTimeSeries.WindData;
                if (MeteopgtLst.Count == 0) // no data available
                {
                    string err = "Error when reading file meteopgt.all -> Execution stopped: press ESC to stop";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);
                }
                // Read precipitation data
                ReaderClass.ReadPrecipitationTXT();
            }
            else
            {
                WetDeposition = false;
            }

            if (ISTATIONAER == 0)
            {
                Info = "Transient GRAL mode. Number of weather situations: " + MeteoTimeSer.Count.ToString();
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
            }

            /********************************************
             * MAIN LOOP OVER EACH WEATHER SITUATION    *
             ********************************************/
            Thread ThreadWriteGffFiles = null;
            Thread ThreadWriteConz4dFile = null;

            bool FirstLoop = true;
            while (IEND == 0)
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

                //Next weather situation
                IWET++;
                IDISP = IWET;
                WindVelGramm = -99; WindDirGramm = -99; StabClassGramm = -99; // Reset GRAMM values

                //show actual computed weather situation
                Console.WriteLine("_".PadLeft(79, '_'));
                Console.Write("Weather number: " + IWET.ToString());
                if (ISTATIONAER == 0)
                {
                    int _iwet = IWET - 1;
                    if (_iwet < MeteoTimeSer.Count)
                    {
                        Console.WriteLine(" - " + MeteoTimeSer[_iwet].Day + "." + MeteoTimeSer[_iwet].Month + "-" + MeteoTimeSer[_iwet].Hour + ":00");
                    }
                }
                else
                {
                    Console.WriteLine();
                }

                //time stamp to evaluate computation times
                int StartTime = Environment.TickCount;

                //Set the maximum number of threads to be used in each parallelized region
                ReaderClass.ReadMaxNumbProc();

                //read meteorological input data 
                if (IStatistics == 4)
                {
                    if (ISTATIONAER == 0)
                    {
                        //search for the corresponding weather situation in meteopgt.all
                        IDISP = InputMetTimeSeries.SearchWeatherSituation() + 1;
                        IWETstart = IDISP;
                        if (IWET > MeteoTimeSer.Count)
                        {
                            IEND = 1;
                        }
                    }
                    else
                    {
                        //in stationary mode, meteopgt.all is read here, because we need IEND
                        IEND = Input_MeteopgtAll.Read();
                        IWETstart--; // reset the counter, because metepgt.all is finally read in ReadMeteoData, after the GRAMM wind field is available
                    }
                }

                if (ISTATIONAER == 0 && IDISP == 0)
                {
                    break; // reached last line in mettimeseries -> exit loop				
                }

                if (IEND == 1)
                {
                    break; // reached last line in meteopgt.all ->  exit loop
                }

                String WindfieldPath = ReadWindfeldTXT();

                if (Topo == 1)
                {
                    Console.Write("Reading GRAMM wind field: ");
                }

                if (ISTATIONAER == 0 && IDISP < 0) // found no corresponding weather situation in meteopgt.all
                {
                    Info = "Cannot find a corresponding entry in meteopgt.all to the following line in mettimeseries.dat - situation skipped: " + IWET.ToString();
                    Console.WriteLine();
                    Console.WriteLine(Info);
                    ProgramWriters.LogfileGralCoreWrite(Info);
                }
                else if (Topo == 0 || (Topo == 1 && ReadWndFile.Read(WindfieldPath))) // stationary mode or an entry in meteopgt.all exist && if topo -> wind field does exist
                {
                    //GUI output
                    try
                    {
                        using (StreamWriter wr = new StreamWriter("DispNr.txt"))
                        {
                            wr.WriteLine(IWET.ToString());
                        }
                    }
                    catch { }

                    //Topography mode -> read GRAMM stability classes
                    if (Topo == 1)
                    {
                        string SclFileName = String.Empty;
                        if (WindfieldPath == String.Empty)
                        {
                            //case 1: stability fields are located in the same sub-directory as the GRAL executable
                            SclFileName = Convert.ToString(Program.IDISP).PadLeft(5, '0') + ".scl";
                        }
                        else
                        {
                            //case 2: wind fields are imported from a different project
                            SclFileName = Path.Combine(WindfieldPath, Convert.ToString(Program.IDISP).PadLeft(5, '0') + ".scl");
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
                    ReadMeteoData();

                    //Check if all weather situations have been computed
                    if (IEND == 1)
                    {
                        break;
                    }

                    //potential temperature gradient
                    CalculateTemperatureGradient();

                    //optional: read pre-computed GRAL flow fields
                    bool GffFiles = ReadGralFlowFields.Read();
                    if (GffFiles == false) // Reading of GRAL Flowfields not successful
                    {
                        //microscale flow field: complex terrain
                        if (Topo == 1)
                        {
                            MicroscaleTerrain.Calculate(FirstLoop);
                        }
                        //microscale flow field: flat terrain
                        else if ((Topo == 0) && ((BuildingsExist == true) || (File.Exists("vegetation.dat") == true)))
                        {
                            MicroscaleFlat.Calculate();
                        }
                        if (Topo == 0)
                        {
                            Program.AHMIN = 0;
                        }

                        //optional: write GRAL flow fields
                        ThreadWriteGffFiles = new Thread(WriteGRALFlowFields.Write);
                        ThreadWriteGffFiles.Start(); // start writing thread

                        if (FirstLoop)
                        {
                            WriteGRALFlowFields.WriteGRALGeometries();
                        }
                    }
                    ProgramWriters WriteClass = new ProgramWriters();

                    //time needed for wind-field computations
                    double CalcTimeWindfield = (Environment.TickCount - StartTime) * 0.001;

                    //reset receptor concentrations
                    if (ReceptorsAvailable == 1) // if receptors are acitvated
                    {
                        ReadReceptors.ReceptorResetConcentration();
                    }

                    if (FirstLoop)
                    {
                        //read receptors
                        ReceptorNumber = 0;
                        if (ReceptorsAvailable == 1) // if receptors are acitvated
                        {
                            ReadReceptors.ReadReceptor(); // read coordinates of receptors - flow field data needed
                        }
                        //in case of complex terrain and/or the presence of buildings some data is written for usage in the GUI (visualization of vertical slices)
                        WriteClass.WriteGRALGeometries();
                        //optional: write building heights as utilized in GRAL
                        WriteClass.WriteBuildingHeights("building_heights.txt", Program.BUI_HEIGHT, "0.0", 1, Program.IKOOAGRAL, Program.JKOOAGRAL);

                        Console.WriteLine(Info);
                        ProgramWriters.LogfileGralCoreWrite(Info);
                    }

                    //calculating momentum and bouyancy forces for point sources
                    PointSourceHeight.CalculatePointSourceHeight();

                    //defining the initial positions of particles
                    StartCoordinates.Calculate();

                    //boundary-layer height
                    CalculateBoudaryLayerHeight();

                    //show meteorological parameters on screen
                    OutputOfMeteoData();

                    //Transient mode: non-steady-state particles
                    if (ISTATIONAER == 0)
                    {
                        Console.WriteLine();
                        Console.Write("Dispersion computation.....");
                        // calculate wet deposition parameter
                        if (IWET < WetDepoPrecipLst.Count)
                        {
                            if (WetDepoPrecipLst[IWET] > 0)
                            {
                                WetDepoRW = Wet_Depo_CW * Math.Pow(WetDepoPrecipLst[IWET], WedDepoAlphaW);
                                WetDepoRW = Math.Max(0, Math.Min(1, WetDepoRW));
                            }
                            else
                            {
                                WetDepoRW = 0;
                            }
                        }

                        //set lower concentration threshold for memory effect
                        TransConcThreshold = ReaderClass.ReadTransientThreshold();
                        int IPERCnss = 0;
                        int advancenss = 0;
                        int cellNr = NII * NJJ;
                        int percent10nss = (int)(cellNr * 0.1F);
                        DispTimeSum = TAUS;
                        // loop over all cells
                        Parallel.For(0, cellNr, Program.pOptions, cell =>
                        {
                            Interlocked.Increment(ref advancenss);
                            if (advancenss > percent10nss)
                            {
                                Interlocked.Exchange(ref advancenss, 0); // set advance to 0
                                Interlocked.Add(ref IPERCnss, 10);
                                if (IPERCnss < 100)
                                {
                                    Console.Write("X");
                                    if (IPERCnss % 20 == 0)
                                    {
                                        try
                                        {
                                            using (StreamWriter sr = new StreamWriter("Percent.txt", false))
                                            {
                                                sr.Write(MathF.Round(IPERCnss * 0.5F).ToString());
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }

                            // indices of recent cellNr
                            int i = 1 + (cell % NII);
                            int j = 1 + (int)(cell / NII);
                            for (int k = 1; k <= NKK_Transient; k++)
                            {
                                for (int IQ = 0; IQ < Program.SourceGroups.Count; IQ++)
                                {
                                    if (Conz4d[i][j][k][IQ] >= TransConcThreshold)
                                    {
                                        ZeitschleifeNonSteadyState.Calculate(i, j, k, IQ, Conz4d[i][j][k][IQ]);
                                    }
                                }
                            }
                        });
                        Console.Write("X");
                    } // non-steady-state particles

                    Console.WriteLine();
                    Console.Write("Dispersion computation.....");
                    //new released particles
                    int IPERC = 0;
                    int advance = 0;
                    int percent10 = (int)(NTEILMAX * 0.1F);
                    DispTimeSum = TAUS;
                    Parallel.For(1, NTEILMAX + 1, Program.pOptions, nteil =>
                    {
                        Interlocked.Increment(ref advance);
                        if (advance > percent10)
                        {
                            Interlocked.Exchange(ref advance, 0); // set advance to 0
                            Interlocked.Add(ref IPERC, 10);
                            Console.Write("I");
                            if (IPERC % 20 == 0)
                            {
                                try
                                {
                                    using (StreamWriter sr = new StreamWriter("Percent.txt", false))
                                    {
                                        if (ISTATIONAER == 0)
                                        {
                                            sr.Write((50 + MathF.Round(IPERC * 0.5F)).ToString());
                                        }
                                        else
                                        {
                                            sr.Write(IPERC.ToString());
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        Zeitschleife.Calculate(nteil);
                    });

                    Console.Write("I");
                    Console.WriteLine();

                    // Wait until conz4d file is written
                    if (ThreadWriteConz4dFile != null) // if Thread has been started -> wait until GFF--WriteThread has been finished
                    {
                        ThreadWriteConz4dFile.Join();  // wait, until thread has been finished
                        ThreadWriteConz4dFile = null;  // Release ressources				
                    }

                    //tranferring non-steady-state concentration fields
                    if (ISTATIONAER == 0)
                    {
                        TransferNonSteadyStateConcentrations(WriteClass, ref ThreadWriteConz4dFile);
                    }

                    if (FirstLoop)
                    {
                        ProgramWriters.LogfileGralCoreInfo(ResultFileZipped, GffFiles);
                        FirstLoop = false;
                    }

                    //Correction of concentration volume in each cell and for each gridded receptor 
                    VolumeCorrection();

                    //time needed for dispersion computations
                    double CalcTimeDispersion = (Environment.TickCount - StartTime) * 0.001 - CalcTimeWindfield;
                    Console.WriteLine();
                    Console.WriteLine("Total simulation time [s]: " + (CalcTimeWindfield + CalcTimeDispersion).ToString("0.0"));
                    Console.WriteLine("Dispersion [s]: " + CalcTimeDispersion.ToString("0.0"));
                    Console.WriteLine("Flow field [s]: " + CalcTimeWindfield.ToString("0.0"));

                    if (LogLevel > 0) // additional LOG-Output
                    {
                        LOG01_Output();
                    }

                    ZippedFile = IWET.ToString("00000") + ".grz";
                    try
                    {
                        if (File.Exists(ZippedFile))
                        {
                            File.Delete(ZippedFile); // delete existing files
                        }
                    }
                    catch { }

                    //output of 2-D concentration files (concentrations, deposition, odour-files)
                    WriteClass.Write2DConcentrations();

                    //receptor concentrations
                    if (ISTATIONAER == 0)
                    {
                        WriteClass.WriteReceptorTimeseries(0);
                    }
                    WriteClass.WriteReceptorConcentrations();

                    //microscale flow-field at receptors
                    WriteClass.WriteMicroscaleFlowfieldReceptors();

                } //skipped situation if no entry in meteopgt.all could be found in transient GRAL mode
            }

            // Write summarized emission per source group and 3D Concentration file
            if (ISTATIONAER == 0)
            {
                ProgramWriters WriteClass = new ProgramWriters();
                if (WriteVerticalConcentration)
                {
                    WriteClass.Write3DTextConcentrations();
                }
                Console.WriteLine();
                ProgramWriters.LogfileGralCoreWrite("");
                ProgramWriters.ShowEmissionRate();
            }

            if (ThreadWriteGffFiles != null) // if Thread has been started -> wait until GFF--WriteThread has been finished
            {
                ThreadWriteGffFiles.Join(); // wait, until thread has been finished
                ThreadWriteGffFiles = null; // Release ressources
            }

            // Clean transient files
            Delete_Temp_Files();

            //Write receptor statistical error
            if (Program.ReceptorsAvailable > 0)
            {
                ProgramWriters WriteClass = new ProgramWriters();
                WriteClass.WriteReceptorTimeseries(1);
            }

            ProgramWriters.LogfileGralCoreWrite("GRAL simulations finished at: " + DateTime.Now.ToString());
            ProgramWriters.LogfileGralCoreWrite(new String('-', 80));

            if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
            {
                Console.WriteLine();
                Console.WriteLine("GRAL simulations finished. Press any key to continue...");
                Console.ReadKey(true); 	// wait for a key input
            }

            Environment.Exit(0);        // Exit console
        }
    }
}