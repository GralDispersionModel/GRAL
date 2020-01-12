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

/// <summary>
/// Source Data Class
/// </summary>
	public class SourceData
	{
		public double ER {get; set;} 			// emission rate
		public double ER_dep {get; set;} 		// real emission rate for deposition only sources if mode == 2
		public double X1 {get; set;}             // X-Coordinate 1
		public double Y1 {get; set;}             // Y-Coordinate 1
		public float Z1 {get; set;}             // Z-Coordinate 1
		public double X2 {get; set;}             // X-Coordinate 2
		public double Y2 {get; set;}             // Y-Coordinate 2
		public float Z2 {get; set;}             // Z-Coordinate 2
		public float V {get; set;}              // exit velocities of point sources
		public float D {get; set;}              // stack diameter of point sources
		public float T {get; set;}              // excess temperature of point sources
		public int   SG  {get; set;}            // Source group
		public float Vsed {get; set;}           // sedimentation speed of pollutant
		public float Vdep {get; set;}           // deposition speed of pollutant
		public byte  Mode {get; set;}			// mode: 0 = concentration only, 1 = conc + dep, 2 =dep only
		public float Laerm {get; set;}          // L�rmschutzwand
		public float Width {get; set;}          // Breite
		public int   TimeSeriesTemperature {get; set;}       // Reference index to a column in a temperature time series file 
		public int   TimeSeriesVelocity {get; set;}          // Reference index to a column in a velocity time series file 
	}