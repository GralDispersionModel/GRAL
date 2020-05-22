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

namespace GRAL_2001
{
    class GRALONLINE
    {
        /// <summary>
    	///Output of the GRAL online data for the GUI
    	/// </summary>    
        public static void Output(int NI, int NJ, int NK)
        {
            try
            {
                string config1 = "";
                string config2 = "";
                string filename = "";

                for (int Ausgabe = 1; Ausgabe <= 16; Ausgabe++) // 16 different outputs possible
                {
                    switch (Ausgabe) // für jeden Fall den Filenamen festlegen
                    {
                        case 1: //ausgabe des u,v-windfeldes fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "uv";
                            break;
                        case 2: // ausgabe der u-windfeldkomponente fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "u";
                            break;
                        case 3: // ausgabe der horizontalen windgeschwindigkeit fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "speed";
                            break;
                        case 4: // ausgabe der horizontalen windgeschwindigkeit v fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "v";
                            break;
                        case 5: // ausgabe der vertikalen windgeschwindigkeit fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "w";
                            break;
                        case 6: // ausgabe des nicht hydrostat. Drucks fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "nhp";
                            break;
                        case 7: // ausgabe der turbulenten kinetischen Energie fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "tke";
                            break;
                        case 8: // ausgabe der Dissipation fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "dis";
                            break;
                        case 9: //ausgabe der Globalstrahlung fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "tpot";
                            break;
                        case 10: // ausgabe der horizontalen Windgeschwindigkeit fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "vp_Wind-speed";
                            break;
                        case 11: // ausgabe der West/Ost Windkomponente fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "vp_u-wind-component";
                            break;
                        case 12: // ausgabe der Nord/sued Windkomponente fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "vp_v-wind-component";
                            break;
                        case 13: // ausgabe der vertikalen Windkomponente fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "vp_w-wind-component";
                            break;
                        case 14: // ausgabe des nicht-hydrostatischen Drucks fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "vp_non-hydr-pressure";
                            break;
                        case 15: // ausgabe der turbulenten kinetischen Energie fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "vp_Turbulent-kinetic-energy";
                            break;
                        case 16: // ausgabe der Dissipation fuer GRAL Online Analyse mit Benutzeroberflaeche
                            filename = "vp_Dissipation";
                            break;
                    }

                    if (File.Exists(filename + ".txt")) // muss file ausgeschrieben werden?
                    {
                        using (StreamReader rd = new StreamReader(filename + ".txt")) // dann einlesen des gewuenschten Ausgabelevels ueber Grund
                        {
                            config1 = rd.ReadLine();   // versuche string zu lesen - bei Fehler ans Ende des using-Statements
                            config2 = rd.ReadLine();   // auch zweite Zeile vorhanden?
                        }

                        int LX = 1; // Defaultwert
                        Int32.TryParse(config1, out LX); // Layer oder LX
                        int LY = 1; // default
                        Int32.TryParse(config2, out LY); // immer LY

                        string writefile;
                        if (Ausgabe < 10) // man könnte auf den Textteil "vp_" prüfen, ist unabhängiger, aber nicht so schnell 
                        {
                            writefile = filename + "_GRAMM.txt";
                        }
                        else
                        {
                            writefile = "GRAMM-" + filename + ".txt";
                        }

                        using (StreamWriter wt = new StreamWriter(writefile)) // ausgabe des windfeldes - using = try-catch
                        {
                            if (Ausgabe < 10)
                            {
                                wt.WriteLine("ncols         " + NI.ToString(CultureInfo.InvariantCulture));
                                wt.WriteLine("nrows         " + NJ.ToString(CultureInfo.InvariantCulture));
                                wt.WriteLine("xllcorner     " + Program.IKOOAGRAL.ToString(CultureInfo.InvariantCulture));
                                wt.WriteLine("yllcorner     " + Program.JKOOAGRAL.ToString(CultureInfo.InvariantCulture));
                                wt.WriteLine("cellsize      " + Program.DXK.ToString(CultureInfo.InvariantCulture));
                                wt.WriteLine("NODATA_value  " + "-9999");

                                for (int jj = NJ; jj >= 1; jj--)
                                {
                                    for (int o = 1; o <= NI; o++)
                                    {
                                        if (Ausgabe == 1) // Sonderfall Ausgabe 1
                                        {
                                            wt.Write(Math.Round(0.5 * (Program.UK[o][jj][LX] + Program.UK[o + 1][jj][LX]), 3).ToString(CultureInfo.InvariantCulture) + " " + Math.Round(0.5 * (Program.VK[o][jj][LX] + Program.VK[o][jj + 1][LX]), 3).ToString(System.Globalization.CultureInfo.InvariantCulture) + " ");
                                        }
                                        else
                                        {
                                            wt.Write(GetValue(Ausgabe, o, jj, LX, LY).ToString(CultureInfo.InvariantCulture) + " ");
                                        }
                                    }
                                    wt.WriteLine();
                                }
                            }

                            else // Ausgabe ab Variante 10
                            {
                                for (int k = 1; k <= NK; k++)
                                {
                                    wt.WriteLine(Math.Round(Program.HOKART[k] - Program.DZK[k] * 0.5, 3).ToString(CultureInfo.InvariantCulture) + " " + GetValue(Ausgabe, 1, k, LX, LY).ToString(CultureInfo.InvariantCulture) + " ");
                                }
                            }

                        } // StreamWriter Ende

                    } // FileExist() Ende
                }

            } // Ausgabenschleife Ende
            catch
            {
            }

        } // GRAMMONLINE Ende

        private static double GetValue(int Ausgabe, int o, int jj, int LX, int LY)
        { // Ermittle Ausgabewerte und Zahlen-Rundung
            try
            {
                switch (Ausgabe)
                {
                    case 1: // Ausgabe des u,v-windfeldes fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return 0;

                    case 2: // Ausgabe der u-windfeldkomponente fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(0.5F * (Program.UK[o][jj][LX] + Program.UK[o + 1][jj][LX]), 3);

                    case 3: // Ausgabe der horizontalen windgeschwindigkeit fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(MathF.Sqrt(MathF.Pow(0.5F * (Program.UK[o][jj][LX] + Program.UK[o + 1][jj][LX]), 2) + MathF.Pow(0.5F * (Program.VK[o][jj][LX] + Program.VK[o][jj + 1][LX]), 2)), 3);

                    case 4: //Ausgabe der horizontalen windgeschwindigkeit v fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(0.5F * (Program.VK[o][jj][LX] + Program.VK[o][jj + 1][LX]), 3);

                    case 5: //Ausgabe der vertikalen windgeschwindigkeit fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(0.5F * (Program.WK[o][jj][LX] + Program.WK[o][jj][LX + 1]), 3);

                    case 6: //Ausgabe des nicht-hydrostatischen Drucks fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.DPM[o][jj][LX], 3);

                    case 7: // Ausgabe der turbulenten kinetischen Energie fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.TURB[o][jj][LX], 3);

                    case 8: //Ausgabe der turbulenten kinetischen Energie fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.TDISS[o][jj][LX], 6);

                    case 10: // ausgabe der horizontalen Windgeschwindigkeit fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(MathF.Sqrt(MathF.Pow(Program.UK[LX][LY][jj], 2) + MathF.Pow(Program.VK[LX][LY][jj], 2)), 3);

                    case 11: // ausgabe der West/Ost Windkomponente fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.UK[LX][LY][jj], 3);

                    case 12: // ausgabe der Nord/sued Windkomponente fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.VK[LX][LY][jj], 3);

                    case 13: // ausgabe der vertikalen Windkomponente fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.WK[LX][LY][jj], 3);

                    case 14: // ausgabe des nicht-hydrostatischen Drucks fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.DPM[LX][LY][jj], 3);

                    case 15: // ausgabe der turbulenten kinetischen Energie fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.TURB[LX][LY][jj], 3);

                    case 16: // ausgabe der Dissipation fuer GRAL Online Analyse mit Benutzeroberflaeche
                        return MathF.Round(Program.TDISS[LX][LY][jj], 6);

                    default:
                        return -9999; // ERROR
                }
            }
            catch
            {
                return -9999; // ERROR
            }
        } // value() Ende
    }
}
