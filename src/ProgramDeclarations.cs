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
using System.Threading.Tasks;
namespace GRAL_2001
{
    partial class Program
    {
        //global variables declaration block
        //note that arrays are defined as jagged-arrays as they have a much better computational performance than matrices

        ///<summary>
        ///Sets the maximum number of cores to be used in the parallelisation
        /// </summary>
        public static ParallelOptions pOptions = new ParallelOptions();
        ///<summary>
        ///Global decimal separator of the system
        /// </summary>
        public static string Decsep;
        ///<summary>
        ///Flag to take orography into account - 0: no terrain 1:complex terrain
        ///</summary>
        public static int Topo = 0;
        ///<summary>
        ///Header in *con files to mark new file format for the GUI
        ///</summary>
        public static int ResultFileHeader = -1;
        ///<summary>
        ///Wind speed at anemometer height in [m/s]
        ///</summary>
        public static float WindVelGral = -99;
        ///<summary>
        ///Wind direction at anemometer height in [degrees]
        ///</summary>
        public static float WindDirGral = -99;
        ///<summary>
        ///Stability class (1=extremly convective,...,7=extremly stable)
        ///</summary>
        public static int StabClass = 0;
        ///<summary>
        ///Mean GRAMM wind speed at anemometer height in [m/s]
        ///</summary>
        public static float WindVelGramm = -99;
        ///<summary>
        ///Mean GRAMM wind direction at anemometer height in [degrees]
        ///</summary>
        public static float WindDirGramm = -99;
        ///<summary>
        ///Mean GRAMM stability class (1=extremly convective,...,7=extremly stable)
        ///</summary>
        public static int StabClassGramm = 0;
        ///<summary>
        ///Flag, determining which output format for *con files is used above 0: soundplan below and equal to 0: GRAL GUI formats
        ///</summary>
        public static int IOUTPUT = 0;
        ///<summary>
        ///Number of cells in vertical direction used for the prognostic wind field calculations
        ///</summary>
        public static int VertCellsFF = 40;
        ///<summary>
        ///Number of used source groups
        ///</summary>
        public static List<int> SourceGroups = new List<int>();
        ///<summary>
        ///Maximum number of vertical cells in the flow field grid
        ///</summary>
        public const int VerticalCellMaxBound = 2048;
        ///<summary>
        ///Vertical grid size (height dependent)
        ///</summary>
        public static float[] DZK = new float[VerticalCellMaxBound];
        ///<summary>
        ///Vertical grid size (height dependent) for transient computation
        ///</summary>
        public static float[] DZK_Trans = new float[VerticalCellMaxBound];
        ///<summary>
        ///Height above minimum model elevation of the GRAL grid for the flow field simulations
        ///</summary>
        public static float[] HOKART = new float[VerticalCellMaxBound];
        ///<summary>
        ///Height above topography of the transient concentration grid
        ///</summary>
        public static float[] HoKartTrans = new float[VerticalCellMaxBound];
        ///<summary>
        ///Number of processors used for parallel computation
        ///</summary>
        public static int IPROC = 1;
        ///<summary>
        ///Horizontal grid size for the flow field computation in x-direction
        ///</summary>
        public static float DXK = 1;
        ///<summary>
        ///Horizontal grid size for the flow field computation in y-direction
        ///</summary>
        public static float DYK = 1;
        ///<summary>
        ///Vertical stretching factor for the flow field grid in z-direction
        ///</summary>
        public static float StretchFF = 1;
        ///<summary>
        ///Vertical stretching factor for the flow field grid in z-direction
        ///</summary>
        public static List<float[]> StretchFlexible = new List<float[]>();
        ///<summary>
        ///Number of cells in x-direction of the concentration grid
        ///</summary>
        public static int NXL = 1;
        ///<summary>
        ///Number of cells in y-direction of the concentration grid
        ///</summary>
        public static int NYL = 1;
        ///<summary>
        ///Number of horizontal concentration result grids
        ///</summary>
        public static int NS = 1;
        ///<summary>
        ///Western border of the GRAL domain relative coordinates
        ///</summary>
        public static double XsiMinGral = 0;
        ///<summary>
        ///Eastern border of the GRAL domain relative coordinates
        ///</summary>
        public static double XsiMaxGral = 0;
        ///<summary>
        ///Southern border of the GRAL domain relative coordinates
        ///</summary>
        public static double EtaMinGral = 0;
        ///<summary>
        ///Northern border of the GRAL domain relative coordinates
        ///</summary>
        public static double EtaMaxGral = 0;
        ///<summary>
        ///Western border of the GRAL domain real world coordinates
        ///</summary>
        public static double GralWest = 0;
        ///<summary>
        ///Eastern border of the GRAL domain real world coordinates
        ///</summary>
        public static double GralEast = 0;
        ///<summary>
        ///Southern border of the GRAL domain real world coordinates
        ///</summary>
        public static double GralSouth = 0;
        ///<summary>
        ///Northern border of the GRAL domain real world coordinates
        ///</summary>
        public static double GralNorth = 0;
        ///<summary>
        ///Number of cells in x-direction of the GRAMM grid
        ///</summary>
        public static int NX = 1;
        ///<summary>
        ///Number of cells in y-direction of the GRAMM grid
        ///</summary>
        public static int NY = 1;
        ///<summary>
        ///Number of cells in z-direction of the GRAMM grid
        ///</summary>
        public static int NZ = 3;
        ///<summary>
        ///Number of cells in x-direction of the GRAMM grid as read from ggeom.asc
        ///</summary>
        public static int NI = 1;
        ///<summary>
        ///Number of cells in y-direction of the GRAMM grid as read from ggeom.asc
        ///</summary>
        public static int NJ = 1;
        ///<summary>
        ///Number of cells in z-direction of the GRAMM grid as read from ggeom.asc
        ///</summary>
        public static int NK = 1;
        ///<summary>
        ///Western border of the GRAMM domain (optional)
        ///</summary>
        public static double XsiMinGramm = 0;
        ///<summary>
        ///Eastern border of the GRAMM domain (optional)
        ///</summary>
        public static double XsiMaxGramm = 0;
        ///<summary>
        ///Southern border of the GRAMM domain (optional)
        ///</summary>
        public static double EtaMinGramm = 0;
        ///<summary>
        ///Northern border of the GRAMM domain (optional)
        ///</summary>
        public static double EtaMaxGramm = 0;
        ///<summary>
        ///Number of GRAMM-cells in x-direction +1
        ///</summary>
        public static int NX1;
        ///<summary>
        ///Number of GRAMM-cells in y-direction +1
        ///</summary>
        public static int NY1;
        ///<summary>
        ///Number of GRAMM-cells in z-direction +1
        ///</summary>
        public static int NZ1;
        ///<summary>
        ///Number of GRAMM-cells in x-direction +2
        ///</summary>
        public static int NX2;
        ///<summary>
        ///Number of GRAMM-cells in y-direction +2
        ///</summary>
        public static int NY2;
        ///<summary>
        ///Number of GRAMM-cells in z-direction +2
        ///</summary>
        public static int NZ2;
        ///<summary>
        ///Horizontal grid-size of the GRAL concentration grid in x-direction
        ///</summary>
        public static float GralDx = 1;
        ///<summary>
        ///Horizontal grid-size of the GRAL concentration grid in y-direction
        ///</summary>
        public static float GralDy = 1;
        ///<summary>
        ///Horizontal grid-size of the GRAL concentration grid in z-direction
        ///</summary>
        public static float GralDz = 1;
        ///<summary>
        ///Number of dispersion situation from which onward the simulation will be launched
        ///</summary>
        public static int IWETstart = 1;
        ///<summary>
        ///Number of cells near the walls of obstacles for which a boundary layer is computed in the diagnostic flow-field approach
        ///</summary>
        public static int IGEB = 1;
        ///<summary>
        ///2D concentration separated into source groups and horizontal slices
        ///</summary>
        public static float[][][][] Conz3d = CreateArray<float[][][]>(1, () => CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1])));
        ///<summary>
        ///Height of u,v measurements
        ///</summary>
        public static float[] MeasurementHeight = new float[100];
        ///<summary>
        ///Distance of GRAMM grid cell centre from the eastern domain border
        ///</summary>
        public static double[] XKO = new double[1];
        ///<summary>
        ///Distance of GRAMM grid cell centre from the southern domain border
        ///</summary>
        public static double[] YKO = new double[1];
        ///<summary>
        ///Height of GRAMM grid cell centre above sea level
        ///</summary>
        public static double[] ZKO = new double[1];
        ///<summary>
        ///Height of the surface of the GRAMM grid
        ///</summary>
        public static float[][] AH = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Heights of the corner points of each grid cell of the GRAMM grid
        ///</summary>
        public static float[][][] AHE = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Height of the centre point of each grid cell of the GRAMM grid
        ///</summary>
        public static float[][][] ZSP = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Horizontal grid size in x-direction of the GRAMM grid; Without terrain, index 1 is set to East - West of the GRAL domain!
        ///</summary>
        public static float[] DDX = new float[1];
        ///<summary>
        ///Horizontal grid size in y-direction of the GRAMM grid; Without terrain, index 1 is set to North - South of the GRAL domain!
        ///</summary>
        public static float[] DDY = new float[1];
        ///<summary>
        ///U-wind component of GRAMM wind fields
        ///</summary>
        public static float[][][] UWIN = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///V-wind component of GRAMM wind fields
        ///</summary>
        public static float[][][] VWIN = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///W-wind component of GRAMM wind fields
        ///</summary>
        public static float[][][] WWIN = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Turbulent kinetic energy of GRAMM
        ///</summary>
        public static float[][][] TKE = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Turbulent dissipation rate of GRAMM
        ///</summary>
        public static float[][][] DISS = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Heights of the horizontal slices of the GRAL concentration grids
        ///</summary>
        public static float[] HorSlices = new float[1];
        ///<summary>
        ///Adaptive roughness lenght max value; 0 = default GRAL mode using one roughness lenght 
        ///</summary>
        public static float AdaptiveRoughnessMax = 0;
        ///<summary>
        ///Roughness length [m] for the adaptive roughness lenght mode
        ///</summary>
        public static float[][] Z0Gral = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Fricition velocity for the adaptive roughness lenght mode
        ///</summary>
        public static float[][] USternGral = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        /// Obukhov-length for the adaptive roughness lenght mode
        ///</summary>
        public static float[][] OLGral = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Roughness length [m] from the file landuse.asc
        ///</summary>
        public static float[][] Z0Gramm = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Fricition velocity
        ///</summary>
        public static float[][] Ustern = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        /// Obukhov-length
        ///</summary>
        public static float[][] Ob = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        /// Stability class
        ///</summary>
        public static byte[][] SC_Gral = CreateArray<byte[]>(1, () => new byte[1]);
        ///<summary>
        ///Stability class obtained from GRAMM simulations
        ///</summary>
        public static int[,] AKL_GRAMM = new int[1, 1];
        ///<summary>
        ///Saves source group for each source, the maximum number of sources is limited by the memory
        ///</summary>
        public static List<int> IMQ = new List<int>();
        ///<summary>
        ///Observed vertical profile of u-wind components
        ///</summary>
        public static float[] ObsWindU = new float[51];
        ///<summary>
        ///Observed vertical profile of v-wind components
        ///</summary>
        public static float[] ObsWindV = new float[51];
        ///<summary>
        ///Observed vertical profile of the standard deviation u-wind fluctuations
        ///</summary>
        public static float[] U0 = new float[51];
        ///<summary>
        ///Observed vertical profile of the standard deviation v-wind fluctuations
        ///</summary>
        public static float[] V0 = new float[51];
        ///<summary>
        ///Universal constant in z-direction
        ///</summary>
        public static float C0z = 4;
        ///<summary>
        ///Universal constant in horizontal-directions
        ///</summary>
        public static float C0x = 3;
        ///<summary>
        ///Western border of the GRAMM domain
        ///</summary>
        public static int GrammWest;
        ///<summary>
        ///Southern border of the GRAMM domain
        ///</summary>
        public static int GrammSouth;
        ///<summary>
        ///Western border of the GRAL domain
        ///</summary>
        public static int IKOOAGRAL;
        ///<summary>
        ///Southern border of the GRAL domain
        ///</summary>
        public static int JKOOAGRAL;
        ///<summary>
        ///Flag indicating the existance of buildings 
        ///</summary>
        public static bool BuildingsExist = false;
        ///<summary>
        ///Flag indicating the existance of vegetation
        ///</summary>
        public static bool VegetationExist = false;
        ///<summary>
        ///Flag indicating the existance of an area where the jet stream out of a tunnel is destroyed due to traffic on the opposite lanes
        ///</summary>
        public static bool TunnelOppLane = false;
        ///<summary>
        ///Flag indicating the existance of an area where pollutants are sucked into a tunnel entrance
        ///</summary>
        public static bool TunnelEntr = false;
        ///<summary>
        ///Minimum elevation of the GRAMM orography
        ///</summary>
        public static float AHMIN = 10000;
        ///<summary>
        ///Maximum elevation of the GRAMM orography
        ///</summary>
        public static float AHMAX = -10000;
        ///<summary>
        ///Number of cells in x-direction of the flow field grid
        ///</summary>
        public static int NII = 1;
        ///<summary>
        ///Number of cells in y-direction of the flow field grid
        ///</summary>
        public static int NJJ = 1;
        ///<summary>
        ///Number of cells in z-direction of the flow field grid
        ///</summary>
        public static int NKK = 1;
        ///<summary>
        ///Number of cells in z-direction of the transient concentration grid
        ///</summary>
        public static int NKK_Transient = 1;
        ///<summary>
        ///Point for the reference position inside the prognostic subdomain to get the initial GRAMM wind vector
        ///</summary>
        public static IntPoint SubDomainRefPos = new IntPoint();
        ///<summary>
        ///U-wind component of GRAL wind fields
        ///</summary>
        public static float[][][] UK = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///V-wind component of GRAL wind fields
        ///</summary>
        public static float[][][] VK = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///W-wind component of GRAL wind fields
        ///</summary>
        public static float[][][] WK = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Height of the surface of the GRAL grid
        ///</summary>
        public static float[][] AHK = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Height of the surface of orographical input data
        ///</summary>
        public static float[][] AHKOri = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        /// Minimum height of original GRAL topography
        ///</summary>
        public static float AHKOriMin = 1000000;
        ///<summary>
        ///Determine the surface index of the GRAL grid
        ///</summary>
        public static Int16[][] KKART = CreateArray<Int16[]>(1, () => new Int16[1]);
        ///<summary>
        ///Array defining the GRAL sub-domains for the microscale flow field simulations (prognostic mode - sub domain mask) 0 = outside prognostic area 1 = inside prognostic area
        ///</summary>
        public static Byte[][] ADVDOM = CreateArray<Byte[]>(1, () => new Byte[1]);
        ///<summary>
        ///Turbulent kinetic energy of the GRAL microscale flow field
        ///</summary>
        public static float[][][] TURB = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Turbulent dissipation rate of the GRAL microscale flow field
        ///</summary>
        public static float[][][] TDISS = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Array used for Buildings - relative building height above ground
        ///</summary>
        public static float[][] CUTK = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Array used for Buildings - rasterized building height
        ///</summary>
        public static float[][] BUI_HEIGHT = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Array used for Vegetation
        ///</summary>
        public static float[][][] VEG = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Array used for Vegetation Coverage
        ///</summary>
        public static float[][] COV = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Steady-state (1) or transient (0) simulation mode
        ///</summary>
        public static int ISTATIONAER = 0;
        ///<summary>
        ///Released particles per second per weather situation
        ///</summary>
        public static float TPS = 100;
        ///<summary>
        ///Dispersion time per weather situation
        ///</summary>
        public static float TAUS = 3600;
        ///<summary>
        ///Flag determining the input file for meteorological data 4:wind speed, wind direction, stability class 
        /// 0: friction velocity, Obukhov length, boundary-layer height, standard deviation of horizontal wind fluctuations, u- and v-wind components 
        /// 2: friction velocity, Obukhov length, standard deviation of horizontal wind fluctuations, u- and v-wind components
        /// 3: velocity, direction, friction velocity, standard deviation of horizontal and vertical wind fluctuations, Obukhov length, meandering parameters
        ///</summary>
        public static int IStatistics = 4;
        ///<summary>
        ///Flag determining if receptor points are included or not
        ///</summary>
        public static int ReceptorsAvailable = 0;
        ///<summary>
        ///Default roughness length in [m]
        ///</summary>
        public static float Z0 = 1 / 10;
        ///<summary>
        ///Latitude
        ///</summary>
        public static float LatitudeDomain = 47;
        ///<summary>
        ///Flag determining if meandering is switched on (=false) or off (=true)
        ///</summary>
        public static bool MeanderingOff = false;
        ///<summary>
        ///Pollutant: by default NOx
        ///</summary>
        public static string Pollutant = "NOx";
        ///<summary>
        ///Defines the method to take buildings into account: diagnostic (=1), prognostic (=2), no buildings (=0)
        ///</summary>
        public static int FlowFieldLevel = 0;
        ///<summary>
        ///Defines the factor for the calculation of prognostic sub-domains, default = 15
        ///</summary>
        public static int PrognosticSubDomainFactor = 15;
        ///<summary>
        ///Number of Receptor points
        ///</summary>
        public static int ReceptorNumber = 0;
        ///<summary>
        ///x-coordinate of receptor points
        ///</summary>
        public static double[] ReceptorX = new double[1];
        ///<summary>
        ///y-coordinate of receptor points
        ///</summary>
        public static double[] ReceptorY = new double[1];
        ///<summary>
        ///z-coordinate of receptor points above ground level
        ///</summary>
        public static float[] ReceptorZ = new float[1];
        ///<summary>
        ///is the receptor nearby a building?
        ///</summary>
        public static bool[] ReceptorNearbyBuilding = new bool[1];
        ///<summary>
        ///Receptor name
        ///</summary>
        public static List<string> ReceptorName = new List<string>();
        ///<summary>
        ///x-index of receptor points in the GRAL concentration grid
        ///</summary>
        public static int[] ReceptorIInd = new int[1];
        ///<summary>
        ///y-index of receptor points in the GRAL concentration grid
        ///</summary>
        public static int[] ReceptorJInd = new int[1];
        ///<summary>
        ///Z-index of receptor points in the GRAL concentration grid
        ///</summary>
        public static int[] ReceptorKInd = new int[1];
        ///<summary>
        ///x-index of receptor points in the GRAL flow-field grid
        ///</summary>
        public static int[] ReceptorIIndFF = new int[1];
        ///<summary>
        ///y-index of receptor points in the GRAL flow-field grid
        ///</summary>
        public static int[] ReceptorJIndFF = new int[1];
        ///<summary>
        ///Z-index of receptor points in the GRAL flow-field grid
        ///</summary>
        public static int[] ReceptorKIndFF = new int[1];
        ///<summary>
        ///Array containing concentrations at receptor points
        ///</summary>
        public static double[][] ReceptorConc = CreateArray<double[]>(1, () => new double[1]);
        ///<summary>
        ///Array containing the maximum concentration of one particle at a receptor to estimate the statistical error
        ///</summary>
        public static double[][] ReceptorParticleMaxConc = CreateArray<double[]>(1, () => new double[1]);
        ///<summary>
        ///Array containing the total concentration of one particle at a receptor to estimate the statistical error
        ///</summary>
        public static double[][] ReceptorTotalConc = CreateArray<double[]>(1, () => new double[1]);
        ///<summary>
        ///Factor to decrease the standard deviation of the horizontal wind fluctuations in dependency on the averaging time
        ///</summary>
        public static float StdDeviationV = 1;
        ///<summary>
        ///Factor to decrease the standard deviation of the vertical wind fluctuations in dependency on the averaging time
        ///</summary>
        public static float StdDeviationW = 1;
        ///<summary>
        ///Flag set when line source data is provided
        ///</summary>
        public static int LS_Count = 0;
        ///<summary>
        ///Flag set when tunnel portal source data is provided
        ///</summary>
        public static int TS_Count = 0;
        ///<summary>
        ///Flag set when point source data is provided
        ///</summary>
        public static int PS_Count = 0;
        ///<summary>
        ///Flag set when area source data is provided
        ///</summary>
        public static int AS_Count = 0;
        ///<summary>
        ///2D Array defining areas where the tunnel jet stream is destroyed due to traffic on the opposite lanes of highways
        ///</summary>
        public static byte[][] OPP_LANE = CreateArray<byte[]>(1, () => new byte[1]);
        ///<summary>
        ///2D Array defining areas where the tunnel jet stream is destroyed due to traffic on the opposite lanes of highways
        ///</summary>
        public static byte[][] TUN_ENTR = CreateArray<byte[]>(1, () => new byte[1]);
        ///<summary>
        ///Emission rates of point sources
        ///</summary>
        public static double[] PS_ER = new double[1];
        ///<summary>
        ///x-coordinate of point sources
        ///</summary>
        public static double[] PS_X = new double[1];
        ///<summary>
        ///y-coordinate of point sources
        ///</summary>
        public static double[] PS_Y = new double[1];
        ///<summary>
        ///z-coordinate of point sources
        ///</summary>
        public static float[] PS_Z = new float[1];
        ///<summary>
        ///Exit velocities of point sources
        ///</summary>
        public static float[] PS_V = new float[1];
        ///<summary>
        ///Stack diameter of point sources
        ///</summary>
        public static float[] PS_D = new float[1];
        ///<summary>
        ///Exit temperature of point sources
        ///</summary>
        public static float[] PS_T = new float[1];
        ///<summary>
        ///Source group of point source
        ///</summary>
        public static byte[] PS_SG = new byte[1];
        ///<summary>
        ///Final plume rise of point sources
        ///</summary>
        public static float[] PS_effqu = new float[1];
        ///<summary>
        ///Deposition mode 0 = no deposition, 1 = conc+dep, 2= dep only
        ///</summary>
        public static byte[] PS_Mode = new byte[1];
        ///<summary>
        ///Sedimentation velocity
        ///</summary>
        public static float[] PS_V_sed = new float[1];
        ///<summary>
        ///Deposition velocity
        ///</summary>
        public static float[] PS_V_Dep = new float[1];
        ///<summary>
        ///Emission rate for deposition
        ///</summary>
        public static float[] PS_ER_Dep = new float[1];
        ///<summary>
        ///Absolute source height if true
        ///</summary>
        public static bool[] PS_Absolute_Height = new bool[1];
        ///<summary>
        ///Index to time series file column
        ///</summary>
        public static int[] PS_TimeSeriesTemperature = new int[1];
        ///<summary>
        ///Index to time series file column
        ///</summary>
        public static int[] PS_TimeSeriesVelocity = new int[1];
        ///<summary>
        ///All Columns for the Exit Temperature Time Series
        ///</summary>
        public static List<TimeSeriesColumn> PS_TimeSerTempValues;
        ///<summary>
        ///All Columns for the Exit Velocity Time Series
        ///</summary>
        public static List<TimeSeriesColumn> PS_TimeSerVelValues;
        ///<summary>
        ///Emission rates of line sources
        ///</summary>
        public static double[] LS_ER = new double[1];
        ///<summary>
        ///First x-coordinate of line sources
        ///</summary>
        public static double[] LS_X1 = new double[1];
        ///<summary>
        ///Second x-coordinate of line sources
        ///</summary>
        public static double[] LS_X2 = new double[1];
        ///<summary>
        ///First y-coordinate of line sources
        ///</summary>
        public static double[] LS_Y1 = new double[1];
        ///<summary>
        ///Second y-coordinate of line sources
        ///</summary>
        public static double[] LS_Y2 = new double[1];
        ///<summary>
        ///First z-coordinate of line sources
        ///</summary>
        public static float[] LS_Z1 = new float[1];
        ///<summary>
        ///Second z-coordinate of line sources
        ///</summary>
        public static float[] LS_Z2 = new float[1];
        ///<summary>
        ///Vertical extension of the source at negative values / height of a noise abatement wall in m
        ///</summary>
        public static float[] LS_Laerm = new float[1];
        ///<summary>
        ///Width of road in m
        ///</summary>
        public static float[] LS_Width = new float[1];
        ///<summary>
        ///Source group of line source
        ///</summary>
        public static byte[] LS_SG = new byte[1];
        ///<summary>
        ///Deposition mode 0 = no deposition, 1 = conc+dep, 2= dep only
        ///</summary>
        public static byte[] LS_Mode = new byte[1];
        ///<summary>
        ///Sedimentation velocity
        ///</summary>
        public static float[] LS_V_sed = new float[1];
        ///<summary>
        ///Deposition velocity
        ///</summary>
        public static float[] LS_V_Dep = new float[1];
        ///<summary>
        ///Emission rate for deposition
        ///</summary>
        public static float[] LS_ER_Dep = new float[1];
        ///<summary>
        ///Absolute source height if true
        ///</summary>
        public static bool[] LS_Absolute_Height = new bool[1];
        ///<summary>
        ///Emission rates of tunnel portals
        ///</summary>
        public static double[] TS_ER = new double[1];
        ///<summary>
        ///Excess temperature of the tunnel portal jet stream
        ///</summary>
        public static float[] TS_T = new float[1];
        ///<summary>
        ///Exit velocity of the tunnel portal jet stream
        ///</summary>
        public static float[] TS_V = new float[1];
        ///<summary>
        ///First x-coordinate of the tunnel portal
        ///</summary>
        public static double[] TS_X1 = new double[1];
        ///<summary>
        ///First y-coordinate of the tunnel portal
        ///</summary>
        public static double[] TS_Y1 = new double[1];
        ///<summary>
        ///First z-coordinate of the tunnel portal
        ///</summary>
        public static float[] TS_Z1 = new float[1];
        ///<summary>
        ///Second x-coordinate of the tunnel portal
        ///</summary>
        public static double[] TS_X2 = new double[1];
        ///<summary>
        ///Second y-coordinate of the tunnel portal
        ///</summary>
        public static double[] TS_Y2 = new double[1];
        ///<summary>
        ///Second z-coordinate of the tunnel portal
        ///</summary>
        public static float[] TS_Z2 = new float[1];
        ///<summary>
        ///Source group of Tunnel Portal source
        ///</summary>
        public static byte[] TS_SG = new byte[1];
        ///<summary>
        ///Cross area of tunnel portals
        ///</summary>
        public static float[] TS_Area = new float[1];
        ///<summary>
        ///Width of tunnel portals
        ///</summary>
        public static float[] TS_Width = new float[1];
        ///<summary>
        ///Height of tunnel portals
        ///</summary>
        public static float[] TS_Height = new float[1];
        ///<summary>
        ///Cosine of the angle between the orientation of the tunnel portals and the y-axis
        ///</summary>
        public static float[] TS_cosalpha = new float[1];
        ///<summary>
        ///Sinus of the angle between the orientation of the tunnel portals and the y-axis		
        ///</summary>
        public static float[] TS_sinalpha = new float[1];
        ///<summary>
        ///Deposition mode 0 = no deposition, 1 = conc+dep, 2= dep only
        ///</summary>
        public static byte[] TS_Mode = new byte[1];
        ///<summary>
        ///Sedimentation velocity
        ///</summary>
        public static float[] TS_V_sed = new float[1];
        ///<summary>
        ///Deposition velocity
        ///</summary>
        public static float[] TS_V_Dep = new float[1];
        ///<summary>
        ///Emission rate for deposition
        ///</summary>
        public static float[] TS_ER_Dep = new float[1];
        ///<summary>
        ///Absolute source height if true
        ///</summary>
        public static bool[] TS_Absolute_Height = new bool[1];
        ///<summary>
        ///Index to time series file column
        ///</summary>
        public static int[] TS_TimeSeriesTemperature = new int[1];
        ///<summary>
        ///Index to time series file column
        ///</summary>
        public static int[] TS_TimeSeriesVelocity = new int[1];
        ///<summary>
        ///All Columns for the Exit Temperature Time Series
        ///</summary>
        public static List<TimeSeriesColumn> TS_TimeSerTempValues;
        ///<summary>
        ///All Columns for the Exit Velocity Time Series
        ///</summary>
        public static List<TimeSeriesColumn> TS_TimeSerVelValues;
        ///<summary>
        ///x-coordinate of the centre of area sources
        ///</summary>
        public static double[] AS_X = new double[1];
        ///<summary>
        ///y-coordinate of the centre of area sources
        ///</summary>
        public static double[] AS_Y = new double[1];
        ///<summary>
        ///z-coordinate of the centre of area sources
        ///</summary>
        public static float[] AS_Z = new float[1];
        ///<summary>
        ///Extension in x-direction of area sources
        ///</summary>
        public static double[] AS_dX = new double[1];
        ///<summary>
        ///Extension in y-direction of area sources
        ///</summary>
        public static double[] AS_dY = new double[1];
        ///<summary>
        ///Extension in z-direction of area sources
        ///</summary>
        public static float[] AS_dZ = new float[1];
        ///<summary>
        ///Emission rates of area sources
        ///</summary>
        public static double[] AS_ER = new double[1];
        ///<summary>
        ///Source group of area source
        ///</summary>
        public static byte[] AS_SG = new byte[1];
        ///<summary>
        ///Deposition mode 0 = no deposition, 1 = conc+dep, 2= dep only
        ///</summary>
        public static byte[] AS_Mode = new byte[1];
        ///<summary>
        ///Sedimentation velocity
        ///</summary>
        public static float[] AS_V_sed = new float[1];
        ///<summary>
        ///Deposition velocity
        ///</summary>
        public static float[] AS_V_Dep = new float[1];
        ///<summary>
        ///Emission rate for deposition
        ///</summary>
        public static float[] AS_ER_Dep = new float[1];
        ///<summary>
        ///Absolute source height if true
        ///</summary>
        public static bool[] AS_Absolute_Height = new bool[1];
        ///<summary>
        ///Total number of particles released in each weather situation
        ///</summary>
        public static int NTEILMAX = 0;
        ///<summary>
        ///Each particle is assigned a specific source number
        ///</summary>
        public static int[] ParticleSource = new int[1];
        ///<summary>
        ///Each particle is assigned a specific source type (0: point, 1: portal, 2: line, 3: area)
        ///</summary>
        public static byte[] SourceType = new byte[1];
        ///<summary>
        ///Each particle is assigned a source group
        ///</summary>
        public static byte[] ParticleSG = new byte[1];
        ///<summary>
        ///x-coordinate of a particle
        ///</summary>
        public static double[] Xcoord = new double[1];
        ///<summary>
        ///y-coordinate of a particle
        ///</summary>
        public static double[] YCoord = new double[1];
        ///<summary>
        ///z-coordinate of a particle
        ///</summary>
        public static float[] ZCoord = new float[1];
        ///<summary>
        ///Emission mass of the lagrangian particle
        ///</summary>
        public static double[] ParticleMass = new double[1];
        ///<summary>
        ///Mean emission mass of all lagrangian particles
        ///</summary>
        public static double ParticleMassMean;
        ///<summary>
        ///Sedimentation velocity of one lagrangian particle
        ///</summary>
        public static float[] ParticleVsed = new float[1];
        ///<summary>
        ///deposition velocity of one lagrangian particle
        ///</summary>
        public static float[] ParticleVdep = new float[1];
        ///<summary>
        ///Deposition type 0 = no deposition, 1 = deposition+concentration, 2 = only deposition
        ///</summary>
        public static byte[] ParticleMode = new byte[1];
        ///<summary>
        ///Deposition flag
        ///</summary>
        public static bool DepositionExist = false;
        ///<summary>
        ///Coriolis parameter
        ///</summary>
        public static float CorolisParam = 0;
        ///<summary>
        ///Actual computed weather situation
        ///</summary>
        public static int IWET = 0;
        ///<summary>
        ///Flag when reaching the final weather situation
        ///</summary>
        public static int IEND = 0;
        ///<summary>
        ///Volume of the GRAL concentration grid
        ///</summary>
        public static float GridVolume = 0;
        ///<summary>
        ///Flag indicating whether land-use data is available or not
        ///</summary>
        public static bool LandUseAvailable = false;
        ///<summary>
        ///Total number of meteo observations of a profile
        ///</summary>
        public static int MetProfileNumb = 1;
        ///<summary>
        ///Boundary-layer height in m in steep terrain
        ///</summary>
        public static float BdLayHeight = 0;
        ///<summary>
        ///Boundary-layer height in m in flat terrain
        ///</summary>
        public static float BdLayFlat = 0;
        ///<summary>
        ///Surface-layer height in m -> 10% of boundary-layer height
        ///</summary>
        public static float BdLayH10 = 0;
        ///<summary>
        ///Standard deviation of the vertical wind fluctuation
        ///</summary>
        public static float W0int = 0;
        ///<summary>
        ///Parameter m to describe horizontal wind meandering
        ///</summary>
        public static float MWindMEander = 0;
        ///<summary>
        ///Parameter T to describe horizontal wind meandering
        ///</summary>
        public static float TWindMeander = 0;
        ///<summary>
        ///Maximum cell index in z-direction up to which a microscale flow field is computed in every column
        ///</summary>
        public static int KADVMAX = 1;
        ///<summary>
        ///Total mass divergence in each cell
        ///</summary>
        public static float[][][] DIV = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Non-hydrostatic pressure
        ///</summary>
        public static float[][][] DPM = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Potential temperature gradient
        ///</summary>
        public static float PotdTdz = 0;
        ///<summary>
        ///U-component of the staggered grid
        ///</summary>
        public static float[][][] UKS = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///V-component of the staggered grid
        ///</summary>
        public static float[][][] VKS = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///W-component of the staggered grid
        ///</summary>
        public static float[][][] WKS = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Non-hydrostatic pressure at time t+1
        ///</summary>
        public static float[][][] DPMNEW = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Minimum cell index up to which calculations are carried out
        ///</summary>
        public static int[][] VerticalIndex = CreateArray<int[]>(1, () => new int[1]);
        ///<summary>
        ///Term used to compute friction velocity near obstacles efficiently
        ///</summary>
        public static float[][] UsternObstaclesHelpterm = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Term used to compute friction velocity near terrain efficiently
        ///</summary>
        public static float[][] UsternTerrainHelpterm = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///Total number of particles allocated per point source
        ///</summary>
        public static int[] PS_PartNumb = new int[1];
        ///<summary>
        ///Total number of particles allocated per tunnel portal
        ///</summary>
        public static int[] TS_PartNumb = new int[1];
        ///<summary>
        ///Total number of particles allocated per line sources
        ///</summary>
        public static int[] LS_PartNumb = new int[1];
        ///<summary>
        ///Total number of particles allocated per area sources
        ///</summary>
        public static int[] AS_PartNumb = new int[1];
        ///<summary>
        ///Total number of particles allocated to point sources
        ///</summary>
        public static int PS_PartSum = 0;
        ///<summary>
        ///Total number of particles allocated to tunnel portals
        ///</summary>
        public static int TS_PartSum = 0;
        ///<summary>
        ///Total number of particles allocated to line sources
        ///</summary>
        public static int LS_PartSum = 0;
        ///<summary>
        ///Total number of particles allocated to area sources
        ///</summary>
        public static int AS_PartSum = 0;
        ///<summary>
        ///dispersion time
        ///</summary>
        public static float DispTimeSum = 0;
        ///<summary>
        ///indicates if running on unix systems
        ///</summary>
        public static bool RunOnUnix = false;
        ///<summary>
        ///Flag determining whether odour dispersion is carried out or not
        ///</summary>
        public static bool Odour = false;
        ///<summary>
        ///2D concentration separated into source groups and horizontal slices for the adjecent layer upwards
        ///</summary>
        public static float[][][][] Conz3dp = CreateArray<float[][][]>(1, () => CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1])));
        ///<summary>
        ///2D concentration separated into source groups and horizontal slices for the adjecent layer downwards
        ///</summary>
        public static float[][][][] Conz3dm = CreateArray<float[][][]>(1, () => CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1])));
        ///<summary>
        ///2D source term for concentration variance
        ///</summary>
        public static float[][] Q_cv0 = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///2D dissipation rate of concentration variance
        ///</summary>
        public static float[][] DisConcVar = CreateArray<float[]>(1, () => new float[1]);
        ///<summary>
        ///deposition results
        ///</summary>
        public static double[][][] Depo_conz = CreateArray<double[][]>(1, () => CreateArray<double[]>(1, () => new double[1]));
        ///<summary>
        ///Top of the GRAL domain in [m] default 800 for flat terrain
        ///</summary>
        public static float ModelTopHeight = 800;
        ///<summary>
        ///3D concentration separated into source groups old time step
        ///</summary>
        public static float[][][][] Conz4d = CreateArray<float[][][]>(1, () => CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1])));
        ///<summary>
        ///3D concentration separated into source groups new time step
        ///</summary>
        public static float[][][][] Conz5d = CreateArray<float[][][]>(1, () => CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1])));
        ///<summary>
        ///3D concentration sum file
        ///</summary>
        public static float[][][] ConzSsum = CreateArray<float[][]>(1, () => CreateArray<float[]>(1, () => new float[1]));
        ///<summary>
        ///Sets the lower concentration threshold below which the memory effect in transient simulations is neglected
        ///</summary>
        public static float TransConcThreshold = 0.1f;
        ///<summary>
        ///if file emissions_timeseries.txt exists, these modulation factors will be used
        ///</summary>
        public static float[,] EmFacTimeSeries = new float[1, 1];
        ///<summary>
        ///Exists file emissions_timeseries.txt for transient simulation
        ///</summary>
        public static bool EmissionTimeseriesExist = false;
        ///<summary>
        ///Check if high-resolution topography for GRAL simulations exists
        ///</summary>
        public static bool GralTopofile = false;
        ///<summary>
        ///Flag if output files are zipped
        ///</summary>
        public static bool ResultFileZipped = false;
        ///<summary>
        /// filename for GRAL output files
        ///</summary>
        public static string ZippedFile;
        ///<summary>
        /// time series of meteorological  data -> mettimeseries.dat
        ///</summary>
        public static List<WindData> MeteoTimeSer = new List<WindData>();
        ///<summary>
        /// time series of meteorological  data -> meteopgt.all
        ///</summary>
        public static List<WindData> MeteopgtLst = new List<WindData>();
        ///<summary>
        /// number of the actual weather situation in meteopgt.all corresponding with the actual situation in mettimeseries for transient simulations
        ///</summary>
        public static int IDISP = 0;
        ///<summary>
        /// Average deposition settings in transient mode
        ///</summary>
        public static TransientDeposition[] TransientDepo;
        ///<summary>
        /// Sum of particle emission per source group inclusive modulation in transient mode
        ///</summary>
        public static double[] EmissionPerSG;
        ///<summary>
        /// Counter of sum emission for 3D concentration field
        ///</summary>
        public static int ConzSumCounter = 0;
        ///<summary>
        /// Compute wet deposition?
        ///</summary>
        public static bool WetDeposition = false;
        ///<summary>
        /// Wet deposition CW
        ///</summary>
        public static double Wet_Depo_CW = 0;
        ///<summary>
        /// Wet deposition AlphaW
        ///</summary>
        public static double WedDepoAlphaW = 0;
        ///<summary>
        /// Wet deposition rW per dispersion situation
        ///</summary>
        public static double WetDepoRW = 0;
        ///<summary>
        /// Wet deposition precipitation data for the timeseries
        ///</summary>
        public static List<float> WetDepoPrecipLst = new List<float>();
        ///<summary>
        /// Log additional informations at the console
        ///</summary>
        public static int LogLevel = 0;
        ///<summary>
        /// Reflexion counter for Loglevel == 1;
        ///</summary>
        public static int[] LogReflexions = new int[6];
        ///<summary>
        /// Timestep counter for Loglevel == 1;
        ///</summary>
        public static int[] Log_Timesteps = new int[6];
        ///<summary>
        /// Write vertical concentration files?		
        ///</summary>
        public static bool WriteVerticalConcentration = false;
        ///<summary>
        /// decay rate for bioaerosols for each source group
        ///</summary>
        public static double[] DecayRate = new double[102];
        ///<summary>
        /// Pollutant name
        ///</summary>
        public static string PollutantType = "NOx";
        ///<summary>
        /// Interval for temp file output
        ///</summary>
        public static int TransientTempFileInterval = 24;
        ///<summary>
        /// Delete transient temporary files if the calculation is finished
        ///</summary>
        public static bool TransientTempFileDelete = true;
        ///<summary>
        /// Path for gff files 
        ///</summary>
        public static string GFFFilePath = string.Empty;
        ///<summary>
        /// Determines if the GRAL geometry has already been loaded from file
        ///</summary>
        public static bool GeometryAlreadyRead = false;
        ///<summary>
        /// Use a console key if an error occurs or the calculation has been finished (true) or exit console without keystroke (false)
        ///</summary>
        public static bool WaitForConsoleKey = true;
        ///<summary>
        /// Write additional ASCii output
        ///</summary>
        public static bool WriteASCiiResults = false;
    }
}