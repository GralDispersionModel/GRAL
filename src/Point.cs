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

namespace GRAL_2001
{
	///<summary>
	///Point structure for integer values
	///</summary>
	public struct IntPoint
	{
		public int X;
		public int Y;

		public IntPoint(int x, int y) 
		{
			X = x;
			Y = y;
		}
		
		public override bool Equals(object obj) 
		{
			return obj is IntPoint && this == (IntPoint)obj;
		}
		public override int GetHashCode() 
		{
			return X.GetHashCode() ^ Y.GetHashCode();
		}
		public static bool operator ==(IntPoint a, IntPoint b) 
		{
			return a.X == b.X && a.Y == b.Y;
		}
		public static bool operator !=(IntPoint a, IntPoint b) 
		{
			return !(a == b);
		}
	}

}