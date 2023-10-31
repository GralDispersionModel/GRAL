#region Copyright
///<remarks>
/// <Graz Lagrangian Particle Dispersion Model>
/// Copyright (C) [2023]  [Markus Kuntner]
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
    partial class Program
    {
        /// <summary>
        /// Clean the memory when the app has been finished or interrupted
        /// </summary>
        public static void CleanUpMemory()
        {
            DZK = null;
            DZK_Trans = null;
            HOKART = null;
            HoKartTrans = null;
            if (StretchFlexible != null)
            {
                StretchFlexible.Clear();
            }
            StretchFlexible = null;
            Conz3d = null;
            MeasurementHeight = null;
            XKO = null;
            YKO = null;
            ZKO = null;
            AH = null;
            AHE = null;
            ZSP = null;
            DDX = null;
            DDY = null;
            UWIN = null;
            VWIN = null;
            WWIN = null;
            Z0Gral = null;
            USternGral = null;
            OLGral = null;
            Z0Gramm = null;
            Ustern = null;
            Ob = null;
            SC_Gral = null;
            AKL_GRAMM = null;

            ObsWindU = null;
            ObsWindV = null;
            U0 = null;
            V0 = null;
            UK = null;
            VK = null;
            WK = null;
            AHK = null;
            AHKOri = null;
            KKART = null;
            ADVDOM = null;
            VEG = null;
            COV = null;
            ReceptorX = null;
            ReceptorY = null;
            ReceptorZ = null;
            ReceptorNearbyBuilding = null;
            if (ReceptorName != null)
            {
                ReceptorName.Clear();
            }
            ReceptorName = null;
            ReceptorIInd = null;
            ReceptorJInd = null;
            ReceptorKInd = null;
            ReceptorIIndFF = null;
            ReceptorJIndFF = null;
            ReceptorKIndFF = null;
            ReceptorConc = null;
            ReceptorParticleMaxConc = null;
            ReceptorTotalConc = null;
            
            OPP_LANE = null;
            TUN_ENTR = null;

            PS_SG = null;
            PS_Mode = null;

            PS_V_sed = null;
            PS_V_Dep = null;
            PS_ER_Dep = null;
            PS_Absolute_Height = null;
            PS_TimeSeriesTemperature = null;
            PS_TimeSeriesVelocity = null;
            if (PS_TimeSerTempValues != null)
            {
                PS_TimeSerTempValues.Clear();
            }
            PS_TimeSerTempValues = null;
            if (PS_TimeSerVelValues != null)
            {
                PS_TimeSerVelValues.Clear();
            }
            PS_TimeSerVelValues = null;

            LS_ER = null;
            LS_X1 = null;
            LS_X2 = null;
            LS_Y1 = null;
            LS_Y2 = null;
            LS_Z1 = null;
            LS_Z2 = null;
            LS_Laerm = null;
            LS_Width = null;
            LS_SG = null;
            LS_Mode = null;
            LS_V_sed = null;
            LS_V_Dep = null;
            LS_ER_Dep = null;
            LS_Absolute_Height = null;

            TS_ER = null;
            TS_T = null;
            TS_V = null;
            TS_X1 = null;
            TS_Y1 = null;
            TS_Z1 = null;
            TS_X2 = null;
            TS_Y2 = null;
            TS_Z2 = null;
            TS_SG = null;
            TS_Area = null;
            TS_Width = null;
            TS_Height = null;
            TS_cosalpha = null;
            TS_sinalpha = null;
            TS_Mode = null;
            TS_V_sed = null;
            TS_V_Dep = null;
            TS_ER_Dep = null;
            TS_Absolute_Height = null;
            TS_TimeSeriesTemperature = null;
            TS_TimeSeriesVelocity = null;
            if (TS_TimeSerTempValues != null)
            {
                TS_TimeSerTempValues.Clear();
            }
            TS_TimeSerTempValues = null;
            if (TS_TimeSerVelValues != null)
            {
                TS_TimeSerVelValues.Clear();
            }
            TS_TimeSerVelValues = null;

            AS_X = null;
            AS_Y = null;
            AS_Z = null;
            AS_dX = null;
            AS_dY = null;
            AS_dZ = null;
            AS_ER = null;
            AS_SG = null;
            AS_Mode = null;
            AS_V_sed = null;
            AS_V_Dep = null;
            AS_ER_Dep = null;
            AS_Absolute_Height = null;

            ParticleSource = null;
            SourceType = null;
            ParticleSG = null;
            Xcoord = null;
            YCoord = null;
            ZCoord = null;
            ParticleMass = null;
            ParticleVsed = null;
            ParticleVdep = null;
            ParticleMode = null;

            DIV = null;
            DPM = null;
            UKS = null;
            VKS = null;
            WKS = null;
            DPMNEW = null;
            VerticalIndex = null;
            UsternObstaclesHelpterm = null;
            UsternTerrainHelpterm = null;

            PS_PartNumb = null;
            TS_PartNumb = null;
            LS_PartNumb = null;
            AS_PartNumb = null;

            Conz3dp = null;
            Conz3dm = null;
            Q_cv0 = null;
            DisConcVar = null;
            Depo_conz = null;
            Conz4d = null;
            Conz5d = null;
            ConzSsum = null;
            EmFacTimeSeries = null;

            if (MeteoTimeSer != null)
            {
                MeteoTimeSer.Clear();
            }
            MeteoTimeSer = null;
            if (MeteopgtLst != null)
            {
                MeteopgtLst.Clear();
            }
            MeteopgtLst = null;

            TransientDepo = null;
            if (WetDepoPrecipLst != null)
            {
                WetDepoPrecipLst.Clear();
            }
            
            // Release the memory
            System.GC.Collect(System.GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            System.GC.WaitForPendingFinalizers();
        }
    }
}