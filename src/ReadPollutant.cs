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

/*
 * Created by SharpDevelop.
 * User: Markus Kuntner
 * Date: 15.01.2018
 * Time: 13:58
*/

using System;
using System.IO;

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
        /// <summary>
        ///Check Pollutant.txt and if odour is computed
        /// </summary>
        /// <result>
        /// True -> Odour calculation, false -> Other pollution
        ///</result>
        public bool ReadPollutantTXT()
        {
            bool odour = false;

            string pollutant_file = "Pollutant.txt";
            if (File.Exists(pollutant_file)) // new pollutant.txt since V 18.07 with wet depo data and decay rate
            {
                try
                {
                    using (StreamReader myreader = new StreamReader(pollutant_file))
                    {
                        string text = myreader.ReadLine();
                        string[] txt = new string[10];

                        Program.PollutantType = text;
                        if (text.ToLower().Contains("odour"))
                        {
                            odour = true;
                        }

                        if (myreader.EndOfStream == false)
                        {
                            txt = myreader.ReadLine().Split(new char[] { '\t', '!' });
                            txt[0] = txt[0].Trim();
                            txt[0] = txt[0].Replace(".", decsep);
                            Program.Wet_Depo_CW = Convert.ToDouble(txt[0]);

                            txt = myreader.ReadLine().Split(new char[] { '\t', '!' });
                            txt[0] = txt[0].Trim();
                            txt[0] = txt[0].Replace(".", decsep);
                            Program.WedDepoAlphaW = Convert.ToDouble(txt[0]);

                            if (Program.ISTATIONAER == Consts.TransientMode && Program.Wet_Depo_CW > 0 && Program.WedDepoAlphaW > 0)
                            {
                                Program.WetDeposition = true;
                            }
                            else
                            {
                                Program.WetDeposition = false;
                            }
                        }

                        if (myreader.EndOfStream == false)
                        {
                            txt = myreader.ReadLine().Split(new char[] { '\t', '!' });
                            txt[0] = txt[0].Trim();
                            txt[0] = txt[0].Replace(".", decsep);
                            double decay = Convert.ToDouble(txt[0]);
                            for (int i = 0; i < Program.DecayRate.Length; i++) // set all source groups to this decay rate
                            {
                                Program.DecayRate[i] = decay;
                            }

                            if (myreader.EndOfStream == false) // decay rate for each source group
                            {
                                txt = myreader.ReadLine().Split(new char[] { '\t', '!' }); // split groups

                                for (int i = 0; i < txt.Length; i++)
                                {
                                    string[] _values = txt[i].Split(new char[] { ':' }); // split source group number and decay rate
                                    if (_values.Length > 1)
                                    {
                                        int sg_number = 0;
                                        if (int.TryParse(_values[0], out sg_number))
                                        {
                                            if (sg_number > 0 && sg_number < 100)
                                            {
                                                try
                                                {
                                                    decay = Convert.ToDouble(_values[1], ic);
                                                }
                                                catch
                                                {
                                                    decay = 0;
                                                }
                                                Program.DecayRate[sg_number] = decay;
                                            }
                                        }
                                    }
                                }


                            }
                        }
                    }
                }
                catch { }
            }
            else // old pollutant.txt at the Settings folder
            {
                pollutant_file = System.IO.Directory.GetCurrentDirectory(); //actual path without / at the end
                DirectoryInfo di = new DirectoryInfo(pollutant_file);
                pollutant_file = Path.Combine(di.Parent.FullName, "Settings", "Pollutant.txt");
                if (File.Exists(pollutant_file) == true)
                {
                    try
                    {
                        using (StreamReader myreader = new StreamReader(pollutant_file))
                        {
                            string txt = myreader.ReadLine();
                            if (txt.ToLower().Contains("odour"))
                            {
                                odour = true;
                            }
                        }
                    }
                    catch { }
                }
            }
            return odour;
        } //check odour
    }
}