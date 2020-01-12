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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace GRAL_2001
{
    partial class Zeitschleife
    {
    	[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IntStandCalculate(int nteil, int IUstern, int JUstern, float DiffBuilding, float windge, float sigmauhurly, ref float U0int, ref float V0int)
        {
            if ((Program.IStatistics == 0) || (Program.IStatistics == 3))
            {
                if (DiffBuilding <= Program.MeasurementHeight[1])
                {
                    U0int = Program.U0[1];
                    V0int = Program.V0[1];
                }
                else if (DiffBuilding >= Program.MeasurementHeight[Program.MetProfileNumb])
                {
                    U0int = Program.U0[Program.MetProfileNumb];
                    V0int = Program.V0[Program.MetProfileNumb];
                }
                else
                {
                    int ipo = 0;
                    for (int iprof = 1; iprof <= Program.MetProfileNumb; iprof++)
                    {
                        if (DiffBuilding > Program.MeasurementHeight[iprof])
                            ipo = iprof + 1;
                    }
                    float help = 1 / (Program.MeasurementHeight[ipo] - Program.MeasurementHeight[ipo - 1]) * (DiffBuilding - Program.MeasurementHeight[ipo - 1]);
                    U0int = Program.U0[ipo - 1] + (Program.U0[ipo] - Program.U0[ipo - 1]) * help;
                    V0int = Program.V0[ipo - 1] + (Program.V0[ipo] - Program.V0[ipo - 1]) * help;
                }
            }
            else
            {
                U0int = windge * (0.2F * MathF.Pow(windge, -0.9F) + 0.32F * Program.Z0Gramm[IUstern][JUstern] + 0.18F);
                U0int = Program.FloatMax(U0int, 0.3F) * Program.StdDeviationV;
                V0int = U0int;
            }

            //enhancing horizontal diffusion for point sources (Hurley, 2005)
            if(Program.SourceType[nteil] == 0)
            {
                //U0int += (float)Math.Sqrt(Program.Pow2(sigmauhurly));
                //V0int += (float)Math.Sqrt(Program.Pow2(sigmauhurly));
                U0int += Math.Abs(sigmauhurly);
                V0int += Math.Abs(sigmauhurly);
            }
        }

    }
}
