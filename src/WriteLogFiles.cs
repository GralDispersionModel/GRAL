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
using System.Linq;

namespace GRAL_2001
{
    public partial class ProgramWriters
    {
        /// <summary>
        ///Output of GRAL logging file
        /// </summary>
        public static void LogfileGralCoreInfo(bool zipped, bool gff_files)
        {
            // Write additional Data to the Log-File
            LogfileGralCoreWrite("");

            string err = "Particles per second per weather situation: " + Program.TPS.ToString();
            LogfileGralCoreWrite(err);
            err = "Dispersion time [s]: " + Program.TAUS.ToString();
            LogfileGralCoreWrite(err);
            err = "Sum of all particles: " + Program.NTEILMAX.ToString();
            LogfileGralCoreWrite(err);
            LogfileGralCoreWrite("");

            err = "GRAL domain area";
            LogfileGralCoreWrite(err);
            err = "  West  (abs): " + Program.GralWest.ToString() + " (rel): " + Program.XsiMinGral.ToString();
            LogfileGralCoreWrite(err);
            err = "  East  (abs): " + Program.GralEast.ToString() + " (rel): " + Program.XsiMaxGral.ToString();
            LogfileGralCoreWrite(err);
            err = "  North (abs): " + Program.GralNorth.ToString() + " (rel): " + Program.EtaMaxGral.ToString();
            LogfileGralCoreWrite(err);
            err = "  South (abs): " + Program.GralSouth.ToString() + " (rel): " + Program.EtaMinGral.ToString();
            LogfileGralCoreWrite(err);
            err = "  Latitude [°]: " + Program.LatitudeDomain.ToString();
            LogfileGralCoreWrite(err);

            LogfileGralCoreWrite("");
            err = "  Concentration grid cell nr. in x-direction: " + Program.NXL.ToString();
            LogfileGralCoreWrite(err);
            err = "  Concentration grid cell nr. in y-direction: " + Program.NYL.ToString();
            LogfileGralCoreWrite(err);
            err = "  Concentration grid size in x direction  [m]: " + Program.dx.ToString();
            LogfileGralCoreWrite(err);
            err = "  Concentration grid size in y direction  [m]: " + Program.dy.ToString();
            LogfileGralCoreWrite(err);
            err = "  Concentration grid size in z direction  [m]: " + Program.dz.ToString();
            LogfileGralCoreWrite(err);

            err = "  Total number of horizontal slices for the concentration grid: " + Program.NS.ToString();
            LogfileGralCoreWrite(err);

            for (int i = 0; i < Program.NS; i++)
            {
                try
                {
                    err = "    Slice height above ground [m]: " + Program.HorSlices[i + 1].ToString();
                    LogfileGralCoreWrite(err);
                }
                catch
                { }
            }

            float height = Program.ModelTopHeight;
            if (Program.AHMIN < 9999)
                height += Program.AHMIN;

            err = "  Top height of the GRAL domain (abs) [m]: " + (Math.Round(height)).ToString();
            LogfileGralCoreWrite(err);

            LogfileGralCoreWrite("");

            if ((Program.Topo == 1) || (Program.BuildingFlatExist == true) || (Program.BuildingTerrExist == true)) // compute prognostic flow field in these cases
            {
                err = "  Flow field grid cell nr. in x-direction: " + Program.NII.ToString();
                LogfileGralCoreWrite(err);
                err = "  Flow field grid cell nr. in y-direction: " + Program.NJJ.ToString();
                LogfileGralCoreWrite(err);
                err = "  Flow field grid size in x direction  [m]: " + Program.DXK.ToString();
                LogfileGralCoreWrite(err);
                err = "  Flow field grid size in y direction  [m]: " + Program.DYK.ToString();
                LogfileGralCoreWrite(err);

                if (gff_files == false) // KADVMAX is not valid, if gff files are read from file
                {
                    err = "  Flow field grid cell nr. in z-direction: " + Program.KADVMAX.ToString();
                    LogfileGralCoreWrite(err);
                    err = "  Flow field grid height of first layer [m]: " + Math.Round(Program.DZK[1]).ToString();
                    LogfileGralCoreWrite(err);
                    if (Program.StretchFF > 0)
                    {
                        err = "  Flow field vertical stretching factor : " + Program.StretchFF.ToString();
                        LogfileGralCoreWrite(err);
                    }
                    else
                    {
                        foreach (float[] stretchflex in Program.StretchFlexible)
                        {
                            err = "  Flow field vertical stretching factor " + stretchflex[1].ToString() + " Starting at a height of " + Math.Round(stretchflex[0], 1).ToString();
                            LogfileGralCoreWrite(err);
                        }
                    }


                    try
                    {
                        height = 0;
                        for (int i = 1; i < Program.KADVMAX + 1; i++)
                            height += Program.DZK[i];

                        if (Program.AHMIN < 9999)
                            height += Program.AHMIN;

                        err = "  Flow field grid maximum elevation (abs) [m]: " + Math.Round(height).ToString();
                        LogfileGralCoreWrite(err);
                    }
                    catch { }

                    if (File.Exists("Integrationtime.txt") == true)
                    {
                        int INTEGRATIONSSCHRITTE_MIN = 0;
                        int INTEGRATIONSSCHRITTE_MAX = 0;
                        try
                        {
                            using (StreamReader sr = new StreamReader("Integrationtime.txt"))
                            {
                                INTEGRATIONSSCHRITTE_MIN = Convert.ToInt32(sr.ReadLine());
                                INTEGRATIONSSCHRITTE_MAX = Convert.ToInt32(sr.ReadLine());
                            }
                        }
                        catch
                        { }
                        err = "  Flow field minimum iterations : " + INTEGRATIONSSCHRITTE_MIN.ToString();
                        LogfileGralCoreWrite(err);
                        err = "  Flow field maximum iterations : " + INTEGRATIONSSCHRITTE_MAX.ToString();
                        LogfileGralCoreWrite(err);
                    }

                    //roughness length for building walls
                    float building_Z0 = 0.001F;
                    if (File.Exists("building_roughness.txt") == true)
                    {
                        try
                        {
                            using (StreamReader sr = new StreamReader("building_roughness.txt"))
                            {
                                building_Z0 = Convert.ToSingle(sr.ReadLine().Replace(".", Program.Decsep));
                            }
                        }
                        catch
                        { }
                    }
                    err = "  Flow field roughness of building walls : " + building_Z0.ToString();
                    LogfileGralCoreWrite(err);


                }
                else
                {
                    err = "  Reading *.gff files";
                    LogfileGralCoreWrite(err);
                }
                LogfileGralCoreWrite("");
            }

            if (Program.ISTATIONAER == 0)
            {
                err = "";
                LogfileGralCoreWrite(err);
                err = "  Transient concentration grid cell nr. in x-direction: " + Program.NII.ToString();
                LogfileGralCoreWrite(err);
                err = "  Transient concentration grid cell nr. in y-direction: " + Program.NJJ.ToString();
                LogfileGralCoreWrite(err);
                err = "  Transient concentration grid cell nr. in z-direction: " + Program.NKK_Transient.ToString();
                LogfileGralCoreWrite(err);
                err = "  Transient concentration threshold [µg/m³]: " + Program.TransConcThreshold.ToString();
                LogfileGralCoreWrite(err);
                err = "";
                LogfileGralCoreWrite(err);
            }

            if (Program.Topo == 0)
            {
                err = "Flat terrain ";
                LogfileGralCoreWrite(err);
            }
            else if (Program.Topo == 1)
            {
                if (File.Exists("GRAL_topofile.txt") == false || Program.AHKOriMin > 100000) // Gral topofile not available or corrupt (AHK_Ori_Min)
                    err = "Complex terrain - using interpolated GRAMM topography";
                else
                    err = "Complex terrain - using GRAL_topofile.txt";
                LogfileGralCoreWrite(err);

                // get GRAMM Path if available
                err = String.Empty;
                string sclfilename = Convert.ToString(Program.IWET).PadLeft(5, '0') + ".scl";
                if (File.Exists(sclfilename))
                {
                    err = "Reading GRAMM wind fields from: " + Directory.GetCurrentDirectory();
                }
                else if (File.Exists("windfeld.txt") == true)
                {
                    err = "Reading GRAMM wind fields from: " + Program.ReadWindfeldTXT();
                }
                if (err != String.Empty)
                {
                    LogfileGralCoreWrite(err);
                }
            }

            if (Program.AHMAX > -1)
            {
                err = "Minimum elevation of topography [m]: " + Math.Round(Program.AHMIN, 1).ToString();
                LogfileGralCoreWrite(err);
                err = "Maximum elevation of topography [m]: " + Math.Round(Program.AHMAX, 1).ToString();
                LogfileGralCoreWrite(err);
            }

            if (Program.LandUseAvailable == true)
                err = "Roughness length from landuse file available";
            else
            {
                //                if (topo == 1)
                //                    err = "Roughness from GRAMM Windfield available";
                //                else
                err = "Roughness length [m]: " + Program.Z0.ToString();
            }
            LogfileGralCoreWrite(err);


            if (Program.FlowFieldLevel == 0)
                err = "No buildings";
            else if (Program.FlowFieldLevel == 1)
                err = "Diagnostic approach for buildings";
            else
                err = "Prognostic approach for buildings";
            LogfileGralCoreWrite(err);

            err = "Number of receptor points: " + Program.ReceptorNumber.ToString();
            LogfileGralCoreWrite(err);

            if (zipped == true)
            {
                err = "Writing compressed result files *.grz";
                LogfileGralCoreWrite(err);
            }

            if (Program.Odour == true)
            {
                err = "Computing odour";
            }
            else
            {
                err = "Computing pollutant " + Program.PollutantType;
                if (Program.DepositionExist == true)
                    err += " with deposition";
            }
            LogfileGralCoreWrite(err);

            //decay rate
            for (int i = 0; i < Program.SourceGroups.Count; i++)
            {
                int sg = Program.SourceGroups[i];
                if (Program.DecayRate[sg] > 0)
                {
                    err = "  Decay rate " + Program.DecayRate[sg].ToString() + " for source group " + sg.ToString();
                    LogfileGralCoreWrite(err);
                }
            }

            // Wet deposition
            if (Program.WetDeposition)
            {
                double prec = Program.WetDepoPrecipLst.Sum();
                err = "Wet deposition enabled. Sum of precipitation " + Math.Round(prec, 1).ToString() + " mm";
                LogfileGralCoreWrite(err);
                err = "Wet deposition CW " + Program.Wet_Depo_CW.ToString() + " 1/s      AlphaW " + Program.WedDepoAlphaW.ToString();
                LogfileGralCoreWrite(err);
            }

            err = "Starting computation with weather situation: " + Program.IWET.ToString();
            LogfileGralCoreWrite(err);
            LogfileGralCoreWrite("");

        } // Write Logfile

        /// <summary>
        ///Output of GRAL logging file
        /// </summary>
        public static void LogfileGralCoreWrite(string a)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter("Logfile_GRALCore.txt", true))
                {
                    sw.WriteLine(a);
                    sw.Flush();
                }
            }
            catch { }
        }

        /// <summary>
        ///Output of GRAL Problem report
        /// </summary>
        public static void LogfileProblemreportWrite(string a)
        {
            a = "GRAL Error: " + a;
            try
            {
                using (StreamWriter sw = new StreamWriter("Problemreport_GRAL.txt", true))
                {
                    sw.WriteLine(a);
                    sw.Flush();
                }
            }
            catch { }
            LogfileGralCoreWrite(a); // Write error to LogfileCore
        }
    }
}
