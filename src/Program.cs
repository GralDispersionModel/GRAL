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
using System.Threading;

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
            // Readiness check of the GPU path for the GUI, before any banner or project file
            if (args.Length == 1 && args[0] == GpuCheck.Argument)
            {
                Environment.Exit(GpuCheck.Run());
            }
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
            string Info =     "+  > >         G R A L Version 27.01Beta1        < <   +";
            Console.WriteLine(Info);
            if (RunOnUnix)
            {
                Console.WriteLine("|                     L I N U X                        |");
            }
#if NET6_0
            Console.WriteLine("|                   .NET6 Version                      |");
#elif NET7_0
            Console.WriteLine("|                   .NET7 Version                      |");
#elif NET8_0
            Console.WriteLine("|                   .NET8 Version                      |");
#elif NET10_0_OR_GREATER
            Console.WriteLine("|                  .NET10 Version                      |");
#else
            Console.WriteLine("|                 .Net Core Version                    |");
#endif
            Console.WriteLine("|                                                      |");
            Console.WriteLine("+------------------------------------------------------+");
            Console.WriteLine("");

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(cancelApp);
            
            ShowCopyright(args);

            // write zipped files?
            ResultFileZipped = false;
            
            LogLevel = CheckCommandLineArguments(args);

            //Delete file Problemreport_GRAL.txt
            if (File.Exists("Problemreport_GRAL.txt") == true && IWETstartstop.Start == 0) // delete Problemreport if a single instance has been started
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
            // Decide the GPU switch here, so that its line is in the GRAL log of every run, also one that ends before the first situation
            _ = GpuExperimental.Enabled;
            //override first weather situation when using a command line argument for the 1st and last weather situation
            if (IWETstartstop.Start > 0)
            {
                IWETstart = IWETstartstop.Start;
                ProgramWriters.LogfileGralCoreWrite("Performing calculation between weather situations " + IWETstartstop.Start + " to " + IWETstartstop.End);
            }
            else if (IWETstart == 1)
            {
                DeleteReceptorConcentrationFiles(); // delete receptor files if a new single instance has been started with weather situation 1
            }

            //total number of particles released for each weather situation
            NTEILMAX = (int)(TAUS * TPS);
            //Volume of the GRAL concentration grid
            GridVolume = GralDx * GralDy * GralDz;

            //Reading building data
            //case 1: complex terrain
            {
                int SIMD = CheckSIMD();
                if (Topo == Consts.TerrainAvailable)
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
            }
            
            // array declarations for prognostic and diagnostic flow field
            if ((FlowFieldLevel > Consts.FlowFieldNoBuildings) || (Topo == Consts.TerrainAvailable))
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
            if (ISTATIONAER == Consts.TransientMode)
            {
                TransientPresets.LoadAndDefine();
            }

            Console.WriteLine(".");
            //reading receptors from file Receptor.dat
            ReaderClass.ReadReceptors();

            //the horizontal standard deviations of wind component fluctuations are dependent on the averaging time (dispersion time)
            if ((IStatistics == Consts.MeteoPgtAll))
            {
                StdDeviationV = (float)Math.Pow(TAUS / 3600, 0.2);
            }

            //for applications in flat terrain, the roughness length is homogenous as defined in the file in.dat
            if (Topo != Consts.TerrainAvailable)
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

            //Create Vegetation array only in areas of interest and at point 0/0 -> reduce memory footprint
            //VEG = CreateArray<float[][]>(NII + 2, () => CreateArray<float[]>(NJJ + 2, () => new float[NKK + 1]));
            VEG = new float [NII + 2][][];
            for (int i = 0; i < NII + 2; i++)
            {
                VEG[i] = new float [NJJ + 2][];
                if (i == 0)
                {
                    VEG[i][0] = new float[NKK + 1];
                }
            }
            Program.VEG[0][0][0] = -0.001f; //sign for reading vegetation one times, when claculating flow fields
            Program.VEG[0][0][1] = -0.001f; //sign for reading vegetation, if *.gff files exist
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

            if (Program.UseFixedRndSeedVal)
            {
                Info = "GRAL Deterministic Mode";
                Console.WriteLine(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
            }

            //setting the lateral borders of the domain -> Attention: changes the GRAL borders from absolute to relative values!
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
                if ((TunnelEntr || TunnelOppLane) && TS_Count == 0)
                {
                    // The cell flags stay active: tunnel_entrance.txt can still remove area, line and point source particles.
                    string warn = "Warning: tunnel_entrance.txt or Opposite_lane.txt exists, but portals.dat defines no portal";
                    Console.WriteLine(warn);
                    ProgramWriters.LogfileGralCoreWrite(warn);
                }
            }
            //reading area source data
            if (AS_Count > 0)
            {
                AS_Count = 0;
                Console.WriteLine();
                Console.WriteLine("Reading file cadastre.dat");
                ReadAreaSources.Read();
            }

            ReaderClass.RemovePrognosticSubDomainsFarDistanceToSources();

            //distribution of all particles over all sources according to their source strengths (the higher the emission rate the larger the number of particles)
            NTEILMAX = ParticleManagement.Calculate();
            //Coriolis parameter
            CorolisParam = (float)(2 * 7.29 * 0.00001 * Math.Sin(Math.Abs(LatitudeDomain) * 3.1415 / 180));

            //weather situation to start with
            IWET = IWETstart - 1;

            //read mettimeseries.dat, meteopgt.all and Precipitation.txt in case of transient simulations
            InputMettimeSeries InputMetTimeSeries = new InputMettimeSeries();

            // read vegetation deposition factors
            VegetationDepoVelFactors = ReaderClass.ReadVegetationDeposition();

            if ((ISTATIONAER == Consts.TransientMode) && (IStatistics == Consts.MeteoPgtAll))
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

            if (ISTATIONAER == Consts.TransientMode)
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
            Thread ThreadWrite2DConcentrationFiles = null;
            int recentWeatherSituation = 0;
            // A GPU failure (startup without fallback, or an error after GPU work started) ends the run like a failed meteorology
            // input: the reason goes to the console and the logs, the finish sequence runs and the exit code is 1.
            bool gpuFailed = false;
            void ReportGpuFailure(Exception exc)
            {
                gpuFailed = true;
                string err = "GPU failure in weather situation " + IWET.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + exc.Message +
                             (exc.InnerException != null && !exc.Message.Contains(exc.InnerException.Message) ? ": " + exc.InnerException.Message : string.Empty);
                // A runtime compiler log can be very long; the full text is in the GPU runtime log
                if (err.Length > 2000) err = err.Substring(0, 2000) + " ... (shortened; see the GPU runtime log)";
                Console.WriteLine();
                Console.WriteLine("ERROR: " + err);
                ProgramWriters.LogfileProblemreportWrite(err); // also writes to Logfile_GRALCore
            }
            // A failed GPU situation ends the run; let the writer threads of earlier situations finish their files first.
            void RunGpuSituation()
            {
                try
                {
                    GpuExperimental.Run(1, NTEILMAX);
                }
                catch (Exception) when (CancelToken.IsCancellationRequested)
                {
                    // A cancelled run stops like the CPU path: the caller sees the token and finishes GRAL normally.
                    ThreadWriteGffFiles?.Join();
                    ThreadWriteConz4dFile?.Join();
                    ThreadWrite2DConcentrationFiles?.Join();
                }
                catch (Exception exc)
                {
                    ThreadWriteGffFiles?.Join();
                    ThreadWriteConz4dFile?.Join();
                    ThreadWrite2DConcentrationFiles?.Join();
                    ReportGpuFailure(exc);
                }
            }

            bool FirstLoop = true;
            while (IEND == Consts.CalculationRunning)
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

                if (CancelToken.IsCancellationRequested)
                {
                    goto FinishGral;
                }

                // Odour output reads the current wind and turbulence fields.
                if (Odour && ThreadWrite2DConcentrationFiles != null)
                {
                    ThreadWrite2DConcentrationFiles.Join();
                    ThreadWrite2DConcentrationFiles = null;
                }

                //Next weather situation
                IWET++;
                IDISP = IWET;
                WindVelGramm = -99; WindDirGramm = -99; StabClassGramm = -99; // Reset GRAMM values

                //show actual computed weather situation
                Console.WriteLine("_".PadLeft(79, '_'));
                Console.Write("Weather number: " + IWET.ToString());
                if (ISTATIONAER == Consts.TransientMode)
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
                if (IStatistics == Consts.MeteoPgtAll)
                {
                    if (ISTATIONAER == Consts.TransientMode)
                    {
                        //search for the corresponding weather situation in meteopgt.all
                        IDISP = InputMetTimeSeries.SearchWeatherSituation() + 1;
                        IWETstart = IDISP;
                        if (IWET > MeteoTimeSer.Count)
                        {
                            IEND = Consts.CalculationFinished;
                        }
                    }
                    else
                    {
                        //in stationary mode, meteopgt.all is read here, because we need IEND
                        IEND = Input_MeteopgtAll.Read();
                        IWETstart--; // reset the counter, because metepgt.all is finally read in ReadMeteoData, after the GRAMM wind field is available
                    }
                }

                if (ISTATIONAER == Consts.TransientMode && IDISP == 0)
                {
                    break; // reached last line in mettimeseries -> exit loop				
                }
                if (IEND == Consts.CalculationFinished)
                {
                    break; // reached last line in meteopgt.all ->  exit loop
                }
                if (IWET > IWETstartstop.End)
                {
                    break; // reached last weather situation from console command line input
                }

                String WindfieldPath = ReadWindfeldTXT();

                if (Topo == Consts.TerrainAvailable)
                {
                    Console.Write("Reading GRAMM wind field: ");
                }

                if (ISTATIONAER == Consts.TransientMode && IDISP < 0) // found no corresponding weather situation in meteopgt.all
                {
                    Info = "Cannot find a corresponding entry in meteopgt.all to the following line in mettimeseries.dat - situation skipped: " + IWET.ToString();
                    Console.WriteLine();
                    Console.WriteLine(Info);
                    ProgramWriters.LogfileGralCoreWrite(Info);
                }
                else if (Topo == Consts.TerrainFlat || (Topo == Consts.TerrainAvailable && ReadWndFile.Read(WindfieldPath))) // stationary mode or an entry in meteopgt.all exist && if topo -> wind field does exist
                {
                    if (Program.IWETstartstop.Start < 2) // //GUI status output for the 1st instance only
                    {
                        try
                        {
                            using (StreamWriter wr = new StreamWriter("DispNr.txt"))
                            {
                                wr.WriteLine(IWET.ToString());
                            }
                        }
                        catch { }
                    }
                    //Topography mode -> read GRAMM stability classes
                    if (Topo == Consts.TerrainAvailable)
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
                    if (IEND == Consts.CalculationFinished)
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
                        if (Topo == Consts.TerrainAvailable)
                        {
                            MicroscaleTerrain.Calculate(FirstLoop);
                        }
                        //microscale flow field: flat terrain
                        else if ((Topo == Consts.TerrainFlat) && ((BuildingsExist == true) || (File.Exists("vegetation.dat") == true)))
                        {
                            MicroscaleFlat.Calculate();
                        }
                        if (Topo == Consts.TerrainFlat)
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
                    ProgramWriters WriteClass = new ProgramWriters();

                    //time needed for wind-field computations
                    double CalcTimeWindfield = (Environment.TickCount - StartTime) * 0.001;

                    //reset receptor concentrations
                    if (ReceptorsAvailable) // if receptors are acitvated
                    {
                        ReadReceptors.ReceptorResetConcentration();
                    }

                    // if writing thread for *.grz files has been started -> wait until Thread has been finished
                    if (ThreadWrite2DConcentrationFiles != null)
                    {
                        ThreadWrite2DConcentrationFiles.Join(5000); // wait up to 5s or until thread has been finished
                        if (ThreadWrite2DConcentrationFiles.IsAlive)
                        {
                            Console.Write("Finish writing *.grz file..");
                            while (ThreadWrite2DConcentrationFiles.IsAlive)
                            {
                                ThreadWrite2DConcentrationFiles.Join(30000); // wait up to 30s or until thread has been finished
                                Console.Write(".");
                            }
                            Console.WriteLine();
                        }
                        ThreadWrite2DConcentrationFiles = null; // Release ressources				
                    }
                    if (CancelToken.IsCancellationRequested)
                    {
                        goto FinishGral;
                    }

                    if (FirstLoop)
                    {
                        //read receptors
                        ReceptorNumber = 0;
                        if (ReceptorsAvailable) // if receptors are acitvated
                        {
                            ReadReceptors.ReadReceptor(); // read coordinates of receptors - flow field data needed
                        }
                        
                        if (SyncWithMutex != null && SyncWithMutex.WaitOne(4000)) //File access synchronisation across multiple GRAL instances allows for a waiting time of up to 4000 ms.
                        {
                            //in case of complex terrain and/or the presence of buildings some data is written for usage in the GUI (visualization of vertical slices)
                            WriteClass.WriteGRALGeometries();
                            //optional: write building heights as utilized in GRAL
                            WriteClass.WriteBuildingHeights("building_heights.txt", Program.BUI_HEIGHT, "0.0", 1, Program.IKOOAGRAL, Program.JKOOAGRAL);
                            //optional: write sub Domains as utilized in GRAL
                            WriteClass.WriteSubDomain("PrognosticSubDomainAreas.txt", Program.ADVDOM, "0", 1, Program.IKOOAGRAL, Program.JKOOAGRAL);
                            SyncWithMutex.ReleaseMutex(); // release the mutex
                        }
                        else
                        {
                            //in case of complex terrain and/or the presence of buildings some data is written for usage in the GUI (visualization of vertical slices)
                            WriteClass.WriteGRALGeometries();
                            //optional: write building heights as utilized in GRAL
                            WriteClass.WriteBuildingHeights("building_heights.txt", Program.BUI_HEIGHT, "0.0", 1, Program.IKOOAGRAL, Program.JKOOAGRAL);
                            //optional: write sub Domains as utilized in GRAL
                            WriteClass.WriteSubDomain("PrognosticSubDomainAreas.txt", Program.ADVDOM, "0", 1, Program.IKOOAGRAL, Program.JKOOAGRAL);
                        }
                    }

                    RnGSeed = new DeterministicRandomGenerator(IWET, WindVelGral, WindDirGral);

                    //calculating momentum and bouyancy forces for point sources
                    PointSourceHeight.CalculatePointSourceHeight();

                    // Allocate new particles for the current transient situation.
                    if (ISTATIONAER == Consts.TransientMode)
                    {
                        NTEILMAX = ParticleManagement.Calculate(true);
                    }

                    //defining the initial positions of particles
                    StartCoordinates.Calculate();

                    //boundary-layer height
                    CalculateBoudaryLayerHeight();

                    //show meteorological parameters on screen
                    OutputOfMeteoData();

                    bool useGpu;
                    try { useGpu = GpuExperimental.Enabled && GpuExperimental.CanRun(1, NTEILMAX); }
                    catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
                    {
                        // A stop during the GPU check or startup finishes GRAL like the checks below.
                        goto FinishGral;
                    }
                    catch (Exception exc)
                    {
                        // Only the GPU switch and the GPU startup run in this try block
                        ReportGpuFailure(exc);
                        goto FinishGral;
                    }

                    //Transient mode: non-steady-state particles
                    if (ISTATIONAER == Consts.TransientMode)
                    {
                        Console.WriteLine();
                        Console.Write("Dispersion computation.....");
                        // calculate wet deposition parameter
                        if (IWET < WetDepoPrecipLst.Count)
                        {
                            if (WetDepoPrecipLst[IWET - 1] > 0)
                            {
                                WetDepoPrecipitation = WetDepoPrecipLst[IWET - 1];
                                WetDepoRW = Wet_Depo_CW * Math.Pow(WetDepoPrecipitation, WedDepoAlphaW);
                                WetDepoRW = Math.Max(0, Math.Min(1, WetDepoRW));
                            }
                            else
                            {
                                WetDepoRW = 0;
                            }
                        }

                        //set lower concentration threshold for memory effect
                        TransConcThreshold = ReaderClass.ReadTransientThreshold();
                        int cellNr = NII * NJJ;
                        DispTimeSum = TAUS;
                        // start the calculation for transient particle from all cells
                        if (useGpu)
                        {
                            if (ThreadWriteConz4dFile != null)
                            {
                                ThreadWriteConz4dFile.Join();
                                ThreadWriteConz4dFile = null;
                            }
                            RunGpuSituation();
                        }
                        else { ParallelTransientParticleDriver(0, cellNr); }
                    } // non-steady-state particles
                    if (CancelToken.IsCancellationRequested || gpuFailed)
                    {
                        goto FinishGral;
                    }
                    Console.WriteLine();
                    Console.Write("Dispersion computation.....");
                    //new released the particles from all sources
                    DispTimeSum = TAUS;
                    if (useGpu && ISTATIONAER == Consts.StationaryMode) { RunGpuSituation(); }
                    else if (!useGpu) { ParallelParticleDriver(1, NTEILMAX + 1); }
                    Console.WriteLine();
                    if (CancelToken.IsCancellationRequested || gpuFailed)
                    {
                        goto FinishGral;
                    }
                    // Wait until conz4d file is written
                    if (ThreadWriteConz4dFile != null) // if Thread has been started -> wait until GFF--WriteThread has been finished
                    {
                        ThreadWriteConz4dFile.Join();  // wait, until thread has been finished
                        ThreadWriteConz4dFile = null;  // Release ressources				
                    }

                    //tranferring non-steady-state concentration fields
                    if (ISTATIONAER == Consts.TransientMode)
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

                    if (LogLevel > Consts.LogLevelOff) // additional LOG-Output
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
                    Console.WriteLine("Writing result files in a background thread");
                    recentWeatherSituation = IWET;
                    ThreadWrite2DConcentrationFiles = new Thread(() => WriteClass.Write2DConcentrations(recentWeatherSituation, ZippedFile));
                    ThreadWrite2DConcentrationFiles.Start(); // start writing thread

                    if (SyncWithMutex != null && SyncWithMutex.WaitOne(2000)) //File access synchronisation across multiple GRAL instances allows for a waiting time of up to 2000 ms.
                    {
                        //receptor concentrations
                        if (ISTATIONAER == Consts.TransientMode)
                        {
                            WriteClass.WriteReceptorTimeseries(0);
                        }
                        WriteClass.WriteReceptorConcentrations();

                        //microscale flow-field at receptors
                        WriteClass.WriteMicroscaleFlowfieldReceptors();
                        SyncWithMutex.ReleaseMutex(); // release the mutex
                    }
                    else
                    {
                        //receptor concentrations
                        if (ISTATIONAER == Consts.TransientMode)
                        {
                            WriteClass.WriteReceptorTimeseries(0);
                        }
                        WriteClass.WriteReceptorConcentrations();

                        //microscale flow-field at receptors
                        WriteClass.WriteMicroscaleFlowfieldReceptors();
                    }
                } //skipped situation if no entry in meteopgt.all could be found in transient GRAL mode
            } // loop for all meteorological situations
FinishGral:
            bool gpuShutdownFailed = false;
            GpuExperimental.LogSummary(gpuFailed);
            try
            {
                GpuExperimental.Close();
            }
            catch (Exception exc)
            {
                // All situations are computed; finish writing the outputs and report the failure in the exit code.
                gpuShutdownFailed = true;
                string err = "GPU server shutdown failed: " + exc.Message + (exc.InnerException != null ? ": " + exc.InnerException.Message : string.Empty);
                Console.WriteLine();
                Console.WriteLine("ERROR: " + err);
                ProgramWriters.LogfileProblemreportWrite(err);
            }
            bool verticalOutputFailed = false;
            // Write summarized emission per source group and 3D Concentration file
            if (ISTATIONAER == Consts.TransientMode)
            {
                ProgramWriters WriteClass = new ProgramWriters();
                if (WriteVerticalConcentration && Program.IWETstartstop.Start < 1 && !gpuFailed) // do not write the files for multiple instances or from a situation that the GPU did not finish
                {
                    try
                    {
                        WriteClass.Write3DTextConcentrations();
                    }
                    catch (IOException exc)
                    {
                        // Finish the remaining output and cleanup steps, then report the failure in the exit code.
                        verticalOutputFailed = true;
                        string err = exc.Message + (exc.InnerException != null ? ": " + exc.InnerException.Message : string.Empty);
                        Console.WriteLine();
                        Console.WriteLine("ERROR: " + err);
                        ProgramWriters.LogfileProblemreportWrite(err);
                    }
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

            if (ThreadWrite2DConcentrationFiles != null) // if Thread has been started -> wait until *.grz WriteThread has been finished
            {
                ThreadWrite2DConcentrationFiles.Join(); // wait, until thread has been finished
                ThreadWrite2DConcentrationFiles = null; // Release ressources
            }

            if (ThreadWriteConz4dFile != null) // wait for the last transient checkpoint before the temporary files are deleted
            {
                ThreadWriteConz4dFile.Join();
                ThreadWriteConz4dFile = null;
            }

            if (gpuFailed)
            {
                // Leave the checkpoint of the last finished situation, as the unhandled GPU error did before and as an interrupted run does
                if (File.Exists("Transient_Concentrations1.tmp") || File.Exists("Transient_Concentrations2.tmp"))
                {
                    ProgramWriters.LogfileProblemreportWrite("The run stopped with a GPU error. The transient checkpoint files were kept, as after an interrupted run; delete the *.tmp files to repeat the run from the start.");
                }
            }
            else if (verticalOutputFailed && (File.Exists("Transient_Concentrations1.tmp") || File.Exists("Transient_Concentrations2.tmp")))
            {
                // Leave the checkpoint as an interrupted run would; GRAL's usual restart rules apply to a rerun in this folder
                ProgramWriters.LogfileProblemreportWrite("Vertical_Concentrations.txt was not written. The transient checkpoint files were kept, as after an interrupted run; delete the *.tmp files to repeat the run from the start.");
            }
            else
            {
                Delete_Temp_Files();
                if (verticalOutputFailed)
                {
                    ProgramWriters.LogfileProblemreportWrite("Vertical_Concentrations.txt was not written and no transient checkpoint exists; repeat the run to write it.");
                }
            }

            //Write receptor statistical error
            if (Program.ReceptorsAvailable)
            {
                ProgramWriters WriteClass = new ProgramWriters();
                if (SyncWithMutex != null && SyncWithMutex.WaitOne(2000)) //File access synchronisation across multiple GRAL instances allows for a waiting time of up to 2000 ms.
                {
                    WriteClass.WriteReceptorTimeseries(1);
                    SyncWithMutex.ReleaseMutex(); // release the mutex
                }
                else
                {
                    WriteClass.WriteReceptorTimeseries(1);
                }
            }

            if (SyncWithMutex != null)
            {
                SyncWithMutex.Close();
                SyncWithMutex.Dispose();
            }

            bool runFailed = verticalOutputFailed || gpuShutdownFailed || gpuFailed || MeteoInputFailed;
            if (Program.CancelToken.IsCancellationRequested)
            {
                ProgramWriters.LogfileGralCoreWrite("GRAL simulations cancelled at: " + DateTime.Now.ToString());    
            }
            else
            {
                ProgramWriters.LogfileGralCoreWrite((runFailed ? "GRAL simulations stopped with errors at: " : "GRAL simulations finished at: ") + DateTime.Now.ToString());
            }
            ProgramWriters.LogfileGralCoreWrite(new String('-', 80));

            if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
            {
                Console.WriteLine();
                if (Program.CancelToken.IsCancellationRequested)
                {
                    Console.WriteLine("GRAL simulations cancelled. Press any key to continue...");
                }
                else
                {
                    Console.WriteLine(runFailed ? "GRAL simulations stopped with errors. Press any key to continue..." : "GRAL simulations finished. Press any key to continue...");
                }
                Program.CleanUpMemory();
                Console.ReadKey(true); 	// wait for a key input
            }

            Environment.Exit(runFailed ? 1 : 0);        // Exit console; nonzero if the vertical file, the GPU or the meteorology input failed
        }
    }
}
