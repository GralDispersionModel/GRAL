#region Copyright
///<remarks>
/// <Graz Lagrangian Particle Dispersion Model>
/// Copyright (C) [2020]  [Markus Kuntner]
/// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
/// the Free Software Foundation version 3 of the License
/// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
/// You should have received a copy of the GNU General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.
///</remarks>
#endregion

using System;
using System.Threading.Tasks;

namespace GRAL_2001
{
    public partial class ProgramReaders
    {
        /// <summary>
        /// Check if parts of a prognostic sub domain are far away from sources
        /// </summary>
        public void RemovePrognosticSubDomainsFarDistanceToSources()
        {
            // Apply the algorithm?
            if (Program.SubDomainDistance < 10000)
            {
                string Info = "Remove prognostic sub domains located more than " + Math.Round(Program.SubDomainDistance).ToString() + " m away from sources: ";
                Console.Write(Info);
                ProgramWriters.LogfileGralCoreWrite(Info);
                int count = 0;
                //for (int i = 1; i <= Program.NII; i++)
                Parallel.For(1, Program.NII + 1, (i, state) =>
                {
                    //last source type closer than distance to a cell
                    int sourcetype = -1;
                    //last  source number closer than distance to a cell
                    int sourcenumber = 0;
                    bool isCloser = false;

                    System.Threading.Interlocked.Increment(ref count);
                    if (count % 100 == 0)
                    {
                        Console.Write(".");
                    }
                    for (int j = 1; j <= Program.NJJ; j++)
                    {
                        if (Program.ADVDOM[i][j] > 0)
                        {
                            // reset the prognostic sub domain if the distance between cell and next source is large
                            (isCloser, sourcetype, sourcenumber) = IsCellCloseToASource(i, j, Program.SubDomainDistance, sourcetype, sourcenumber);
                            if (!isCloser)
                            {
                                Program.ADVDOM[i][j] = 0;
                            }
                        }
                    }
                });
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Check if a point is closer than "Distance" to a source
        /// </summary>
        /// <param name="X">Cell index in horizontal direction</param>
        /// <param name="Y">Cell index in vertical direction</param>
        /// <param name="Distance">Max distance to a source</param>
        /// <param name="SourceType>"Last fitting source type</param>
        /// <param name="SourceNumber>"Last fitting source number</param>
        private (bool, int, int) IsCellCloseToASource(int X, int Y, double Distance, int SourceType, int SourceNumber)
        {
            int inside = 0;      
            double x = (X - 1) * Program.DXK + Program.GralWest;
            double y = (Y - 1) * Program.DYK + Program.GralSouth;
            
            //check last fitting source -> improve performance
            if (SourceType == 1) // Point source
            {
                int i = SourceNumber;
                float dx = (float)(Program.PS_X[i] - x);
                //filter large delta x
                if (dx < Distance)
                {
                    float dy = (float)(Program.PS_Y[i] - y);
                    //filter large delta y
                    if (dy < Distance)
                    {
                        //Console.Write(Math.Round(dist) + " ");
                        if (MathF.Sqrt(dx * dx + dy * dy) < Distance)
                        {
                            System.Threading.Interlocked.Increment(ref inside);
                        }
                    }
                }
            }
            else if (SourceType == 2) // Portal source
            {
                int i = SourceNumber;
                float dx = (float)(Program.TS_X1[i] - x);
                //filter large delta x
                if (dx < Distance)
                {
                    float dy = (float)(Program.TS_Y1[i] - y);
                    //filter large delta y
                    if (dy < Distance)
                    {
                        //Console.Write(Math.Round(dist) + " ");
                        if (MathF.Sqrt(dx * dx + dy * dy) < Distance)
                        {
                            System.Threading.Interlocked.Increment(ref inside);
                        }
                    }
                }
            }
            else if (SourceType == 3) // line source
            {
                int i = SourceNumber;
                float dx = (float)(Math.Min(Math.Min(Math.Min(Program.LS_X1[i] - x, Program.LS_X2[i] - x), Program.LS_Y1[i] - y), Program.LS_Y2[i] - y));
                //filter large delta x/y
                if (dx < 1000) // one line segment should not exceed 1000 m
                {
                    if (DistanceLineToPoint(Program.LS_X1[i], Program.LS_Y1[i], Program.LS_X2[i], Program.LS_Y2[i], x, y) < Distance)
                    {
                        System.Threading.Interlocked.Increment(ref inside);
                    }
                }
            }
            else if (SourceType == 4) // area source
            {
                int i = SourceNumber;
                float dx = (float)(Program.AS_X[i] - x);
                //filter large delta x
                if (dx < Distance)
                {
                    float dy = (float)(Program.AS_Y[i] - y);
                    //filter large delta y
                    if (dy < Distance)
                    {
                        if (MathF.Sqrt(dx * dx + dy * dy) < Distance)
                        {
                            System.Threading.Interlocked.Increment(ref inside);
                        }
                    }
                }
            }

            //check distance to all source types - all coordinates of items are relative coordinates
            // Check Point Sources
            if (inside == 0)
            {
                //Parallel.For(1, Program.PS_Count + 1, (i, state) =>
                for (int i = 1; i <= Program.PS_Count; i++)
                {
                    float dx = (float)(Program.PS_X[i] - x);
                    //filter large delta x
                    if (dx < Distance)
                    {
                        float dy = (float)(Program.PS_Y[i] - y);
                        //filter large delta y
                        if (dy < Distance)
                        {
                            //Console.Write(Math.Round(dist) + " ");
                            if (MathF.Sqrt(dx * dx + dy * dy) < Distance)
                            {
                                System.Threading.Interlocked.Increment(ref inside);
                                //state.Break();
                                SourceNumber = i;
                                SourceType = 1;
                                i = Program.PS_Count + 2;                      
                            }
                        }
                    }
                }//);
            }

            // Check Portal Sources
            if (inside == 0)
            {
                for (int i = 1; i <= Program.TS_Count; i++)
                //Parallel.For(1, Program.TS_Count + 1, (i, state) =>
                {
                    float dx = (float)(Math.Min(Math.Min(Math.Min(Program.TS_X1[i] - x, Program.TS_X2[i] - x), Program.TS_Y1[i] - y), Program.TS_Y2[i] - y));
                    //filter large delta x/y
                    if (dx < 2 * Distance)
                    {
                        if (DistanceLineToPoint(Program.TS_X1[i], Program.TS_Y1[i], Program.TS_X2[i], Program.TS_Y2[i], x, y) < Distance)
                        {
                            System.Threading.Interlocked.Increment(ref inside);
                            //state.Break();
                            SourceNumber = i;
                            SourceType = 2;
                            i = Program.TS_Count + 2;
                        }
                    }
                }//);
            }

            // Check Line Sources
            if (inside == 0)
            {
                for (int i = 1; i <= Program.LS_Count; i++)
                //Parallel.For(1, Program.LS_Count + 1, (i, state) =>
                {
                    float dx = (float)(Math.Min(Math.Min(Math.Min(Program.LS_X1[i] - x, Program.LS_X2[i] - x), Program.LS_Y1[i] - y), Program.LS_Y2[i] - y));
                    //filter large delta x/y
                    if (dx < 1000) // one line segment should not exceed 1000 m
                    {
                        if (DistanceLineToPoint(Program.LS_X1[i], Program.LS_Y1[i], Program.LS_X2[i], Program.LS_Y2[i], x, y) < Distance)
                        {
                            System.Threading.Interlocked.Increment(ref inside);
                            //state.Break();
                            SourceNumber = i;
                            SourceType = 3;
                            i = Program.LS_Count + 2;
                        }
                    }
                }//);
            }

            //Check Area Sources
            if (inside == 0)
            {
                for (int i = 1; i <= Program.AS_Count; i++)
                //Parallel.For(1, Program.AS_Count + 1, (i, state) =>
                {
                    float dx = (float)(Program.AS_X[i] - x);
                    //filter large delta x
                    if (dx < Distance)
                    {
                        float dy = (float)(Program.AS_Y[i] - y);
                        //filter large delta y
                        if (dy < Distance)
                        {
                            if (MathF.Sqrt(dx * dx + dy * dy) < Distance)
                            {
                                System.Threading.Interlocked.Increment(ref inside);
                                //state.Break();
                                SourceNumber = i;
                                SourceType = 4;
                                i = Program.AS_Count + 2;
                            }
                        }
                    }
                }//);
            }
            
            if (inside == 0)
            {
                return (false, -1, 0);
            }
            else
            {
                return (true, SourceType, SourceNumber);
            }
        }

        /// <summary>
        /// Calculate the nearest distance between a line and a point
        /// </summary>
        private static double DistanceLineToPoint(double X1, double Y1, double X2,
                             double Y2, double X, double Y)
        {
            float x1 = (float) X1;
            float y1 = (float) Y1;
            float x2 = (float) X2;
            float y2 = (float) Y2;
            float x = (float) X;
            float y  = (float) Y;

            float dx2 = x - x2;
            float dy2 = y - y2;
            float dx12 = x1 - x2;
            float dy12 = y1 - y2;

            if (dx12 * dx2 + dy12 * dy2 <= 0)
            {
                return MathF.Sqrt(dx2 * dx2  + dy2 * dy2);
            }

            float dx1  = x - x1;
            float dy1  = y - y1;
            float dy21 = y2 - y1;
            float dx21 = x2 - x1;
            if (dx21 * dx1 + dy21 * dy1 <= 0)
            {
                return MathF.Sqrt(dx1 * dx1 + dy1 * dy1);
            }

            return MathF.Abs(x * dy21 - y * dx21 + x2 * y1 - y2 * x1) / MathF.Sqrt(dx12 * dx12 + dy12 * dy12);
        }
    }
}