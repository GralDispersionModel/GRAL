// Generated trajectory body retains GRAL GPL-3.0 source provenance. This support layer is task local.
template<class T,int N> struct Arr {
 T* p; int d[N];
 __device__ Arr<T,N-1> operator[](int i) const { Arr<T,N-1> a; unsigned long long stride=1; for(int j=1;j<N;j++){a.d[j-1]=d[j];stride*=d[j];} a.p=p+(unsigned long long)i*stride;return a; }
 __device__ int size()const{return d[0];}
 __device__ int upper(int k)const{return d[k]-1;}
 __device__ bool operator==(decltype(nullptr))const{return p==nullptr;}
 __device__ bool operator!=(decltype(nullptr))const{return p!=nullptr;}
 __device__ T& operator()(int i,int j)const{return p[(unsigned long long)i*d[1]+j];}
};
template<class T> struct Arr<T,1> {
 T* p; int d[1];
 __device__ T& operator[](int i) const{return p[i];}
 __device__ int size()const{return d[0];}
 __device__ int upper(int)const{return d[0]-1;}
 __device__ bool operator==(decltype(nullptr))const{return p==nullptr;}
 __device__ bool operator!=(decltype(nullptr))const{return p!=nullptr;}
 __device__ void clear(){for(int i=0;i<d[0];i++)p[i]=T{};}
};
struct TimeSeriesColumn {Arr<float,1> Value;};
template<class T>__device__ Arr<T,1> ParticleScratchRow(Arr<T,2> scratch,int length){
 Arr<T,1> row=scratch[blockIdx.x*blockDim.x+threadIdx.x];
 row.clear();row.d[0]=length;return row;
}
template<class A,class B>struct Tuple2{A v0;B v1;};
template<class A,class B,class C,class D>struct Tuple4{A v0;B v1;C v2;D v3;};
template<class T>__device__ T gral_abs(T x){return x<0?-x:x;}
template<>__device__ float gral_abs(float x){return fabsf(x);}
template<>__device__ double gral_abs(double x){return fabs(x);}
template<>__device__ int gral_abs(int x){if(x==(-2147483647-1))asm("trap;");return x<0?-x:x;}
template<class T>__device__ T gral_min(T a,T b){return a<b?a:b;}
template<class T>__device__ T gral_max(T a,T b){return a>b?a:b;}
template<>__device__ float gral_min(float a,float b){return isnan(a)?a:a==b?(signbit(a)?a:b):a<b?a:b;}
template<>__device__ double gral_min(double a,double b){return isnan(a)?a:a==b?(signbit(a)?a:b):a<b?a:b;}
template<>__device__ float gral_max(float a,float b){return isnan(a)?a:a==b?(signbit(a)?b:a):a>b?a:b;}
template<>__device__ double gral_max(double a,double b){return isnan(a)?a:a==b?(signbit(a)?b:a):a>b?a:b;}
template<class T>__device__ T gral_clamp(T a,T b,T c){return gral_min(gral_max(a,b),c);}
template<class T>__device__ int gral_sign(T a){return a<0?-1:a>0?1:0;}
template<>__device__ int gral_sign(float a){if(isnan(a))asm("trap;");return a<0?-1:a>0?1:0;}
template<>__device__ int gral_sign(double a){if(isnan(a))asm("trap;");return a<0?-1:a>0?1:0;}
// .NET 9+ saturating floating-point-to-integer conversions, including NaN -> zero.
template<class T>__device__ T cs_cast(double x);
template<>__device__ int cs_cast<int>(double x){if(isnan(x))return 0;if(x>=2147483647.0)return 2147483647;if(x<=-2147483648.0)return (-2147483647-1);return (int)x;}
template<>__device__ unsigned int cs_cast<unsigned int>(double x){if(isnan(x)||x<=0)return 0;if(x>=4294967295.0)return 4294967295u;return (unsigned int)x;}
template<>__device__ long long cs_cast<long long>(double x){if(isnan(x))return 0;if(x>=9223372036854775808.0)return 9223372036854775807ll;if(x<=-9223372036854775808.0)return (-9223372036854775807ll-1);return (long long)x;}
template<>__device__ unsigned long long cs_cast<unsigned long long>(double x){if(isnan(x)||x<=0)return 0;if(x>=18446744073709551616.0)return 18446744073709551615ull;return (unsigned long long)x;}
__device__ float gral_round(float x,int digits){float z=powf(10.0f,digits);return nearbyintf(x*z)/z;}
#if defined(__CUDA_ARCH__) && __CUDA_ARCH__ < 600
// Native double atomicAdd requires sm_60; compare integer bits to handle NaN.
__device__ double atomicAdd(double* address, double value){
 unsigned long long* bits=(unsigned long long*)address;
 unsigned long long old=*bits, assumed;
 do {
  assumed=old;
  old=atomicCAS(bits,assumed,(unsigned long long)__double_as_longlong(value+__longlong_as_double((long long)assumed)));
 } while(assumed!=old);
 return __longlong_as_double((long long)old);
}
#endif
template<class T,class V>__device__ T atomic_add(T* p,V v){return atomicAdd(p,(T)v);}
template<class V>__device__ long long atomic_add(long long* p,V v){return (long long)atomicAdd((unsigned long long*)p,(unsigned long long)v);}
template<class T>__device__ T atomic_inc(T* p){return atomic_add(p,1)+1;}
__device__ double atomic_exchange(double* p,double v){return __longlong_as_double((long long)atomicExch((unsigned long long*)p,(unsigned long long)__double_as_longlong(v)));}
__device__ double atomic_max(double* p,double v){
 unsigned long long* address=(unsigned long long*)p;
 unsigned long long old=atomicCAS(address,0ull,0ull);
 while(v>__longlong_as_double((long long)old)){
  unsigned long long assumed=old;
  old=atomicCAS(address,assumed,(unsigned long long)__double_as_longlong(v));
  if(old==assumed)break;
 }
 return __longlong_as_double((long long)old);
}
__device__ int atomic_compare(int* p,int cmp,int val){return atomicCAS(p,cmp,val);}
__device__ bool gral_drop_finite(double x,double y,float z,float u,float v,float w,float ground,double height,float dt,float fall,double mass,float first){
 return isfinite(x)&&isfinite(y)&&isfinite(z)&&isfinite(u)&&isfinite(v)&&isfinite(w)&&isfinite(ground)&&
  isfinite(height)&&height>0&&isfinite(dt)&&dt>0&&isfinite(fall)&&fall>0&&isfinite(mass)&&mass>=0&&isfinite(first)&&first>0;
}
struct State {
Arr<float,2> AHK;
float AHMIN;
float AdaptiveRoughnessMax;
float BdLayH10;
float BdLayHeight;
bool BuildingsExist;
float C0x;
float C0z;
Arr<float,2> COV;
Arr<float,2> CUTK;
Arr<double,4> Conz3dDouble;
Arr<double,4> Conz3dm;
Arr<double,4> Conz3dp;
Arr<float,4> Conz5d;
Arr<double,3> ConzSsum;
Arr<float,1> DDX;
Arr<float,1> DDY;
float DXK;
float DYK;
Arr<float,1> DZK;
Arr<float,1> DZK_Trans;
Arr<double,1> DecayRate;
Arr<double,3> Depo_conz;
bool DepositionExist;
float DispTimeSum;
Arr<float,2> EmFacTimeSeries;
Arr<double,1> EmissionPerSG;
bool EmissionTimeseriesExist;
double EtaMaxGral;
double EtaMinGral;
float GralDx;
float GralDy;
float GralDz;
int GrammSouth;
int GrammWest;
float GridVolume;
Arr<float,1> HOKART;
Arr<float,1> HoKartTrans;
Arr<float,1> HorSlices;
int IKOOAGRAL;
int ISTATIONAER;
int IStatistics;
int IWET;
int JKOOAGRAL;
Arr<short,2> KKART;
int LogLevel;
Arr<int,1> LogReflexions;
Arr<int,1> Log_Timesteps;
float MWindMEander;
bool MeanderingOff;
Arr<float,1> MeasurementHeight;
int MetProfileNumb;
float ModelTopHeight;
int NII;
int NJJ;
int NKK;
int NKK_Transient;
int NS;
int NX;
int NXL;
int NY;
int NYL;
Arr<float,2> OLGral;
Arr<unsigned char,2> OPP_LANE;
Arr<float,2> Ob;
Arr<float,1> ObsWindU;
Arr<float,1> ObsWindV;
bool Odour;
Arr<float,1> PS_D;
Arr<float,1> PS_HorExitDir;
Arr<float,1> PS_HorExitVel;
Arr<float,1> PS_T;
Arr<float,2> PS_TimeSerTempValues;
Arr<float,2> PS_TimeSerVelValues;
Arr<int,1> PS_TimeSeriesTemperature;
Arr<int,1> PS_TimeSeriesVelocity;
Arr<float,1> PS_V;
Arr<double,1> ParticleMass;
Arr<unsigned char,1> ParticleMode;
Arr<unsigned char,1> ParticleSG;
Arr<int,1> ParticleSource;
Arr<float,1> ParticleVdep;
Arr<float,1> ParticleVsed;
Arr<double,2> ReceptorConc;
Arr<int,1> ReceptorIInd;
Arr<int,1> ReceptorJInd;
Arr<bool,1> ReceptorNearbyBuilding;
int ReceptorNumber;
Arr<double,2> ReceptorParticleMaxConc;
Arr<double,1> ReceptorX;
Arr<double,1> ReceptorY;
Arr<float,1> ReceptorZ;
bool ReceptorsAvailable;
Arr<int,1> SourceGroups;
Arr<unsigned char,1> SourceType;
float StdDeviationV;
float StdDeviationW;
float TAUS;
Arr<float,1> TS_Area;
int TS_Count;
Arr<float,1> TS_Height;
Arr<float,1> TS_T;
Arr<float,2> TS_TimeSerTempValues;
Arr<float,2> TS_TimeSerVelValues;
Arr<int,1> TS_TimeSeriesTemperature;
Arr<int,1> TS_TimeSeriesVelocity;
Arr<float,1> TS_V;
Arr<float,1> TS_Width;
Arr<double,1> TS_X1;
Arr<double,1> TS_X2;
Arr<double,1> TS_Y1;
Arr<double,1> TS_Y2;
Arr<unsigned char,2> TUN_ENTR;
float TWindMeander;
int Topo;
Arr<int,1> TransientDepoMode;
Arr<double,1> TransientDepoVdep;
Arr<double,1> TransientDepoVsed;
bool TunnelEntr;
bool TunnelOppLane;
Arr<float,1> U0;
Arr<float,3> UK;
Arr<float,2> USternGral;
bool UseFixedRndSeedVal;
Arr<float,2> Ustern;
Arr<float,1> V0;
Arr<float,3> VK;
float VegetationDepoGasFactor;
float VegetationDepoPMFactor;
float W0int;
Arr<float,3> WK;
Arr<float,2> WetDepoDropVelocity;
float WetDepoPrecipitation;
double WetDepoRW;
bool WetDeposition;
bool WetDopoDropDrift;
bool WriteVerticalConcentration;
Arr<double,1> Xcoord;
double XsiMaxGral;
double XsiMinGral;
Arr<double,1> YCoord;
Arr<float,2> Z0Gral;
Arr<float,2> Z0Gramm;
Arr<float,1> ZCoord;
unsigned int RnGSeed1; unsigned int RnGSeed2; Arr<int,1> GridRecStart; Arr<int,1> GridRecList; int GridRecRow; bool TraceEnabled; Arr<double,2> Trace; Arr<int,1> TraceIds; Arr<double,3> StepTrace; Arr<double,2> ResidualTrace; Arr<double,2> ParticleReceptorScratch; Arr<int,2> ParticleSliceScratch; int ParticleWindowFirst; int DeviceError; int DeviceErrorPhase; int DeviceErrorParticle; };
extern "C" {__device__ State P;}
__device__ bool CheckParticleFinite(double x,double y,float z,float t,float dt,int phase,int id){if(isfinite(x)&&isfinite(y)&&isfinite(z)&&isfinite(t)&&isfinite(dt))return true;if(atomicCAS(&P.DeviceError,0,1)==0){P.DeviceErrorPhase=phase;P.DeviceErrorParticle=id;}return false;}
// GPU-only bound for the wet-deposition droplet drift loop (no CPU equivalent); code 3 aborts the run instead of stalling the kernel.
#define GRAL_DROP_DRIFT_MAX_ITER 1000000u
__device__ void RecordDeviceError(int code,int phase,int id){if(atomicCAS(&P.DeviceError,0,code)==0){P.DeviceErrorPhase=phase;P.DeviceErrorParticle=id;}}
__device__ int ParticleIndex(int index){return index==0?0:index-P.ParticleWindowFirst+1;}
__device__ Tuple2<float,float> Zeitschleife_IntStandCalculate(int nteil,float Roughness,float DiffBuilding,float windge,float sigmauHurley);
__device__ void TransientConcentration_Conz5dZeitschleife(int reflexion_flag,float zcoord_nteil,float AHint,double masse,double Area_cart,float idt,double xsi,double eta,int SG_nteil);
__device__ void TransientConcentration_Conz5dZeitschleifeTransient(int reflexion_flag,float zcoord_nteil,float AHint,double mass_real,double Area_cart,float idt,double xsi,double eta,int SG_nteil);
__device__ int TransientConcentration_BinarySearchTransient(float Height);
__device__ void Zeitschleife_Calculate(int nteil);
__device__ float Zeitschleife_Erf(float x);
__device__ float Zeitschleife_Pd(float varw,float vsed,float vdep,int Deposition_Type,int IndexI,int IndexJ);
__device__ void ZeitschleifeNonSteadyState_ParticleDriver(int i,int j,int k,int SG,float TransConcentration,int splitIndex,int trace_index);
__device__ unsigned long long ZeitschleifeNonSteadyState_MixSeed(unsigned long long seed);
__device__ void ZeitschleifeNonSteadyState_ResidualSeeds(int i,int j,int k,int SG,int splitIndex,unsigned int& m_w,unsigned int& m_z);
__device__ Tuple4<float,float,float,int> Zeitschleife_IntWindCalculate(int IndexI,int IndexJ,float AHint,double xcoord,double ycoord,float zcoord);
__device__ int Zeitschleife_BinarySearch(float Height);
__device__ float Program_FloatMax(float val1,float val2);
__device__ double Program_Pow3(double x1);
__device__ float Program_Pow3(float x1);
__device__ double Program_Pow2(double x1);
__device__ float Program_Pow2(float x1);
__device__ bool GridCellLookup(int i,int j,Arr<int,1>& r){if(!P.GridRecStart.p || P.GridRecRow<=0 || i<0 || j<0 || j>=P.GridRecRow)return false;int c=i*P.GridRecRow+j;if(c+1>=P.GridRecStart.d[0])return false;int s=P.GridRecStart[c];int e=P.GridRecStart[c+1];if(e<=s)return false;r.p=P.GridRecList.p+s;r.d[0]=e-s;return true;}
__device__ Tuple2<float,float> Zeitschleife_IntStandCalculate(int nteil,float Roughness,float DiffBuilding,float windge,float sigmauHurley) {
float U0int_5{};
float V0int_6{};
int ipo_7{};
int iprof_8{};
float help_9{};
U0int_5 = 0;
V0int_6 = 0;
if ((P.IStatistics == (0)) || (P.IStatistics == (3))) {
if (DiffBuilding <= P.MeasurementHeight[1]) {
U0int_5 = P.U0[1];
V0int_6 = P.V0[1];
} else if (DiffBuilding >= P.MeasurementHeight[P.MetProfileNumb]) {
U0int_5 = P.U0[P.MetProfileNumb];
V0int_6 = P.V0[P.MetProfileNumb];
} else {
ipo_7 = 0;
for (iprof_8 = 1;iprof_8 <= P.MetProfileNumb;iprof_8++) {
if (DiffBuilding > P.MeasurementHeight[iprof_8]) {
ipo_7 = iprof_8 + 1;
}
}
help_9 = 1 / (P.MeasurementHeight[ipo_7] - P.MeasurementHeight[ipo_7 - 1]) * (DiffBuilding - P.MeasurementHeight[ipo_7 - 1]);
U0int_5 = P.U0[ipo_7 - 1] + (P.U0[ipo_7] - P.U0[ipo_7 - 1]) * help_9;
V0int_6 = P.V0[ipo_7 - 1] + (P.V0[ipo_7] - P.V0[ipo_7 - 1]) * help_9;
}
} else {
U0int_5 = windge * (0.2F * powf((float)(windge),(float)(-0.9F)) + 0.32F * Roughness + 0.18F);
U0int_5 = Program_FloatMax(U0int_5,0.3F) * P.StdDeviationV;
V0int_6 = U0int_5;
}
if (P.SourceType[ParticleIndex(nteil)] == (0)) {
U0int_5 += gral_abs((float)(sigmauHurley));
V0int_6 += gral_abs((float)(sigmauHurley));
}
return {U0int_5,V0int_6};
}

__device__ void TransientConcentration_Conz5dZeitschleife(int reflexion_flag,float zcoord_nteil,float AHint,double masse,double Area_cart,float idt,double xsi,double eta,int SG_nteil) {
int IndexK3d_9{};
int IndexI3d_10{};
int IndexJ3d_11{};
Arr<float,1> conz5d_L_12{};
if (reflexion_flag == 0) {
IndexK3d_9 = TransientConcentration_BinarySearchTransient(zcoord_nteil - AHint);
IndexI3d_10 = cs_cast<int>((xsi / P.DXK)) + 1;
IndexJ3d_11 = cs_cast<int>((eta / P.DYK)) + 1;
conz5d_L_12 = P.Conz5d[IndexI3d_10][IndexJ3d_11][IndexK3d_9];
{
atomic_add(&(conz5d_L_12[SG_nteil]),(float)((masse * P.GridVolume * P.TAUS / (Area_cart * P.DZK_Trans[IndexK3d_9]))));
}
}
}

__device__ void TransientConcentration_Conz5dZeitschleifeTransient(int reflexion_flag,float zcoord_nteil,float AHint,double mass_real,double Area_cart,float idt,double xsi,double eta,int SG_nteil) {
int IndexK3d_9{};
int IndexI3d_10{};
int IndexJ3d_11{};
Arr<float,1> conz5d_L_12{};
if (reflexion_flag == 0) {
IndexK3d_9 = TransientConcentration_BinarySearchTransient(zcoord_nteil - AHint);
IndexI3d_10 = cs_cast<int>((xsi / P.DXK)) + 1;
IndexJ3d_11 = cs_cast<int>((eta / P.DYK)) + 1;
conz5d_L_12 = P.Conz5d[IndexI3d_10][IndexJ3d_11][IndexK3d_9];
{
atomic_add(&(conz5d_L_12[SG_nteil]),(float)((mass_real * P.TAUS / (Area_cart * P.DZK_Trans[IndexK3d_9]))));
}
}
}

__device__ int TransientConcentration_BinarySearchTransient(float Height) {
int low_1{};
int high_2{};
Arr<float,1> ho_3{};
int aQuarter_4{};
int mid_5{};
low_1 = 1;
high_2 = P.NKK_Transient;
ho_3 = P.HoKartTrans;
aQuarter_4 = high_2 >> 3;
if (ho_3[aQuarter_4] >= Height) {
high_2 = aQuarter_4;
} else {
low_1 = aQuarter_4;
}
mid_5 = 0;
while (low_1 <= high_2) {
mid_5 = (low_1 + high_2) >> 1;
if (ho_3[mid_5] >= Height) {
high_2 = mid_5 - 1;
} else {
low_1 = mid_5 + 1;
}
}
if (low_1 > P.NKK_Transient) {
low_1 = P.NKK_Transient;
}
return low_1;
}

__device__ void Zeitschleife_Calculate(int nteil) {
unsigned int m_w_2{};
unsigned int m_z_3{};
float zahl1_4{};
unsigned int u_rg_5{};
float u1_rg_6{};
double xcoord_nteil_7{};
double ycoord_nteil_8{};
float zcoord_nteil_9{};
double masse_10{};
int SG_nteil_11{};
int i_12{};
int Deposition_type_13{};
float vsed_14{};
float vdep_15{};
double area_rez_fac_16{};
double xtun_17{};
double ytun_18{};
float MeanHurley_19{};
float GHurley_20{};
float FHurley_21{};
float MHurley_22{};
float RHurley_23{};
float wpHurley_24{};
float upHurley_25{};
float DeltaZHurley_26{};
float SigmaUpHurley_27{};
float minVDI3782_28{};
float horPSExitDistance_29{};
float horPSExitDirection_30{};
float horPSExitRnd_31{};
float horPSExitVertical_32{};
float horPSExitDeltaX_33{};
float horPSExitDeltaY_34{};
float horPSExitVelocity_35{};
float horPSPlumeVelocity_36{};
int GrammCellX_37{};
int GrammCellY_38{};
int FFCellX_39{};
int FFCellY_40{};
float FFGridX_41{};
float FFGridY_42{};
float FFGridXRez_43{};
float FFGridYRez_44{};
float GrammGridXRez_45{};
float GrammGridYRez_46{};
float ConcGridXRez_47{};
float ConcGridYRez_48{};
float ConcGridZRez_49{};
float ConcGridXHalf_50{};
float ConcGridYHalf_51{};
int IFQ_52{};
int Kenn_NTeil_53{};
int SourceType_54{};
float blh_55{};
float blhRez_56{};
float blhRez2_57{};
float cachedObLength_58{};
float cachedUstern_59{};
float cachedVariance_60{};
int IKOOAGRAL_61{};
int JKOOAGRAL_62{};
Arr<int,1> kko_63{};
int receptorCount_64{};
Arr<double,1> ReceptorConcentration_65{};
int reflexion_flag_66{};
int ISTATISTIK_67{};
int ISTATIONAER_68{};
int topo_69{};
int TransientGridX_70{};
int TransientGridY_71{};
int TransientGridZ_72{};
bool emission_timeseries_73{};
double Area_cart_74{};
int reflexion_number_75{};
int timestep_number_76{};
double decay_rate_77{};
float tgesamt_78{};
unsigned int u9_79{};
float zuff1_80{};
float AHint_81{};
float AHintold_82{};
float varw_83{};
double xturb_84{};
double yturb_85{};
float idt_86{};
float auszeit_87{};
int tunfak_88{};
float DSIGUDX_89{};
float DSIGUDY_90{};
float DSIGVDX_91{};
float DSIGVDY_92{};
float DUDX_93{};
float DVDX_94{};
float DUDY_95{};
float DVDY_96{};
double xsi_97{};
double eta_98{};
double xsi1_99{};
double eta1_100{};
int Max_Loops_101{};
int Max_Reflections_102{};
int ConcCellPrevX_103{};
int ConcCellPrevY_104{};
int ConcCellPrevZ_105{};
float ConcCellTimeMax_106{};
float ConcCellTime_107{};
float distanceParticle_108{};
float PartHeightAboveTerrain_109{};
float PartHeightAboveBuilding_110{};
float UXint_111{};
float UYint_112{};
float UZint_113{};
int IndexK_114{};
float windge_115{};
float tunpa_116{};
float ObL_117{};
float roughZ0_118{};
float deltaZ_119{};
float U0int_120{};
float V0int_121{};
float velxold_122{};
float velyold_123{};
float velzold_124{};
float exitVelocity_125{};
int velTimeSeriesIndex_126{};
float exitDiameter_127{};
float exitTemperature_128{};
int tempTimeSeriesIndex_129{};
float exrnd_130{};
double vtunx_131{};
double vtuny_132{};
float tunbe_133{};
double tuncos_134{};
double tunsin_135{};
double tunqu_136{};
double tunx_137{};
double tuny_138{};
float tunbreitalt_139{};
float tunbreit_140{};
float yteilchen_141{};
double yneu_142{};
double xneuy_143{};
double yneuy_144{};
float zturb_145{};
double yold_146{};
float coruri_147{};
float corqri_148{};
float coswind_149{};
float sinwind_150{};
float UXneu_151{};
float UYneu_152{};
float xrikor_153{};
float yrikor_154{};
float buoy_155{};
float B_156{};
float HydrD_157{};
float TLT0_158{};
float faktzeit_159{};
float geschwalt_160{};
float geschwneu_161{};
float TS_ExitTemperature_162{};
float TS_ExitVelocity_163{};
int velTimeSeriesIndex_164{};
int tempTimeSeriesIndex_165{};
int depo_reflection_counter_166{};
bool TerrainStepAllowed_167{};
float ObLength_168{};
float Ustern_169{};
double xcoord_nteil_Prev_170{};
double ycoord_nteil_Prev_171{};
float zcoord_nteil_Prev_172{};
int FFCellXPrev_173{};
int FFCellYPrev_174{};
float eps_175{};
float W0int_176{};
float varw2_177{};
float skew_178{};
float curt_179{};
float dskew_180{};
float wstern_181{};
float vert_blh_182{};
float hterm_183{};
float dvarw_184{};
float dcurt_185{};
float idtt_186{};
float dummyterm_187{};
float alpha_188{};
float beta_189{};
float gamma_190{};
float acc_191{};
float velz_192{};
short _kkart_193{};
float stab_194{};
float windSpeedStandDev_195{};
float maxFac_196{};
float minFac_197{};
float sigmawpHurley_198{};
float wpold_199{};
float wpmittel_200{};
float corx_201{};
float cory_202{};
float psDiameter_203{};
float a_204{};
float param_205{};
float T3_206{};
float termp_207{};
float termq_208{};
float Tstern_209{};
float zuff1_210{};
float zuff2_211{};
float velX_212{};
float velY_213{};
float velx_214{};
float vely_215{};
float tunpao_216{};
float windpa_217{};
float windqu_218{};
float tunjettraeg_219{};
float TLW_220{};
float sigmaz_221{};
float epsa_222{};
float epst_223{};
float TE_224{};
float termw_225{};
float zuff4_226{};
double xoldy_227{};
double yoldy_228{};
int IndexIalt_229{};
int IndexJalt_230{};
int IndexKOld_231{};
float Pd1_232{};
int ik_233{};
int jk_234{};
Arr<double,1> depo_L_235{};
double conc_236{};
float Pd1_237{};
int ik_238{};
int jk_239{};
Arr<double,1> depo_L_240{};
double conc_241{};
float Pd1_242{};
int ik_243{};
int jk_244{};
Arr<double,1> depo_L_245{};
double conc_246{};
int back_247{};
int IndexXDelta_248{};
int IndexYDelta_249{};
int KKARTOld_250{};
int KKARTNew_251{};
float AHK_252{};
float AHKold_253{};
float faktor_254{};
int KKARTRecent_255{};
int IndexKOld_256{};
float Pd1_257{};
int ik_258{};
int jk_259{};
Arr<double,1> depo_L_260{};
double conc_261{};
float Pd1_262{};
int ik_263{};
int jk_264{};
Arr<double,1> depo_L_265{};
double conc_266{};
float terrainHeight_267{};
float Pd1_268{};
int ik_269{};
int jk_270{};
Arr<double,1> depo_L_271{};
double conc_272{};
double deltax_273{};
double deltay_274{};
float newTerrainHeight_275{};
int vorzeichen_276{};
int vorzeichen1_277{};
float Pd1_278{};
int ik_279{};
int jk_280{};
Arr<double,1> depo_L_281{};
double conc_282{};
int iko_283{};
int jko_284{};
float zcoordRelative_285{};
int zko_286{};
int II_287{};
float slice_288{};
double epsilonW_Masse_289{};
bool ex_290{};
Arr<double,1> depo_L_291{};
double conc_292{};
int dropDataIndex_293{};
int dropSizeClass_294{};
float u_random_295{};
double depoX_296{};
double depoY_297{};
double depoEpsilonW_298{};
double depoCellH_299{};
int depoIndexK_300{};
int depoFFX_301{};
int depoFFY_302{};
float depo1stCell_303{};
float depoVelVertical_304{};
float depoTime_305{};
float depoU_306{};
float depoV_307{};
float depoZ_308{};
float depoZcoord_309{};
float depoAH_310{};
int depoiko_311{};
int depojko_312{};
Arr<double,1> depo_L_313{};
double conc_314{};
Arr<int,1> receptors_315{};
int irec_316{};
float slice_317{};
float slice_318{};
int II_319{};
Arr<double,1> conz3d_L_320{};
double conc_321{};
Arr<double,1> conzsum_L_322{};
double conc_323{};
int II_324{};
float slice_325{};
int II_326{};
Arr<double,1> conz3dp_L_327{};
double conc_328{};
int II_329{};
float slice_330{};
int II_331{};
Arr<double,1> conz3dm_L_332{};
double conc_333{};
int ii_334{};
double recconc_335{};
Arr<double,1> recmit_L_336{};
Tuple4<float,float,float,int> tuple_tmp_0{};
Tuple2<float,float> tuple_tmp_1{};
Tuple4<float,float,float,int> tuple_tmp_2{};
Tuple2<float,float> tuple_tmp_3{};
Tuple4<float,float,float,int> tuple_tmp_4{};
int trace_slot=-1;if(P.TraceEnabled && P.TraceIds.p){for(int qi=0;qi<P.TraceIds.d[0];qi++)if(P.TraceIds[qi]==nteil)trace_slot=qi;}



{
m_w_2 = P.RnGSeed1 + (unsigned int)(nteil);
m_z_3 = P.RnGSeed2 + (unsigned int)(nteil) * 2;
}
zahl1_4 = 0;
u_rg_5 = 0;
u1_rg_6 = 0;
xcoord_nteil_7 = P.Xcoord[ParticleIndex(nteil)];if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;
ycoord_nteil_8 = P.YCoord[ParticleIndex(nteil)];if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;
zcoord_nteil_9 = P.ZCoord[ParticleIndex(nteil)];if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;
masse_10 = P.ParticleMass[ParticleIndex(nteil)];
SG_nteil_11 = P.ParticleSG[ParticleIndex(nteil)];
for (i_12 = 0;i_12 < P.SourceGroups.size();i_12++) {
if (SG_nteil_11 == P.SourceGroups[i_12]) {
SG_nteil_11 = i_12;
break;
}
}
Deposition_type_13 = P.ParticleMode[ParticleIndex(nteil)];
vsed_14 = P.ParticleVsed[ParticleIndex(nteil)];
vdep_15 = P.ParticleVdep[ParticleIndex(nteil)];
area_rez_fac_16 = P.TAUS * P.GridVolume * 24 / 1000;
area_rez_fac_16 = area_rez_fac_16 / (P.GralDx * P.GralDy);
xtun_17 = 0;
ytun_18 = 0;
MeanHurley_19 = 0.1F;
GHurley_20 = 0;
FHurley_21 = 0;
MHurley_22 = 0;
RHurley_23 = 0;
wpHurley_24 = 0;
upHurley_25 = 0;
DeltaZHurley_26 = 0;
SigmaUpHurley_27 = 0;
minVDI3782_28 = 0;
horPSExitDistance_29 = 0;
horPSExitDirection_30 = 0;
horPSExitRnd_31 = 1;
horPSExitVertical_32 = 0;
horPSExitDeltaX_33 = 0;
horPSExitDeltaY_34 = 0;
horPSExitVelocity_35 = 0;
horPSPlumeVelocity_36 = 0;
GrammCellX_37 = 1;GrammCellY_38 = 1;

FFGridX_41 = P.DXK;
FFGridY_42 = P.DYK;
FFGridXRez_43 = 1 / P.DXK;
FFGridYRez_44 = 1 / P.DYK;
GrammGridXRez_45 = 1 / P.DDX[1];
GrammGridYRez_46 = 1 / P.DDY[1];
ConcGridXRez_47 = 1 / P.GralDx;
ConcGridYRez_48 = 1 / P.GralDy;
ConcGridZRez_49 = 2 / P.GralDz;
ConcGridXHalf_50 = P.GralDx * 0.5F;
ConcGridYHalf_51 = P.GralDy * 0.5F;
IFQ_52 = P.TS_Count;
Kenn_NTeil_53 = P.ParticleSource[ParticleIndex(nteil)];
if (Kenn_NTeil_53 == 0) {
{if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;if(P.TraceEnabled && P.Trace.p && nteil<P.Trace.d[0]){P.Trace(nteil,0)=xcoord_nteil_7;P.Trace(nteil,1)=ycoord_nteil_8;P.Trace(nteil,2)=zcoord_nteil_9;P.Trace(nteil,3)=auszeit_87;P.Trace(nteil,4)=timestep_number_76;P.Trace(nteil,5)=m_w_2;P.Trace(nteil,6)=m_z_3;P.Trace(nteil,7)=masse_10;}
return ;}
}
SourceType_54 = P.SourceType[ParticleIndex(nteil)];
blh_55 = P.BdLayHeight;
blhRez_56 = 1 / blh_55;
blhRez2_57 = 1 / Program_Pow2(blh_55);
cachedObLength_58 = (__int_as_float((int)0xFFC00000));cachedUstern_59 = (__int_as_float((int)0xFFC00000));cachedVariance_60 = 0;
IKOOAGRAL_61 = P.IKOOAGRAL;
JKOOAGRAL_62 = P.JKOOAGRAL;
kko_63 = ParticleScratchRow(P.ParticleSliceScratch,P.NS);
receptorCount_64 = P.ReceptorNumber == 0 ? 0 : P.ReceptorNumber + 1;
ReceptorConcentration_65 = ParticleScratchRow(P.ParticleReceptorScratch,receptorCount_64);

reflexion_flag_66 = (0);
ISTATISTIK_67 = P.IStatistics;
ISTATIONAER_68 = P.ISTATIONAER;
topo_69 = P.Topo;
TransientGridX_70 = 1;
TransientGridY_71 = 1;
TransientGridZ_72 = 1;
emission_timeseries_73 = P.EmissionTimeseriesExist;
Area_cart_74 = P.DXK * P.DYK;
reflexion_number_75 = 0;
timestep_number_76 = 0;
decay_rate_77 = P.DecayRate[P.ParticleSG[ParticleIndex(nteil)]];
tgesamt_78 = P.DispTimeSum;
if (ISTATIONAER_68 == (0)) {
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u9_79 = (m_z_3 << 16) + m_w_2;
zuff1_80 = (float)(((u9_79 + 1.0) * 2.328306435454494e-10));
tgesamt_78 = zuff1_80 * tgesamt_78;
if (emission_timeseries_73 == true) {
if (P.IWET <= P.EmFacTimeSeries.upper(0) && SG_nteil_11 <= P.EmFacTimeSeries.upper(1)) {
masse_10 *= P.EmFacTimeSeries(P.IWET - 1,SG_nteil_11);
}
if (masse_10 <= 0) {
goto REMOVE_PARTICLE;
}
}
if (Deposition_type_13 < (2)) {
{
atomic_add(&(P.EmissionPerSG[SG_nteil_11]),masse_10);
}
}
}
AHint_81 = 0;
AHintold_82 = 0;
varw_83 = 0;
xturb_84 = 0;
yturb_85 = 0;
idt_86 = 0.1F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;
auszeit_87 = 0;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;
tunfak_88 = (1);
DSIGUDX_89 = 0;DSIGUDY_90 = 0;DSIGVDX_91 = 0;DSIGVDY_92 = 0;
DUDX_93 = 0;DVDX_94 = 0;DUDY_95 = 0;DVDY_96 = 0;
xsi_97 = xcoord_nteil_7 - IKOOAGRAL_61;
eta_98 = ycoord_nteil_8 - JKOOAGRAL_62;
xsi1_99 = xcoord_nteil_7 - P.GrammWest;
eta1_100 = ycoord_nteil_8 - P.GrammSouth;
if ((eta_98 <= P.EtaMinGral) || (xsi_97 <= P.XsiMinGral) || (eta_98 >= P.EtaMaxGral) || (xsi_97 >= P.XsiMaxGral)) {
goto REMOVE_PARTICLE;
}
Max_Loops_101 = cs_cast<int>((3E6 + gral_min((double)(9.7E7),(double)(gral_max((double)(gral_abs((double)(P.EtaMaxGral - P.EtaMinGral))),(double)(gral_abs((double)(P.XsiMaxGral - P.XsiMinGral)))) * 1000))));
Max_Reflections_102 = gral_min((int)(1000000),(int)(P.NII * P.NJJ * 10));
ConcCellPrevX_103 = -1;
ConcCellPrevY_104 = -1;
ConcCellPrevZ_105 = 0;
ConcCellTimeMax_106 = gral_max((float)(100),(float)(P.GralDx / 0.04F));
ConcCellTime_107 = 0;
distanceParticle_108 = 50;
PartHeightAboveTerrain_109 = 0;
PartHeightAboveBuilding_110 = 0;
FFCellX_39 = cs_cast<int>((xsi_97 * FFGridXRez_43)) + 1;
FFCellY_40 = cs_cast<int>((eta_98 * FFGridYRez_44)) + 1;
if ((FFCellX_39 < 1) || (FFCellX_39 > P.NII) || (FFCellY_40 < 1) || (FFCellY_40 > P.NJJ)) {
goto REMOVE_PARTICLE;
}
if (topo_69 == (1)) {
GrammCellX_37 = gral_clamp((int)(cs_cast<int>((xsi1_99 * GrammGridXRez_45)) + 1),(int)(1),(int)(P.NX));
GrammCellY_38 = gral_clamp((int)(cs_cast<int>((eta1_100 * GrammGridYRez_46)) + 1),(int)(1),(int)(P.NY));
AHint_81 = P.AHK[FFCellX_39][FFCellY_40];
PartHeightAboveTerrain_109 = zcoord_nteil_9 - AHint_81;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
} else {
AHint_81 = 0;
PartHeightAboveTerrain_109 = zcoord_nteil_9;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
}
UXint_111 = 0;UYint_112 = 0;UZint_113 = 0;
IndexK_114 = 1;
{tuple_tmp_0 = Zeitschleife_IntWindCalculate(FFCellX_39,FFCellY_40,AHint_81,xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9); UXint_111 = tuple_tmp_0.v0; UYint_112 = tuple_tmp_0.v1; UZint_113 = tuple_tmp_0.v2; IndexK_114 = tuple_tmp_0.v3;}
windge_115 = gral_max((float)(0.1F),(float)(sqrtf((float)(Program_Pow2(UXint_111) + Program_Pow2(UYint_112)))));
tunpa_116 = 0;
ObL_117 = P.Ob[GrammCellX_37][GrammCellY_38];
roughZ0_118 = P.Z0Gramm[GrammCellX_37][GrammCellY_38];
if (P.AdaptiveRoughnessMax > 0) {
ObL_117 = P.OLGral[FFCellX_39][FFCellY_40];
roughZ0_118 = P.Z0Gral[FFCellX_39][FFCellY_40];
}
deltaZ_119 = roughZ0_118;
{tuple_tmp_1 = Zeitschleife_IntStandCalculate(nteil,roughZ0_118,PartHeightAboveBuilding_110,windge_115,SigmaUpHurley_27); U0int_120 = tuple_tmp_1.v0; V0int_121 = tuple_tmp_1.v1;}
if ((PartHeightAboveBuilding_110 > blh_55) && (P.ISTATIONAER != (0)) && (ObL_117 < 0)) {
goto REMOVE_PARTICLE;
}
velxold_122 = 0;
velyold_123 = 0;
velzold_124 = 0;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velxold_122 = U0int_120 * zahl1_4;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velyold_123 = V0int_121 * zahl1_4;
{auszeit_87 = 0;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
if (SourceType_54 == (0)) {
horPSExitVelocity_35 = P.PS_HorExitVel[Kenn_NTeil_53];
exitVelocity_125 = P.PS_V[Kenn_NTeil_53];
velTimeSeriesIndex_126 = P.PS_TimeSeriesVelocity[Kenn_NTeil_53];
exitDiameter_127 = P.PS_D[Kenn_NTeil_53];
if (velTimeSeriesIndex_126 != -1) {
if ((P.IWET - 1) < P.PS_TimeSerVelValues[velTimeSeriesIndex_126].size()) {
if (P.PS_HorExitVel[Kenn_NTeil_53] > 0.1) {
horPSExitVelocity_35 = P.PS_TimeSerVelValues[velTimeSeriesIndex_126][P.IWET - 1];
} else {
exitVelocity_125 = P.PS_TimeSerVelValues[velTimeSeriesIndex_126][P.IWET - 1];
}
}
}
exitVelocity_125 = gral_max((float)(0),(float)(exitVelocity_125));
horPSExitVelocity_35 = gral_max((float)(0),(float)(horPSExitVelocity_35));
exitTemperature_128 = P.PS_T[Kenn_NTeil_53];
tempTimeSeriesIndex_129 = P.PS_TimeSeriesTemperature[Kenn_NTeil_53];
if (tempTimeSeriesIndex_129 != -1) {
if ((P.IWET - 1) < P.PS_TimeSerTempValues[tempTimeSeriesIndex_129].size()) {
exitTemperature_128 = P.PS_TimeSerTempValues[tempTimeSeriesIndex_129][P.IWET - 1] + 273;
}
}
if (P.PS_HorExitVel[Kenn_NTeil_53] > 0.1) {
exrnd_130 = gral_clamp((float)(zahl1_4 * 0.7F),(float)(-1),(float)(1));
horPSExitDirection_30 = P.PS_HorExitDir[Kenn_NTeil_53] + 8 * exrnd_130;
if (horPSExitDirection_30 < 0) horPSExitDirection_30 += 360;
if (horPSExitDirection_30 > 360) horPSExitDirection_30 -= 360;
horPSExitRnd_31 = gral_clamp((float)(1 - gral_abs((float)(exrnd_130))),(float)(0.01F),(float)(1));
horPSExitDirection_30 *= (3.1415927f) / 180;
if (exitTemperature_128 < 275) {
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
horPSExitVertical_32 = 8 * exrnd_130 * gral_sign((float)(zahl1_4));
} else {
horPSExitVertical_32 = 0;
exitTemperature_128 = 273 + (exitTemperature_128 - 273) * gral_min((float)(1),(float)(0.2F + horPSExitRnd_31));
}
horPSExitDistance_29 = 0.1F;
{xcoord_nteil_7 += P.PS_D[Kenn_NTeil_53] * 0.5 * cos((double)(P.PS_HorExitDir[Kenn_NTeil_53] * (3.1415927f) / 180)) * exrnd_130;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
{ycoord_nteil_8 += P.PS_D[Kenn_NTeil_53] * 0.5 * sin((double)(P.PS_HorExitDir[Kenn_NTeil_53] * (3.1415927f) / 180)) * exrnd_130;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
{zcoord_nteil_9 = gral_max((float)(AHint_81 + 0.1F + gral_abs((float)(P.PS_D[Kenn_NTeil_53] * 0.5F * zahl1_4))),(float)(zcoord_nteil_9 + P.PS_D[Kenn_NTeil_53] * 0.5F * zahl1_4));if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
exitVelocity_125 = horPSExitVelocity_35 * 0.019128F;
exitDiameter_127 = exitDiameter_127 * 7.2305086F;
horPSPlumeVelocity_36 = horPSExitVelocity_35;
}
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = (u_rg_5 + 1) * (2.3283064E-10f);
exitTemperature_128 = gral_max((float)(273),(float)(exitTemperature_128 * (1.05F - u1_rg_6 * 0.2F)));
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
FHurley_21 = 9.81F * exitVelocity_125 * Program_Pow2(exitDiameter_127 * 0.5F) * (exitTemperature_128 - 273.0f) / exitTemperature_128;
GHurley_20 = 273 / exitTemperature_128 * exitVelocity_125 * Program_Pow2(exitDiameter_127 * 0.5F);
MHurley_22 = GHurley_20 * exitVelocity_125;
RHurley_23 = sqrtf((float)(exitVelocity_125 / sqrtf((float)(Program_Pow2(windge_115) + Program_Pow2(exitVelocity_125)))));
wpHurley_24 = exitVelocity_125;
upHurley_25 = sqrtf((float)(Program_Pow2(windge_115) + Program_Pow2(wpHurley_24)));
MeanHurley_19 = 0;
DeltaZHurley_26 = 0;
if (horPSExitDistance_29 > 0) {
minVDI3782_28 = 0;
} else {
minVDI3782_28 = 3 * exitVelocity_125 * exitDiameter_127 / windge_115;
}
}
vtunx_131 = 0;
vtuny_132 = 0;
tunbe_133 = 0;
tuncos_134 = 0;
tunsin_135 = 0;
tunqu_136 = 0;
tunx_137 = 0;
tuny_138 = 0;
tunbreitalt_139 = 0;
tunbreit_140 = 0;
yteilchen_141 = 0;
yneu_142 = 0;
xneuy_143 = 0;
yneuy_144 = 0;
zturb_145 = 0;
yold_146 = 0;
coruri_147 = 0;
corqri_148 = 0;
coswind_149 = 0;
sinwind_150 = 0;
UXneu_151 = 0;
UYneu_152 = 0;
xrikor_153 = 0;
yrikor_154 = 0;
buoy_155 = 0;
B_156 = 0;
HydrD_157 = 0;
TLT0_158 = 0;
faktzeit_159 = 1;
geschwalt_160 = 0;
geschwneu_161 = 0;
TS_ExitTemperature_162 = 0;
TS_ExitVelocity_163 = 0;
if (SourceType_54 == (1)) {
TS_ExitVelocity_163 = P.TS_V[Kenn_NTeil_53];
velTimeSeriesIndex_164 = P.TS_TimeSeriesVelocity[Kenn_NTeil_53];
if (velTimeSeriesIndex_164 != -1) {
if ((P.IWET - 1) < P.TS_TimeSerVelValues[velTimeSeriesIndex_164].size()) {
TS_ExitVelocity_163 = P.TS_TimeSerVelValues[velTimeSeriesIndex_164][P.IWET - 1];
}
}
TS_ExitTemperature_162 = P.TS_T[Kenn_NTeil_53];
tempTimeSeriesIndex_165 = P.TS_TimeSeriesTemperature[Kenn_NTeil_53];
if (tempTimeSeriesIndex_165 != -1) {
if ((P.IWET - 1) < P.TS_TimeSerTempValues[tempTimeSeriesIndex_165].size()) {
TS_ExitTemperature_162 = P.TS_TimeSerTempValues[tempTimeSeriesIndex_165][P.IWET - 1];
}
}
vtunx_131 = TS_ExitVelocity_163 * (P.TS_Y2[Kenn_NTeil_53] - P.TS_Y1[Kenn_NTeil_53]) / P.TS_Width[Kenn_NTeil_53];
vtuny_132 = TS_ExitVelocity_163 * (P.TS_X1[Kenn_NTeil_53] - P.TS_X2[Kenn_NTeil_53]) / P.TS_Width[Kenn_NTeil_53];
tunbe_133 = (float)(sqrt((double)(Program_Pow2(vtunx_131) + Program_Pow2(vtuny_132))));
tuncos_134 = vtunx_131 / (tunbe_133 + 0.01F);
tunsin_135 = vtuny_132 / (tunbe_133 + 0.01F);
tunqu_136 = 0;
tunpa_116 = tunbe_133;
tunfak_88 = (0);
tunx_137 = tunpa_116 * tuncos_134 - tunqu_136 * tunsin_135;
tuny_138 = tunpa_116 * tunsin_135 + tunqu_136 * tuncos_134;
tunbreitalt_139 = Program_FloatMax(P.TS_Width[Kenn_NTeil_53],0.00001F);
tunbreit_140 = tunbreitalt_139;
xtun_17 = 0.5F * (P.TS_X1[Kenn_NTeil_53] + P.TS_X2[Kenn_NTeil_53]);
ytun_18 = 0.5F * (P.TS_Y1[Kenn_NTeil_53] + P.TS_Y2[Kenn_NTeil_53]);
yteilchen_141 = (float)((-xcoord_nteil_7 * tunsin_135 + ycoord_nteil_8 * tuncos_134 + xtun_17 * tunsin_135 - ytun_18 * tuncos_134));
yold_146 = yteilchen_141;
yneu_142 = yold_146;
xneuy_143 = xtun_17 - yteilchen_141 * tunsin_135;
yneuy_144 = ytun_18 + yteilchen_141 * tuncos_134;
zturb_145 = 0;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
coruri_147 = windge_115 + U0int_120 * (float)(zahl1_4);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
corqri_148 = V0int_121 * zahl1_4;
coswind_149 = UXint_111 / windge_115;
sinwind_150 = UYint_112 / windge_115;
UXneu_151 = coruri_147 * coswind_149 - corqri_148 * sinwind_150;
UYneu_152 = coruri_147 * sinwind_150 + corqri_148 * coswind_149;
if (UXint_111 == 0) {
UXint_111 = 0.001F;
}
if (UYint_112 == 0) {
UYint_112 = 0.001F;
}
xrikor_153 = UXneu_151 / UXint_111;
yrikor_154 = UYneu_152 / UYint_112;
buoy_155 = 0;
B_156 = TS_ExitTemperature_162 / 283 * 9.81F;
HydrD_157 = 4 * P.TS_Height[Kenn_NTeil_53] * P.TS_Width[Kenn_NTeil_53] / (2 * (P.TS_Height[Kenn_NTeil_53] + P.TS_Width[Kenn_NTeil_53]));
if (B_156 >= 0) {
TLT0_158 = sqrtf((float)(HydrD_157 / (1.8F * (B_156 + 0.00001F))));
} else {
B_156 = 0;
}
AHintold_82 = AHint_81;
}
depo_reflection_counter_166 = -5;
TerrainStepAllowed_167 = true;
ObLength_168 = 0;
Ustern_169 = 0;
MOVE_FORWARD: ;
while (timestep_number_76 <= Max_Loops_101) {
{++timestep_number_76;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;if(trace_slot>=0 && timestep_number_76<=P.StepTrace.d[1]){P.StepTrace[trace_slot][timestep_number_76-1][0]=xcoord_nteil_7;P.StepTrace[trace_slot][timestep_number_76-1][1]=ycoord_nteil_8;P.StepTrace[trace_slot][timestep_number_76-1][2]=zcoord_nteil_9;P.StepTrace[trace_slot][timestep_number_76-1][3]=auszeit_87;P.StepTrace[trace_slot][timestep_number_76-1][4]=idt_86;P.StepTrace[trace_slot][timestep_number_76-1][5]=velxold_122;P.StepTrace[trace_slot][timestep_number_76-1][6]=velyold_123;P.StepTrace[trace_slot][timestep_number_76-1][7]=velzold_124;P.StepTrace[trace_slot][timestep_number_76-1][8]=m_w_2;P.StepTrace[trace_slot][timestep_number_76-1][9]=m_z_3;P.StepTrace[trace_slot][timestep_number_76-1][10]=FFCellX_39;P.StepTrace[trace_slot][timestep_number_76-1][11]=FFCellY_40;P.StepTrace[trace_slot][timestep_number_76-1][12]=IndexK_114;P.StepTrace[trace_slot][timestep_number_76-1][13]=ObLength_168;P.StepTrace[trace_slot][timestep_number_76-1][14]=roughZ0_118;P.StepTrace[trace_slot][timestep_number_76-1][15]=windge_115;}}
xcoord_nteil_Prev_170 = xcoord_nteil_7;
ycoord_nteil_Prev_171 = ycoord_nteil_8;
zcoord_nteil_Prev_172 = zcoord_nteil_9;
FFCellXPrev_173 = FFCellX_39;
FFCellYPrev_174 = FFCellY_40;
if (P.TunnelEntr == true) {
if ((P.TUN_ENTR[FFCellX_39][FFCellY_40] == 1) && (PartHeightAboveTerrain_109 <= 5)) {
goto REMOVE_PARTICLE;
}
}
{tuple_tmp_2 = Zeitschleife_IntWindCalculate(FFCellX_39,FFCellY_40,AHint_81,xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9); UXint_111 = tuple_tmp_2.v0; UYint_112 = tuple_tmp_2.v1; UZint_113 = tuple_tmp_2.v2; IndexK_114 = tuple_tmp_2.v3;}
windge_115 = gral_max((float)(0.01F),(float)(sqrtf((float)(Program_Pow2(UXint_111) + Program_Pow2(UYint_112)))));
if (PartHeightAboveTerrain_109 < 0) {
goto REMOVE_PARTICLE;
}
if (P.AdaptiveRoughnessMax > 0) {
ObLength_168 = P.OLGral[FFCellX_39][FFCellY_40];
Ustern_169 = P.USternGral[FFCellX_39][FFCellY_40];
roughZ0_118 = P.Z0Gral[FFCellX_39][FFCellY_40];
} else {
ObLength_168 = P.Ob[GrammCellX_37][GrammCellY_38];
Ustern_169 = P.Ustern[GrammCellX_37][GrammCellY_38];
roughZ0_118 = P.Z0Gramm[GrammCellX_37][GrammCellY_38];
}
deltaZ_119 = roughZ0_118;
if (deltaZ_119 < PartHeightAboveBuilding_110) {
deltaZ_119 = PartHeightAboveBuilding_110;
}
eps_175 = Program_Pow3(Ustern_169) / deltaZ_119 / 0.4F * powf((float)(1.0f + 0.5F * powf((float)(PartHeightAboveBuilding_110 / gral_abs((float)(ObLength_168))),(float)(0.8F))),(float)(1.8F));
depo_reflection_counter_166++;
W0int_176 = 0;
varw2_177 = 0;
skew_178 = 0;
curt_179 = 0;
dskew_180 = 0;
wstern_181 = Ustern_169 * 2.9F;
vert_blh_182 = PartHeightAboveBuilding_110 * blhRez_56;
hterm_183 = (1 - vert_blh_182);
dvarw_184 = 0;
dcurt_185 = 0;
if (ObLength_168 >= 0) {
if (ISTATISTIK_67 != (3)) {
W0int_176 = Ustern_169 * P.StdDeviationW * 1.25F;
} else {
W0int_176 = P.W0int;
}
varw_83 = Program_Pow2(W0int_176);
varw2_177 = Program_Pow2(varw_83);
curt_179 = 3 * varw2_177;
} else {
if (ISTATISTIK_67 == (3)) {
varw_83 = Program_Pow2(P.W0int);
} else {
if (ObLength_168 != cachedObLength_58 || Ustern_169 != cachedUstern_59) {
cachedVariance_60 = Program_Pow2(Ustern_169) * Program_Pow2(1.15F + 0.1F * powf((float)(blh_55 / (-ObLength_168)),(float)(0.67F))) * Program_Pow2(P.StdDeviationW);
cachedObLength_58 = ObLength_168;
cachedUstern_59 = Ustern_169;
}
varw_83 = cachedVariance_60;
}
varw2_177 = Program_Pow2(varw_83);
if ((PartHeightAboveBuilding_110 < P.BdLayH10) || (PartHeightAboveBuilding_110 >= blh_55)) {
skew_178 = 0;
dskew_180 = 0;
} else {
skew_178 = (1.1f) * PartHeightAboveBuilding_110 * Program_Pow2(hterm_183) * Program_Pow3(wstern_181) * blhRez_56;
dskew_180 = (1.1f) * Program_Pow3(wstern_181) * blhRez_56 * Program_Pow2(hterm_183) - 2 * (1.1f) * Program_Pow3(wstern_181) * PartHeightAboveBuilding_110 * hterm_183 * blhRez2_57;
}
if (PartHeightAboveBuilding_110 < P.BdLayH10) {
curt_179 = 3 * varw2_177;
} else {
curt_179 = 3.5F * varw2_177;
}
}
if (tunfak_88 == (1)) {
idt_86 = 0.1F * varw_83 / eps_175;
if (SourceType_54 == (0) && auszeit_87 < 20) {
{idt_86 = gral_min((float)(0.2F),(float)(idt_86));if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
if (horPSExitDistance_29 > 0 && auszeit_87 < 3) {
{idt_86 = gral_round((float)(gral_max((float)(0.01F),(float)(gral_min((float)(0.2F),(float)(0.5F / (5 + horPSPlumeVelocity_36)))))),(int)(3));if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
}
idtt_186 = 0.5F * gral_min((float)(P.GralDx),(float)(FFGridX_41)) / (windge_115 + sqrtf((float)(Program_Pow2(velxold_122) + Program_Pow2(velyold_123))));
if (idtt_186 < idt_86) {
{idt_86 = idtt_186;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
{gral_clamp((float)(idt_86),(float)(0.1F),(float)(4.0f));if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
dummyterm_187 = curt_179 - Program_Pow2(skew_178) / varw_83 - varw2_177;
if (dummyterm_187 == 0) {
dummyterm_187 = 0.000001F;
}
alpha_188 = (0.3333F * dcurt_185 - skew_178 / (2 * varw_83) * (dskew_180 - P.C0z * eps_175) - varw_83 * dvarw_184) / dummyterm_187;
gral_clamp((float)(alpha_188),(float)(-0.1F),(float)(0.1F));
beta_189 = (dskew_180 - 2 * skew_178 * alpha_188 - P.C0z * eps_175) / (2 * varw_83);
gamma_190 = dvarw_184 - varw_83 * alpha_188;
acc_191 = alpha_188 * Program_Pow2(velzold_124) + beta_189 * velzold_124 + gamma_190;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
gral_clamp((float)(zahl1_4),(float)(-2),(float)(2));
velz_192 = acc_191 * idt_86 + sqrtf((float)(P.C0z * eps_175 * idt_86)) * zahl1_4 + velzold_124;
if (velz_192 == 0) {
velz_192 = 0.01F;
}
_kkart_193 = P.KKART[FFCellX_39][FFCellY_40];
if ((P.KKART[FFCellX_39 - 1][FFCellY_40] > _kkart_193) && (IndexK_114 <= P.KKART[FFCellX_39 - 1][FFCellY_40]) && (P.CUTK[FFCellX_39 - 1][FFCellY_40] > 0)) {
velz_192 += velz_192 / gral_abs((float)(velz_192)) * 0.67F * Program_FloatMax(-P.UK[FFCellX_39 + 1][FFCellY_40][IndexK_114],0);
}
if ((P.KKART[FFCellX_39 + 1][FFCellY_40] > _kkart_193) && (IndexK_114 <= P.KKART[FFCellX_39 + 1][FFCellY_40]) && (P.CUTK[FFCellX_39 + 1][FFCellY_40] > 0)) {
velz_192 += velz_192 / gral_abs((float)(velz_192)) * 0.67F * Program_FloatMax(P.UK[FFCellX_39][FFCellY_40][IndexK_114],0);
}
if ((P.KKART[FFCellX_39][FFCellY_40 - 1] > _kkart_193) && (IndexK_114 <= P.KKART[FFCellX_39][FFCellY_40 - 1]) && (P.CUTK[FFCellX_39][FFCellY_40 - 1] > 0)) {
velz_192 += velz_192 / gral_abs((float)(velz_192)) * 0.67F * Program_FloatMax(-P.VK[FFCellX_39][FFCellY_40 + 1][IndexK_114],0);
}
if ((P.KKART[FFCellX_39][FFCellY_40 + 1] > _kkart_193) && (IndexK_114 <= P.KKART[FFCellX_39][FFCellY_40 + 1]) && (P.CUTK[FFCellX_39][FFCellY_40 + 1] > 0)) {
velz_192 += velz_192 / gral_abs((float)(velz_192)) * 0.67F * Program_FloatMax(P.VK[FFCellX_39][FFCellY_40][IndexK_114],0);
}
if (ObLength_168 >= 0) {
gral_clamp((float)(velz_192),(float)(-3),(float)(3));
} else {
gral_clamp((float)(velz_192),(float)(-5),(float)(5));
}
if (SourceType_54 == (0) && wpHurley_24 > 0) {
DeltaZHurley_26 = 0;
SigmaUpHurley_27 = 0;
if (1.5F * Program_Pow3(wpHurley_24) / MeanHurley_19 <= eps_175) {
wpHurley_24 = 0;
} else {
stab_194 = 0;
if (ObLength_168 >= 0) {
stab_194 = 0.04F * powf((float)(2.73F),(float)(-ObLength_168 * 0.05F));
}
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
windSpeedStandDev_195 = 0;
if (auszeit_87 < 3) {
maxFac_196 = gral_max((float)(8 - windge_115 * 0.3F),(float)(1.2F));
minFac_197 = gral_min((float)(windge_115 * 0.06F),(float)(0.8F));
windSpeedStandDev_195 = windge_115 * gral_clamp((float)(1 + (windge_115 * 0.35F + 0.25F) * zahl1_4),(float)(minFac_197),(float)(maxFac_196));
} else {
windSpeedStandDev_195 = windge_115 * gral_clamp((float)(1 + (windge_115 * 0.31F + 0.25F) * zahl1_4),(float)(0.1F),(float)(8));
}
GHurley_20 += 2 * RHurley_23 * ((0.1f) * Program_Pow2(wpHurley_24) + (0.6f) * windSpeedStandDev_195 * wpHurley_24 + 0.1F * upHurley_25 * sqrtf((float)(0.5F * (Program_Pow2(velxold_122) + Program_Pow2(velyold_123))))) * idt_86;
FHurley_21 += -0.035934F * stab_194 * MHurley_22 / upHurley_25 * (0.4444444F * windSpeedStandDev_195 + wpHurley_24) * idt_86;
MHurley_22 += FHurley_21 * idt_86;
RHurley_23 = sqrtf((float)((GHurley_20 + FHurley_21 * 0.1020408F) / upHurley_25));
upHurley_25 = sqrtf((float)(Program_Pow2(windSpeedStandDev_195) + Program_Pow2(wpHurley_24)));
sigmawpHurley_198 = ((0.1f) * Program_Pow2(wpHurley_24) + (0.6f) * windSpeedStandDev_195 * wpHurley_24) / (4.2F * upHurley_25);
SigmaUpHurley_27 = 2 * sigmawpHurley_198;
wpold_199 = wpHurley_24;
wpHurley_24 = Program_FloatMax(MHurley_22 / GHurley_20,0);
wpmittel_200 = (wpHurley_24 + wpold_199) * 0.5F;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
if (auszeit_87 < 0.6F) {
wpold_199 *= expf((float)(-(auszeit_87 + idt_86)));
if (wpHurley_24 < wpold_199) {
if (MeanHurley_19 < minVDI3782_28) {
wpHurley_24 = gral_min((float)(wpold_199),(float)((minVDI3782_28 - MeanHurley_19) / idt_86));
wpmittel_200 = wpHurley_24;
}
}
DeltaZHurley_26 = gral_max((float)(0),(float)((wpmittel_200 + sigmawpHurley_198 * zahl1_4) * idt_86));
} else {
DeltaZHurley_26 = gral_max((float)(0),(float)((wpmittel_200 + sigmawpHurley_198 * zahl1_4) * idt_86));
}
MeanHurley_19 += DeltaZHurley_26;
}
if (tunfak_88 == (1)) {
{zcoord_nteil_9 = zcoord_nteil_9 + (velz_192 + UZint_113) * idt_86 + DeltaZHurley_26;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
velzold_124 = velz_192;
} else {
if (tunfak_88 == (1)) {
{zcoord_nteil_9 += (velz_192 + UZint_113) * idt_86;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
velzold_124 = velz_192;
}
if (Deposition_type_13 > (0) && vsed_14 > 0) {
{zcoord_nteil_9 -= vsed_14 * idt_86;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
{auszeit_87 += idt_86;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
if (ISTATIONAER_68 == (0)) {
if (auszeit_87 >= tgesamt_78) {
if (SourceType_54 == (0)) {
if (wpHurley_24 < 0.01) {
TransientConcentration_Conz5dZeitschleife(reflexion_flag_66,zcoord_nteil_9,AHint_81,masse_10,Area_cart_74,idt_86,xsi_97,eta_98,SG_nteil_11);
goto REMOVE_PARTICLE;
}
} else if (SourceType_54 == (1)) {
if (tunfak_88 == (1)) {
TransientConcentration_Conz5dZeitschleife(reflexion_flag_66,zcoord_nteil_9,AHint_81,masse_10,Area_cart_74,idt_86,xsi_97,eta_98,SG_nteil_11);
goto REMOVE_PARTICLE;
}
} else {
TransientConcentration_Conz5dZeitschleife(reflexion_flag_66,zcoord_nteil_9,AHint_81,masse_10,Area_cart_74,idt_86,xsi_97,eta_98,SG_nteil_11);
goto REMOVE_PARTICLE;
}
}
}
if (zcoord_nteil_9 >= P.ModelTopHeight) {
goto REMOVE_PARTICLE;
}
corx_201 = velxold_122 * idt_86;
cory_202 = velyold_123 * idt_86;
horPSExitDeltaX_33 = 0;
horPSExitDeltaY_34 = 0;
if (horPSExitDistance_29 > 0) {
psDiameter_203 = P.PS_D[Kenn_NTeil_53];
a_204 = 0.2f + 0.02f * windge_115;
if (horPSExitDistance_29 <= psDiameter_203 * 6) {
horPSPlumeVelocity_36 = horPSExitVelocity_35 * 0.48F / (a_204 * horPSExitDistance_29 / psDiameter_203 + 0.145F);
if (horPSPlumeVelocity_36 < horPSExitVelocity_35) {
horPSPlumeVelocity_36 *= horPSExitRnd_31;
} else {
horPSPlumeVelocity_36 = horPSExitVelocity_35 * horPSExitRnd_31;
}
} else {
horPSPlumeVelocity_36 = horPSExitVelocity_35 * 0.48F / (a_204 * horPSExitDistance_29 / psDiameter_203 + 0.145F) * psDiameter_203 * psDiameter_203 * 36 / (horPSExitDistance_29 * horPSExitDistance_29);
}
if (horPSPlumeVelocity_36 > 0.5F) {
horPSExitDeltaX_33 = -horPSPlumeVelocity_36 * idt_86 * sinf((float)(horPSExitDirection_30));
horPSExitDeltaY_34 = -horPSPlumeVelocity_36 * idt_86 * cosf((float)(horPSExitDirection_30));
if (horPSExitDistance_29 < 6 * psDiameter_203) {
{zcoord_nteil_9 = zcoord_nteil_9 + horPSPlumeVelocity_36 * idt_86 * horPSExitVertical_32 * 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
horPSExitDistance_29 += horPSPlumeVelocity_36 * idt_86;
} else {
horPSExitDistance_29 = 0;
}
}
{tuple_tmp_3 = Zeitschleife_IntStandCalculate(nteil,roughZ0_118,PartHeightAboveBuilding_110,windge_115,SigmaUpHurley_27); U0int_120 = tuple_tmp_3.v0; V0int_121 = tuple_tmp_3.v1;}
param_205 = 0;
if (ISTATISTIK_67 != (3)) {
param_205 = Program_FloatMax(8.5F / Program_Pow2(windge_115 + 1),0.0f);
} else {
param_205 = P.MWindMEander;
}
T3_206 = 0;
termp_207 = 0;
termq_208 = 0;
if (windge_115 >= 2.5F || P.MeanderingOff || param_205 == 0 || P.TAUS <= 600) {
T3_206 = 2 * Program_Pow2(V0int_121) / P.C0x / eps_175;
termp_207 = 1 / T3_206;
} else {
termp_207 = 1 / (2 * Program_Pow2(V0int_121) / P.C0x / eps_175);
if (ISTATISTIK_67 != (3)) {
Tstern_209 = 200 * param_205 + 350;
T3_206 = Tstern_209 / 6.28F / (Program_Pow2(param_205) + 1) * param_205;
} else if (ISTATISTIK_67 == (3)) {
T3_206 = P.TWindMeander;
}
termq_208 = param_205 / (Program_Pow2(param_205) + 1) / T3_206;
}
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zuff1_210 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
gral_clamp((float)(zuff1_210),(float)(-2),(float)(2));
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zuff2_211 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
gral_clamp((float)(zuff2_211),(float)(-2),(float)(2));
velX_212 = velxold_122 + UXint_111;
velY_213 = velyold_123 + UYint_112;
{
velx_214 = velxold_122 - (termp_207 * velxold_122 + termq_208 * velyold_123) * idt_86 + U0int_120 * sqrtf((float)(2 * termp_207 * idt_86)) * zuff1_210 + idt_86 * (DUDX_93 * velX_212 + DUDY_95 * velY_213 + U0int_120 * DSIGUDX_89) + idt_86 * (velxold_122 / U0int_120 * (DSIGUDX_89 * velX_212 + DSIGUDY_90 * velY_213));
vely_215 = velyold_123 - (termp_207 * velyold_123 - termq_208 * velxold_122) * idt_86 + V0int_121 * sqrtf((float)(2 * termp_207 * idt_86)) * zuff2_211 + idt_86 * (DVDX_94 * velX_212 + DVDY_96 * velY_213 + V0int_121 * DSIGVDY_92) + idt_86 * (velyold_123 / V0int_121 * (DSIGVDX_91 * velX_212 + DSIGVDY_92 * velY_213));
velxold_122 = velx_214;
velyold_123 = vely_215;
}
if (tunfak_88 == (1)) {
{xcoord_nteil_7 += idt_86 * UXint_111 + corx_201 + horPSExitDeltaX_33;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
{ycoord_nteil_8 += idt_86 * UYint_112 + cory_202 + horPSExitDeltaY_34;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
if (tunfak_88 == (0)) {
if ((gral_abs((double)(UXint_111 - tunx_137)) < 0.01) && (gral_abs((double)(UYint_112 - tuny_138)) < 0.01)) {
tunfak_88 = (1);
}
if (tunpa_116 <= 0) {
tunfak_88 = (1);
}
if (P.TunnelOppLane == true) {
if ((P.OPP_LANE[FFCellX_39][FFCellY_40] == 1) && (PartHeightAboveTerrain_109 <= 4)) {
tunfak_88 = (1);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velxold_122 = 2 * zahl1_4;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velyold_123 = 2 * zahl1_4;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velzold_124 = 2 * zahl1_4;
goto MOVE_FORWARD;
}
}
UXint_111 = UXneu_151;
UYint_112 = UYneu_152;
{idt_86 = P.GralDx / (tunpa_116 + 0.05F) * 0.5F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
gral_clamp((float)(idt_86),(float)(0.05F),(float)(0.5F));
tunpao_216 = tunpa_116;
{auszeit_87 += idt_86;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
windpa_217 = (float)((UXint_111 * tuncos_134 + UYint_112 * tunsin_135));
windqu_218 = (float)((-UXint_111 * tunsin_135 + UYint_112 * tuncos_134));
if (windqu_218 == 0) {
windqu_218 = 0.000001F;
}
tunjettraeg_219 = 1;
tunjettraeg_219 = 5 * expf((float)(-0.005F * P.TS_Area[Kenn_NTeil_53] * TS_ExitVelocity_163));
tunjettraeg_219 = Program_FloatMax(tunjettraeg_219,0.05F);
tunjettraeg_219 = gral_min((float)(tunjettraeg_219),(float)(1.5F));
tunqu_136 = idt_86 * 0.5F * Program_Pow2(windqu_218) * gral_abs((float)(windqu_218)) / windqu_218 * tunjettraeg_219;
tunjettraeg_219 = 0.3F;
tunpa_116 -= auszeit_87 * idt_86 * (tunpa_116 - windpa_217) / (Program_Pow2(tunbreitalt_139)) * tunjettraeg_219;
if (tunpa_116 < 0) {
tunpa_116 = 0;
tunfak_88 = (1);
}
tunx_137 = tunpa_116 * tuncos_134 - tunqu_136 * tunsin_135;
tuny_138 = tunpa_116 * tunsin_135 + tunqu_136 * tuncos_134;
xtun_17 += idt_86 * tunx_137;
ytun_18 += idt_86 * tuny_138;
tuncos_134 = (float)((tunx_137 / (sqrt((double)(Program_Pow2(tunx_137) + Program_Pow2(tuny_138) + 0.001F)))));
tunsin_135 = (float)((tuny_138 / (sqrt((double)(Program_Pow2(tunx_137) + Program_Pow2(tuny_138) + 0.001F)))));
if (tunpao_216 > tunpa_116) {
tunbreit_140 = tunbreitalt_139 * (gral_abs((float)(tunpao_216 / (tunpa_116 + 0.01F))));
}
yold_146 = yneu_142;
yneu_142 = yold_146 + yold_146 / tunbreitalt_139 * (tunbreitalt_139 - tunbreitalt_139);
tunbreitalt_139 = Program_FloatMax(tunbreit_140,0.000001F);
TLW_220 = 2 * PartHeightAboveBuilding_110 / (tunpa_116 + 0.01F);
sigmaz_221 = 1.25F * Ustern_169;
epsa_222 = (Program_Pow3(Ustern_169) / 0.4F / Program_FloatMax(PartHeightAboveBuilding_110,P.Z0Gramm[GrammCellX_37][GrammCellY_38]) * powf((float)(1 + 0.5F * powf((float)(PartHeightAboveBuilding_110 / gral_abs((float)(ObLength_168))),(float)(0.8F))),(float)(1.8F)));
epst_223 = 0.06F * Program_Pow2(tunpa_116) / TLW_220;
if (TS_ExitTemperature_162 < 0) {
epst_223 = gral_max((float)(epst_223 / (10 * gral_max((float)(0.1F),(float)(Program_Pow2(TS_ExitTemperature_162))))),(float)(epst_223 * 0.01F));
} else {
epst_223 *= (1 + TS_ExitTemperature_162 * 0.5F) * 0.5F;
}
TE_224 = 2 * Program_Pow2(sigmaz_221) / P.C0z / epsa_222;
termw_225 = 1 / TLW_220;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
zuff4_226 = idt_86 * zahl1_4;
buoy_155 += -buoy_155 * termw_225 * idt_86 + zuff4_226 * sqrtf((float)(epst_223));
zturb_145 = buoy_155 * idt_86;
if (TS_ExitTemperature_162 >= 0) {
zturb_145 = buoy_155 * idt_86 + velz_192 * idt_86;
}
{zcoord_nteil_9 += zturb_145;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
xoldy_227 = xneuy_143;
yoldy_228 = yneuy_144;
xneuy_143 = xtun_17 - yteilchen_141 * tunsin_135;
yneuy_144 = ytun_18 + yteilchen_141 * tuncos_134;
xturb_84 += corx_201;
yturb_85 += cory_202;
{xcoord_nteil_7 = xtun_17 - yneu_142 * tunsin_135 + xturb_84;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
{ycoord_nteil_8 = ytun_18 + yneu_142 * tuncos_134 + yturb_85;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
xsi_97 = xcoord_nteil_7 - IKOOAGRAL_61;
eta_98 = ycoord_nteil_8 - JKOOAGRAL_62;
xsi1_99 = xcoord_nteil_7 - P.GrammWest;
eta1_100 = ycoord_nteil_8 - P.GrammSouth;
if ((eta_98 <= P.EtaMinGral) || (xsi_97 <= P.XsiMinGral) || (eta_98 >= P.EtaMaxGral) || (xsi_97 >= P.XsiMaxGral)) {
goto REMOVE_PARTICLE;
}
IndexIalt_229 = 1;
IndexJalt_230 = 1;
if (topo_69 == (1)) {
IndexIalt_229 = FFCellX_39;
IndexJalt_230 = FFCellY_40;
FFCellX_39 = cs_cast<int>((xsi_97 * FFGridXRez_43)) + 1;
FFCellY_40 = cs_cast<int>((eta_98 * FFGridYRez_44)) + 1;
if ((FFCellX_39 < 1) || (FFCellX_39 > P.NII) || (FFCellY_40 < 1) || (FFCellY_40 > P.NJJ)) {
goto REMOVE_PARTICLE;
}
GrammCellX_37 = gral_clamp((int)(cs_cast<int>((xsi1_99 * GrammGridXRez_45)) + 1),(int)(1),(int)(P.NX));
GrammCellY_38 = gral_clamp((int)(cs_cast<int>((eta1_100 * GrammGridYRez_46)) + 1),(int)(1),(int)(P.NY));
AHintold_82 = AHint_81;
AHint_81 = P.AHK[FFCellX_39][FFCellY_40];
PartHeightAboveTerrain_109 = zcoord_nteil_9 - AHint_81;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
} else {
FFCellX_39 = cs_cast<int>((xsi_97 * FFGridXRez_43)) + 1;
FFCellY_40 = cs_cast<int>((eta_98 * FFGridYRez_44)) + 1;
if ((FFCellX_39 > P.NII) || (FFCellX_39 > P.NJJ) || (FFCellY_40 < 1) || (FFCellY_40 < 1)) {
goto REMOVE_PARTICLE;
}
AHintold_82 = AHint_81;
PartHeightAboveTerrain_109 = zcoord_nteil_9;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
}
if (zcoord_nteil_9 >= P.ModelTopHeight) {
goto REMOVE_PARTICLE;
}
if (topo_69 == (1)) {
if ((P.CUTK[FFCellX_39][FFCellY_40] + P.CUTK[FFCellXPrev_173][FFCellYPrev_174]) == 0) {
{zcoord_nteil_9 += (AHint_81 - AHintold_82);if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
}
geschwneu_161 = (float)((sqrt((double)(Program_Pow2(xneuy_143 - xoldy_227) + Program_Pow2(yneuy_144 - yoldy_228))) / idt_86));
if (geschwalt_160 == 0) {
geschwalt_160 = geschwneu_161;
}
faktzeit_159 = Program_FloatMax(faktzeit_159 * geschwneu_161 / (geschwalt_160 + 0.001F),0.001F);
{idt_86 *= faktzeit_159;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
if (idt_86 < 0.01) {
{idt_86 = 0.01f;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
geschwalt_160 = geschwneu_161;
if (topo_69 == (1)) {
if (zcoord_nteil_9 >= P.ModelTopHeight) {
goto REMOVE_PARTICLE;
}
}
reflexion_flag_66 = (0);
IndexKOld_231 = 0;
if (topo_69 == (1)) {
if (P.BuildingsExist == true) {
IndexKOld_231 = IndexK_114;
IndexK_114 = Zeitschleife_BinarySearch(zcoord_nteil_9 - P.AHMIN);
if ((IndexK_114 <= P.KKART[FFCellX_39][FFCellY_40]) && (P.CUTK[FFCellX_39][FFCellY_40] > 0)) {
if (Deposition_type_13 > (0) && depo_reflection_counter_166 >= 0) {
Pd1_232 = Zeitschleife_Pd(varw_83,vsed_14,vdep_15,Deposition_type_13,FFCellX_39,FFCellY_40);
ik_233 = cs_cast<int>((xsi_97 * ConcGridXRez_47)) + 1;
jk_234 = cs_cast<int>((eta_98 * ConcGridYRez_48)) + 1;
depo_L_235 = P.Depo_conz[ik_233][jk_234];
conc_236 = masse_10 * Pd1_232 * area_rez_fac_16;
{
atomic_add(&(depo_L_235[SG_nteil_11]),conc_236);
}
masse_10 -= masse_10 * Pd1_232;
depo_reflection_counter_166 = -2;
if (masse_10 <= 0) {
goto REMOVE_PARTICLE;
}
}
tunfak_88 = (1);
reflexion_flag_66 = (1);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velzold_124 = (sqrtf((float)(varw_83)) + 0.82F * tunpa_116) * zahl1_4;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velxold_122 = (U0int_120 + 0.82F * tunpa_116) * zahl1_4;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velyold_123 = (V0int_121 + 0.82F * tunpa_116) * zahl1_4;
tunpa_116 = 0.01F;
goto MOVE_FORWARD;
}
}
} else if ((topo_69 == (0)) && (P.BuildingsExist == true)) {
IndexKOld_231 = IndexK_114;
IndexK_114 = Zeitschleife_BinarySearch(zcoord_nteil_9);
if (IndexK_114 <= P.KKART[FFCellX_39][FFCellY_40]) {
if (Deposition_type_13 > (0) && depo_reflection_counter_166 >= 0) {
Pd1_237 = Zeitschleife_Pd(varw_83,vsed_14,vdep_15,Deposition_type_13,FFCellX_39,FFCellY_40);
ik_238 = cs_cast<int>((xsi_97 * ConcGridXRez_47)) + 1;
jk_239 = cs_cast<int>((eta_98 * ConcGridYRez_48)) + 1;
depo_L_240 = P.Depo_conz[ik_238][jk_239];
conc_241 = masse_10 * Pd1_237 * area_rez_fac_16;
{
atomic_add(&(depo_L_240[SG_nteil_11]),conc_241);
}
masse_10 -= masse_10 * Pd1_237;
depo_reflection_counter_166 = -2;
if (masse_10 <= 0) {
goto REMOVE_PARTICLE;
}
}
tunfak_88 = (1);
reflexion_flag_66 = (1);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velzold_124 = (sqrtf((float)(varw_83)) + 0.82F * tunpa_116) * zahl1_4;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velxold_122 = (U0int_120 + 0.82F * tunpa_116) * zahl1_4;
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velyold_123 = (V0int_121 + 0.82F * tunpa_116) * zahl1_4;
tunpa_116 = 0.01F;
goto MOVE_FORWARD;
}
}
if (PartHeightAboveTerrain_109 <= 0) {
{zcoord_nteil_9 = AHint_81 - PartHeightAboveTerrain_109 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
PartHeightAboveTerrain_109 = zcoord_nteil_9 - AHint_81;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
if (Deposition_type_13 > (0) && depo_reflection_counter_166 >= 0) {
Pd1_242 = Zeitschleife_Pd(varw_83,vsed_14,vdep_15,Deposition_type_13,FFCellX_39,FFCellY_40);
ik_243 = cs_cast<int>((xsi_97 * ConcGridXRez_47)) + 1;
jk_244 = cs_cast<int>((eta_98 * ConcGridYRez_48)) + 1;
depo_L_245 = P.Depo_conz[ik_243][jk_244];
conc_246 = masse_10 * Pd1_242 * area_rez_fac_16;
{
atomic_add(&(depo_L_245[SG_nteil_11]),conc_246);
}
masse_10 -= masse_10 * Pd1_242;
depo_reflection_counter_166 = -2;
if (masse_10 <= 0) {
goto REMOVE_PARTICLE;
}
}
}
if (PartHeightAboveBuilding_110 >= blh_55 && P.ISTATIONAER != (0)) {
goto REMOVE_PARTICLE;
}
goto MOVE_TO_CONCENTRATIONCALCULATION;
}
reflexion_flag_66 = (0);
back_247 = 1;
while (back_247 == 1) {
xsi_97 = xcoord_nteil_7 - IKOOAGRAL_61;
eta_98 = ycoord_nteil_8 - JKOOAGRAL_62;
xsi1_99 = xcoord_nteil_7 - P.GrammWest;
eta1_100 = ycoord_nteil_8 - P.GrammSouth;
if ((eta_98 <= P.EtaMinGral) || (xsi_97 <= P.XsiMinGral) || (eta_98 >= P.EtaMaxGral) || (xsi_97 >= P.XsiMaxGral)) {
goto REMOVE_PARTICLE;
}
if (topo_69 == (1)) {
FFCellXPrev_173 = FFCellX_39;
FFCellYPrev_174 = FFCellY_40;
FFCellX_39 = cs_cast<int>((xsi_97 * FFGridXRez_43)) + 1;
FFCellY_40 = cs_cast<int>((eta_98 * FFGridYRez_44)) + 1;
GrammCellX_37 = gral_clamp((int)(cs_cast<int>((xsi1_99 * GrammGridXRez_45)) + 1),(int)(1),(int)(P.NX));
GrammCellY_38 = gral_clamp((int)(cs_cast<int>((eta1_100 * GrammGridYRez_46)) + 1),(int)(1),(int)(P.NY));
if ((FFCellX_39 > P.NII) || (FFCellY_40 > P.NJJ) || (FFCellX_39 < 1) || (FFCellY_40 < 1)) {
goto REMOVE_PARTICLE;
}
if (auszeit_87 < 20 && TerrainStepAllowed_167 && SourceType_54 > 1) {
IndexXDelta_248 = 2 * FFCellX_39 - FFCellXPrev_173;
IndexYDelta_249 = 2 * FFCellY_40 - FFCellYPrev_174;
if (IndexXDelta_248 > 1 && IndexXDelta_248 < P.NII && IndexYDelta_249 > 1 && IndexYDelta_249 < P.NJJ) {
if (P.CUTK[FFCellX_39][FFCellY_40] == 0 && P.CUTK[FFCellXPrev_173][FFCellYPrev_174] == 0 && P.CUTK[IndexXDelta_248][IndexYDelta_249] == 0) {
KKARTOld_250 = P.KKART[FFCellXPrev_173][FFCellYPrev_174];
KKARTNew_251 = P.KKART[IndexXDelta_248][IndexYDelta_249];
if (KKARTOld_250 != KKARTNew_251 && gral_abs((int)(KKARTOld_250 - KKARTNew_251)) < 3) {
AHK_252 = P.AHK[IndexXDelta_248][IndexYDelta_249];
AHKold_253 = P.AHK[FFCellXPrev_173][FFCellYPrev_174];
if (gral_abs((float)(AHK_252 - AHKold_253)) > P.GralDx * 0.5F) {
faktor_254 = gral_abs((float)(zcoord_nteil_9 - P.AHK[FFCellX_39][FFCellY_40])) / P.DZK[KKARTNew_251];
if (faktor_254 < 3) {
KKARTRecent_255 = P.KKART[FFCellX_39][FFCellY_40];
faktor_254 = expf((float)(-faktor_254 * faktor_254 * faktor_254 * faktor_254 * 0.01F));
if (KKARTRecent_255 == KKARTOld_250 && KKARTNew_251 < KKARTRecent_255) {
faktor_254 = 0.0f;
} else if (KKARTNew_251 < KKARTOld_250) {
faktor_254 *= 0.5F;
}
if (faktor_254 > 0) {
{zcoord_nteil_9 += (AHK_252 - P.AHK[FFCellXPrev_173][FFCellYPrev_174]) * faktor_254 * 0.8F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
if (zcoord_nteil_9 < P.AHK[FFCellX_39][FFCellY_40]) {
{zcoord_nteil_9 = P.AHK[FFCellX_39][FFCellY_40] + 0.2F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
TerrainStepAllowed_167 = false;
}
}
}
}
}
}
}
} else {
FFCellXPrev_173 = FFCellX_39;
FFCellYPrev_174 = FFCellY_40;
FFCellX_39 = cs_cast<int>((xsi_97 * FFGridXRez_43)) + 1;
FFCellY_40 = cs_cast<int>((eta_98 * FFGridYRez_44)) + 1;
if ((FFCellX_39 > P.NII) || (FFCellY_40 > P.NJJ) || (FFCellX_39 < 1) || (FFCellY_40 < 1)) {
goto REMOVE_PARTICLE;
}
}
back_247 = 0;
if (P.BuildingsExist == true || topo_69 == (1)) {
IndexKOld_256 = IndexK_114;
IndexK_114 = Zeitschleife_BinarySearch(zcoord_nteil_9 - P.AHMIN);
if (IndexK_114 <= P.KKART[FFCellX_39][FFCellY_40]) {
if ((IndexK_114 > P.KKART[FFCellXPrev_173][FFCellYPrev_174]) && (IndexK_114 == IndexKOld_256)) {
if (idt_86 * UXint_111 + corx_201 <= 0) {
{xcoord_nteil_7 = 2 * (IKOOAGRAL_61 + FFCellX_39 * FFGridX_41) - xcoord_nteil_7 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
} else {
{xcoord_nteil_7 = 2 * (IKOOAGRAL_61 + (FFCellX_39 - 1) * FFGridX_41) - xcoord_nteil_7 - 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
if (idt_86 * UYint_112 + cory_202 <= 0) {
{ycoord_nteil_8 = 2 * (JKOOAGRAL_62 + FFCellY_40 * FFGridY_42) - ycoord_nteil_8 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
} else {
{ycoord_nteil_8 = 2 * (JKOOAGRAL_62 + (FFCellY_40 - 1) * FFGridY_42) - ycoord_nteil_8 - 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
} else if ((IndexK_114 > P.KKART[FFCellXPrev_173][FFCellY_40]) && (IndexK_114 == IndexKOld_256)) {
if (idt_86 * UXint_111 + corx_201 <= 0) {
{xcoord_nteil_7 = 2 * (IKOOAGRAL_61 + FFCellX_39 * FFGridX_41) - xcoord_nteil_7 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
} else {
{xcoord_nteil_7 = 2 * (IKOOAGRAL_61 + (FFCellX_39 - 1) * FFGridX_41) - xcoord_nteil_7 - 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
} else if ((IndexK_114 > P.KKART[FFCellX_39][FFCellYPrev_174]) && (IndexK_114 == IndexKOld_256)) {
if (idt_86 * UYint_112 + cory_202 <= 0) {
{ycoord_nteil_8 = 2 * (JKOOAGRAL_62 + FFCellY_40 * FFGridY_42) - ycoord_nteil_8 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
} else {
{ycoord_nteil_8 = 2 * (JKOOAGRAL_62 + (FFCellY_40 - 1) * FFGridY_42) - ycoord_nteil_8 - 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
} else if ((IndexKOld_256 > P.KKART[FFCellX_39][FFCellY_40]) && (FFCellX_39 == FFCellXPrev_173) && (FFCellY_40 == FFCellYPrev_174)) {
{zcoord_nteil_9 = 2 * P.AHK[FFCellX_39][FFCellY_40] - zcoord_nteil_9 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
PartHeightAboveTerrain_109 = zcoord_nteil_9 - AHint_81;
if (topo_69 == (1)) {
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
}
velzold_124 = -velzold_124;
if (Deposition_type_13 > (0) && depo_reflection_counter_166 >= 0) {
Pd1_257 = Zeitschleife_Pd(varw_83,vsed_14,vdep_15,Deposition_type_13,FFCellX_39,FFCellY_40);
ik_258 = cs_cast<int>((xsi_97 * ConcGridXRez_47)) + 1;
jk_259 = cs_cast<int>((eta_98 * ConcGridYRez_48)) + 1;
depo_L_260 = P.Depo_conz[ik_258][jk_259];
conc_261 = masse_10 * Pd1_257 * area_rez_fac_16;
{
atomic_add(&(depo_L_260[SG_nteil_11]),conc_261);
}
masse_10 -= masse_10 * Pd1_257;
depo_reflection_counter_166 = -2;
if (masse_10 <= 0) {
goto REMOVE_PARTICLE;
}
}
} else if ((IndexKOld_256 > P.KKART[FFCellX_39][FFCellY_40])) {
if (Deposition_type_13 > (0) && depo_reflection_counter_166 >= 0) {
Pd1_262 = Zeitschleife_Pd(varw_83,vsed_14,vdep_15,Deposition_type_13,FFCellX_39,FFCellY_40);
ik_263 = cs_cast<int>((xsi_97 * ConcGridXRez_47)) + 1;
jk_264 = cs_cast<int>((eta_98 * ConcGridYRez_48)) + 1;
depo_L_265 = P.Depo_conz[ik_263][jk_264];
conc_266 = masse_10 * Pd1_262 * area_rez_fac_16;
{
atomic_add(&(depo_L_265[SG_nteil_11]),conc_266);
}
masse_10 -= masse_10 * Pd1_262;
depo_reflection_counter_166 = -2;
if (masse_10 <= 0) {
goto REMOVE_PARTICLE;
}
}
terrainHeight_267 = P.AHK[FFCellX_39][FFCellY_40];
{zcoord_nteil_9 = gral_max((float)(terrainHeight_267),(float)(2 * terrainHeight_267 - zcoord_nteil_9 + 0.01F));if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
} else {
if (Deposition_type_13 > (0) && depo_reflection_counter_166 >= 0) {
Pd1_268 = Zeitschleife_Pd(varw_83,vsed_14,vdep_15,Deposition_type_13,FFCellX_39,FFCellY_40);
ik_269 = cs_cast<int>((xsi_97 * ConcGridXRez_47)) + 1;
jk_270 = cs_cast<int>((eta_98 * ConcGridYRez_48)) + 1;
depo_L_271 = P.Depo_conz[ik_269][jk_270];
conc_272 = masse_10 * Pd1_268 * area_rez_fac_16;
{
atomic_add(&(depo_L_271[SG_nteil_11]),conc_272);
}
masse_10 -= masse_10 * Pd1_268;
depo_reflection_counter_166 = -2;
if (masse_10 <= 0) {
goto REMOVE_PARTICLE;
}
}
if (tunpa_116 == 0) {
if (P.CUTK[FFCellX_39][FFCellY_40] < 1 && (P.AHK[FFCellX_39][FFCellY_40] - zcoord_nteil_9) < 10) {
{zcoord_nteil_9 = P.AHK[FFCellX_39][FFCellY_40] + gral_abs((float)((idt_86 * (UZint_113 + velz_192) + DeltaZHurley_26)));if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
} else {
deltax_273 = xcoord_nteil_7 - xcoord_nteil_Prev_170;
deltay_274 = ycoord_nteil_8 - ycoord_nteil_Prev_171;
{xcoord_nteil_7 -= deltax_273 * 1.05;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
{ycoord_nteil_8 -= deltay_274 * 1.05;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
FFCellXPrev_173 = FFCellX_39;
FFCellYPrev_174 = FFCellY_40;
FFCellX_39 = cs_cast<int>(((xcoord_nteil_7 - IKOOAGRAL_61) * FFGridXRez_43)) + 1;
FFCellY_40 = cs_cast<int>(((ycoord_nteil_8 - JKOOAGRAL_62) * FFGridYRez_44)) + 1;
if ((FFCellX_39 > P.NII) || (FFCellY_40 > P.NJJ) || (FFCellX_39 < 1) || (FFCellY_40 < 1)) {
goto REMOVE_PARTICLE;
}
newTerrainHeight_275 = P.AHK[FFCellX_39][FFCellY_40];
if (zcoord_nteil_9 < newTerrainHeight_275) {
{zcoord_nteil_9 = 2 * newTerrainHeight_275 - zcoord_nteil_9;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
}
if (topo_69 == (1)) {
PartHeightAboveTerrain_109 = zcoord_nteil_9 - newTerrainHeight_275;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
} else {
PartHeightAboveTerrain_109 = zcoord_nteil_9;
PartHeightAboveBuilding_110 = zcoord_nteil_9;
}
}
}
vorzeichen_276 = -1;
if (velzold_124 < 0) {
vorzeichen_276 = 1;
}
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velzold_124 = gral_abs((float)(zahl1_4 * sqrtf((float)(varw_83)))) * vorzeichen_276;
}
vorzeichen1_277 = -1;
if (velxold_122 < 0) {
vorzeichen1_277 = 1;
}
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velxold_122 = gral_abs((float)(zahl1_4 * U0int_120 * 3)) * vorzeichen1_277;
vorzeichen1_277 = -1;
if (velyold_123 < 0) {
vorzeichen1_277 = 1;
}
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u1_rg_6 = u_rg_5 == (4294967295u) ? 1.0F : (u_rg_5 + 1) * (2.3283064E-10f);
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
zahl1_4 = sqrtf((float)(-2.0f * logf((float)(u1_rg_6)))) * sinf((float)((6.2831855f) * (u_rg_5 + 1) * (2.3283064E-10f)));
velyold_123 = gral_abs((float)(zahl1_4 * V0int_121 * 3)) * vorzeichen1_277;
back_247 = 1;
{idt_86 = Program_FloatMax(idt_86 * 0.5F,0.05F);if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
if (tunpa_116 > 0) {
goto MOVE_FORWARD;
}
}
}
if (back_247 > 0) {
reflexion_flag_66 = (1);
reflexion_number_75++;
if (reflexion_number_75 > Max_Reflections_102) {
goto REMOVE_PARTICLE;
}
}
}
depo_reflection_counter_166 = 0;
if (topo_69 == (1)) {
if ((FFCellX_39 < 1) || (FFCellX_39 > P.NII) || (FFCellY_40 < 1) || (FFCellY_40 > P.NJJ)) {
goto REMOVE_PARTICLE;
}
AHint_81 = P.AHK[FFCellX_39][FFCellY_40];
PartHeightAboveTerrain_109 = zcoord_nteil_9 - AHint_81;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
} else {
if ((FFCellX_39 < 1) || (FFCellX_39 > P.NII) || (FFCellY_40 < 1) || (FFCellY_40 > P.NJJ)) {
goto REMOVE_PARTICLE;
}
AHint_81 = 0;
PartHeightAboveTerrain_109 = zcoord_nteil_9;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
}
if (P.AdaptiveRoughnessMax > 0) {
ObL_117 = P.OLGral[FFCellX_39][FFCellY_40];
} else {
ObL_117 = P.Ob[GrammCellX_37][GrammCellY_38];
}
if ((PartHeightAboveBuilding_110 >= blh_55) && (ObL_117 < 0)) {
if ((topo_69 == (1)) && (UZint_113 >= 0) && P.ISTATIONAER != (0)) {
goto REMOVE_PARTICLE;
}
{zcoord_nteil_9 = blh_55 + AHint_81 - (PartHeightAboveBuilding_110 - blh_55) - 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
velzold_124 = -velzold_124;
PartHeightAboveTerrain_109 = zcoord_nteil_9 - AHint_81;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
}
if (PartHeightAboveTerrain_109 <= 0) {
{zcoord_nteil_9 = AHint_81 - PartHeightAboveTerrain_109 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
velzold_124 = -velzold_124;
PartHeightAboveTerrain_109 = zcoord_nteil_9 - AHint_81;
PartHeightAboveBuilding_110 = PartHeightAboveTerrain_109;
if (Deposition_type_13 > (0) && depo_reflection_counter_166 >= 0) {
Pd1_278 = Zeitschleife_Pd(varw_83,vsed_14,vdep_15,Deposition_type_13,FFCellX_39,FFCellY_40);
ik_279 = cs_cast<int>((xsi_97 * ConcGridXRez_47)) + 1;
jk_280 = cs_cast<int>((eta_98 * ConcGridYRez_48)) + 1;
depo_L_281 = P.Depo_conz[ik_279][jk_280];
conc_282 = masse_10 * Pd1_278 * area_rez_fac_16;
{
atomic_add(&(depo_L_281[SG_nteil_11]),conc_282);
}
masse_10 -= masse_10 * Pd1_278;
depo_reflection_counter_166 = -2;
if (masse_10 <= 0) {
goto REMOVE_PARTICLE;
}
}
}
if (auszeit_87 > 3600) {
distanceParticle_108 += (float)(sqrt((double)(Program_Pow2(xcoord_nteil_7 - xcoord_nteil_Prev_170) + Program_Pow2(ycoord_nteil_8 - ycoord_nteil_Prev_171))));
distanceParticle_108 *= 0.5F;
if (distanceParticle_108 < 0.02F) {
goto REMOVE_PARTICLE;
}
}
MOVE_TO_CONCENTRATIONCALCULATION: ;
iko_283 = cs_cast<int>((xsi_97 * ConcGridXRez_47)) + 1;
jko_284 = cs_cast<int>((eta_98 * ConcGridYRez_48)) + 1;
if ((iko_283 > P.NXL) || (iko_283 < 0) || (jko_284 > P.NYL) || (jko_284 < 0)) {
goto REMOVE_PARTICLE;
}
zcoordRelative_285 = 0;
if (P.BuildingsExist == true && P.Topo == (1)) {
zcoordRelative_285 = zcoord_nteil_9 - AHint_81 + P.CUTK[FFCellX_39][FFCellY_40];
} else {
zcoordRelative_285 = zcoord_nteil_9 - AHint_81;
}
{
zko_286 = cs_cast<int>(((zcoordRelative_285 - AHint_81) * ConcGridZRez_49));
if (iko_283 == ConcCellPrevX_103 && jko_284 == ConcCellPrevY_104 && zko_286 == ConcCellPrevZ_105) {
ConcCellTime_107 += idt_86;
if (ConcCellTime_107 > ConcCellTimeMax_106 - 10) {
{zcoord_nteil_9 += 2;if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;}
if (ConcCellTime_107 > ConcCellTimeMax_106) {
goto REMOVE_PARTICLE;
}
}
} else {
ConcCellPrevX_103 = iko_283;
ConcCellPrevY_104 = jko_284;
ConcCellPrevZ_105 = zko_286;
ConcCellTime_107 = 0;
}
}
for (II_287 = 0;II_287 < kko_63.size();II_287++) {
slice_288 = (zcoordRelative_285 - P.HorSlices[II_287]) * ConcGridZRez_49;
if (gral_abs((float)(slice_288)) > (32767)) {
goto REMOVE_PARTICLE;
}
kko_63[II_287] = cs_cast<int>(slice_288);
}
if (decay_rate_77 > 0) {
masse_10 *= exp((double)(-decay_rate_77 * idt_86));
}
if (P.WetDeposition == true && P.WetDepoRW != 0) {
epsilonW_Masse_289 = masse_10 * P.WetDepoRW * idt_86;
ex_290 = false;
if (epsilonW_Masse_289 > masse_10) {
epsilonW_Masse_289 = masse_10;
ex_290 = true;
}
masse_10 -= epsilonW_Masse_289;
if (!P.WetDopoDropDrift) {
depo_L_291 = P.Depo_conz[iko_283][jko_284];
conc_292 = epsilonW_Masse_289 * area_rez_fac_16;
{
atomic_add(&(depo_L_291[SG_nteil_11]),conc_292);
}
} else {

for (dropDataIndex_293 = 0;dropDataIndex_293 < P.WetDepoDropVelocity.d[1];dropDataIndex_293++) {
if (P.WetDepoDropVelocity(dropDataIndex_293,0) >= P.WetDepoPrecipitation) {
break;
}
}
for (dropSizeClass_294 = 1;dropSizeClass_294 < 5;dropSizeClass_294++) {
m_z_3 = 36969 * (m_z_3 & 65535) + (m_z_3 >> 16);
m_w_2 = 18000 * (m_w_2 & 65535) + (m_w_2 >> 16);
u_rg_5 = (m_z_3 << 16) + m_w_2;
u_random_295 = (float)((0.5F + u_rg_5 * 1.16415321772724E-10));
depoX_296 = xcoord_nteil_7;depoY_297 = ycoord_nteil_8;depoEpsilonW_298 = epsilonW_Masse_289 * P.WetDepoDropVelocity(dropDataIndex_293,4 + dropSizeClass_294) * 0.01F;depoCellH_299 = P.DZK[IndexK_114];
depoIndexK_300 = IndexK_114;depoFFX_301 = FFCellX_39;depoFFY_302 = FFCellY_40;
depo1stCell_303 = P.DZK[1];
if (P.BuildingsExist == false) {
depoCellH_299 = 2;
depo1stCell_303 = 2;
}
depoVelVertical_304 = P.WetDepoDropVelocity(dropDataIndex_293,dropSizeClass_294) * u_random_295;depoTime_305 = (float)(depoCellH_299) * 4 / depoVelVertical_304;depoU_306 = UXint_111;depoV_307 = UYint_112;depoZ_308 = UZint_113;depoZcoord_309 = zcoord_nteil_9;depoAH_310 = AHint_81;if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
unsigned int depoDriftIter = 0;
while (depoZcoord_309 > depoAH_310) {
if(++depoDriftIter > GRAL_DROP_DRIFT_MAX_ITER){RecordDeviceError(3,1,nteil);return;}
if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
depoZcoord_309 += depoZ_308 * depoTime_305 - (float)(depoCellH_299) * 4;if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
if (depoZcoord_309 > depo1stCell_303) {
depoX_296 += depoTime_305 * depoU_306;if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
depoY_297 += depoTime_305 * depoV_307;if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
depoFFX_301 = cs_cast<int>(((depoX_296 - IKOOAGRAL_61) * FFGridXRez_43)) + 1;
depoFFY_302 = cs_cast<int>(((depoY_297 - JKOOAGRAL_62) * FFGridYRez_44)) + 1;
if ((depoFFX_301 < 1) || (depoFFX_301 > P.NII) || (depoFFY_302 < 1) || (depoFFY_302 > P.NJJ)) {
depoEpsilonW_298 = 0;if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
break;
}
depoAH_310 = P.AHK[depoFFX_301][depoFFY_302];if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
{tuple_tmp_4 = Zeitschleife_IntWindCalculate(depoFFX_301,depoFFY_302,depoAH_310,depoX_296,depoY_297,depoZcoord_309); depoU_306 = tuple_tmp_4.v0; depoV_307 = tuple_tmp_4.v1; depoZ_308 = tuple_tmp_4.v2; depoIndexK_300 = tuple_tmp_4.v3;if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}}
if (P.BuildingsExist == false) {
depoCellH_299 = 2;if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
} else {
depoCellH_299 = P.DZK[depoIndexK_300];if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
}
depoTime_305 = (float)(depoCellH_299) * 4 / depoVelVertical_304;if(!gral_drop_finite(depoX_296,depoY_297,depoZcoord_309,depoU_306,depoV_307,depoZ_308,depoAH_310,depoCellH_299,depoTime_305,depoVelVertical_304,depoEpsilonW_298,depo1stCell_303)){RecordDeviceError(2,1,nteil);return;}
}
}
depoiko_311 = cs_cast<int>(((depoX_296 - IKOOAGRAL_61) * ConcGridXRez_47)) + 1;
depojko_312 = cs_cast<int>(((depoY_297 - JKOOAGRAL_62) * ConcGridYRez_48)) + 1;
if ((depoiko_311 >= P.NXL) || (depoiko_311 < 2) || (depojko_312 >= P.NYL) || (depojko_312 < 2)) {
depoEpsilonW_298 = 0;
} else {
depo_L_313 = P.Depo_conz[depoiko_311][depojko_312];
conc_314 = depoEpsilonW_298 * area_rez_fac_16;
{
atomic_add(&(depo_L_313[SG_nteil_11]),conc_314);
}
}
}
}
if (ex_290) {
goto REMOVE_PARTICLE;
}
}
if (Deposition_type_13 < (2)) {
if (P.ReceptorsAvailable && reflexion_flag_66 == (0) && P.GridRecStart != nullptr) {
if (GridCellLookup(iko_283,jko_284,receptors_315)) {
for (int fe_irec_316=0;fe_irec_316<receptors_315.size();fe_irec_316++) {
irec_316=receptors_315[fe_irec_316];
{
if (P.ReceptorNearbyBuilding[irec_316]) {
if (iko_283 == P.ReceptorIInd[irec_316] && jko_284 == P.ReceptorJInd[irec_316]) {
slice_317 = (zcoord_nteil_9 - AHint_81 - P.ReceptorZ[irec_316]) * ConcGridZRez_49;
if (cs_cast<int>(slice_317) == 0) {
ReceptorConcentration_65[irec_316] += idt_86 * masse_10;
}
}
} else {
if (gral_abs((double)(xcoord_nteil_7 - P.ReceptorX[irec_316])) < ConcGridXHalf_50 && gral_abs((double)(ycoord_nteil_8 - P.ReceptorY[irec_316])) < ConcGridYHalf_51) {
slice_318 = (zcoord_nteil_9 - AHint_81 - P.ReceptorZ[irec_316]) * ConcGridZRez_49;
if (cs_cast<int>(slice_318) == 0) {
ReceptorConcentration_65[irec_316] += idt_86 * masse_10;
}
}
}
}
}
}
}
for (II_319 = 0;II_319 < kko_63.size();II_319++) {
if ((kko_63[II_319] == 0) && (reflexion_flag_66 == (0))) {
conz3d_L_320 = P.Conz3dDouble[iko_283][jko_284][II_319];
conc_321 = masse_10 * idt_86;
{
atomic_add(&(conz3d_L_320[SG_nteil_11]),conc_321);
}
}
}
if (ISTATIONAER_68 == (0) && P.WriteVerticalConcentration) {
if (reflexion_flag_66 == (0)) {
TransientGridZ_72 = TransientConcentration_BinarySearchTransient(zcoord_nteil_9 - AHint_81);
TransientGridX_70 = cs_cast<int>((xsi_97 * FFGridXRez_43)) + 1;
TransientGridY_71 = cs_cast<int>((eta_98 * FFGridYRez_44)) + 1;
conzsum_L_322 = P.ConzSsum[TransientGridX_70][TransientGridY_71];
conc_323 = masse_10 * P.GridVolume * idt_86 / (Area_cart_74 * P.DZK_Trans[TransientGridZ_72]);
{
atomic_add(&(conzsum_L_322[TransientGridZ_72]),(float)(conc_323));
}
}
}
if (P.Odour == true) {
for (II_324 = 0;II_324 < kko_63.size();II_324++) {
slice_325 = (zcoordRelative_285 - (P.HorSlices[II_324] + P.GralDz)) * ConcGridZRez_49;
kko_63[II_324] = cs_cast<int>((gral_min((float)((2147483647)),(float)(slice_325))));
}
for (II_326 = 0;II_326 < kko_63.size();II_326++) {
if ((kko_63[II_326] == 0) && (reflexion_flag_66 == (0))) {
conz3dp_L_327 = P.Conz3dp[iko_283][jko_284][II_326];
conc_328 = masse_10 * idt_86;
{
atomic_add(&(conz3dp_L_327[SG_nteil_11]),(float)(conc_328));
}
}
}
for (II_329 = 0;II_329 < kko_63.size();II_329++) {
slice_330 = (zcoordRelative_285 - (P.HorSlices[II_329] - P.GralDz)) * ConcGridZRez_49;
kko_63[II_329] = cs_cast<int>((gral_min((float)((2147483647)),(float)(slice_330))));
}
for (II_331 = 0;II_331 < kko_63.size();II_331++) {
if ((kko_63[II_331] == 0) && (reflexion_flag_66 == (0))) {
conz3dm_L_332 = P.Conz3dm[iko_283][jko_284][II_331];
conc_333 = masse_10 * idt_86;
{
atomic_add(&(conz3dm_L_332[SG_nteil_11]),(float)(conc_333));
}
}
}
}
}
}
REMOVE_PARTICLE: ;
if (P.LogLevel > (0)) {
if (reflexion_number_75 > 400000) {
{
atomic_add(&(P.LogReflexions[5]),1);
}
} else if (reflexion_number_75 > 100000) {
{
atomic_add(&(P.LogReflexions[4]),1);
}
} else if (reflexion_number_75 > 50000) {
{
atomic_add(&(P.LogReflexions[3]),1);
}
} else if (reflexion_number_75 > 10000) {
{
atomic_add(&(P.LogReflexions[2]),1);
}
} else if (reflexion_number_75 > 5000) {
{
atomic_add(&(P.LogReflexions[1]),1);
}
} else if (reflexion_number_75 > 500) {
{
atomic_add(&(P.LogReflexions[0]),1);
}
}
if (timestep_number_76 > 100000000) {
{
atomic_add(&(P.Log_Timesteps[5]),1);
}
} else if (timestep_number_76 > 10000000) {
{
atomic_add(&(P.Log_Timesteps[4]),1);
}
} else if (timestep_number_76 > 1000000) {
{
atomic_add(&(P.Log_Timesteps[3]),1);
}
} else if (timestep_number_76 > 100000) {
{
atomic_add(&(P.Log_Timesteps[2]),1);
}
} else if (timestep_number_76 > 10000) {
{
atomic_add(&(P.Log_Timesteps[1]),1);
}
} else if (timestep_number_76 > 1000) {
{
atomic_add(&(P.Log_Timesteps[0]),1);
}
}
}
if (P.ReceptorsAvailable) {
for (ii_334 = 1;ii_334 < ReceptorConcentration_65.size();ii_334++) {
recconc_335 = ReceptorConcentration_65[ii_334];
if (recconc_335 > 0) {
recmit_L_336 = P.ReceptorConc[ii_334];
{
atomic_add(&(recmit_L_336[SG_nteil_11]),recconc_335);
atomic_max(&(P.ReceptorParticleMaxConc[ii_334][SG_nteil_11]),recconc_335);
}
}
}
}
{if(!CheckParticleFinite(xcoord_nteil_7,ycoord_nteil_8,zcoord_nteil_9,auszeit_87,idt_86,1,nteil))return;if(P.TraceEnabled && P.Trace.p && nteil<P.Trace.d[0]){P.Trace(nteil,0)=xcoord_nteil_7;P.Trace(nteil,1)=ycoord_nteil_8;P.Trace(nteil,2)=zcoord_nteil_9;P.Trace(nteil,3)=auszeit_87;P.Trace(nteil,4)=timestep_number_76;P.Trace(nteil,5)=m_w_2;P.Trace(nteil,6)=m_z_3;P.Trace(nteil,7)=masse_10;}
return ;}
}

__device__ float Zeitschleife_Erf(float x) {
float a1_1{};
float a2_2{};
float a3_3{};
float a4_4{};
float a5_5{};
float p_6{};
int sign_7{};
float t_8{};
float y_9{};
a1_1 = 0.254829592F;
a2_2 = -0.284496736F;
a3_3 = 1.421413741F;
a4_4 = -1.453152027F;
a5_5 = 1.061405429F;
p_6 = 0.3275911F;
sign_7 = 1;
if (x < 0) {
sign_7 = -1;
}
x = gral_abs((float)(x));
t_8 = 1 / (1 + p_6 * x);
y_9 = 1 - (((((a5_5 * t_8 + a4_4) * t_8) + a3_3) * t_8 + a2_2) * t_8 + a1_1) * t_8 * expf((float)(-x * x));
return sign_7 * y_9;
}

__device__ float Zeitschleife_Pd(float varw,float vsed,float vdep,int Deposition_Type,int IndexI,int IndexJ) {
float Ks_6{};
float Fs_7{};
float Pd_8{};
varw = sqrtf((float)(varw));
if (P.COV[IndexI][IndexJ] > 0) {
if (Deposition_Type == 2) {
vdep *= (1.0f + P.VegetationDepoPMFactor * P.COV[IndexI][IndexJ]);
} else {
vdep *= (1.0f + P.VegetationDepoGasFactor * P.COV[IndexI][IndexJ]);
}
}
Ks_6 = vsed / ((1.4142135f) * varw);
Fs_7 = (1.7724539f) * Ks_6 + expf((float)(-powf((float)(Ks_6),(float)(2)))) / (1 + Zeitschleife_Erf(Ks_6));
Pd_8 = 3 * (1.7724539f) * 3600 / P.TAUS * vdep / ((1.4142135f) * varw * Fs_7 + (1.7724539f) * vdep);
if (Pd_8 < 1.0) {
return Pd_8;
} else {
return 1;
}
}

__device__ void ZeitschleifeNonSteadyState_ParticleDriver(int i,int j,int k,int SG,float TransConcentration,int splitIndex,int trace_index) {
unsigned int m_w_7{};
unsigned int m_z_8{};
float zahl1_9{};
unsigned int u_rg_10{};
float u1_rg_11{};
double xcoord_nteil_12{};
double ycoord_nteil_13{};
float zcoord_nteil_14{};
double Vol_ratio_15{};
double Vol_cart_16{};
double Area_cart_17{};
double masse_18{};
double mass_real_19{};
int SG_nteil_20{};
int Deposition_type_21{};
float vsed_22{};
float vdep_23{};
double area_rez_fac_24{};
float FFGridX_25{};
float FFGridY_26{};
float FFGridXRez_27{};
float FFGridYRez_28{};
float GrammGridXRez_29{};
float GrammGridYRez_30{};
float ConcGridXRez_31{};
float ConcGridYRez_32{};
float ConcGridZRez_33{};
float ConcGridXHalf_34{};
float ConcGridYHalf_35{};
int IFQ_36{};
float blh_37{};
float blhRez_38{};
float blhRez2_39{};
float cachedObLength_40{};
float cachedUstern_41{};
float cachedVariance_42{};
int IKOOAGRAL_43{};
int JKOOAGRAL_44{};
Arr<int,1> kko_45{};
int receptorCount_46{};
Arr<double,1> ReceptorConcentration_47{};
float a3_48{};
int reflexion_flag_49{};
int ISTATISTIK_50{};
int ISTATIONAER_51{};
int topo_52{};
int TransientGridX_53{};
int TransientGridY_54{};
int TransientGridZ_55{};
int GrammCellX_56{};
int GrammCellY_57{};
float AHint_58{};
float AHintold_59{};
float varw_60{};
float sigmauhurly_61{};
float idt_62{};
float auszeit_63{};
float DSIGUDX_64{};
float DSIGUDY_65{};
float DSIGVDX_66{};
float DSIGVDY_67{};
float DUDX_68{};
float DVDX_69{};
float DUDY_70{};
float DVDY_71{};
double decay_rate_72{};
double xsi_73{};
double eta_74{};
double xsi1_75{};
double eta1_76{};
int Max_Reflections_77{};
int ConcCellPrevX_78{};
int ConcCellPrevY_79{};
int ConcCellPrevZ_80{};
float ConcCellTimeMax_81{};
float ConcCellTime_82{};
float distanceParticle_83{};
int FFCellX_84{};
int FFCellY_85{};
float PartHeightAboveTerrain_86{};
float PartHeightAboveBuilding_87{};
float UXint_88{};
float UYint_89{};
float UZint_90{};
int IndexK_91{};
float windge_92{};
float tunpa_93{};
float ObL_94{};
float roughZ0_95{};
float deltaZ_96{};
float U0int_97{};
float V0int_98{};
float velxold_99{};
float velyold_100{};
float velzold_101{};
int depo_reflection_counter_102{};
int reflexion_number_103{};
int timestep_number_104{};
int Max_Loops_105{};
float ObLength_106{};
float Ustern_107{};
double xcoord_nteil_Prev_108{};
double ycoord_nteil_Prev_109{};
float zcoord_nteil_Prev_110{};
int FFCellXPrev_111{};
int FFCellYPrev_112{};
float eps_113{};
float W0int_114{};
float varw2_115{};
float skew_116{};
float curt_117{};
float dskew_118{};
float wstern_119{};
float vert_blh_120{};
float hterm_121{};
float dvarw_122{};
float dcurt_123{};
float idtt_124{};
float dummyterm_125{};
float alpha_126{};
float beta_127{};
float gamma_128{};
float acc_129{};
float velz_130{};
short _kkart_131{};
float corx_132{};
float cory_133{};
float param_134{};
float T3_135{};
float termp_136{};
float termq_137{};
float Tstern_138{};
float zuff1_139{};
float zuff2_140{};
float velX_141{};
float velY_142{};
float velx_143{};
float vely_144{};
int back_145{};
int IndexKOld_146{};
float Pd1_147{};
int ik_148{};
int jk_149{};
Arr<double,1> depo_L_150{};
double conc_151{};
float Pd1_152{};
int ik_153{};
int jk_154{};
Arr<double,1> depo_L_155{};
double conc_156{};
float terrainHeight_157{};
float Pd1_158{};
int ik_159{};
int jk_160{};
Arr<double,1> depo_L_161{};
double conc_162{};
double deltax_163{};
double deltay_164{};
float newTerrainHeight_165{};
int vorzeichen_166{};
int vorzeichen1_167{};
float Pd1_168{};
int ik_169{};
int jk_170{};
Arr<double,1> depo_L_171{};
double conc_172{};
int iko_173{};
int jko_174{};
float zcoordRelative_175{};
int zko_176{};
int II_177{};
float slice_178{};
double epsilonW_Masse_179{};
bool ex_180{};
Arr<double,1> depo_L_181{};
double conc_182{};
int dropDataIndex_183{};
int dropSizeClass_184{};
float u_random_185{};
double depoX_186{};
double depoY_187{};
double depoEpsilonW_188{};
double depoCellH_189{};
int depoIndexK_190{};
int depoFFX_191{};
int depoFFY_192{};
float depo1stCell_193{};
float depoVelVertical_194{};
float depoTime_195{};
float depoU_196{};
float depoV_197{};
float depoZ_198{};
float depoZcoord_199{};
float depoAH_200{};
int depoiko_201{};
int depojko_202{};
Arr<double,1> depo_L_203{};
double conc_204{};
Arr<int,1> receptors_205{};
int irec_206{};
float slice_207{};
float slice_208{};
int II_209{};
Arr<double,1> conz3d_L_210{};
double conc_211{};
Arr<double,1> conzsum_L_212{};
double conc_213{};
int II_214{};
float slice_215{};
int II_216{};
Arr<double,1> conz3dp_L_217{};
double conc_218{};
int II_219{};
float slice_220{};
int II_221{};
Arr<double,1> conz3dm_L_222{};
double conc_223{};
int ii_224{};
double recconc_225{};
Arr<double,1> recmit_L_226{};
Tuple4<float,float,float,int> tuple_tmp_0{};
Tuple2<float,float> tuple_tmp_1{};
Tuple4<float,float,float,int> tuple_tmp_2{};
Tuple2<float,float> tuple_tmp_3{};
Tuple4<float,float,float,int> tuple_tmp_4{};
int trace_slot=(P.TraceEnabled && P.ResidualTrace.p && trace_index>=0 && trace_index<P.ResidualTrace.d[0])?trace_index:-1;



{
m_w_7 = P.RnGSeed1 + (unsigned int)((i + j));
m_z_8 = P.RnGSeed2 + (unsigned int)((i * 2));
}
ZeitschleifeNonSteadyState_ResidualSeeds(i,j,k,SG,splitIndex,m_w_7,m_z_8);if(trace_slot>=0){P.ResidualTrace(trace_slot,0)=i;P.ResidualTrace(trace_slot,1)=j;P.ResidualTrace(trace_slot,2)=k;P.ResidualTrace(trace_slot,3)=SG;P.ResidualTrace(trace_slot,4)=splitIndex;P.ResidualTrace(trace_slot,5)=m_w_7;P.ResidualTrace(trace_slot,6)=m_z_8;}
zahl1_9 = 0;
u_rg_10 = 0;
u1_rg_11 = 0;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = -1 + (u_rg_10 + 1) * (2.3283064E-10f) * 2;
xcoord_nteil_12 = P.IKOOAGRAL + i * P.DXK - (0.5 + u1_rg_11 * 0.5) * P.DXK;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = -1 + (u_rg_10 + 1) * (2.3283064E-10f) * 2;
ycoord_nteil_13 = P.JKOOAGRAL + j * P.DYK - (0.5 + u1_rg_11 * 0.5) * P.DYK;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = -1 + (u_rg_10 + 1) * (2.3283064E-10f) * 2;
zcoord_nteil_14 = (float)((P.AHK[i][j] + P.HoKartTrans[k] - (0.3 + u1_rg_11 * 0.5) * P.DZK_Trans[k]));if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;
Vol_ratio_15 = P.DXK * P.DYK * P.DZK_Trans[k] / P.GridVolume;
Vol_cart_16 = P.DXK * P.DYK * P.DZK_Trans[k];
Area_cart_17 = P.DXK * P.DYK;
masse_18 = TransConcentration * Vol_ratio_15 / P.TAUS;
mass_real_19 = TransConcentration * Vol_cart_16 / P.TAUS;
if (mass_real_19 == 0 || masse_18 == 0) {
{if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;if(trace_slot>=0){P.ResidualTrace(trace_slot,7)=xcoord_nteil_12;P.ResidualTrace(trace_slot,8)=ycoord_nteil_13;P.ResidualTrace(trace_slot,9)=zcoord_nteil_14;P.ResidualTrace(trace_slot,10)=auszeit_63;P.ResidualTrace(trace_slot,11)=timestep_number_104;P.ResidualTrace(trace_slot,12)=m_w_7;P.ResidualTrace(trace_slot,13)=m_z_8;P.ResidualTrace(trace_slot,14)=masse_18;P.ResidualTrace(trace_slot,15)=1;}
return ;}
}
SG_nteil_20 = SG;
Deposition_type_21 = 0;
vsed_22 = 0;
vdep_23 = 0;
if (P.TransientDepoMode != nullptr && P.TransientDepoMode[SG_nteil_20] > 0) {
vsed_22 = (float)(P.TransientDepoVsed[SG_nteil_20]);
vdep_23 = (float)(P.TransientDepoVdep[SG_nteil_20]);
Deposition_type_21 = P.TransientDepoMode[SG_nteil_20];
}
area_rez_fac_24 = P.TAUS * P.GridVolume * 24 / 1000;
area_rez_fac_24 = area_rez_fac_24 / (P.GralDx * P.GralDy);
FFGridX_25 = P.DXK;
FFGridY_26 = P.DYK;
FFGridXRez_27 = 1 / P.DXK;
FFGridYRez_28 = 1 / P.DYK;
GrammGridXRez_29 = 1 / P.DDX[1];
GrammGridYRez_30 = 1 / P.DDY[1];
ConcGridXRez_31 = 1 / P.GralDx;
ConcGridYRez_32 = 1 / P.GralDy;
ConcGridZRez_33 = 2 / P.GralDz;
ConcGridXHalf_34 = P.GralDx * 0.5F;
ConcGridYHalf_35 = P.GralDy * 0.5F;
IFQ_36 = P.TS_Count;
blh_37 = P.BdLayHeight;
blhRez_38 = 1 / blh_37;
blhRez2_39 = 1 / Program_Pow2(blh_37);
cachedObLength_40 = (__int_as_float((int)0xFFC00000));cachedUstern_41 = (__int_as_float((int)0xFFC00000));cachedVariance_42 = 0;
IKOOAGRAL_43 = P.IKOOAGRAL;
JKOOAGRAL_44 = P.JKOOAGRAL;
kko_45 = ParticleScratchRow(P.ParticleSliceScratch,P.NS);
receptorCount_46 = P.ReceptorNumber == 0 ? 0 : P.ReceptorNumber + 1;
ReceptorConcentration_47 = ParticleScratchRow(P.ParticleReceptorScratch,receptorCount_46);

a3_48 = 1.1F;
reflexion_flag_49 = (0);
ISTATISTIK_50 = P.IStatistics;
ISTATIONAER_51 = P.ISTATIONAER;
topo_52 = P.Topo;
TransientGridX_53 = 1;
TransientGridY_54 = 1;
TransientGridZ_55 = 1;
GrammCellX_56 = 1;GrammCellY_57 = 1;
AHint_58 = 0;
AHintold_59 = 0;
varw_60 = 0;sigmauhurly_61 = 0;
idt_62 = 0.1F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;
auszeit_63 = 0;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;
DSIGUDX_64 = 0;DSIGUDY_65 = 0;DSIGVDX_66 = 0;DSIGVDY_67 = 0;
DUDX_68 = 0;DVDX_69 = 0;DUDY_70 = 0;DVDY_71 = 0;
decay_rate_72 = P.DecayRate[P.SourceGroups[SG]];
xsi_73 = xcoord_nteil_12 - IKOOAGRAL_43;
eta_74 = ycoord_nteil_13 - JKOOAGRAL_44;
xsi1_75 = xcoord_nteil_12 - P.GrammWest;
eta1_76 = ycoord_nteil_13 - P.GrammSouth;
if ((eta_74 <= P.EtaMinGral) || (xsi_73 <= P.XsiMinGral) || (eta_74 >= P.EtaMaxGral) || (xsi_73 >= P.XsiMaxGral)) {
{if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;if(trace_slot>=0){P.ResidualTrace(trace_slot,7)=xcoord_nteil_12;P.ResidualTrace(trace_slot,8)=ycoord_nteil_13;P.ResidualTrace(trace_slot,9)=zcoord_nteil_14;P.ResidualTrace(trace_slot,10)=auszeit_63;P.ResidualTrace(trace_slot,11)=timestep_number_104;P.ResidualTrace(trace_slot,12)=m_w_7;P.ResidualTrace(trace_slot,13)=m_z_8;P.ResidualTrace(trace_slot,14)=masse_18;P.ResidualTrace(trace_slot,15)=1;}
return ;}
}
Max_Reflections_77 = gral_min((int)(100000),(int)(P.NII * P.NJJ * 2));
ConcCellPrevX_78 = -1;
ConcCellPrevY_79 = -1;
ConcCellPrevZ_80 = 0;
ConcCellTimeMax_81 = gral_max((float)(100),(float)(P.GralDx / 0.04F));
ConcCellTime_82 = 0;
distanceParticle_83 = 50;


PartHeightAboveTerrain_86 = 0;
PartHeightAboveBuilding_87 = 0;
if (topo_52 == (1)) {
FFCellX_84 = cs_cast<int>((xsi_73 * FFGridXRez_27)) + 1;
FFCellY_85 = cs_cast<int>((eta_74 * FFGridYRez_28)) + 1;
if ((FFCellX_84 < 1) || (FFCellX_84 > P.NII) || (FFCellY_85 < 1) || (FFCellY_85 > P.NJJ)) {
goto REMOVE_PARTICLE;
}
GrammCellX_56 = gral_clamp((int)(cs_cast<int>((xsi1_75 * GrammGridXRez_29)) + 1),(int)(1),(int)(P.NX));
GrammCellY_57 = gral_clamp((int)(cs_cast<int>((eta1_76 * GrammGridYRez_30)) + 1),(int)(1),(int)(P.NY));
AHint_58 = P.AHK[FFCellX_84][FFCellY_85];
PartHeightAboveTerrain_86 = zcoord_nteil_14 - AHint_58;
PartHeightAboveBuilding_87 = PartHeightAboveTerrain_86;
} else {
FFCellX_84 = cs_cast<int>((xsi_73 * FFGridXRez_27)) + 1;
FFCellY_85 = cs_cast<int>((eta_74 * FFGridYRez_28)) + 1;
if ((FFCellX_84 < 1) || (FFCellX_84 > P.NII) || (FFCellY_85 < 1) || (FFCellY_85 > P.NJJ)) {
goto REMOVE_PARTICLE;
}
PartHeightAboveTerrain_86 = zcoord_nteil_14;
PartHeightAboveBuilding_87 = PartHeightAboveTerrain_86;
}
UXint_88 = 0;UYint_89 = 0;UZint_90 = 0;
IndexK_91 = 1;
{tuple_tmp_0 = Zeitschleife_IntWindCalculate(FFCellX_84,FFCellY_85,AHint_58,xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14); UXint_88 = tuple_tmp_0.v0; UYint_89 = tuple_tmp_0.v1; UZint_90 = tuple_tmp_0.v2; IndexK_91 = tuple_tmp_0.v3;}
windge_92 = sqrtf((float)(Program_Pow2(UXint_88) + Program_Pow2(UYint_89)));
if (windge_92 < 0.01F) {
windge_92 = 0.01F;
}
tunpa_93 = 0;
ObL_94 = P.Ob[GrammCellX_56][GrammCellY_57];
roughZ0_95 = P.Z0Gramm[GrammCellX_56][GrammCellY_57];
if (P.AdaptiveRoughnessMax > 0) {
ObL_94 = P.OLGral[FFCellX_84][FFCellY_85];
roughZ0_95 = P.Z0Gral[FFCellX_84][FFCellY_85];
}
deltaZ_96 = roughZ0_95;
{tuple_tmp_1 = Zeitschleife_IntStandCalculate(0,roughZ0_95,PartHeightAboveBuilding_87,windge_92,sigmauhurly_61); U0int_97 = tuple_tmp_1.v0; V0int_98 = tuple_tmp_1.v1;}
velxold_99 = 0;
velyold_100 = 0;
velzold_101 = 0;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = u_rg_10 == (4294967295u) ? 1.0F : (u_rg_10 + 1) * 2.328306435454494e-10F;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
zahl1_9 = sqrtf((float)(-2.0f * logf((float)(u1_rg_11)))) * sinf((float)((6.2831855f) * (u_rg_10 + 1) * (2.3283064E-10f)));
velxold_99 = U0int_97 * zahl1_9;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = u_rg_10 == (4294967295u) ? 1.0F : (u_rg_10 + 1) * 2.328306435454494e-10F;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
zahl1_9 = sqrtf((float)(-2.0f * logf((float)(u1_rg_11)))) * sinf((float)((6.2831855f) * (u_rg_10 + 1) * (2.3283064E-10f)));
velyold_100 = V0int_98 * zahl1_9;
{auszeit_63 = 0;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
depo_reflection_counter_102 = -5;
reflexion_number_103 = 0;
timestep_number_104 = 0;
Max_Loops_105 = cs_cast<int>((3E6 + gral_min((double)(9.7E7),(double)(gral_max((double)(gral_abs((double)(P.EtaMaxGral - P.EtaMinGral))),(double)(gral_abs((double)(P.XsiMaxGral - P.XsiMinGral)))) * 1000))));
ObLength_106 = 0;
Ustern_107 = 0;
MOVE_FORWARD: ;
while (timestep_number_104 <= Max_Loops_105) {
{++timestep_number_104;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
xcoord_nteil_Prev_108 = xcoord_nteil_12;
ycoord_nteil_Prev_109 = ycoord_nteil_13;
zcoord_nteil_Prev_110 = zcoord_nteil_14;
FFCellXPrev_111 = FFCellX_84;
FFCellYPrev_112 = FFCellY_85;
if (P.TunnelEntr == true) {
if ((P.TUN_ENTR[FFCellX_84][FFCellY_85] == 1) && (PartHeightAboveTerrain_86 <= 5)) {
goto REMOVE_PARTICLE;
}
}
{tuple_tmp_2 = Zeitschleife_IntWindCalculate(FFCellX_84,FFCellY_85,AHint_58,xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14); UXint_88 = tuple_tmp_2.v0; UYint_89 = tuple_tmp_2.v1; UZint_90 = tuple_tmp_2.v2; IndexK_91 = tuple_tmp_2.v3;}
windge_92 = sqrtf((float)(Program_Pow2(UXint_88) + Program_Pow2(UYint_89)));
if (windge_92 == 0) {
windge_92 = 0.01F;
}
if (PartHeightAboveTerrain_86 < 0) {
goto REMOVE_PARTICLE;
}
if (P.AdaptiveRoughnessMax > 0) {
ObLength_106 = P.OLGral[FFCellX_84][FFCellY_85];
Ustern_107 = P.USternGral[FFCellX_84][FFCellY_85];
roughZ0_95 = P.Z0Gral[FFCellX_84][FFCellY_85];
} else {
ObLength_106 = P.Ob[GrammCellX_56][GrammCellY_57];
Ustern_107 = P.Ustern[GrammCellX_56][GrammCellY_57];
roughZ0_95 = P.Z0Gramm[GrammCellX_56][GrammCellY_57];
}
deltaZ_96 = roughZ0_95;
if (deltaZ_96 < PartHeightAboveBuilding_87) {
deltaZ_96 = PartHeightAboveBuilding_87;
}
eps_113 = Program_Pow3(Ustern_107) / deltaZ_96 / 0.4F * powf((float)(1.0f + 0.5F * powf((float)(PartHeightAboveBuilding_87 / gral_abs((float)(ObLength_106))),(float)(0.8F))),(float)(1.8F));
depo_reflection_counter_102++;
W0int_114 = 0;
varw2_115 = 0;
skew_116 = 0;
curt_117 = 0;
dskew_118 = 0;
wstern_119 = Ustern_107 * 2.9F;
vert_blh_120 = PartHeightAboveBuilding_87 * blhRez_38;
hterm_121 = (1 - vert_blh_120);
dvarw_122 = 0;
dcurt_123 = 0;
if (ObLength_106 >= 0) {
if (ISTATISTIK_50 != (3)) {
W0int_114 = Ustern_107 * P.StdDeviationW * 1.25F;
} else {
W0int_114 = P.W0int;
}
varw_60 = Program_Pow2(W0int_114);
varw2_115 = Program_Pow2(varw_60);
curt_117 = 3 * varw2_115;
} else {
if (ISTATISTIK_50 == (3)) {
varw_60 = Program_Pow2(P.W0int);
} else {
if (ObLength_106 != cachedObLength_40 || Ustern_107 != cachedUstern_41) {
cachedVariance_42 = Program_Pow2(Ustern_107) * Program_Pow2(1.15F + 0.1F * powf((float)(blh_37 / (-ObLength_106)),(float)(0.67F))) * Program_Pow2(P.StdDeviationW);
cachedObLength_40 = ObLength_106;
cachedUstern_41 = Ustern_107;
}
varw_60 = cachedVariance_42;
}
varw2_115 = Program_Pow2(varw_60);
if ((PartHeightAboveBuilding_87 < P.BdLayH10) || (PartHeightAboveBuilding_87 >= blh_37)) {
skew_116 = 0;
dskew_118 = 0;
} else {
skew_116 = a3_48 * PartHeightAboveBuilding_87 * Program_Pow2(hterm_121) * Program_Pow3(wstern_119) * blhRez_38;
dskew_118 = a3_48 * Program_Pow3(wstern_119) * blhRez_38 * Program_Pow2(hterm_121) - 2 * a3_48 * Program_Pow3(wstern_119) * PartHeightAboveBuilding_87 * hterm_121 * blhRez2_39;
}
if (PartHeightAboveBuilding_87 < P.BdLayH10) {
curt_117 = 3 * varw2_115;
} else {
curt_117 = 3.5F * varw2_115;
}
}
idt_62 = 0.1F * varw_60 / eps_113;
idtt_124 = 0.5F * gral_min((float)(P.GralDx),(float)(FFGridX_25)) / (windge_92 + sqrtf((float)(Program_Pow2(velxold_99) + Program_Pow2(velyold_100))));
if (idtt_124 < idt_62) {
{idt_62 = idtt_124;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
}
{gral_clamp((float)(idt_62),(float)(0.1F),(float)(4.0f));if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
{auszeit_63 += idt_62;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
if (auszeit_63 >= P.DispTimeSum) {
TransientConcentration_Conz5dZeitschleifeTransient(reflexion_flag_49,zcoord_nteil_14,AHint_58,mass_real_19,Area_cart_17,idt_62,xsi_73,eta_74,SG_nteil_20);
goto REMOVE_PARTICLE;
}
dummyterm_125 = curt_117 - Program_Pow2(skew_116) / varw_60 - varw2_115;
if (dummyterm_125 == 0) {
dummyterm_125 = 0.000001F;
}
alpha_126 = (0.3333F * dcurt_123 - skew_116 / (2 * varw_60) * (dskew_118 - P.C0z * eps_113) - varw_60 * dvarw_122) / dummyterm_125;
gral_clamp((float)(alpha_126),(float)(-0.1F),(float)(0.1F));
beta_127 = (dskew_118 - 2 * skew_116 * alpha_126 - P.C0z * eps_113) / (2 * varw_60);
gamma_128 = dvarw_122 - varw_60 * alpha_126;
acc_129 = alpha_126 * Program_Pow2(velzold_101) + beta_127 * velzold_101 + gamma_128;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = u_rg_10 == (4294967295u) ? 1.0F : (u_rg_10 + 1) * 2.328306435454494e-10F;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
zahl1_9 = sqrtf((float)(-2.0f * logf((float)(u1_rg_11)))) * sinf((float)((6.2831855f) * (u_rg_10 + 1) * (2.3283064E-10f)));
gral_clamp((float)(zahl1_9),(float)(-2),(float)(2));
velz_130 = acc_129 * idt_62 + sqrtf((float)(P.C0z * eps_113 * idt_62)) * zahl1_9 + velzold_101;
if (velz_130 == 0) {
velz_130 = 0.01F;
}
_kkart_131 = P.KKART[FFCellX_84][FFCellY_85];
if ((P.KKART[FFCellX_84 - 1][FFCellY_85] > _kkart_131) && (IndexK_91 <= P.KKART[FFCellX_84 - 1][FFCellY_85]) && (P.CUTK[FFCellX_84 - 1][FFCellY_85] > 0)) {
velz_130 += velz_130 / gral_abs((float)(velz_130)) * 0.67F * Program_FloatMax(-P.UK[FFCellX_84 + 1][FFCellY_85][IndexK_91],0);
}
if ((P.KKART[FFCellX_84 + 1][FFCellY_85] > _kkart_131) && (IndexK_91 <= P.KKART[FFCellX_84 + 1][FFCellY_85]) && (P.CUTK[FFCellX_84 + 1][FFCellY_85] > 0)) {
velz_130 += velz_130 / gral_abs((float)(velz_130)) * 0.67F * Program_FloatMax(P.UK[FFCellX_84][FFCellY_85][IndexK_91],0);
}
if ((P.KKART[FFCellX_84][FFCellY_85 - 1] > _kkart_131) && (IndexK_91 <= P.KKART[FFCellX_84][FFCellY_85 - 1]) && (P.CUTK[FFCellX_84][FFCellY_85 - 1] > 0)) {
velz_130 += velz_130 / gral_abs((float)(velz_130)) * 0.67F * Program_FloatMax(-P.VK[FFCellX_84][FFCellY_85 + 1][IndexK_91],0);
}
if ((P.KKART[FFCellX_84][FFCellY_85 + 1] > _kkart_131) && (IndexK_91 <= P.KKART[FFCellX_84][FFCellY_85 + 1]) && (P.CUTK[FFCellX_84][FFCellY_85 + 1] > 0)) {
velz_130 += velz_130 / gral_abs((float)(velz_130)) * 0.67F * Program_FloatMax(P.VK[FFCellX_84][FFCellY_85][IndexK_91],0);
}
if (ObLength_106 >= 0) {
gral_clamp((float)(velz_130),(float)(-3),(float)(3));
} else {
gral_clamp((float)(velz_130),(float)(-5),(float)(5));
}
{zcoord_nteil_14 += (velz_130 + UZint_90) * idt_62;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
velzold_101 = velz_130;
if (Deposition_type_21 > 0 && vsed_22 > 0) {
{zcoord_nteil_14 -= vsed_22 * idt_62;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
}
if (zcoord_nteil_14 >= P.ModelTopHeight) {
goto REMOVE_PARTICLE;
}
corx_132 = velxold_99 * idt_62;
cory_133 = velyold_100 * idt_62;
{tuple_tmp_3 = Zeitschleife_IntStandCalculate(0,roughZ0_95,PartHeightAboveBuilding_87,windge_92,sigmauhurly_61); U0int_97 = tuple_tmp_3.v0; V0int_98 = tuple_tmp_3.v1;}
param_134 = 0;
if (ISTATISTIK_50 != (3)) {
param_134 = Program_FloatMax(8.5F / Program_Pow2(windge_92 + 1),0.0f);
} else {
param_134 = P.MWindMEander;
}
T3_135 = 0;
termp_136 = 0;
termq_137 = 0;
if (windge_92 >= 2.5F || P.MeanderingOff || param_134 == 0 || P.TAUS <= 600) {
T3_135 = 2 * Program_Pow2(V0int_98) / P.C0x / eps_113;
termp_136 = 1 / T3_135;
} else {
termp_136 = 1 / (2 * Program_Pow2(V0int_98) / P.C0x / eps_113);
if (ISTATISTIK_50 != (3)) {
Tstern_138 = 200 * param_134 + 350;
T3_135 = Tstern_138 / 6.28F / (Program_Pow2(param_134) + 1) * param_134;
} else if (ISTATISTIK_50 == (3)) {
T3_135 = P.TWindMeander;
}
termq_137 = param_134 / (Program_Pow2(param_134) + 1) / T3_135;
}
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = u_rg_10 == (4294967295u) ? 1.0F : (u_rg_10 + 1) * 2.328306435454494e-10F;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
zuff1_139 = sqrtf((float)(-2.0f * logf((float)(u1_rg_11)))) * sinf((float)((6.2831855f) * (u_rg_10 + 1) * (2.3283064E-10f)));
gral_clamp((float)(zuff1_139),(float)(-2),(float)(2));
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = u_rg_10 == (4294967295u) ? 1.0F : (u_rg_10 + 1) * 2.328306435454494e-10F;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
zuff2_140 = sqrtf((float)(-2.0f * logf((float)(u1_rg_11)))) * sinf((float)((6.2831855f) * (u_rg_10 + 1) * (2.3283064E-10f)));
gral_clamp((float)(zuff1_139),(float)(-2),(float)(2));
velX_141 = velxold_99 + UXint_88;
velY_142 = velyold_100 + UYint_89;
velx_143 = velxold_99 - (termp_136 * velxold_99 + termq_137 * velyold_100) * idt_62 + U0int_97 * sqrtf((float)(2 * termp_136 * idt_62)) * zuff1_139 + idt_62 * (DUDX_68 * velX_141 + DUDY_70 * velY_142 + U0int_97 * DSIGUDX_64) + idt_62 * (velxold_99 / U0int_97 * (DSIGUDX_64 * velX_141 + DSIGUDY_65 * velY_142));
vely_144 = velyold_100 - (termp_136 * velyold_100 - termq_137 * velxold_99) * idt_62 + V0int_98 * sqrtf((float)(2 * termp_136 * idt_62)) * zuff2_140 + idt_62 * (DVDX_69 * velX_141 + DVDY_71 * velY_142 + V0int_98 * DSIGVDY_67) + idt_62 * (velyold_100 / V0int_98 * (DSIGVDX_66 * velX_141 + DSIGVDY_67 * velY_142));
velxold_99 = velx_143;
velyold_100 = vely_144;
{xcoord_nteil_12 += idt_62 * UXint_88 + corx_132;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
{ycoord_nteil_13 += idt_62 * UYint_89 + cory_133;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
reflexion_flag_49 = (0);
back_145 = 1;
while (back_145 == 1) {
xsi_73 = xcoord_nteil_12 - IKOOAGRAL_43;
eta_74 = ycoord_nteil_13 - JKOOAGRAL_44;
xsi1_75 = xcoord_nteil_12 - P.GrammWest;
eta1_76 = ycoord_nteil_13 - P.GrammSouth;
if ((eta_74 <= P.EtaMinGral) || (xsi_73 <= P.XsiMinGral) || (eta_74 >= P.EtaMaxGral) || (xsi_73 >= P.XsiMaxGral)) {
goto REMOVE_PARTICLE;
}
if (topo_52 == (1)) {
FFCellXPrev_111 = FFCellX_84;
FFCellYPrev_112 = FFCellY_85;
FFCellX_84 = cs_cast<int>((xsi_73 * FFGridXRez_27)) + 1;
FFCellY_85 = cs_cast<int>((eta_74 * FFGridYRez_28)) + 1;
GrammCellX_56 = gral_clamp((int)(cs_cast<int>((xsi1_75 * GrammGridXRez_29)) + 1),(int)(1),(int)(P.NX));
GrammCellY_57 = gral_clamp((int)(cs_cast<int>((eta1_76 * GrammGridYRez_30)) + 1),(int)(1),(int)(P.NY));
if ((FFCellX_84 > P.NII) || (FFCellY_85 > P.NJJ) || (FFCellX_84 < 1) || (FFCellY_85 < 1)) {
goto REMOVE_PARTICLE;
}
} else {
FFCellXPrev_111 = FFCellX_84;
FFCellYPrev_112 = FFCellY_85;
FFCellX_84 = cs_cast<int>((xsi_73 * FFGridXRez_27)) + 1;
FFCellY_85 = cs_cast<int>((eta_74 * FFGridYRez_28)) + 1;
if ((FFCellX_84 > P.NII) || (FFCellY_85 > P.NJJ) || (FFCellX_84 < 1) || (FFCellY_85 < 1)) {
goto REMOVE_PARTICLE;
}
}
back_145 = 0;
if (P.BuildingsExist == true || topo_52 == (1)) {
IndexKOld_146 = IndexK_91;
IndexK_91 = Zeitschleife_BinarySearch(zcoord_nteil_14 - P.AHMIN);
if (IndexK_91 <= P.KKART[FFCellX_84][FFCellY_85]) {
if ((IndexK_91 > P.KKART[FFCellXPrev_111][FFCellYPrev_112]) && (IndexK_91 == IndexKOld_146)) {
if (idt_62 * UXint_88 + corx_132 <= 0) {
{xcoord_nteil_12 = 2 * (IKOOAGRAL_43 + FFCellX_84 * FFGridX_25) - xcoord_nteil_12 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
} else {
{xcoord_nteil_12 = 2 * (IKOOAGRAL_43 + (FFCellX_84 - 1) * FFGridX_25) - xcoord_nteil_12 - 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
}
if (idt_62 * UYint_89 + cory_133 <= 0) {
{ycoord_nteil_13 = 2 * (JKOOAGRAL_44 + FFCellY_85 * FFGridY_26) - ycoord_nteil_13 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
} else {
{ycoord_nteil_13 = 2 * (JKOOAGRAL_44 + (FFCellY_85 - 1) * FFGridY_26) - ycoord_nteil_13 - 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
}
} else if ((IndexK_91 > P.KKART[FFCellXPrev_111][FFCellY_85]) && (IndexK_91 == IndexKOld_146)) {
if (idt_62 * UXint_88 + corx_132 <= 0) {
{xcoord_nteil_12 = 2 * (IKOOAGRAL_43 + FFCellX_84 * FFGridX_25) - xcoord_nteil_12 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
} else {
{xcoord_nteil_12 = 2 * (IKOOAGRAL_43 + (FFCellX_84 - 1) * FFGridX_25) - xcoord_nteil_12 - 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
}
} else if ((IndexK_91 > P.KKART[FFCellX_84][FFCellYPrev_112]) && (IndexK_91 == IndexKOld_146)) {
if (idt_62 * UYint_89 + cory_133 <= 0) {
{ycoord_nteil_13 = 2 * (JKOOAGRAL_44 + FFCellY_85 * FFGridY_26) - ycoord_nteil_13 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
} else {
{ycoord_nteil_13 = 2 * (JKOOAGRAL_44 + (FFCellY_85 - 1) * FFGridY_26) - ycoord_nteil_13 - 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
}
} else if ((IndexKOld_146 > P.KKART[FFCellX_84][FFCellY_85]) && (FFCellX_84 == FFCellXPrev_111) && (FFCellY_85 == FFCellYPrev_112)) {
{zcoord_nteil_14 = 2 * P.AHK[FFCellX_84][FFCellY_85] - zcoord_nteil_14 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
PartHeightAboveTerrain_86 = zcoord_nteil_14 - AHint_58;
if (topo_52 == (1)) {
PartHeightAboveBuilding_87 = PartHeightAboveTerrain_86;
}
velzold_101 = -velzold_101;
if (Deposition_type_21 > (0) && depo_reflection_counter_102 >= 0) {
Pd1_147 = Zeitschleife_Pd(varw_60,vsed_22,vdep_23,Deposition_type_21,FFCellX_84,FFCellY_85);
ik_148 = cs_cast<int>((xsi_73 * ConcGridXRez_31)) + 1;
jk_149 = cs_cast<int>((eta_74 * ConcGridYRez_32)) + 1;
depo_L_150 = P.Depo_conz[ik_148][jk_149];
conc_151 = masse_18 * Pd1_147 * area_rez_fac_24;
{
atomic_add(&(depo_L_150[SG_nteil_20]),conc_151);
}
masse_18 -= masse_18 * Pd1_147;
depo_reflection_counter_102 = -2;
if (masse_18 <= 0) {
goto REMOVE_PARTICLE;
}
}
} else if ((IndexKOld_146 > P.KKART[FFCellX_84][FFCellY_85])) {
if (Deposition_type_21 > (0) && depo_reflection_counter_102 >= 0) {
Pd1_152 = Zeitschleife_Pd(varw_60,vsed_22,vdep_23,Deposition_type_21,FFCellX_84,FFCellY_85);
ik_153 = cs_cast<int>((xsi_73 * ConcGridXRez_31)) + 1;
jk_154 = cs_cast<int>((eta_74 * ConcGridYRez_32)) + 1;
depo_L_155 = P.Depo_conz[ik_153][jk_154];
conc_156 = masse_18 * Pd1_152 * area_rez_fac_24;
{
atomic_add(&(depo_L_155[SG_nteil_20]),conc_156);
}
masse_18 -= masse_18 * Pd1_152;
depo_reflection_counter_102 = -2;
if (masse_18 <= 0) {
goto REMOVE_PARTICLE;
}
}
terrainHeight_157 = P.AHK[FFCellX_84][FFCellY_85];
{zcoord_nteil_14 = gral_max((float)(terrainHeight_157),(float)(2 * terrainHeight_157 - zcoord_nteil_14 + 0.01F));if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
} else {
if (Deposition_type_21 > (0) && depo_reflection_counter_102 >= 0) {
Pd1_158 = Zeitschleife_Pd(varw_60,vsed_22,vdep_23,Deposition_type_21,FFCellX_84,FFCellY_85);
ik_159 = cs_cast<int>((xsi_73 * ConcGridXRez_31)) + 1;
jk_160 = cs_cast<int>((eta_74 * ConcGridYRez_32)) + 1;
depo_L_161 = P.Depo_conz[ik_159][jk_160];
conc_162 = masse_18 * Pd1_158 * area_rez_fac_24;
{
atomic_add(&(depo_L_161[SG_nteil_20]),conc_162);
}
masse_18 -= masse_18 * Pd1_158;
depo_reflection_counter_102 = -2;
if (masse_18 <= 0) {
goto REMOVE_PARTICLE;
}
}
if (tunpa_93 == 0) {
if (P.CUTK[FFCellX_84][FFCellY_85] < 1 && (P.AHK[FFCellX_84][FFCellY_85] - zcoord_nteil_14) < 10) {
{zcoord_nteil_14 = P.AHK[FFCellX_84][FFCellY_85] + gral_abs((float)(idt_62 * (UZint_90 + velz_130)));if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
} else {
deltax_163 = xcoord_nteil_12 - xcoord_nteil_Prev_108;
deltay_164 = ycoord_nteil_13 - ycoord_nteil_Prev_109;
{xcoord_nteil_12 -= deltax_163 * 1.05;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
{ycoord_nteil_13 -= deltay_164 * 1.05;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
FFCellXPrev_111 = FFCellX_84;
FFCellYPrev_112 = FFCellY_85;
FFCellX_84 = cs_cast<int>(((xcoord_nteil_12 - IKOOAGRAL_43) * FFGridXRez_27)) + 1;
FFCellY_85 = cs_cast<int>(((ycoord_nteil_13 - JKOOAGRAL_44) * FFGridYRez_28)) + 1;
if ((FFCellX_84 > P.NII) || (FFCellY_85 > P.NJJ) || (FFCellX_84 < 1) || (FFCellY_85 < 1)) {
goto REMOVE_PARTICLE;
}
newTerrainHeight_165 = P.AHK[FFCellX_84][FFCellY_85];
if (zcoord_nteil_14 < newTerrainHeight_165) {
{zcoord_nteil_14 = 2 * newTerrainHeight_165 - zcoord_nteil_14;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
}
if (topo_52 == (1)) {
PartHeightAboveTerrain_86 = zcoord_nteil_14 - newTerrainHeight_165;
PartHeightAboveBuilding_87 = PartHeightAboveTerrain_86;
} else {
PartHeightAboveTerrain_86 = zcoord_nteil_14;
PartHeightAboveBuilding_87 = zcoord_nteil_14;
}
}
}
vorzeichen_166 = -1;
if (velzold_101 < 0) {
vorzeichen_166 = 1;
}
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = u_rg_10 == (4294967295u) ? 1.0F : (u_rg_10 + 1) * 2.328306435454494e-10F;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
zahl1_9 = sqrtf((float)(-2.0f * logf((float)(u1_rg_11)))) * sinf((float)((6.2831855f) * (u_rg_10 + 1) * (2.3283064E-10f)));
velzold_101 = gral_abs((float)(zahl1_9 * sqrtf((float)(varw_60)))) * vorzeichen_166;
}
vorzeichen1_167 = -1;
if (velxold_99 < 0) {
vorzeichen1_167 = 1;
}
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = u_rg_10 == (4294967295u) ? 1.0F : (u_rg_10 + 1) * 2.328306435454494e-10F;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
zahl1_9 = sqrtf((float)(-2.0f * logf((float)(u1_rg_11)))) * sinf((float)((6.2831855f) * (u_rg_10 + 1) * (2.3283064E-10f)));
velxold_99 = gral_abs((float)(zahl1_9 * U0int_97 * 3)) * vorzeichen1_167;
vorzeichen1_167 = -1;
if (velyold_100 < 0) {
vorzeichen1_167 = 1;
}
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u1_rg_11 = u_rg_10 == (4294967295u) ? 1.0F : (u_rg_10 + 1) * 2.328306435454494e-10F;
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
zahl1_9 = sqrtf((float)(-2.0f * logf((float)(u1_rg_11)))) * sinf((float)((6.2831855f) * (u_rg_10 + 1) * (2.3283064E-10f)));
velyold_100 = gral_abs((float)(zahl1_9 * V0int_98 * 3)) * vorzeichen1_167;
back_145 = 1;
{idt_62 = Program_FloatMax(idt_62 * 0.5F,0.05F);if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
if (tunpa_93 > 0) {
goto MOVE_FORWARD;
}
}
}
if (back_145 > 0) {
reflexion_flag_49 = (1);
reflexion_number_103++;
if (reflexion_number_103 > Max_Reflections_77) {
goto REMOVE_PARTICLE;
}
}
}
AHintold_59 = AHint_58;
if (topo_52 == (1)) {
if ((FFCellX_84 < 1) || (FFCellX_84 > P.NII) || (FFCellY_85 < 1) || (FFCellY_85 > P.NJJ)) {
goto REMOVE_PARTICLE;
}
AHint_58 = P.AHK[FFCellX_84][FFCellY_85];
PartHeightAboveTerrain_86 = zcoord_nteil_14 - AHint_58;
PartHeightAboveBuilding_87 = PartHeightAboveTerrain_86;
} else {
if ((FFCellX_84 < 1) || (FFCellX_84 > P.NII) || (FFCellY_85 < 1) || (FFCellY_85 > P.NJJ)) {
goto REMOVE_PARTICLE;
}
AHint_58 = 0;
PartHeightAboveTerrain_86 = zcoord_nteil_14;
PartHeightAboveBuilding_87 = PartHeightAboveTerrain_86;
}
ObL_94 = P.Ob[GrammCellX_56][GrammCellY_57];
if (P.AdaptiveRoughnessMax > 0) {
ObL_94 = P.OLGral[FFCellX_84][FFCellY_85];
}
if ((PartHeightAboveBuilding_87 >= blh_37) && (ObL_94 < 0)) {
{zcoord_nteil_14 = blh_37 + AHint_58 - (PartHeightAboveBuilding_87 - blh_37) - 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
velzold_101 = -velzold_101;
PartHeightAboveTerrain_86 = zcoord_nteil_14 - AHint_58;
PartHeightAboveBuilding_87 = PartHeightAboveTerrain_86;
}
if (PartHeightAboveTerrain_86 <= 0) {
{zcoord_nteil_14 = AHint_58 - PartHeightAboveTerrain_86 + 0.01F;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
velzold_101 = -velzold_101;
PartHeightAboveTerrain_86 = zcoord_nteil_14 - AHint_58;
PartHeightAboveBuilding_87 = PartHeightAboveTerrain_86;
if (Deposition_type_21 > 0 && depo_reflection_counter_102 >= 0) {
Pd1_168 = Zeitschleife_Pd(varw_60,vsed_22,vdep_23,Deposition_type_21,FFCellX_84,FFCellY_85);
ik_169 = cs_cast<int>((xsi_73 * ConcGridXRez_31)) + 1;
jk_170 = cs_cast<int>((eta_74 * ConcGridYRez_32)) + 1;
depo_L_171 = P.Depo_conz[ik_169][jk_170];
conc_172 = masse_18 * Pd1_168 * area_rez_fac_24;
{
atomic_add(&(depo_L_171[SG_nteil_20]),conc_172);
}
masse_18 -= masse_18 * Pd1_168;
depo_reflection_counter_102 = -2;
if (masse_18 <= 0) {
goto REMOVE_PARTICLE;
}
}
}
iko_173 = cs_cast<int>((xsi_73 * ConcGridXRez_31)) + 1;
jko_174 = cs_cast<int>((eta_74 * ConcGridYRez_32)) + 1;
if ((iko_173 > P.NXL) || (iko_173 < 0) || (jko_174 > P.NYL) || (jko_174 < 0)) {
goto REMOVE_PARTICLE;
}
zcoordRelative_175 = 0;
if (P.BuildingsExist == true && P.Topo == (1)) {
zcoordRelative_175 = zcoord_nteil_14 - AHint_58 + P.CUTK[FFCellX_84][FFCellY_85];
} else {
zcoordRelative_175 = zcoord_nteil_14 - AHint_58;
}
{
if (auszeit_63 > 3600) {
distanceParticle_83 += (float)(sqrt((double)(Program_Pow2(xcoord_nteil_12 - xcoord_nteil_Prev_108) + Program_Pow2(ycoord_nteil_13 - ycoord_nteil_Prev_109))));
distanceParticle_83 *= 0.5F;
if (distanceParticle_83 < 0.02F) {
goto REMOVE_PARTICLE;
}
}
zko_176 = cs_cast<int>(((zcoordRelative_175 - AHint_58) * ConcGridZRez_33));
if (iko_173 == ConcCellPrevX_78 && jko_174 == ConcCellPrevY_79 && zko_176 == ConcCellPrevZ_80) {
ConcCellTime_82 += idt_62;
if (ConcCellTime_82 > ConcCellTimeMax_81 - 10) {
{zcoord_nteil_14 += 2;if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;}
if (ConcCellTime_82 > ConcCellTimeMax_81) {
goto REMOVE_PARTICLE;
}
}
} else {
ConcCellPrevX_78 = iko_173;
ConcCellPrevY_79 = jko_174;
ConcCellPrevZ_80 = zko_176;
ConcCellTime_82 = 0;
}
}
for (II_177 = 0;II_177 < kko_45.size();II_177++) {
slice_178 = (zcoordRelative_175 - P.HorSlices[II_177]) * ConcGridZRez_33;
if (gral_abs((float)(slice_178)) > (32767)) {
goto REMOVE_PARTICLE;
}
kko_45[II_177] = cs_cast<int>(slice_178);
}
if (decay_rate_72 > 0) {
masse_18 *= exp((double)(-decay_rate_72 * idt_62));
}
if (P.WetDeposition == true && P.WetDepoRW != 0) {
epsilonW_Masse_179 = masse_18 * P.WetDepoRW * idt_62;
ex_180 = false;
if (epsilonW_Masse_179 > masse_18) {
epsilonW_Masse_179 = masse_18;
ex_180 = true;
}
masse_18 -= epsilonW_Masse_179;
if (!P.WetDopoDropDrift) {
depo_L_181 = P.Depo_conz[iko_173][jko_174];
conc_182 = epsilonW_Masse_179 * area_rez_fac_24;
{
atomic_add(&(depo_L_181[SG_nteil_20]),conc_182);
}
} else {

for (dropDataIndex_183 = 0;dropDataIndex_183 < P.WetDepoDropVelocity.d[1];dropDataIndex_183++) {
if (P.WetDepoDropVelocity(dropDataIndex_183,0) >= P.WetDepoPrecipitation) {
break;
}
}
for (dropSizeClass_184 = 1;dropSizeClass_184 < 5;dropSizeClass_184++) {
m_z_8 = 36969 * (m_z_8 & 65535) + (m_z_8 >> 16);
m_w_7 = 18000 * (m_w_7 & 65535) + (m_w_7 >> 16);
u_rg_10 = (m_z_8 << 16) + m_w_7;
u_random_185 = (float)((0.5F + u_rg_10 * 1.16415321772724E-10));
depoX_186 = xcoord_nteil_12;depoY_187 = ycoord_nteil_13;depoEpsilonW_188 = epsilonW_Masse_179 * P.WetDepoDropVelocity(dropDataIndex_183,4 + dropSizeClass_184) * 0.01F;depoCellH_189 = P.DZK[IndexK_91];
depoIndexK_190 = IndexK_91;depoFFX_191 = FFCellX_84;depoFFY_192 = FFCellY_85;
depo1stCell_193 = P.DZK[1];
if (P.BuildingsExist == false) {
depoCellH_189 = 2;
depo1stCell_193 = 2;
}
depoVelVertical_194 = P.WetDepoDropVelocity(dropDataIndex_183,dropSizeClass_184) * u_random_185;depoTime_195 = (float)(depoCellH_189) * 4 / depoVelVertical_194;depoU_196 = UXint_88;depoV_197 = UYint_89;depoZ_198 = UZint_90;depoZcoord_199 = zcoord_nteil_14;depoAH_200 = AHint_58;if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
unsigned int depoDriftIter = 0;
while (depoZcoord_199 > depoAH_200) {
if(++depoDriftIter > GRAL_DROP_DRIFT_MAX_ITER){RecordDeviceError(3,2,trace_index);return;}
if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
depoZcoord_199 += depoZ_198 * depoTime_195 - (float)(depoCellH_189) * 4;if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
if (depoZcoord_199 > depo1stCell_193) {
depoX_186 += depoTime_195 * depoU_196;if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
depoY_187 += depoTime_195 * depoV_197;if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
depoFFX_191 = cs_cast<int>(((depoX_186 - IKOOAGRAL_43) * FFGridXRez_27)) + 1;
depoFFY_192 = cs_cast<int>(((depoY_187 - JKOOAGRAL_44) * FFGridYRez_28)) + 1;
if ((depoFFX_191 < 1) || (depoFFX_191 > P.NII) || (depoFFY_192 < 1) || (depoFFY_192 > P.NJJ)) {
depoEpsilonW_188 = 0;if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
break;
}
depoAH_200 = P.AHK[depoFFX_191][depoFFY_192];if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
{tuple_tmp_4 = Zeitschleife_IntWindCalculate(depoFFX_191,depoFFY_192,depoAH_200,depoX_186,depoY_187,depoZcoord_199); depoU_196 = tuple_tmp_4.v0; depoV_197 = tuple_tmp_4.v1; depoZ_198 = tuple_tmp_4.v2; depoIndexK_190 = tuple_tmp_4.v3;if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}}
if (P.BuildingsExist == false) {
depoCellH_189 = 2;if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
} else {
depoCellH_189 = P.DZK[depoIndexK_190];if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
}
depoTime_195 = (float)(depoCellH_189) * 4 / depoVelVertical_194;if(!gral_drop_finite(depoX_186,depoY_187,depoZcoord_199,depoU_196,depoV_197,depoZ_198,depoAH_200,depoCellH_189,depoTime_195,depoVelVertical_194,depoEpsilonW_188,depo1stCell_193)){RecordDeviceError(2,2,trace_index);return;}
}
}
depoiko_201 = cs_cast<int>(((depoX_186 - IKOOAGRAL_43) * ConcGridXRez_31)) + 1;
depojko_202 = cs_cast<int>(((depoY_187 - JKOOAGRAL_44) * ConcGridYRez_32)) + 1;
if ((depoiko_201 >= P.NXL) || (depoiko_201 < 2) || (depojko_202 >= P.NYL) || (depojko_202 < 2)) {
depoEpsilonW_188 = 0;
} else {
depo_L_203 = P.Depo_conz[depoiko_201][depojko_202];
conc_204 = depoEpsilonW_188 * area_rez_fac_24;
{
atomic_add(&(depo_L_203[SG_nteil_20]),conc_204);
}
}
}
}
if (ex_180) {
goto REMOVE_PARTICLE;
}
}
if (Deposition_type_21 < 2) {
if (P.ReceptorsAvailable && reflexion_flag_49 == (0) && P.GridRecStart != nullptr) {
if (GridCellLookup(iko_173,jko_174,receptors_205)) {
for (int fe_irec_206=0;fe_irec_206<receptors_205.size();fe_irec_206++) {
irec_206=receptors_205[fe_irec_206];
{
if (P.ReceptorNearbyBuilding[irec_206]) {
if (iko_173 == P.ReceptorIInd[irec_206] && jko_174 == P.ReceptorJInd[irec_206]) {
slice_207 = (zcoord_nteil_14 - AHint_58 - P.ReceptorZ[irec_206]) * ConcGridZRez_33;
if (cs_cast<int>(slice_207) == 0) {
ReceptorConcentration_47[irec_206] += idt_62 * masse_18;
}
}
} else {
if (gral_abs((double)(xcoord_nteil_12 - P.ReceptorX[irec_206])) < ConcGridXHalf_34 && gral_abs((double)(ycoord_nteil_13 - P.ReceptorY[irec_206])) < ConcGridYHalf_35) {
slice_208 = (zcoord_nteil_14 - AHint_58 - P.ReceptorZ[irec_206]) * ConcGridZRez_33;
if (cs_cast<int>(slice_208) == 0) {
ReceptorConcentration_47[irec_206] += idt_62 * masse_18;
}
}
}
}
}
}
}
for (II_209 = 0;II_209 < kko_45.size();II_209++) {
if ((kko_45[II_209] == 0) && (reflexion_flag_49 == (0))) {
conz3d_L_210 = P.Conz3dDouble[iko_173][jko_174][II_209];
conc_211 = masse_18 * idt_62;
{
atomic_add(&(conz3d_L_210[SG_nteil_20]),conc_211);
}
}
}
if (reflexion_flag_49 == (0) && P.WriteVerticalConcentration) {
TransientGridZ_55 = TransientConcentration_BinarySearchTransient(zcoord_nteil_14 - AHint_58);
TransientGridX_53 = cs_cast<int>((xsi_73 * FFGridXRez_27)) + 1;
TransientGridY_54 = cs_cast<int>((eta_74 * FFGridYRez_28)) + 1;
conzsum_L_212 = P.ConzSsum[TransientGridX_53][TransientGridY_54];
conc_213 = mass_real_19 * idt_62 / (Area_cart_17 * P.DZK_Trans[TransientGridZ_55]);
{
atomic_add(&(conzsum_L_212[TransientGridZ_55]),(float)(conc_213));
}
}
if (P.Odour == true) {
for (II_214 = 0;II_214 < kko_45.size();II_214++) {
slice_215 = (zcoordRelative_175 - (P.HorSlices[II_214] + P.GralDz)) * ConcGridZRez_33;
kko_45[II_214] = cs_cast<int>((gral_min((float)((2147483647)),(float)(slice_215))));
}
for (II_216 = 0;II_216 < kko_45.size();II_216++) {
if ((kko_45[II_216] == 0) && (reflexion_flag_49 == (0))) {
conz3dp_L_217 = P.Conz3dp[iko_173][jko_174][II_216];
conc_218 = masse_18 * idt_62;
{
atomic_add(&(conz3dp_L_217[SG_nteil_20]),(float)(conc_218));
}
}
}
for (II_219 = 0;II_219 < kko_45.size();II_219++) {
slice_220 = (zcoordRelative_175 - (P.HorSlices[II_219] - P.GralDz)) * ConcGridZRez_33;
kko_45[II_219] = cs_cast<int>((gral_min((float)((2147483647)),(float)(slice_220))));
}
for (II_221 = 0;II_221 < kko_45.size();II_221++) {
if ((kko_45[II_221] == 0) && (reflexion_flag_49 == (0))) {
conz3dm_L_222 = P.Conz3dm[iko_173][jko_174][II_221];
conc_223 = masse_18 * idt_62;
{
atomic_add(&(conz3dm_L_222[SG_nteil_20]),(float)(conc_223));
}
}
}
}
}
}
REMOVE_PARTICLE: ;
if (P.ReceptorsAvailable) {
for (ii_224 = 1;ii_224 < ReceptorConcentration_47.size();ii_224++) {
recconc_225 = ReceptorConcentration_47[ii_224];
if (recconc_225 > 0) {
recmit_L_226 = P.ReceptorConc[ii_224];
{
atomic_add(&(recmit_L_226[SG_nteil_20]),recconc_225);
atomic_max(&(P.ReceptorParticleMaxConc[ii_224][SG_nteil_20]),recconc_225);
}
}
}
}
{if(!CheckParticleFinite(xcoord_nteil_12,ycoord_nteil_13,zcoord_nteil_14,auszeit_63,idt_62,2,trace_index))return;if(trace_slot>=0){P.ResidualTrace(trace_slot,7)=xcoord_nteil_12;P.ResidualTrace(trace_slot,8)=ycoord_nteil_13;P.ResidualTrace(trace_slot,9)=zcoord_nteil_14;P.ResidualTrace(trace_slot,10)=auszeit_63;P.ResidualTrace(trace_slot,11)=timestep_number_104;P.ResidualTrace(trace_slot,12)=m_w_7;P.ResidualTrace(trace_slot,13)=m_z_8;P.ResidualTrace(trace_slot,14)=masse_18;P.ResidualTrace(trace_slot,15)=1;}
return ;}
}

__device__ unsigned long long ZeitschleifeNonSteadyState_MixSeed(unsigned long long seed) {

{
seed += 0x9E3779B97F4A7C15ull;
seed = (seed ^ (seed >> 30)) * 0xBF58476D1CE4E5B9ull;
seed = (seed ^ (seed >> 27)) * 0x94D049BB133111EBull;
return seed ^ (seed >> 31);
}
}

__device__ void ZeitschleifeNonSteadyState_ResidualSeeds(int i,int j,int k,int SG,int splitIndex,unsigned int& m_w,unsigned int& m_z) {
unsigned long long seed_7{};
seed_7 = ZeitschleifeNonSteadyState_MixSeed(((unsigned long long)(m_z) << 32) | m_w);
seed_7 = ZeitschleifeNonSteadyState_MixSeed(seed_7 ^ (unsigned long long)((unsigned int)(P.IWET)) ^ 0x7265736964ull);
seed_7 = ZeitschleifeNonSteadyState_MixSeed(seed_7 ^ ((unsigned long long)((unsigned int)(i)) | ((unsigned long long)((unsigned int)(j)) << 32)));
seed_7 = ZeitschleifeNonSteadyState_MixSeed(seed_7 ^ ((unsigned long long)((unsigned int)(k)) | ((unsigned long long)((unsigned int)(SG)) << 32)));
seed_7 = ZeitschleifeNonSteadyState_MixSeed(seed_7 ^ (unsigned long long)((unsigned int)(splitIndex)));
m_w = (unsigned int)(seed_7);
m_z = (unsigned int)((seed_7 >> 32));
if (m_w % 1179647999u == 0u) m_w = 521288629u;
if (m_z % 2422800383u == 0u) m_z = 2232121u;
}

__device__ Tuple4<float,float,float,int> Zeitschleife_IntWindCalculate(int IndexI,int IndexJ,float AHint,double xcoord,double ycoord,float zcoord) {
float DXK_6{};
float DYK_7{};
float UXint_8{};
float UYint_9{};
float UZint_10{};
int IndexK_11{};
float f1_12{};
float u1_13{};
float u2_14{};
float v1_15{};
float v2_16{};
Arr<float,1> WK_L_17{};
float w1_18{};
float w2_19{};
int inumm_20{};
int NKK_21{};
float exponent_22{};
float dumfac_23{};
int ipo_24{};
int iprof_25{};
float help_26{};
float u1_27{};
float u2_28{};
float v1_29{};
float v2_30{};
Arr<float,1> WK_L_31{};
float w1_32{};
float w2_33{};
DXK_6 = P.DXK;
DYK_7 = P.DYK;
UXint_8 = 0;
UYint_9 = 0;
UZint_10 = 0;
IndexK_11 = 0;
if (P.Topo == (1)) {
f1_12 = zcoord - P.AHMIN;
IndexK_11 = Zeitschleife_BinarySearch(f1_12);
u1_13 = P.UK[IndexI][IndexJ][IndexK_11];
u2_14 = P.UK[IndexI + 1][IndexJ][IndexK_11];
UXint_8 = (float)((u1_13 + (u2_14 - u1_13) / DXK_6 * (xcoord - (P.IKOOAGRAL + (IndexI - 1) * DXK_6))));
v1_15 = P.VK[IndexI][IndexJ][IndexK_11];
v2_16 = P.VK[IndexI][IndexJ + 1][IndexK_11];
UYint_9 = (float)((v1_15 + (v2_16 - v1_15) / DYK_7 * (ycoord - (P.JKOOAGRAL + (IndexJ - 1) * DYK_7))));
WK_L_17 = P.WK[IndexI][IndexJ];
w1_18 = WK_L_17[IndexK_11];
w2_19 = WK_L_17[IndexK_11 + 1];
UZint_10 = w1_18 + (w2_19 - w1_18) / P.DZK[IndexK_11] * (f1_12 - P.HOKART[IndexK_11 - 1]);
} else if (P.Topo == (0)) {
inumm_20 = P.MetProfileNumb;
NKK_21 = P.NKK;
if (P.BuildingsExist == false) {


if (zcoord <= P.MeasurementHeight[1]) {
if (P.Ob[1][1] <= 0) {
exponent_22 = gral_max((float)(0.35F - 0.4F * powf((float)(gral_abs((float)(P.Ob[1][1]))),(float)(-0.15F))),(float)(0.05F));
} else {
exponent_22 = 0.56F * powf((float)(P.Ob[1][1]),(float)(-0.15F));
}
dumfac_23 = powf((float)(zcoord / P.MeasurementHeight[1]),(float)(exponent_22));
UXint_8 = P.ObsWindU[1] * dumfac_23;
UYint_9 = P.ObsWindV[1] * dumfac_23;
} else if (zcoord >= P.MeasurementHeight[inumm_20]) {
if (inumm_20 == 1) {
if (P.Ob[1][1] <= 0) {
exponent_22 = gral_max((float)(0.35F - 0.4F * powf((float)(gral_abs((float)(P.Ob[1][1]))),(float)(-0.15F))),(float)(0.05F));
} else {
exponent_22 = 0.56F * powf((float)(P.Ob[1][1]),(float)(-0.15F));
}
dumfac_23 = powf((float)(zcoord / P.MeasurementHeight[inumm_20]),(float)(exponent_22));
UXint_8 = P.ObsWindU[inumm_20] * dumfac_23;
UYint_9 = P.ObsWindV[inumm_20] * dumfac_23;
} else {
UXint_8 = P.ObsWindU[inumm_20];
UYint_9 = P.ObsWindV[inumm_20];
}
} else {
ipo_24 = 1;
for (iprof_25 = 1;iprof_25 <= inumm_20;iprof_25++) {
if (zcoord > P.MeasurementHeight[iprof_25]) {
ipo_24 = iprof_25 + 1;
}
}
help_26 = 1 / (P.MeasurementHeight[ipo_24] - P.MeasurementHeight[ipo_24 - 1]) * (zcoord - P.MeasurementHeight[ipo_24 - 1]);
UXint_8 = P.ObsWindU[ipo_24 - 1] + (P.ObsWindU[ipo_24] - P.ObsWindU[ipo_24 - 1]) * help_26;
UYint_9 = P.ObsWindV[ipo_24 - 1] + (P.ObsWindV[ipo_24] - P.ObsWindV[ipo_24 - 1]) * help_26;
}
} else if (P.BuildingsExist == true) {
for (IndexK_11 = 1;IndexK_11 <= NKK_21;IndexK_11++) {
if (zcoord <= P.HOKART[IndexK_11]) {
break;
}
}
if (IndexK_11 > NKK_21) {
IndexK_11 = NKK_21;
}
u1_27 = P.UK[IndexI][IndexJ][IndexK_11];
u2_28 = P.UK[IndexI + 1][IndexJ][IndexK_11];
UXint_8 = (float)((u1_27 + (u2_28 - u1_27) / DXK_6 * (xcoord - (P.IKOOAGRAL + (IndexI - 1) * DXK_6))));
v1_29 = P.VK[IndexI][IndexJ][IndexK_11];
v2_30 = P.VK[IndexI][IndexJ + 1][IndexK_11];
UYint_9 = (float)((v1_29 + (v2_30 - v1_29) / DYK_7 * (ycoord - (P.JKOOAGRAL + (IndexJ - 1) * DYK_7))));
WK_L_31 = P.WK[IndexI][IndexJ];
w1_32 = WK_L_31[IndexK_11];
w2_33 = WK_L_31[IndexK_11 + 1];
UZint_10 = w1_32 + (w2_33 - w1_32) / P.DZK[IndexK_11] * (zcoord + AHint - P.HOKART[IndexK_11 - 1]);
}
}
return {UXint_8,UYint_9,UZint_10,IndexK_11};
}

__device__ int Zeitschleife_BinarySearch(float Height) {
int nkk_1{};
int low_2{};
int high_3{};
int aQuarter_4{};
Arr<float,1> hokart_5{};
int mid_6{};
nkk_1 = P.NKK;
low_2 = 1;
high_3 = nkk_1;
aQuarter_4 = high_3 >> 3;
hokart_5 = P.HOKART;
if (hokart_5[aQuarter_4] >= Height) {
high_3 = aQuarter_4;
} else {
low_2 = aQuarter_4;
}
mid_6 = 0;
while (low_2 <= high_3) {
mid_6 = (low_2 + high_3) >> 1;
if (hokart_5[mid_6] >= Height) {
high_3 = mid_6 - 1;
} else {
low_2 = mid_6 + 1;
}
}
if (low_2 > nkk_1) {
low_2 = nkk_1;
}
return low_2;
}

__device__ float Program_FloatMax(float val1,float val2) {

if (val1 > val2) {
return val1;
}
return val2;
}

__device__ double Program_Pow3(double x1) {

return (x1 * x1 * x1);
}

__device__ float Program_Pow3(float x1) {

return (x1 * x1 * x1);
}

__device__ double Program_Pow2(double x1) {

return (x1 * x1);
}

__device__ float Program_Pow2(float x1) {

return (x1 * x1);
}

extern "C" __global__ void gral_fresh(int first,int count){int q=blockIdx.x*blockDim.x+threadIdx.x;if(q<count){Zeitschleife_Calculate(first+q);}}
extern "C" __global__ void gral_residual(int count,int trace_base,const int* i,const int* j,const int* k,const int* sg,const float* conc,const int* split){int q=blockIdx.x*blockDim.x+threadIdx.x;if(q<count)ZeitschleifeNonSteadyState_ParticleDriver(i[q],j[q],k[q],sg[q],conc[q],split[q],trace_base+q);}
extern "C" __global__ void gral_state_size(unsigned long long* result){*result=sizeof(State);}

