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

namespace GRAL_2001
{
	class ParticleManagement
	{
		/// <summary>
        /// Calculate the Sum of all Emission Rates 
		/// Assign the particles to the sources
        /// </summary>
		public static int Calculate()
		{
			//total emissions (all sources)
			Program.PS_PartNumb.Initialize(); Program.PS_PartSum = 0;
			Program.LS_PartNumb.Initialize(); Program.LS_PartSum = 0;
			Program.AS_PartNumb.Initialize(); Program.AS_PartSum = 0;
			Program.TS_PartNumb.Initialize(); Program.TS_PartSum = 0;

			double sum_emission = 0.0; // Sum for all sources
			int Sum_of_Particles = 0; // sum for alternative approach
			
			// Calculate sum of all emissions
			for (int i = 1; i <= Program.PS_Count; i++) // Point sources
				sum_emission += Program.PS_ER[i];
			
			for (int i = 1; i <= Program.LS_Count; i++) // Line sources
				sum_emission += Program.LS_ER[i];
			
			for (int i = 1; i <= Program.TS_Count; i++) // Portals
				sum_emission += Program.TS_ER[i];
			
			for (int i = 1; i <= Program.AS_Count; i++) // Area sources
				sum_emission += Program.AS_ER[i];
			
			int PS_Min_Particles = Set_Min_Particles_PS_TS(Program.PS_Count);
			int LS_Min_Particles = Set_Min_Particles_LS(Program.LS_Count);
			int TS_Min_Particles = Set_Min_Particles_PS_TS(Program.TS_Count);
			int AS_Min_Particles = Set_Min_Particles_AS(Program.AS_Count);
			
			//assignment of particles for each source
            double unit = (float)Program.NTEILMAX / sum_emission;

            int sum = 0;
            for (int i = 1; i <= Program.PS_Count; i++)
            {
            	Program.PS_PartNumb[i] = Convert.ToInt32(unit * Program.PS_ER[i]);
				
            	if (Program.PS_PartNumb[i] < PS_Min_Particles)
            		Program.PS_PartNumb[i] = PS_Min_Particles;
            	
				Program.PS_PartSum += Program.PS_PartNumb[i];
            	sum += Program.PS_PartNumb[i];
            	Sum_of_Particles += Program.PS_PartNumb[i];

              	if (Program.LogLevel == 2) // Show all particle-numbers in the case of Log-Level 02
				{
              		Console.WriteLine("PS " + i.ToString() + " : " + Math.Abs(Program.PS_PartNumb[i]).ToString());
				}
        	}

            for (int i = 1; i <= Program.LS_Count; i++)
            {
            	Program.LS_PartNumb[i] = Convert.ToInt32(unit * Program.LS_ER[i]);

            	if (Program.LS_PartNumb[i] < LS_Min_Particles) // avoid Null particle sources
            		Program.LS_PartNumb[i] = LS_Min_Particles; 
            	
				Program.LS_PartSum += Program.LS_PartNumb[i];
            	sum += Program.LS_PartNumb[i];
            	Sum_of_Particles += Program.LS_PartNumb[i];

            	if (Program.LogLevel == 2) // Show all particle-numbers in the case of Log-Level 02
				{
					Console.WriteLine("LS " + i.ToString() + " : " + Math.Abs(Program.LS_PartNumb[i]).ToString());
				}
            }

            for (int i = 1; i <= Program.TS_Count; i++)
            {
            	Program.TS_PartNumb[i] = Convert.ToInt32(unit * Program.TS_ER[i]);
            	
            	if (Program.TS_PartNumb[i] < TS_Min_Particles)
            		Program.TS_PartNumb[i] = TS_Min_Particles;

				Program.TS_PartSum += Program.TS_PartNumb[i];
            	sum += Program.TS_PartNumb[i];
            	Sum_of_Particles += Program.TS_PartNumb[i];

            	if (Program.LogLevel == 2) // Show all particle-numbers in the case of Log-Level 02
				{
            		Console.WriteLine("TS " + i.ToString() + " : " + Math.Abs(Program.TS_PartNumb[i]).ToString());
				}
            }

            for (int i = 1; i <= Program.AS_Count; i++)
            {
            	Program.AS_PartNumb[i] = Convert.ToInt32(unit * Program.AS_ER[i]);
            	
            	if (Program.AS_PartNumb[i] < AS_Min_Particles) // avoid Null particle sources
            		Program.AS_PartNumb[i] = AS_Min_Particles;

				Program.AS_PartSum += Program.AS_PartNumb[i];
            	sum += Program.AS_PartNumb[i];
            	Sum_of_Particles += Program.AS_PartNumb[i];

            	if (Program.LogLevel == 2) // Show all particle-numbers in the case of Log-Level 02
				{
            		Console.WriteLine("AS " + i.ToString() + " : " + Math.Abs(Program.AS_PartNumb[i]).ToString());
				}
            }
           
            Program.ParticleMassMean = sum_emission / 3600 / Program.TPS * 1000000000	/ Program.GridVolume / Program.TAUS;

            string err = "Using a total of " + Sum_of_Particles.ToString() + " particles (" + Math.Round((Sum_of_Particles / Program.TAUS), 0).ToString() + " part./s) within the model domain";
            Console.WriteLine(err);
            ProgramWriters.LogfileGralCoreWrite(err);
            ProgramWriters.LogfileGralCoreWrite("");
            
			// Resize Arrays 
            Program.ParticleSource = new Int32[Sum_of_Particles + 1];
            Program.ParticleSG = new byte[Sum_of_Particles + 1];
            Program.Xcoord = new double[Sum_of_Particles + 1];
            Program.YCoord = new double[Sum_of_Particles + 1];
            Program.ZCoord = new float[Sum_of_Particles + 1];
            Program.ParticleMass = new double[Sum_of_Particles + 1];
            Program.SourceType = new byte[Sum_of_Particles+ 1];
            Program.ParticleVsed = new float[Sum_of_Particles + 1];                         // sedimentation velocity of one lagrangian particle
            Program.ParticleVdep = new float[Sum_of_Particles + 1];                         // deposition velocity of one lagrangian particle
            Program.ParticleMode = new byte[Sum_of_Particles + 1];
            
            Console.WriteLine();
            
            return Sum_of_Particles;
		}
		
		private static int Set_Min_Particles_PS_TS(int source_count)
		{
			int Min_Particles = 10;
			if (source_count < 2000)
			{
				Min_Particles = 20;
			}
			else if (source_count > 30000)
			{
				Min_Particles = 5;
			}
			
			return Min_Particles;
		}
		
		private static int Set_Min_Particles_LS(int source_count)
		{
			int Min_Particles = 5;
			if (source_count < 2000)
			{
				Min_Particles = 16;
			}
			else if (source_count < 10000)
			{
				Min_Particles = 8;
			}
			return Min_Particles;
		}
		
		private static int Set_Min_Particles_AS(int source_count)
		{
			int Min_Particles = 2;
			if (source_count < 2000)
			{
				Min_Particles = 6;
			}
			else if (source_count < 10000)
			{
				Min_Particles = 4;
			}
			
			return Min_Particles;
		}
		
	}
}
