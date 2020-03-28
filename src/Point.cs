using System;

namespace GRAL_2001
{
    ///<summary>
    ///Point structure for Integer values
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