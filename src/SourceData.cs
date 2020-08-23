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


/// <summary>
/// Source Data Class
/// </summary>
public class SourceData
{
    /// <summary>
    /// Emission rate
    /// </summary>
    public double ER { get; set; }          // emission rate
    /// <summary>
    /// Real emission rate for deposition only sources if mode == 2
    /// </summary>
    public double ER_dep { get; set; }      // real emission rate for deposition only sources if mode == 2
    /// <summary>
    /// X-Coordinate 1
    /// </summary>
    public double X1 { get; set; }          // X-Coordinate 1
    /// <summary>
    /// Y-Coordinate 1
    /// </summary>
    public double Y1 { get; set; }          // Y-Coordinate 1
    /// <summary>
    /// Z-Coordinate 1
    /// </summary>
    public float Z1 { get; set; }           // Z-Coordinate 1
    /// <summary>
    /// X-Coordinate 2 
    /// </summary>
    public double X2 { get; set; }          // X-Coordinate 2
    /// <summary>
    /// Y-Coordinate 2
    /// </summary>
    public double Y2 { get; set; }          // Y-Coordinate 2
    /// <summary>
    /// Z-Coordinate 2
    /// </summary>
    public float Z2 { get; set; }           // Z-Coordinate 2
    /// <summary>
    /// exit velocities of point sources
    /// </summary>
    public float V { get; set; }            // exit velocities of point sources
    /// <summary>
    /// stack diameter of point sources
    /// </summary>
    public float D { get; set; }            // stack diameter of point sources
    /// <summary>
    /// exit temperature of point sources
    /// </summary>
    public float T { get; set; }            // excess temperature of point sources
    /// <summary>
    /// Source group number like used in the GUI and input/output files
    /// </summary>
    public int SG { get; set; }             // Source group
    /// <summary>
    /// sedimentation speed of pollutant
    /// </summary>
    public float Vsed { get; set; }         // sedimentation speed of pollutant
    /// <summary>
    /// deposition speed of pollutant
    /// </summary>
    public float Vdep { get; set; }         // deposition speed of pollutant
    /// <summary>
    /// mode: 0 = concentration only, 1 = conc + dep, 2 =dep only
    /// </summary>
    public byte Mode { get; set; }          // mode: 0 = concentration only, 1 = conc + dep, 2 =dep only
    /// <summary>
    /// Vertical extension of a line source when negative
    /// </summary>
    public float VertExt { get; set; }      // Vertical extension of a line source when negative (used), height of a noise abatement wall if positive (unused)
    /// <summary>
    /// Width of a line source
    /// </summary>
    public float Width { get; set; }        // Breite
    /// <summary>
    /// Reference index to a column in a temperature time series file 
    /// </summary>
    public int TimeSeriesTemperature { get; set; }       // Reference index to a column in a temperature time series file 
    /// <summary>
    /// Reference index to a column in a velocity time series file 
    /// </summary>
    public int TimeSeriesVelocity { get; set; }          // Reference index to a column in a velocity time series file 
}