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
using System.Linq;
using System.Threading.Tasks;

namespace GRAL_2001
{
    partial class Program
    {
        private readonly record struct WeatherKey(int Row, int Field);
        private readonly record struct PreparedWind(MeteoReadStatus Status, bool MissingWind = false,
            bool ReadFromGff = false, double Seconds = 0);

        private static (WeatherKey Weather, Task<PreparedWind> Work)? pendingWind;
        private static Task windFileWriter;

        private static WeatherKey? ResolveWeather(int row)
        {
            if (row > IWETstartstop.End) return null;
            if (ISTATIONAER == Consts.TransientMode && IStatistics == Consts.MeteoPgtAll)
            {
                if (row > MeteoTimeSer.Count) return null;
                return new WeatherKey(row, InputMetTimeSeries.SearchWeatherSituation(row) + 1);
            }
            return new WeatherKey(row, row);
        }

        // After GPU init the residual batches read concentrations, source and grid constants.
        // Odour output and volume corrections still read the current host wind or building grid.
        internal static void StartNextWindField()
        {
            if (GpuSettings.Value("fallback_on_startup") != "0" || CancelToken.IsCancellationRequested ||
                ISTATIONAER != Consts.TransientMode || IStatistics != Consts.MeteoPgtAll ||
                Topo != Consts.TerrainAvailable || Odour || BuildingsExist || GRALOnlineFunctions || IWET == int.MaxValue ||
                GFFFilePath.Length != 0 || (IWETstartstop.Start > 0 && File.Exists("GRAL_FlowFields.txt")))
                return;

            // These validation runs publish the current field to a fixed filename.
            if (FlowFieldLevel == Consts.FlowFieldProg &&
                (File.Exists("IDAHO.txt") || Directory.EnumerateFiles(".", "VDI-*.txt").Any()))
                return;

            if (ResolveWeather(IWET + 1) is WeatherKey next && next.Field > 0)
            {
                if (pendingWind.HasValue) throw new InvalidOperationException("A wind field is already pending");
                pendingWind = (next, Task.Run(() => PrepareWind(next, false, true)));
            }
        }

        private static PreparedWind TakeWind(WeatherKey current, bool firstLoop)
        {
            if (pendingWind is not { } pending) return PrepareWind(current, firstLoop, false);
            pendingWind = null;
            var result = pending.Work.GetAwaiter().GetResult();
            if (pending.Weather != current)
                throw new InvalidOperationException($"Prepared weather {pending.Weather} does not match {current}");
            return result;
        }

        private static void DrainWindWriter()
        {
            var writer = windFileWriter;
            windFileWriter = null;
            writer?.GetAwaiter().GetResult();
        }

        private static void DrainWindWork()
        {
            try
            {
                if (pendingWind is { } pending)
                {
                    pendingWind = null;
                    try
                    {
                        if (pending.Work.GetAwaiter().GetResult().Status == MeteoReadStatus.InvalidInput)
                            MeteoInputFailed = true;
                    }
                    catch (Exception exc) when (exc is not OperationCanceledException)
                    {
                        throw new InvalidOperationException($"Weather {pending.Weather.Row}, field {pending.Weather.Field}: {exc.Message}", exc);
                    }
                }
            }
            finally { DrainWindWriter(); }
        }

        private static PreparedWind PrepareWind(WeatherKey weather, bool firstLoop, bool ahead)
        {
            DrainWindWriter();
            CancelToken.Token.ThrowIfCancellationRequested();
            var clock = Stopwatch.StartNew();
            if (ahead)
                Console.WriteLine($"Flow field calculation for the next situation starts in parallel (weather {weather.Row}, field {weather.Field})");

            WindVelGramm = -99;
            WindDirGramm = -99;
            StabClassGramm = -99;
            new ProgramReaders().ReadMaxNumbProc();
            bool terrainPgt = Topo == Consts.TerrainAvailable && IStatistics == Consts.MeteoPgtAll;
            Input_MeteopgtAll.WeatherRow classifiedRow = default;
            var status = terrainPgt ? Input_MeteopgtAll.ReadRow(weather.Field, out classifiedRow) : ReadMeteoData(weather.Field);
            if (status != MeteoReadStatus.Success) return new PreparedWind(status);

            if (Topo == Consts.TerrainAvailable)
            {
                string path = ReadWindfeldTXT();
                string stem = Path.Combine(path, weather.Field.ToString("00000"));
                if (!File.Exists(stem + ".wnd"))
                {
                    if (ISTATIONAER == Consts.TransientMode)
                    {
                        Console.WriteLine($"The following wind field does not exist: {stem}.wnd - situation skipped: {weather.Row}");
                        return new PreparedWind(MeteoReadStatus.Success, MissingWind: true);
                    }
                    throw new FileNotFoundException("The following wind field does not exist: " + stem + ".wnd");
                }
                Console.WriteLine("Reading GRAMM wind field: " + stem + ".wnd");
                ReadWndFile.Read(stem + ".wnd");
                AKL_GRAMM[0, 0] = 0; // no local stability classes until this field supplies them
                if (File.Exists(stem + ".scl"))
                {
                    Console.WriteLine("Reading GRAMM stability classes: " + stem + ".scl");
                    var reader = new ReadSclUstOblClasses { FileName = stem + ".scl", Stabclasses = AKL_GRAMM };
                    try
                    {
                        if (!reader.ReadSclFile() || reader.Stabclasses.GetUpperBound(0) != NX ||
                            reader.Stabclasses.GetUpperBound(1) != NY)
                            throw new InvalidDataException("Invalid GRAMM stability field: " + stem + ".scl");
                        AKL_GRAMM = reader.Stabclasses;
                    }
                    finally { reader.Close(); }
                }
            }

            if (terrainPgt)
            {
                status = Input_MeteopgtAll.Apply(classifiedRow);
                if (status != MeteoReadStatus.Success) return new PreparedWind(status);
            }
            CancelToken.Token.ThrowIfCancellationRequested();
            CalculateTemperatureGradient();
            bool fromGff = ReadGralFlowFields.Read(weather.Field);
            if (!fromGff)
            {
                if (Topo == Consts.TerrainAvailable)
                    MicroscaleTerrain.Calculate(firstLoop, weather.Field, !ahead);
                else if (Topo == Consts.TerrainFlat && (BuildingsExist || File.Exists("vegetation.dat")))
                    MicroscaleFlat.Calculate(weather.Field, !ahead);
                if (Topo == Consts.TerrainFlat) AHMIN = 0;
                CancelToken.Token.ThrowIfCancellationRequested();
                if (File.Exists("GRAL_FlowFields.txt"))
                    windFileWriter = Task.Run(() => WriteGRALFlowFields.Write(weather.Field));
                if (firstLoop) WriteGRALFlowFields.WriteGRALGeometries();
            }
            else if (FlowFieldLevel == Consts.FlowFieldProg && File.Exists("vegetation.dat") && VEG[0][0][1] < 0)
            {
                new ProgramReaders().ReadVegetation();
                VEG[0][0][1] = 0;
                VEG[0][0][0] = -0.001f;
            }
            return new PreparedWind(status, ReadFromGff: fromGff, Seconds: clock.Elapsed.TotalSeconds);
        }
    }
}
