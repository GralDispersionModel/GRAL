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
using System.Text;

namespace GRAL_2001
{
    public partial class ProgramWriters
    {
        private static int month_old = 0;
        private static int day_old = 0;
        private static int hour_old = 0;
        private static int minute_old = 0;


        /// <summary>
        ///In case of complex terrain and/or the presence of buildings some data is written for usage in the GUI (visualization of vertical slices)
        /// </summary>
        public void WriteGRALGeometries()
        {
            if ((Program.Topo == 1) || (Program.BuildingsExist == true))
            {
                //write geometry data
                string GRALgeom = "GRAL_geometries.txt";
                try
                {
                    using (BinaryWriter writer = new BinaryWriter(File.Open(GRALgeom, FileMode.Create)))
                    {
                        writer.Write((Int32)Program.NKK);
                        writer.Write((Int32)Program.NJJ);
                        writer.Write((Int32)Program.NII);
                        writer.Write((Int32)Program.IKOOAGRAL);
                        writer.Write((Int32)Program.JKOOAGRAL);
                        writer.Write((float)Program.DZK[1]);
                        writer.Write((float)Program.StretchFF);
                        if (Program.StretchFF < 0.1)
                        {
                            writer.Write((int)Program.StretchFlexible.Count);
                            foreach (float[] stretchflex in Program.StretchFlexible)
                            {
                                writer.Write((float)stretchflex[0]);
                                writer.Write((float)stretchflex[1]);
                            }
                        }
                        writer.Write((float)Program.AHMIN);

                        for (int i = 1; i <= Program.NII + 1; i++)
                        {
                            for (int j = 1; j <= Program.NJJ + 1; j++)
                            {
                                writer.Write((float)Program.AHK[i][j]);
                                writer.Write((Int32)Program.KKART[i][j]);
                                writer.Write((float)Program.BUI_HEIGHT[i][j]);
                            }
                        }
                    }
                }
                catch
                {
                    string err = "Error when writing file " + GRALgeom;
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
        }//in case of complex terrain and/or the presence of buildings some data is written for usage in the GUI (visualization of vertical slices)

        /// <summary>
        ///optional: write building heights as utilized in GRAL
        /// </summary>
        public void WriteBuildingHeights(string Filename, float[][] Data, string Format, int Digits, double west, double south)
        {
            if ((Math.Abs(Program.IOUTPUT) > 1) && (Program.FlowFieldLevel > 0))
            {
                try
                {
                    CultureInfo ic = CultureInfo.InvariantCulture;
                    using (StreamWriter wt = new StreamWriter(Filename))
                    {
                        wt.WriteLine("ncols         " + Program.NII.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("nrows         " + Program.NJJ.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("xllcorner     " + west.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("yllcorner     " + south.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("cellsize      " + Program.DXK.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("NODATA_value  " + "-9999");

                        StringBuilder SB = new StringBuilder();
                        for (int jj = Program.NJJ; jj >= 1; jj--)
                        {
                            for (int o = 1; o <= Program.NII; o++)
                            {
                                SB.Append(Math.Round(Data[o][jj], Digits).ToString(Format, ic));
                                SB.Append(" ");
                            }
                            wt.WriteLine(SB.ToString());
                            SB.Clear();
                        }
                    }
                }
                catch(Exception ex) {Console.WriteLine(ex.Message);}
            }
        }//optional: write building heights as utilized in GRAL

        /// <summary>
        ///optional: write Sub domain as utilized in GRAL
        /// </summary>
        public void WriteSubDomain(string Filename, byte[][] Data, string Format, int Digits, double west, double south)
        {
            if ((Math.Abs(Program.IOUTPUT) > 1) && (Program.FlowFieldLevel > 0))
            {
                try
                {
                    CultureInfo ic = CultureInfo.InvariantCulture;
                    using (StreamWriter wt = new StreamWriter(Filename))
                    {
                        wt.WriteLine("ncols         " + Program.NII.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("nrows         " + Program.NJJ.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("xllcorner     " + west.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("yllcorner     " + south.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("cellsize      " + Program.DXK.ToString(CultureInfo.InvariantCulture));
                        wt.WriteLine("NODATA_value  " + "-9999");

                        StringBuilder SB = new StringBuilder();
                        for (int jj = Program.NJJ; jj >= 1; jj--)
                        {
                            for (int o = 1; o <= Program.NII; o++)
                            {
                                SB.Append(Data[o][jj].ToString(Format, ic));
                                SB.Append(" ");
                            }
                            wt.WriteLine(SB.ToString());
                            SB.Clear();
                        }
                    }
                }
                catch(Exception ex) {Console.WriteLine(ex.Message);}
            }
        }//optional: write Sub Domain as utilized in GRAL
    }
}
