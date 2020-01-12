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
/// Transient Deposition Data Class
/// </summary>
public class TransientDeposition
	{
		public int DepositionMode { get; set; }
		public double Vdep { get; set; }
		public double Vsed { get; set; }

		public TransientDeposition()
		{
			DepositionMode = 0;
			Vdep = 0;
			Vsed = 0;
		}
	}