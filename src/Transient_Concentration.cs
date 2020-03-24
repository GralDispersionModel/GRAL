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

using System.Runtime.CompilerServices;

namespace GRAL_2001
{
    class TransientConcentration
    {
        /// <summary>
	    ///Store the particle concentration in the transient grid
	    /// </summary>
        public void Conz5dZeitschleife(int reflexion_flag, float zcoord_nteil, float AHint, double masse, double Area_cart, float idt, double xsi, double eta, int SG_nteil)
        {
            if (reflexion_flag == 0)
            {
                int IndexK3d = BinarySearchTransient(zcoord_nteil - AHint);
                int IndexI3d = (int)(xsi / Program.DXK) + 1;
                int IndexJ3d = (int)(eta / Program.DYK) + 1;

                float[] conz5d_L = Program.Conz5d[IndexI3d][IndexJ3d][IndexK3d];
                lock (conz5d_L)
                {
                    conz5d_L[SG_nteil] += (float)(masse * Program.GridVolume * Program.TAUS / (Area_cart * Program.DZK_Trans[IndexK3d]));
                }
            }
        }

        public void Conz5dZeitschleifeTransient(int reflexion_flag, float zcoord_nteil, float AHint, double mass_real, double Area_cart, float idt, double xsi, double eta, int SG_nteil)
        {
            if (reflexion_flag == 0)
            {
                int IndexK3d = BinarySearchTransient(zcoord_nteil - AHint);
                int IndexI3d = (int)(xsi / Program.DXK) + 1;
                int IndexJ3d = (int)(eta / Program.DYK) + 1;

                float[] conz5d_L = Program.Conz5d[IndexI3d][IndexJ3d][IndexK3d];
                lock (conz5d_L)
                {
                    conz5d_L[SG_nteil] += (float)(mass_real * Program.TAUS / (Area_cart * Program.DZK_Trans[IndexK3d]));
                }
            }
        }

        /// <summary>
    	/// Find the index of a value in HOKART_Trans[] that exceeds the value Height - 19.10.05 Ku
    	/// </summary>
    	/// <param name="Height">Height to compare</param> 
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static int BinarySearchTransient(float Height)
        {
            int low = 1;
            int high = Program.NKK_Transient;
            float[] ho = Program.HoKartTrans;

            int aQuarter = high >> 3;
            if (ho[aQuarter] >= Height)
            {
                high = aQuarter;
            }
            else
            {
                low = aQuarter;
            }
            int mid = 0;
            while (low <= high)
            {
                mid = (low + high) >> 1;
                if (ho[mid] >= Height)
                {
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }
            if (low > Program.NKK_Transient)
            {
                low = Program.NKK_Transient;
            }
            return low;
        }
    }
}
