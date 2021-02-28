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
using System.Threading.Tasks;

namespace GRAL_2001
{
    class StartCoordinates
    {
        /// <summary>
        /// Calculate the start coordinates of all particles at a random point within the source geometries.
        /// Calculates the "mass" of each particle, depending on the emission rate of the source and the number of particles per source.
        /// Calculates the average deposition settings for each source group in the transient mode (used for the transient particles).
        /// </summary>
        public static void Calculate()
        {
            /*
             * 1) coordinates for source 1
             * 2) coordinates for source 2
             * 3) etc.
             */

            for (int i = 1; i <= Program.TS_Count; i++)
            {
                Program.TS_Width[i] = (float)Math.Max(0.1F,
                    Math.Sqrt(Math.Pow(Program.TS_X1[i] - Program.TS_X2[i], 2) + Math.Pow(Program.TS_Y1[i] - Program.TS_Y2[i], 2)));
                Program.TS_Height[i] = (float)Math.Max(0.1F,
                    (Math.Max(Program.TS_Z1[i], Program.TS_Z2[i]) - Math.Min(Program.TS_Z1[i], Program.TS_Z2[i])));
                Program.TS_cosalpha[i] = (float)((Program.TS_Y2[i] - Program.TS_Y1[i]) / Program.TS_Width[i]);
                Program.TS_sinalpha[i] = (float)((Program.TS_X1[i] - Program.TS_X2[i]) / Program.TS_Width[i]);
            }

            double Volume_Time_Unit = 1000000000 / Program.GridVolume / 3600;
            //SimpleRNG.SetSeed((uint)Environment.TickCount);

            Parallel.For(1, Program.NTEILMAX + 1, Program.pOptions, nteil =>
            {
                int seed = 0;
                unchecked
                {
                    seed = nteil * Environment.TickCount;
                }
                Random Rng = new Random(seed);

                //random number generator seeds
                double zuff1 = Rng.NextDouble();

                float AHint = 0;

                double sumanz = 0;
                Program.ParticleSource[nteil] = 0; // marker if particle is not used

                int caseswitch = 0; // Point Sources

                if (nteil > (Program.PS_PartSum + Program.TS_PartSum + Program.LS_PartSum)) // >Line -> area sources
                {
                    caseswitch = 3;
                }
                else if (nteil > (Program.PS_PartSum + Program.TS_PartSum)) // > Portal -> Line sources
                {
                    caseswitch = 2;
                }
                else if (nteil > Program.PS_PartSum) // > Point Sources -> Portal
                {
                    caseswitch = 1;
                }

                int i = 0; // number of actual source

                switch (caseswitch)
                {
                    //particle coordinates of point sources
                    case 0:
                        {
                            AHint = 0;
                            for (int j = 1; j <= Program.PS_Count; j++)
                            {
                                sumanz += Program.PS_PartNumb[j];
                                if (nteil <= sumanz) // found the source
                                {
                                    i = j;
                                    break;
                                }
                            }

                            if (i > 0)
                            {
                                zuff1 = Rng.NextDouble();

                                double radius = Program.PS_D[i] * 0.5 * zuff1;

                                zuff1 = Rng.NextDouble();

                                double theta = 6.28 * zuff1;
                                Program.Xcoord[nteil] = Program.PS_X[i] + radius * Math.Cos(theta);
                                Program.YCoord[nteil] = Program.PS_Y[i] + radius * Math.Sin(theta);
                                Program.ZCoord[nteil] = Program.PS_effqu[i];

                                //in complex terrain z-coordinate is placed onto actual model height
                                double xsi = Program.Xcoord[nteil] - Program.IKOOAGRAL;
                                double eta = Program.YCoord[nteil] - Program.JKOOAGRAL;
                                if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                                { }
                                else
                                {
                                    Program.ParticleSource[nteil] = i; // number of source
                                    Program.SourceType[nteil] = 0; // Point Source
                                    Program.ParticleSG[nteil] = Program.PS_SG[i];
                                    Program.ParticleMode[nteil] = Program.PS_Mode[i]; // deposition mode

                                    //Concentration only _______________________________________________
                                    if (Program.PS_Mode[i] == 0)  // concentration, no deposition
                                    {
                                        Program.ParticleMass[nteil] = Program.PS_ER[i] / Program.PS_PartNumb[i] * Volume_Time_Unit;

                                        Program.ParticleVdep[nteil] = 0;
                                        Program.ParticleVsed[nteil] = 0;
                                    }
                                    //Concentration + Deposition _______________________________________
                                    else if (Program.PS_Mode[i] == 1) // concentration and deposition
                                    {
                                        Program.ParticleMass[nteil] = Program.PS_ER[i] / Program.PS_PartNumb[i] * Volume_Time_Unit;

                                        Program.ParticleVdep[nteil] = Program.PS_V_Dep[i];
                                        Program.ParticleVsed[nteil] = Program.PS_V_sed[i];
                                    }
                                    //Deposition only ___________________________________________________
                                    else if (Program.PS_Mode[i] == 2) // deposition only
                                    {
                                        Program.ParticleMass[nteil] = Program.PS_ER_Dep[i] / Program.PS_PartNumb[i] * 1000000000 / Program.GridVolume / Program.TAUS; // Paricle mass for deposition depending to the particle number of this source
                                                                                                                                                                      //										Console.WriteLine(Program.Part_Mass[nteil] + " / " + Program.PS_PartNumb[i] + " / " + Program.PS_ER_Dep[i] + " / TAUS " + Program.TAUS + "/ dV " + Program.dV);
                                        Program.ParticleVdep[nteil] = Program.PS_V_Dep[i];
                                        Program.ParticleVsed[nteil] = Program.PS_V_sed[i];
                                    }
                                }

                                if ((Program.Topo == 1) && (Program.BuildingsExist == true))
                                {
                                    int IndexI = (int)(xsi / Program.DXK) + 1;
                                    int IndexJ = (int)(eta / Program.DYK) + 1;
                                    Program.ZCoord[nteil] = Program.PS_effqu[i] - Program.CUTK[IndexI][IndexJ];
                                    AHint = Program.AHK[IndexI][IndexJ];
                                }
                                else if ((Program.Topo == 0) && (Program.BuildingsExist == true))
                                {
                                    int IndexI = (int)(xsi / Program.DXK) + 1;
                                    int IndexJ = (int)(eta / Program.DYK) + 1;
                                    if (Program.ZCoord[nteil] <= Program.HOKART[Program.KKART[IndexI][IndexJ]])
                                    {
                                        Program.ZCoord[nteil] = Program.HOKART[Program.KKART[IndexI][IndexJ]] + 0.1F;
                                    }

                                    AHint = Program.AHK[IndexI][IndexJ];
                                }
                                if (Program.ZCoord[nteil] <= AHint)
                                {
                                    Program.ZCoord[nteil] = AHint + 0.1F;
                                }
                            }
                            else
                            { }
                            break;
                        }

                    //particle coordinates of portal sources
                    case 1:
                        {
                            sumanz = Program.PS_PartSum;
                            AHint = 0;
                            for (int j = 1; j <= Program.TS_Count; j++)
                            {
                                sumanz += Program.TS_PartNumb[j];
                                if (nteil <= sumanz) // found the source
                                {
                                    i = j;
                                    break;
                                }
                            }
                            if (i > 0)
                            {
                                zuff1 = Rng.NextDouble();

                                double zahl1 = Program.TS_Height[i] * zuff1;

                                zuff1 = Rng.NextDouble();

                                double zahl2 = Program.TS_Width[i] * zuff1;

                                Program.ZCoord[nteil] = (float)(Math.Min(Program.TS_Z1[i], Program.TS_Z2[i]) + zahl1);
                                Program.ParticleSource[nteil] = i;
                                Program.SourceType[nteil] = 1; // Portals
                                Program.ParticleSG[nteil] = Program.TS_SG[i];

                                Program.ParticleMode[nteil] = Program.TS_Mode[i]; // deposition mode

                                //Concentration only _______________________________________________
                                if (Program.TS_Mode[i] == 0)  // concentration, no deposition
                                {
                                    Program.ParticleMass[nteil] = Program.TS_ER[i] / Program.TS_PartNumb[i] * Volume_Time_Unit;

                                    Program.ParticleVdep[nteil] = 0;
                                    Program.ParticleVsed[nteil] = 0;
                                }
                                //Concentration + Deposition _______________________________________
                                else if (Program.TS_Mode[i] == 1) // concentration and deposition
                                {
                                    Program.ParticleMass[nteil] = Program.TS_ER[i] / Program.TS_PartNumb[i] * Volume_Time_Unit;

                                    Program.ParticleVdep[nteil] = Program.TS_V_Dep[i];
                                    Program.ParticleVsed[nteil] = Program.TS_V_sed[i];
                                }
                                //Deposition only ___________________________________________________
                                else if (Program.TS_Mode[i] == 2) // deposition only
                                {
                                    Program.ParticleMass[nteil] = Program.TS_ER_Dep[i] / Program.TS_PartNumb[i] * 1000000000 / Program.GridVolume / Program.TAUS; // Paricle mass for deposition depending to the particle number of this source
                                    Program.ParticleVdep[nteil] = Program.TS_V_Dep[i];
                                    Program.ParticleVsed[nteil] = Program.TS_V_sed[i];
                                }

                                double x1 = -zahl2 * Program.TS_sinalpha[i];
                                double y1 = zahl2 * Program.TS_cosalpha[i];
                                double xb = Program.TS_Width[i] * 0.5 * Program.TS_sinalpha[i];
                                double yb = Program.TS_Width[i] * 0.5 * Program.TS_cosalpha[i];
                                double xv = 0.5 * (Program.TS_X1[i] + Program.TS_X2[i]) + xb;
                                double yv = 0.5 * (Program.TS_Y1[i] + Program.TS_Y2[i]) - yb;
                                Program.Xcoord[nteil] = xv + x1;
                                Program.YCoord[nteil] = yv + y1;

                                //in complex terrain z-coordinate is placed onto actual model height
                                double xsi = Program.Xcoord[nteil] - Program.IKOOAGRAL;
                                double eta = Program.YCoord[nteil] - Program.JKOOAGRAL;
                                double xsi1 = Program.Xcoord[nteil] - Program.GrammWest;
                                double eta1 = Program.YCoord[nteil] - Program.GrammSouth;
                                if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                                { }
                                else
                                {
                                    if (Program.Topo == 1)
                                    {
                                        int IndexI = (int)(xsi / Program.DXK) + 1;
                                        int IndexJ = (int)(eta / Program.DYK) + 1;
                                        AHint = Program.AHK[IndexI][IndexJ];
                                    }

                                    // input = absolute height?
                                    if (Program.TS_Absolute_Height[i])
                                    {
                                        if (Program.ZCoord[nteil] < AHint) // if absolute height < surface
                                        {
                                            Program.ZCoord[nteil] = AHint + 0.1F;
                                        }
                                    }
                                    else
                                    {
                                        Program.ZCoord[nteil] += AHint + 0.1F; // compute abs. height
                                    }

                                    if ((Program.Topo == 0) && (Program.BuildingsExist == true))
                                    {
                                        int IndexI = (int)(xsi / Program.DXK) + 1;
                                        int IndexJ = (int)(eta / Program.DYK) + 1;
                                        if (Program.ZCoord[nteil] <= Program.HOKART[Program.KKART[IndexI][IndexJ]])
                                        {
                                            Program.ZCoord[nteil] = Program.HOKART[Program.KKART[IndexI][IndexJ]] + 0.1F;
                                        }

                                        AHint = Program.AHK[IndexI][IndexJ];
                                    }

                                }
                            }
                            else
                            { }
                            break;
                        }

                    //particle coordinates of line sources
                    case 2:
                        {
                            sumanz = Program.PS_PartSum + Program.TS_PartSum;
                            AHint = 0;
                            for (int j = 1; j <= Program.LS_Count; j++)
                            {
                                sumanz += Program.LS_PartNumb[j];
                                if (nteil <= sumanz) // found the source
                                {
                                    i = j;
                                    break;
                                }
                            }
                            if (i > 0)
                            {
                                Program.ParticleSource[nteil] = i;
                                Program.ParticleSG[nteil] = Program.LS_SG[i];
                                Program.SourceType[nteil] = 2; // Line Source
                                Program.ParticleMode[nteil] = Program.LS_Mode[i]; // deposition mode

                                //Concentration only _______________________________________________
                                if (Program.LS_Mode[i] == 0)  // concentration, no deposition
                                {
                                    Program.ParticleMass[nteil] = Program.LS_ER[i] / Program.LS_PartNumb[i] * Volume_Time_Unit;

                                    Program.ParticleVdep[nteil] = 0;
                                    Program.ParticleVsed[nteil] = 0;
                                }
                                //Concentration + Deposition _______________________________________
                                else if (Program.LS_Mode[i] == 1) // concentration and deposition
                                {
                                    Program.ParticleMass[nteil] = Program.LS_ER[i] / Program.LS_PartNumb[i] * Volume_Time_Unit;

                                    Program.ParticleVdep[nteil] = Program.LS_V_Dep[i];
                                    Program.ParticleVsed[nteil] = Program.LS_V_sed[i];
                                }
                                //Deposition only ___________________________________________________
                                else if (Program.LS_Mode[i] == 2) // deposition only
                                {
                                    Program.ParticleMass[nteil] = Program.LS_ER_Dep[i] / Program.LS_PartNumb[i] * 1000000000 / Program.GridVolume / Program.TAUS; // Particle mass for deposition depending to the particle number of this source
                                    Program.ParticleVdep[nteil] = Program.LS_V_Dep[i];
                                    Program.ParticleVsed[nteil] = Program.LS_V_sed[i];
                                }

                                double lang = Math.Sqrt(Math.Pow(Program.LS_X2[i] - Program.LS_X1[i], 2) +
                                                        Math.Pow(Program.LS_Y2[i] - Program.LS_Y1[i], 2));
                                double zzz = 0;   // z value
                                double zahl1 = 0; // vertical mixing height

                                if (lang > 0.1)
                                {
                                    // default case: horizontal line source 
                                    zuff1 = Rng.NextDouble();

                                    double zahl0 = lang * zuff1;
                                    //noise abatement wall +1m
                                    if (Program.LS_Laerm[i] > 0)
                                    {
                                        zuff1 = Rng.NextDouble();

                                        zahl1 = Program.LS_Laerm[i] + zuff1;
                                    }
                                    //user defined volume for initial mixing of particles (traffic induced turbulence)
                                    else if (Program.LS_Laerm[i] < 0)
                                    {
                                        zuff1 = Rng.NextDouble();
                                        zahl1 = zuff1 * Math.Abs(Program.LS_Laerm[i]);
                                    }
                                    //standard initial mixing is up to 3m
                                    else if (Program.LS_Laerm[i] == 0)
                                    {
                                        zuff1 = Rng.NextDouble();
                                        zahl1 = 3 * zuff1;
                                    }
                                    zuff1 = Rng.NextDouble();

                                    double zahl2 = Program.LS_Width[i] * zuff1;

                                    //rotation of coordinate system
                                    double alpha = (Program.LS_X2[i] - Program.LS_X1[i]) / lang;
                                    double beta = (Program.LS_Y2[i] - Program.LS_Y1[i]) / lang;
                                    double x1 = zahl0 * alpha - zahl2 * beta;
                                    double y1 = zahl0 * beta + zahl2 * alpha;

                                    //transformation of coordinate system
                                    double xb = Program.LS_Width[i] * 0.5 * beta;
                                    double yb = Program.LS_Width[i] * 0.5 * alpha;
                                    double xv = Program.LS_X1[i] + xb;
                                    double yv = Program.LS_Y1[i] - yb;
                                    Program.Xcoord[nteil] = xv + x1;
                                    Program.YCoord[nteil] = yv + y1;
                                    zzz = Program.LS_Z1[i] + (Program.LS_Z2[i] - Program.LS_Z1[i]) / lang * zahl0;
                                }
                                else
                                {
                                    // special case: vertical line source
                                    float _vertExt = Program.LS_Laerm[i];
                                    if (_vertExt < 0)
                                    {
                                        // use vertical extension as radius
                                        Program.Xcoord[nteil] = Program.LS_X1[i] + (1 - Rng.NextDouble() * 2) * _vertExt; // (-1 to 1) * VertExt
                                        Program.YCoord[nteil] = Program.LS_Y1[i] + (1 - Rng.NextDouble() * 2) * _vertExt; // (-1 to 1) * VertExt
                                    }
                                    else
                                    {
                                        // vertical Line source without radius
                                        Program.Xcoord[nteil] = Program.LS_X1[i];
                                        Program.YCoord[nteil] = Program.LS_Y1[i];
                                    }
                                    zuff1 = Rng.NextDouble();
                                    zzz = Program.LS_Z1[i] + (Program.LS_Z2[i] - Program.LS_Z1[i]) * zuff1;
                                }

                                //in complex terrain z-coordinate is placed onto actual model height
                                double xsi = Program.Xcoord[nteil] - Program.IKOOAGRAL;
                                double eta = Program.YCoord[nteil] - Program.JKOOAGRAL;
                                double xsi1 = Program.Xcoord[nteil] - Program.GrammWest;
                                double eta1 = Program.YCoord[nteil] - Program.GrammSouth;
                                if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                                { }
                                else
                                {
                                    if (Program.Topo == 1)
                                    {
                                        int IndexI = (int)(xsi / Program.DXK) + 1;
                                        int IndexJ = (int)(eta / Program.DYK) + 1;
                                        AHint = Program.AHK[IndexI][IndexJ];
                                    }

                                    // input = absolute height? - do nothing -otherwise compute abs. height
                                    if (Program.LS_Absolute_Height[i] == false)
                                    {
                                        zzz += AHint; // compute absolute height 
                                    }

                                    if (zzz <= (AHint + 0.01F)) // set source to the surface
                                    {
                                        zzz = AHint + 0.01F;
                                    }

                                    if ((Program.Topo == 0) && (Program.BuildingsExist == true))
                                    {
                                        int IndexI = (int)(xsi / Program.DXK) + 1;
                                        int IndexJ = (int)(eta / Program.DYK) + 1;
                                        if (zzz <= Program.HOKART[Program.KKART[IndexI][IndexJ]])
                                        {
                                            zzz = Program.HOKART[Program.KKART[IndexI][IndexJ]] + 0.1F;
                                        }

                                        AHint = Program.AHK[IndexI][IndexJ];
                                    }

                                    Program.ZCoord[nteil] = (float)(zzz + zahl1) + 0.1F;
                                    //Console.WriteLine(i.ToString() + "/" +Program.Part_Mass[nteil].ToString() +"/" + Program.xcoord[nteil].ToString() +"/" + Program.ycoord[nteil].ToString() +"/" +Program.zcoord[nteil].ToString());
                                }
                            }
                            else
                            { }
                            break;
                        }

                    //particle coordinates of area sources
                    case 3:
                        {
                            sumanz = Program.PS_PartSum + Program.TS_PartSum + Program.LS_PartSum;
                            AHint = 0;
                            for (int j = 1; j <= Program.AS_Count; j++)
                            {
                                sumanz += Program.AS_PartNumb[j];
                                if (nteil <= sumanz) // found the source
                                {
                                    i = j;
                                    break;
                                }
                            }
                            if (i > 0)
                            {
                                Program.ParticleSource[nteil] = i;
                                Program.ParticleSG[nteil] = Program.AS_SG[i];
                                Program.SourceType[nteil] = 3; // Area Source
                                Program.ParticleMode[nteil] = Program.AS_Mode[i]; // deposition mode

                                //Concentration only _______________________________________________
                                if (Program.AS_Mode[i] == 0)  // concentration, no deposition
                                {
                                    Program.ParticleMass[nteil] = Program.AS_ER[i] / Program.AS_PartNumb[i] * Volume_Time_Unit;

                                    Program.ParticleVdep[nteil] = 0;
                                    Program.ParticleVsed[nteil] = 0;
                                }
                                //Concentration + Deposition _______________________________________
                                else if (Program.AS_Mode[i] == 1) // concentration and deposition
                                {
                                    Program.ParticleMass[nteil] = Program.AS_ER[i] / Program.AS_PartNumb[i] * Volume_Time_Unit;

                                    Program.ParticleVdep[nteil] = Program.AS_V_Dep[i];
                                    Program.ParticleVsed[nteil] = Program.AS_V_sed[i];
                                }
                                //Deposition only ___________________________________________________
                                else if (Program.AS_Mode[i] == 2) // deposition only
                                {
                                    Program.ParticleMass[nteil] = Program.AS_ER_Dep[i] / Program.AS_PartNumb[i] * 1000000000 / Program.GridVolume / Program.TAUS; // Paricle mass for deposition depending to the particle number of this source
                                    Program.ParticleVdep[nteil] = Program.AS_V_Dep[i];
                                    Program.ParticleVsed[nteil] = Program.AS_V_sed[i];
                                }

                                zuff1 = Rng.NextDouble();

                                double zahl0 = Program.AS_dX[i] * zuff1;

                                zuff1 = Rng.NextDouble();

                                double zahl1 = Program.AS_dZ[i] * zuff1;

                                zuff1 = Rng.NextDouble();
                                double zahl2 = Program.AS_dY[i] * zuff1;

                                Program.Xcoord[nteil] = Program.AS_X[i] - Program.AS_dX[i] * 0.5F + zahl0;
                                Program.YCoord[nteil] = Program.AS_Y[i] - Program.AS_dY[i] * 0.5F + zahl2;
                                double zzz = Program.AS_Z[i] - Program.AS_dZ[i] * 0.5F;

                                //in complex terrain z-coordinate is placed onto actual model height
                                double xsi = Program.Xcoord[nteil] - Program.IKOOAGRAL;
                                double eta = Program.YCoord[nteil] - Program.JKOOAGRAL;
                                double xsi1 = Program.Xcoord[nteil] - Program.GrammWest;
                                double eta1 = Program.YCoord[nteil] - Program.GrammSouth;
                                if ((eta <= Program.EtaMinGral) || (xsi <= Program.XsiMinGral) || (eta >= Program.EtaMaxGral) || (xsi >= Program.XsiMaxGral))
                                { }
                                else
                                {
                                    int IndexI = 1;
                                    int IndexJ = 1;
                                    if (Program.Topo == 1)
                                    {
                                        IndexI = (int)(xsi / Program.DXK) + 1;
                                        IndexJ = (int)(eta / Program.DYK) + 1;
                                        AHint = Program.AHK[IndexI][IndexJ];
                                    }

                                    // input = absolute height? -> do nothing, otherwise compute absolute height
                                    if (Program.AS_Absolute_Height[i] == false)
                                    {
                                        zzz += AHint;
                                    }

                                    if ((Program.Topo == 1) && (Program.BuildingsExist == true))
                                    {
                                        zzz -= Program.CUTK[IndexI][IndexJ];
                                    }

                                    if (zzz <= AHint)
                                    {
                                        zzz = AHint + 0.01F;
                                    }

                                    if ((Program.Topo == 0) && (Program.BuildingsExist == true))
                                    {
                                        IndexI = (int)(xsi / Program.DXK) + 1;
                                        IndexJ = (int)(eta / Program.DYK) + 1;
                                        if (zzz <= Program.HOKART[Program.KKART[IndexI][IndexJ]])
                                        {
                                            zzz = Program.HOKART[Program.KKART[IndexI][IndexJ]] + 0.1F;
                                        }

                                        AHint = Program.AHK[IndexI][IndexJ];
                                    }

                                    Program.ZCoord[nteil] = (float)(zzz + zahl1) + 0.1F;
                                }
                            }
                            else
                            { }
                            break;
                        }
                }
                Rng = null;
            });

            // Transient Mode: calculate average deposition settings for each source group one times (if Transient_Depo == null)
            if (Program.ISTATIONAER == 0 && Program.TransientDepo == null)
            {
                Program.TransientDepo = new TransientDeposition[Program.SourceGroups.Count];
                for (int i = 0; i < Program.SourceGroups.Count; i++)
                {
                    Program.TransientDepo[i] = new TransientDeposition();
                }

                int[] mode = new int[Program.SourceGroups.Count];
                int[] counter = new int[Program.SourceGroups.Count];
                double[] vsed = new double[Program.SourceGroups.Count];
                double[] vdep = new double[Program.SourceGroups.Count];

                // loop over all particles
                for (int nteil = 1; nteil < Program.NTEILMAX + 1; nteil++)
                {
                    if (Program.ParticleMode[nteil] < 2) // no deposition weighting if depo only
                    {
                        int SG = Program.ParticleSG[nteil]; // real SG number of particle
                        int SG_index = Program.SourceGroups.IndexOf(Program.ParticleSG[nteil]); // internal source group number 
                        mode[SG_index] = Math.Max(mode[SG_index], Program.ParticleMode[nteil]); // set average mode to 1 if 1 particle has a deposition
                        vdep[SG_index] += Program.ParticleVdep[nteil];
                        vsed[SG_index] += Program.ParticleVsed[nteil];
                        ++counter[SG_index];
                    }
                }

                // Set average deposition values for each source group
                for (int i = 0; i < Program.SourceGroups.Count; i++)
                {
                    Program.TransientDepo[i].DepositionMode = 0;
                    if (counter[i] > 0)
                    {
                        Program.TransientDepo[i].Vdep = vdep[i] / counter[i];
                        Program.TransientDepo[i].Vsed = vsed[i] / counter[i];
                        Program.TransientDepo[i].DepositionMode = mode[i];
                    }
                }
            } // Transient Mode: average depo settings

        }
    }
}
