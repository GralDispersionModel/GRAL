using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GRAL_2001
{
    partial class Program
    {
        // Only the GPU server runs concurrently. The host owns all flow arrays until the next
        // prepare. The objects below retain the inputs still needed by current output writers.
        private sealed class MicroscaleFlowAhead
        {
            internal int Weather = IWET;
            private readonly int windField = IDISP;
            private readonly int cursor = IWETstart;
            private readonly int vertical = KADVMAX;
            private readonly float minHeight = AHMIN;
            private readonly float[][] buildings = BUI_HEIGHT;
            private readonly float windSpeed = WindVelGramm;
            private readonly float windDirection = WindDirGramm;
            private readonly int stability = StabClassGramm;
            internal Exception Error;
            internal Action ReplayLog;
            internal double Seconds;
            internal bool CachedFieldAvailable => File.Exists(Path.Combine(GFFFilePath, windField.ToString("00000") + ".gff"));

            internal void Activate()
            {
                IWET = Weather;
                IDISP = windField;
                IWETstart = cursor;
                KADVMAX = vertical;
                AHMIN = minHeight;
                BUI_HEIGHT = buildings;
                WindVelGramm = windSpeed;
                WindDirGramm = windDirection;
                StabClassGramm = stability;
            }
        }

        private static bool HasFlatFlowObstacles() => BuildingsExist || File.Exists("vegetation.dat");

        private static void CalculateMicroscaleFlow(bool firstLoop)
        {
            if (Topo == Consts.TerrainAvailable)
                MicroscaleTerrain.Calculate(firstLoop);
            else if (Topo == Consts.TerrainFlat)
            {
                if (HasFlatFlowObstacles()) MicroscaleFlat.Calculate();
                AHMIN = 0;
            }
        }

        private static bool CanPrepareMicroscaleFlow()
        {
            // Ranged instances and projects with an external GFF folder may share the wind cache.
            return IWETstartstop.Start == 0 && string.IsNullOrEmpty(GFFFilePath) && IStatistics == Consts.MeteoPgtAll &&
                   (Topo == Consts.TerrainAvailable || (Topo == Consts.TerrainFlat && HasFlatFlowObstacles())) &&
                   !Odour && !ReceptorsAvailable && !GRALOnlineFunctions && !WriteVerticalConcentration &&
                   IWET < IWETstartstop.End && (ISTATIONAER != Consts.TransientMode || IWET < MeteoTimeSer.Count) &&
                   !CancelToken.IsCancellationRequested && (SubDomainRefPos.X != 0 || SubDomainRefPos.Y != 0);
        }

        private static MicroscaleFlowAhead PrepareMicroscaleFlow(InputMettimeSeries timeSeries, ProgramReaders readers)
        {
            var current = new MicroscaleFlowAhead();
            MicroscaleFlowAhead next = null;
            var timer = Stopwatch.StartNew();
            ProgramWriters.BeginFlowLogCapture();
            try
            {
                IWET++;
                IDISP = ISTATIONAER == Consts.TransientMode ? timeSeries.SearchWeatherSituation() + 1 : IWET;
                if (IDISP <= 0 || File.Exists(Path.Combine(GFFFilePath, IDISP.ToString("00000") + ".gff")))
                    return null;
                string windPath = Topo == Consts.TerrainAvailable ? ReadWindfeldTXT() : string.Empty;
                if (Topo == Consts.TerrainAvailable && !File.Exists(Path.Combine(windPath, IDISP.ToString("00000") + ".wnd")))
                    return null;
                // These optional solver modes write situation-specific user files immediately.
                if ((IDISP == 1 && File.Exists("GRAL_Topography.txt")) ||
                    File.Exists("IDAHO.txt") || Directory.EnumerateFiles(".", "VDI-*.txt").Any() ||
                    (File.Exists("vegetation.dat") && VEG[0][0][0] < 0))
                    return null;

                Console.WriteLine();
                Console.WriteLine("Preparing flow field for weather situation " + IWET +
                                  " after GPU submission for situation " + current.Weather);
                readers.ReadMaxNumbProc();
                WindVelGramm = -99;
                WindDirGramm = -99;
                StabClassGramm = -99;
                if (ISTATIONAER == Consts.StationaryMode)
                {
                    if (Input_MeteopgtAll.Read(speculative: true) == Consts.CalculationFinished) return null;
                }
                IWETstart = IDISP;
                if (Topo == Consts.TerrainAvailable)
                {
                    if (!ReadWndFile.Read(windPath, speculative: true)) return null;
                    ReadTerrainStability(windPath);
                }
                if (Input_MeteopgtAll.Read(speculative: true) == Consts.CalculationFinished) return null;
                CalculateTemperatureGradient();
                if (BuildingsExist)
                    BUI_HEIGHT = Array.ConvertAll(BUI_HEIGHT, row => (float[])row.Clone());
                CalculateMicroscaleFlow(false);
                next = new MicroscaleFlowAhead { Seconds = timer.Elapsed.TotalSeconds };
            }
            catch (Exception error)
            {
                // Finish GPU work and publish N first. Report errors against their own weather.
                next = new MicroscaleFlowAhead { Weather = current.Weather + 1, Error = error };
            }
            finally
            {
                Action replay = ProgramWriters.EndFlowLogCapture();
                if (next != null) next.ReplayLog = replay;
                current.Activate();
            }
            return next;
        }

        private static void ReadTerrainStability(string windPath)
        {
            string stabilityFile = Path.Combine(windPath, IDISP.ToString("00000") + ".scl");
            if (File.Exists(stabilityFile))
            {
                Console.WriteLine("Reading GRAMM stability classes: " + stabilityFile);

                var reader = new ReadSclUstOblClasses
                {
                    FileName = stabilityFile,
                    Stabclasses = AKL_GRAMM
                };
                reader.ReadSclFile();
                AKL_GRAMM = reader.Stabclasses;
                reader.Close();
            }
        }
    }
}
