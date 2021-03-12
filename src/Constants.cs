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
using System.Runtime.CompilerServices;

namespace GRAL_2001
{
    class Consts
    {
        ///<summary>
        /// Stationary GRAL Mode 
        ///</summary>
        public const int StationaryMode = 1;
        ///<summary>
        /// Transient GRAL Mode 
        ///</summary>
        public const int TransientMode = 0;
        ///<summary>
        /// Point Source
        ///</summary>
        public const int SourceTypePoint = 0;
        ///<summary>
        /// Line Source
        ///</summary>
        public const int SourceTypeLine = 2;
        ///<summary>
        /// Area Source
        ///</summary>
        public const int SourceTypeArea = 3;
        ///<summary>
        /// Portal Source
        ///</summary>
        public const int SourceTypePortal = 1;
        ///<summary>
        /// Particle is a Portal Source
        ///</summary>
        public const int ParticleIsAPortal = 0;
        ///<summary>
        /// Particle is not a Portal Source
        ///</summary>
        public const int ParticleIsNotAPortal = 1;
        ///<summary>
        /// Particle reflected on a building or the surface
        ///</summary>
        public const int ParticleReflected = 1;
        ///<summary>
        /// Particle not reflected on a building or the surface
        ///</summary>
        public const int ParticleNotReflected = 0;
        ///<summary>
        /// Terrain is available
        ///</summary>
        public const int TerrainAvailable = 1;
        ///<summary>
        /// Flat terrain
        ///</summary>
        public const int TerrainFlat = 0;
        ///<summary>
        /// Meteo Input ZR friction velocity, Obukhov length, boundary-layer height, standard deviation of horizontal wind fluctuations, u- and v-wind components
        ///</summary>
        public const int MeteoZR = 0;
        ///<summary>
        /// Meteo Input All - not supported
        ///</summary>
        public const int MeteoAll = 1;
        ///<summary>
        /// Meteo Input EKI friction velocity, Obukhov length, standard deviation of horizontal wind fluctuations, u- and v-wind components
        ///</summary>
        public const int MeteoEKI = 2;
        ///<summary>
        /// Meteo Input Sonic velocity, direction, friction velocity, standard deviation of horizontal and vertical wind fluctuations, Obukhov length, meandering parameters
        ///</summary>
        public const int MeteoSonic = 3;
        ///<summary>
        /// Meteo Input Meteopgt.all wind speed, wind direction, stability class
        ///</summary>
        public const int MeteoPgtAll = 4;
        ///<summary>
        /// Flow Field No Buildings
        ///</summary>
        public const int FlowFieldNoBuildings = 0;
        ///<summary>
        /// Flow Field Diagnostic Approach
        ///</summary>
        public const int FlowFieldDiag = 1;
        ///<summary>
        /// Flow Field Prognostic Approach
        ///</summary>
        public const int FlowFieldProg = 2;
        ///<summary>
        /// Still available weather situations
        ///</summary>
        public const int CalculationRunning = 0;
        ///<summary>
        /// No more available weather situations
        ///</summary>
        public const int CalculationFinished = 1;
        ///<summary>
        /// No deposition
        ///</summary>
        public const int DepoOff = 0;
        ///<summary>
        /// Deposition and Concentration
        ///</summary>
        public const int DepoAndConc = 1;
        ///<summary>
        /// Deposition only
        ///</summary>
        public const int DepoOnly = 2;
        ///<summary>
        /// Logging Off
        ///</summary>
        public const int LogLevelOff = 0;
        ///<summary>
        /// Logging Reflections and Particles
        ///</summary>
        public const int LogLevelRef = 1;
        ///<summary>
        /// Logging Reflections, Particles and Particle Numbers
        ///</summary>
        public const int LogLevelRefPart = 2;
    }
}