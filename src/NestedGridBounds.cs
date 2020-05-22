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
    ///Min Max structure for integer values
    ///</summary>
    public struct GridBounds
    {
        public IntPoint Min;
        public IntPoint Max;

        public GridBounds(IntPoint min, IntPoint max)
        {
            Min = min;
            Max = max;
        }
        public GridBounds(int minX, int minY, int maxX, int maxY)
        {
            Min = new IntPoint(minX, minY);
            Max = new IntPoint(maxX, maxY);
        }

        public override bool Equals(object obj)
        {
            return obj is GridBounds && this == (GridBounds)obj;
        }
        public override int GetHashCode()
        {
            return Min.X.GetHashCode() ^ Min.Y.GetHashCode() ^ Max.X.GetHashCode() ^ Max.Y.GetHashCode();
        }
        public static bool operator ==(GridBounds a, GridBounds b)
        {
            return a.Min == b.Min && a.Max == b.Max;
        }
        public static bool operator !=(GridBounds a, GridBounds b)
        {
            return !(a == b);
        }
    }
}