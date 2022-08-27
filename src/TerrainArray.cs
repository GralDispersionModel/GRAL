#region Copyright
///<remarks>
/// <Graz Lagrangian Particle Dispersion Model>
/// Copyright (C) [2022]  [Markus Kuntner]
/// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
/// the Free Software Foundation version 3 of the License
/// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
/// You should have received a copy of the GNU General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.
///</remarks>
#endregion

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

/// <summary>
/// Thin wrapper for a terrain following array class
/// </summary>
public class TerrainArray
{
    private float[] arr;
    private readonly int Delta;
    private float[] VectArray =new float[Vector<float>.Count];

    /// <summary>
    /// Init a terrain following array
    /// </summary>
    /// <param name="lenght">Lenght of the array starting at the terrain</param>
    /// <param name="nkk">Total lenght of the array starting at the bottom of the domain</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TerrainArray(int lenght, int nkk)
    {
        arr = new float[lenght];
        Delta = arr.Length - nkk;
    }

    // Enable validation of input
    public int Length => arr.Length;

    // Indexer declaration.
    // If index is out of range, the temps array will throw the exception.
    // <summary>
    /// Get or set a value of the array
    /// </summary>
    /// <param name="index">Absolute index starting at the bottom of the domain</param>
    public float this[int index]
    {
        get 
        {
            index += Delta; 
            if (index < 0)
            {
                return 0;
            }
            else if (index < arr.Length)
            {
                return arr[index];
            }
            else
            {
                return 0;
            }
        }
        set
        {
            index += Delta; 
            if (index >= 0 && index < arr.Length)
            {
                arr[index] = value;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Vector<float> GetVector(int index)
    {
        index += Delta;
        if (index >= 0)
        {
            return new Vector<float>(arr, index);
        }
        else if (index < -(Vector<float>.Count))
        {
            return Vector<float>.Zero;
        }
        else
        {
            Array.Clear(VectArray);
            int _off = Math.Abs(index);
            for (int _c = 0; _c < index + Vector<float>.Count; _c++)
            {
                VectArray[_off + _c] = arr[_c];
            }
            return new Vector<float>(VectArray);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void SetVector(Vector<float> v, int index)
    {
        index += Delta;
        if (index >= 0)
        {
            v.CopyTo(arr, index);
        }
        else if (index < -(Vector<float>.Count))
        {
            // do nothing in this case
        }
        else
        {
            v.CopyTo(VectArray);
            int _off = Math.Abs(index);
            for (int _c = 0; _c < index + Vector<float>.Count; _c++)
            {
                arr[_c] = VectArray[_off + _c];
            }
        }
    }
}