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
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;

/// <summary>
/// Thin wrapper for a terrain following array class
/// </summary>
public class TerrainArray
{
    private float[] arr;
    private readonly int Delta;
    
    /// <summary>
    /// Init a terrain following array
    /// </summary>
    /// <param name="lenght">Lenght of the array starting at the terrain</param>
    /// <param name="start">Index of the vertical flow field grid where the terrain starts</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TerrainArray(int lenght, int start)
    {
        arr = new float[lenght];
        Delta = start;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get 
        {
            index -= Delta; 
            if (index >= 0 && index < arr.Length) //.Net11+ should remove bound checks after this check
            {
                return arr[index];
            }
            else
            {
                return 0;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            index -= Delta; 
            if (index >= 0 && index < arr.Length)
            {
                arr[index] = value;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Vector<float> GetVector(int index)
    {
        index -= Delta;
        if (index >= 0 && index < arr.Length - Vector<float>.Count)
        {
            return new Vector<float>(arr, index);
        }
        else if (index < -Vector<float>.Count)
        {
            return Vector<float>.Zero;
        }
        else // index < 0 > - Vector<float>.lenght
        {
            Span<float> VectArray = stackalloc float [Vector<float>.Count];
            for (int _c = 0; _c < Vector<float>.Count + index; ++_c)
            {
                VectArray[_c - index]  = arr[_c];
            }
            return new Vector<float>(VectArray);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void SetVector(Vector<float> v, int index)
    {
        index -= Delta;
        if (index >= 0 && index < arr.Length - Vector<float>.Count)
        {
            v.CopyTo(arr, index);
        }
        else if (index < -(Vector<float>.Count))
        {
            // do nothing in this case
        }
        else // index < 0 > - Vector<float>.lenght
        {
            Span<float> VectArray = stackalloc float [Vector<float>.Count];
            v.CopyTo(VectArray);
            for (int _c = 0; _c < Vector<float>.Count + index; ++_c)
            {
                arr[_c] = VectArray[_c - index];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Vector512<float> GetVector512(int index)
    {
        index -= Delta;
        if (index >= 0 && index < arr.Length - Vector512<float>.Count)
        {
            return Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(arr), (uint) index);
        }
        else if (index < -Vector<float>.Count)
        {
            return Vector512<float>.Zero;
        }
        else // index < 0 > - Vector<float>.lenght
        {
            Span<float> VectArray = stackalloc float [Vector512<float>.Count];
            for (int _c = 0; _c < Vector512<float>.Count + index; ++_c)
            {
                VectArray[_c - index]  = arr[_c];
            }
            return Vector512.Create(VectArray);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void SetVector512(Vector512<float> v, int index)
    {
        index -= Delta;
        if (index >= 0 && index < arr.Length - Vector512<float>.Count)
        {
            v.CopyTo(arr, index);
        }
        else if (index < -(Vector<float>.Count))
        {
            // do nothing in this case
        }
        else // index < 0 > - Vector<float>.lenght
        {
            Span<float> VectArray = stackalloc float [Vector512<float>.Count];
            v.CopyTo(VectArray);
            for (int _c = 0; _c < Vector512<float>.Count + index; ++_c)
            {
                arr[_c] = VectArray[_c - index];
            }
        }
    }

}