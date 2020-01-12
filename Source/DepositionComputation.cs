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
 * Date: 09.11.2016
 * Time: 11:27
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;


namespace GRAL_2001
{
	/// <summary>
	/// Create internal additional sources for each dust grain size PMxx particles 
	/// Each of these sources get the corresponding emission rate for that grain size
	/// </summary>
	public class Deposition
	{
		private string[] _text;
		public  string[] Text {set {_text = value;} }
		
		private int _start_index;
		public int Dep_Start_Index {set{_start_index = value;} }
		
		private SourceData _sd;
		public  SourceData SD  {set {_sd = value;} }
		
		private List <SourceData> _Q;
		public List<SourceData> SourceData {get{return _Q;} set{_Q = value;}}
		
		/// <summary>
    	///Add sources for each particle class of PMxx grain sizes
    	/// </summary>
		public bool Compute()
		{
			
			if (_text.Length > _start_index + 6) // all deposition parameters available?
			{
				return ComputeDepositionSources();
			}
			else // add source data without deposition
			{
				_Q.Add(_sd);
			}
			return true;
		}
		
		/// <summary>
		///Create deposition only sources and split PM10 sources to PM10 & PM2.5 
		/// </summary>
		private bool ComputeDepositionSources()
		{
			// evaluates the source - deposition data
			int Frac_25 = 0;
			int Frac_10 = 0;
			int Frac_30 = 100;
			int DM_30 = 30;
			double Density = 0;
			float V_Sed25 = 0;
			float V_Sed10 = 0;
			float V_Sed30 = 0;
			float V_Dep25 = 0;
			float V_Dep10 = 0;
			float V_Dep30 = 0;
			double emission25 = 0;
			double emission10 = 0;
			double emission30 = 0;
			double originalemission = _sd.ER;
			
			int mode = 0; // 0 = nothing, 1 = PM2.5 = concentration + deposition; 2 = PM10 + PM 2.5 = concentration + deposition
			try
			{
				Frac_25  = Convert.ToInt32(_text[_start_index].Replace(".", Program.Decsep));
				Frac_10  = Convert.ToInt32(_text[_start_index + 1].Replace(".", Program.Decsep));
				DM_30    = Convert.ToInt32(_text[_start_index + 2].Replace(".", Program.Decsep));
				Density  = Convert.ToDouble(_text[_start_index + 3].Replace(".", Program.Decsep));
				V_Dep25  = Convert.ToSingle(_text[_start_index + 4].Replace(".", Program.Decsep));
				V_Dep10  = Convert.ToSingle(_text[_start_index + 5].Replace(".", Program.Decsep));
				V_Dep30  = Convert.ToSingle(_text[_start_index + 6].Replace(".", Program.Decsep));
				mode     = Convert.ToInt32(_text[_start_index + 7].Replace(".", Program.Decsep));
				
				V_Sed25 = SedimentationVelocity(290, 2.5, Density); 
				V_Sed10 = SedimentationVelocity(290, 6.3, Density);
				V_Sed30 = SedimentationVelocity(290, DM_30, Density); 
//				Console.WriteLine("VSed: " + V_Sed25 + "/" + V_Sed10 +"/" +V_Sed30); // debug
//				Console.WriteLine("VDep: " + V_Dep25 + "/" + V_Dep10 +"/" +V_Dep30); // debug
				
				if (mode == 0) // no deposition at all
				{
					_sd.Mode = 0; // no deposition
					_sd.Vdep = 0;
					_sd.Vsed = 0;
					_Q.Add(_sd);
				}
				
				else if (mode == 1) // PM25 = concentration, PM10 + PM30 = deposition
				{
					if (V_Dep25 < 0.00000001)
					{
						_sd.Mode = 0; // no deposition
						_sd.Vdep = 0;
						_sd.Vsed = 0;
					}
					else
					{
						_sd.Mode = 1; // concentration + deposition
						_sd.Vdep = V_Dep25;
						_sd.Vsed = V_Sed25;
						Program.DepositionExist = true;
					}
					_Q.Add(_sd); // add the 2.5 source
					
					// Further sources for PM10 & PM30?
					if (Frac_25 > 0)
					{
						emission10 = _sd.ER * (Frac_10 - Frac_25) / Frac_25;
						emission30 = _sd.ER * (Frac_30 - Frac_10) / Frac_25;
					}
					//Console.WriteLine("Emission PM2.5: " + _sd.ER + " /PM10: " + emission10 + " /PM30: " + emission30); // debug
					
					if (emission10 > _sd.ER * 0.000001 && V_Dep10 > 0.00000001) // add a further source for deposition only
					{
						SourceData _sd10 = new SourceData();
						Clone(_sd10);
						_sd10.Mode = 2; // deposition only
						_sd10.ER = originalemission * 0.05F; // 5 % of particles for this depo source
						_sd10.ER_dep = emission10; // real emission rate
						_sd10.Vdep = V_Dep10;
						_sd10.Vsed = V_Sed10;						
						_Q.Add(_sd10); // add the 10 source
					}
					
					if (emission30 > _sd.ER * 0.000001 && V_Dep30 > 0.00000001) // add a further source for deposition only
					{
						SourceData _sd30 = new SourceData();
						Clone(_sd30);
						_sd30.Mode = 2; // deposition only
						_sd30.ER = originalemission * 0.05F; // 5 % of particles for this depo source
						_sd30.ER_dep = emission30; // real emission rate
						_sd30.Vdep = V_Dep30;
						_sd30.Vsed = V_Sed30;						
						_Q.Add(_sd30); // add the 30 source
					}
					
					
				}
				else if (mode == 2) // PM10 + PM25 = concentration, PM30 = deposition
				{
					if (Frac_10 > 0)
					{
						emission25 = _sd.ER * Frac_25 / Frac_10;
						emission10 = _sd.ER - emission25;
						emission30 = _sd.ER * (Frac_30 - Frac_10) / Frac_10;
					}
					
					if (emission10 > 0.0000000000001)
					{
						if (V_Dep10 < 0.00000001)
						{
							_sd.ER = emission10;
							_sd.Mode = 0; // no deposition
							_sd.Vdep = 0;
							_sd.Vsed = 0;
						}
						else
						{
							_sd.ER = emission10;
							_sd.Mode = 1; // concentration + deposition
							_sd.Vdep = V_Dep10;
							_sd.Vsed = V_Sed10;
							Program.DepositionExist = true;
						}
						_Q.Add(_sd); // add the 10 source
					}
					// Further sources for PM10 & PM30?
					
					//Console.WriteLine("Emission PM10: " + _sd.ER + " /PM2.5: " + emission25 + " /PM30: " + emission30); // debug
					
					if (emission25 > _sd.ER * 0.000001) // add a further source for PM2.5
					{
						SourceData _sd25 = new SourceData();
						Clone(_sd25);
						_sd25.Mode = 1; // concentration + deposition
						_sd25.ER = emission25; 
						_sd25.ER_dep = 0; 
						_sd25.Vdep = V_Dep25;
						_sd25.Vsed = V_Sed25;		
						_Q.Add(_sd25); // add the 25 source
						Program.DepositionExist = true;
					}
					
					if (emission30 > _sd.ER * 0.000001 && V_Dep30 > 0.00000001) // add a further source for deposition only
					{
						SourceData _sd30 = new SourceData();
						Clone(_sd30);
						_sd30.Mode = 2; // deposition only
						_sd30.ER = originalemission * 0.05F; // 5 % of particles for this depo source
						_sd30.ER_dep = emission30; // real emission rate
						_sd30.Vdep = V_Dep30;
						_sd30.Vsed = V_Sed30;						
						//Console.WriteLine("Deposition only: EM = " + _sd30.ER_dep);
						_Q.Add(_sd30); // add the 30 source
						Program.DepositionExist = true;
					}
					
				}
			}
			catch
			{
				return false;
			}
			
			return true;
		}
		
		private void Clone(SourceData sd)
		{
			sd.ER	 	= _sd.ER;
			sd.ER_dep 	= _sd.ER_dep;
			sd.X1		= _sd.X1;
			sd.Y1		= _sd.Y1;
			sd.Z1		= _sd.Z1;
			sd.X2		= _sd.X2;
			sd.Y2		= _sd.Y2;
			sd.Z2		= _sd.Z2;
			sd.V		= _sd.V;
			sd.D 		= _sd.D;
			sd.T		= _sd.T;
			sd.SG		= _sd.SG;
			sd.Vsed		= _sd.Vsed;
			sd.Vdep 	= _sd.Vdep;
			sd.Mode 	= _sd.Mode;
			sd.Laerm    = _sd.Laerm;
			sd.Width	= _sd.Width;
			sd.TimeSeriesTemperature = _sd.TimeSeriesTemperature;
			sd.TimeSeriesVelocity = _sd.TimeSeriesVelocity;
		}
		
		private float SedimentationVelocity(double temp, double dm, double dns_mat)
		{
			// temp = absolute temperature [K]
			// dm = diameter of particle [µm]
			// dns_mat = density of particle [kg/m³]
			
			if (dns_mat < 0.0000001) return 0.0f; // density = 0 -> vts = 0;
			
			// computation according to VDI 3782 Blatt 1
			double dns_din = dns_mat / 1000;				// density in [g/cm³]
			double w = 4.991e-5 * dm * dm * dm * dns_din;	// "Bestzahl"
			double v_din = 1000;							// initial value 	
			double nre;
			if (w < 0.003)
				v_din = 0;
			else if (w < 0.24)
			{
				nre = w / 24;								// Reynolds number
				v_din = 1462 * nre / dm / 100;				// vts in [m/s]					
			}
			else 
			{
				w = Math.Log(w);
				nre = Math.Exp(
					-0.318657e1  +
					0.992696 	* Math.Pow(w, 1) -
					0.153193e-2	* Math.Pow(w, 2) -
					0.987059e-3	* Math.Pow(w, 3) -
					0.578878e-3 * Math.Pow(w, 4) +
					0.855176e-4 * Math.Pow(w, 5) -
					0.327815e-5 * Math.Pow(w, 6));
				v_din = 1462 * nre / dm / 100;				// vts in [m/s]
			}
			// computation according to VDI 3782 Blatt 1
			
//			dm = dm / 1000000;                        // diameter in [m]
//			const double CONST_BOLTZ  = 1.38065e-23;  // Boltzmann's constant ~ J/K/molecule
//			const double CONST_AVOGAD  = 6.02214e26;  // Avogadro's number ~ molecules/kmole
//			const double CONST_MWDAIR  = 28.966;      // molecular weight dry air ~ kg/kmole
//			const double CONST_RGAS    = CONST_AVOGAD * CONST_BOLTZ; 	// Universal gas constant ~ J/K/kmole
//			const double CONST_RDAIR   = CONST_RGAS / CONST_MWDAIR; 	// Dry air gas constant     ~ J/K/kg
//			double dns_air = 100000 / (temp * CONST_RDAIR);   			//[kg m-3] const prs_mdp & tpt_vrt
//			double vsc_dyn_atm = 1.72e-5 * Math.Pow((temp/273.0), 1.5)
//				* 393.0 / (temp + 120.0);  								// [kg m-1 s-1]
//			double 	vsc_knm_atm = vsc_dyn_atm / dns_air; 				// [m2 s-1] Kinematic viscosity of air
//
//			// try to solve iterative
//			double vts = dns_mat * dm * dm * 9.81 / (18* vsc_knm_atm);	// [m  s-1] vts in stokes's regime
//			double Re = 0;
//
//			for (int i = 0; i < 3; i++)
//			{
//				Re  = dns_air * vts * dm / vsc_knm_atm;				// Reynolds number
//				//Console.WriteLine(Re);
//				if(Re < 0.5)
//				{
//					// solution already found, stokes law is valid -> do nothing
//				}
//				else
//				{
//					double CdRe = 4 * dns_air * dns_mat * dm * dm * dm *
//						9.81 / (3 * vsc_knm_atm * vsc_knm_atm);
//					if (Re < 4)
//					{
//						vts = vsc_knm_atm / (dns_air * dm) * 
//							(CdRe / 24 -
//							 2.3363e-4 * Math.Pow(CdRe, 2) +
//							 2.0154e-6 * Math.Pow(CdRe, 3) -
//							 6.9105e-9 * Math.Pow(CdRe, 4));
//					}
//					else
//					{
//						double A = -1.29536 +
//							0.986 * Math.Log10(CdRe) -
//							0.046677 * Math.Log10(Math.Pow(CdRe, 2)) -
//							0.0011235 * Math.Log10(Math.Pow(CdRe, 3));
//						vts = vsc_knm_atm / (dns_air * dm) * Math.Pow(10, A);
//					}
//				}
//			} // try solution 3 times because ping pong might occur
			
			return (float)  v_din; 
		}
		
	}
}
