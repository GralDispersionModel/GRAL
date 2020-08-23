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

namespace FilterArray
{
    /// <summary>
    /// This class provides functions for filtering and outlining of objects in 2 dimensional arrays  
    /// </summary>
    public class ArrayFilter
    {
        /// <summary>
        /// Find outlines of objects 
        /// </summary>
        /// <param name="source">Array with object heights</param>
        /// <returns>Array with object outlines</returns>
        public float[][] FindOutline(float[][] source)
        {
            // create result array
            float[][] result = new float[source.Length][];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = new float[source[i].Length];
            }

            //find vertical edges
            for (int i = 0; i < source.Length; i++)
            {
                float val = 0;
                for (int j = 1; j < source[i].Length - 1; j++)
                {
                    if (MathF.Abs(val - source[i][j]) > float.Epsilon)
                    {
                        val = source[i][j];
                        result[i][j] = val;
                    }
                    if (val > 0 && MathF.Abs(val - source[i][j + 1]) > float.Epsilon)
                    {
                        result[i][j] = source[i][j];
                        val = source[i][j + 1];
                    }
                }
            }

            //find horizontal edges
            for (int j = 0; j < source[0].Length; j++)
            {
                float val = 0;
                for (int i = 1; i < source.Length - 1; i++)
                {
                    if (MathF.Abs(val - source[i][j]) > float.Epsilon)
                    {
                        val = source[i][j];
                        result[i][j] = val;
                    }
                    if (val > 0 && MathF.Abs(val - source[i + 1][j]) > float.Epsilon)
                    {
                        result[i][j] = source[i][j];
                        val = source[i][j + 1];
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 2 dimensional gaussian low pass filter
        /// </summary>
        /// <param name="Source">Source array</param>
        /// <param name="Dx">Delta x in m</param>
        /// <param name="Sigma">Gaussian sigma in m</param>
        /// <param name="NormalizeMax">Normalize max to this value, no normalization when this param = 0</param>
        /// <returns>Filtered array</returns>
        public float[][] LowPassGaussian(float[][] Source, float Dx, float Sigma, float NormalizeMax)
        {
            // create result array
            float[][] result = new float[Source.Length][];
            for (int i = 0; i < Source.Length; i++)
            {
                result[i] = new float[Source[i].Length];
            }

            //Valid params? 
            if (Dx > 0 && Sigma > 0)
            {
                double weightingFactor = 0;

                // create weighting square with odd lenght
                int gaussRectLenght = Math.Max(5, (int)(Sigma * 2 / Dx));
                if (gaussRectLenght % 2 == 0)
                {
                    gaussRectLenght++;
                }
                int gaussRectMid = (int)(gaussRectLenght / 2);
                float sigma2 = Sigma * Sigma;
                float[,] gaussRect = new float[gaussRectLenght, gaussRectLenght];
                for (int i = 0; i < gaussRectLenght; i++)
                {
                    for (int j = 0; j < gaussRectLenght; j++)
                    {
                        float delta2 = MathF.Pow((gaussRectMid - i) * Dx, 2) + MathF.Pow((gaussRectMid - j) * Dx, 2);
                        gaussRect[i, j] = 1 / (2 * MathF.PI * sigma2) * MathF.Exp(-delta2 / (2 * sigma2));
                        weightingFactor += gaussRect[i, j];
                    }
                }

                //Apply low pass filter
                float maxResult = 0;
                for (int i = 0; i < Source.Length; i++)
                {
                    for (int j = 0; j < Source[i].Length; j++)
                    {
                        double sum = 0;
                        for (int ib = 0; ib < gaussRectLenght; ib++)
                        {
                            for (int jb = 0; jb < gaussRectLenght; jb++)
                            {
                                //use border values if weighting square is outside the array bounds
                                int xval = Math.Min(Source.Length - 2, Math.Max(1, i + ib - gaussRectMid));
                                int yval = Math.Min(Source[i].Length - 2, Math.Max(1, j + jb - gaussRectMid));
                                sum += 1 / weightingFactor * gaussRect[ib, jb] * Source[xval][yval];
                            }
                        }
                        result[i][j] = (float)sum;
                        maxResult = Math.Max(maxResult, result[i][j]);
                    }
                }

                //Normalize the result
                if (NormalizeMax > 0 && maxResult > 0)
                {
                    float factor = NormalizeMax / maxResult;
                    for (int i = 0; i < Source.Length; i++)
                    {
                        for (int j = 0; j < Source[i].Length; j++)
                        {
                            result[i][j] *= factor;
                        }
                    }
                }
            }
            return result;
        }
    }
}
