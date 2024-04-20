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
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using System.Threading;

namespace GRAL_2001
{
    public static class U_PrognosticMicroscaleV1_Vec512
    {
        static readonly Vector512<float> Vect_0 = Vector512<float>.Zero;
        static readonly Vector512<float> Vect_1 = Vector512<float>.One;
        static readonly Vector512<float> Vect_01 = Vector512.Create(0.1F);
        static readonly Vector512<float> Vect_025 = Vector512.Create(0.25F);
        static readonly Vector512<float> Vect_05 = Vector512.Create(0.5F);
        static readonly Vector512<float> Vect_099 = Vector512.Create(0.99F);
        static readonly Vector512<float> Vect_0071 = Vector512.Create(0.071F);
        static readonly Vector512<float> Vect_2 = Vector512.Create(2F);
        static readonly Vector512<float> Vect_001 = Vector512.Create(0.01F);
        static readonly Vector512<float> Vect_15 = Vector512.Create(15F);
        static readonly Vector512<float> Vect_100 = Vector512.Create(100F);
        static readonly Vector512<float> Vect_0001 = Vector512.Create(0.0001F);
        
        /// <summary>
        /// Momentum equations for the u wind component - algebraic mixing lenght model
        /// </summary>
        /// <param name="IS">ADI direction for x cells</param>
        /// <param name="JS">ADI direction for y cells</param>
        /// <param name="Cmueh">Constant</param>
        /// <param name="VISHMIN">Minimum horizontal turbulent exchange coefficients </param>
        /// <param name="AREAxy">Area of a flow field cell</param>
        /// <param name="UG">Geostrophic wind</param>
        /// <param name="building_Z0">Roughness of buildings</param>
        /// <param name="relax">Relaxation factor</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        [SkipLocalsInit]
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, Single UG, float relax)
        {
            float DXK = Program.DXK; float DYK = Program.DYK;
            Vector512<float> DXK_V = Vector512.Create(DXK);
            Vector512<float> DYK_V = Vector512.Create(DYK);
            Vector512<float> VISHMIN_V = Vector512.Create(VISHMIN);
            Vector512<float> AREAxy_V = Vector512.Create(AREAxy);
            Vector512<float> UG_V = Vector512.Create(UG);
            
            int maxTasks = Math.Max(1, Program.IPROC + Math.Abs(Environment.TickCount % 8));
            int minL = Math.Min(64, Program.NII - 4); // Avoid too large slices due to perf issues
            Parallel.ForEach(Partitioner.Create(3, Program.NII, Math.Min(Math.Max(4, (Program.NII / maxTasks)), minL)), Program.pOptions, range =>
            //Parallel.For(3, Program.NII, Program.pOptions, i1 =>
            {
                Span<float> PIMU = stackalloc float[Program.KADVMAX + 1];
                Span<float> QIMU = stackalloc float[Program.KADVMAX + 1];
                Span<float> Mask = stackalloc float[Vector512<float>.Count];
                Span<float> Mask2 = stackalloc float[Vector512<float>.Count];
                Span<float> AIM_A = stackalloc float[Vector512<float>.Count];
                Span<float> BIM_A = stackalloc float[Vector512<float>.Count];
                Span<float> CIM_A = stackalloc float[Vector512<float>.Count];
                Span<float> DIMU_A = stackalloc float[Vector512<float>.Count];

                Vector512<float> DVDXN, DVDXS, DUDXE, DUDXW, DWDXT, DWDXB;
                Vector512<float> DE, DW, DS, DN, DT, DB;
                Vector512<float> FE, FW, FS, FN, FT, FB;
                Vector512<float> PE, PW, PS, PN, PT, PB;
                Vector512<float> BIM, CIM, AE1, AW1, AS1, AN1, AIM;
                Vector512<float> DIM_U, windhilf;
                Vector512<float> DDPX;

                Vector512<float> UKS_W_LV;
                Vector512<float> VKS_S_LV, WKS_W_LV, WKS_WB_LV, WKS_WBm_LV, WKS_B_LV, WKS_Bp_LV;

                Vector512<float> VKS_WS_LV, VKS_WN_LV, VKS_E_LV, UK_E_LV, VKS_W_LV;

                Vector512<float> WKS_V, VKS_V, UKS_V, DZK_V;
                Vector512<float> DXKDZK, DYKDZK, AP0_V, intern, intern2;

                Vector512<float> VIS, zs, mixL;
                Vector512<float> UK_LV, UK_W_LV;
                Vector512<float> HOKART_KKART_V;

                Vector512<float> Mask_V = Vector512<float>.Zero;
                Vector512<float> DUDZ, DVDZ;
                Vector512<float> Coriolis = Vector512.Create(Program.CorolisParam);

                for (int i1 = range.Item1; i1 < range.Item2; i1++)
                {
                    int i = i1;
                    if (IS == -1)
                    {
                        i = Program.NII - i1 + 1;
                    }

                    for (int j1 = 2; j1 <= Program.NJJ - 1; ++j1)
                    {
                        int j = j1;
                        if (JS == -1)
                        {
                            j = Program.NJJ - j1 + 1;
                        }

                        if (Program.ADVDOM[i][j] == 1)
                        {
                            //inside a prognostic sub domain
                            int jM1 = j - 1;
                            int jP1 = j + 1;
                            int iM1 = i - 1;
                            int iP1 = i + 1;

                            float[] UK_L = Program.UK[i][j];
                            Single[] UKS_L = Program.UKS[i][j];
                            Single[] VKS_L = Program.VKS[i][j];
                            Single[] WKS_L = Program.WKS[i][j];
                            Single[] DPMNEW_L = Program.DPMNEW[i][j];
                            Single[] UK_W_L = Program.UK[iM1][j];
                            Single[] UK_E_L = Program.UK[iP1][j];
                            Single[] UK_S_L = Program.UK[i][jM1];
                            Single[] UK_N_L = Program.UK[i][jP1];
                            Single[] UKS_W_L = Program.UKS[iM1][j];
                            Single[] VKS_W_L = Program.VKS[iM1][j];
                            Single[] VKS_WS_L = Program.VKS[iM1][jM1];
                            Single[] VKS_S_L = Program.VKS[i][jM1];
                            Single[] VKS_WN_L = Program.VKS[iM1][jP1];
                            Single[] VKS_N_L = Program.VKS[i][jP1];
                            Single[] WKS_W_L = Program.WKS[iM1][j];
                            Single[] DPMNEW_W_L = Program.DPMNEW[iM1][j];

                            Single[] VKS_EN_L = Program.VKS[iP1][jP1];
                            Single[] VKS_ES_L = Program.VKS[iP1][jM1];
                            Single[] WKS_E_L = Program.WK[iP1][j];
                            Single[] VK_L = Program.VK[i][j];

                            int Vert_Index_LL = Program.VerticalIndex[i][j];
                            float Ustern_terrain_helpterm = Program.UsternTerrainHelpterm[i][j];
                            float Ustern_obstacles_helpterm = Program.UsternObstaclesHelpterm[i][j];
                            //At a boundary of the prognostic domain?
                            bool ADVDOM_S = (Program.ADVDOM[i][jM1] < 1) || (j == 2);
                            bool ADVDOM_N = (Program.ADVDOM[i][jP1] < 1) || (j == Program.NJJ - 1);
                            bool ADVDOM_W = (Program.ADVDOM[iM1][j] < 1) || (i == 2);
                            bool ADVDOM_E = (Program.ADVDOM[iP1][j] < 1) || (i == Program.NII - 1);

                            float CUTK_L = Program.CUTK[i][j];
                            Single[] VEG_L = Program.VEG[i][j];
                            Single COV_L = Program.COV[i][j];

                            float Z0 = Program.Z0;
                            if (Program.AdaptiveRoughnessMax < 0.01)
                            {
                                //Use GRAMM Roughness with terrain or Roughness from point 1,1 without terrain
                                int IUstern = 1;
                                int JUstern = 1;
                                if ((Program.Topo == Consts.TerrainAvailable) && (Program.LandUseAvailable == true))
                                {
                                    double x = i * Program.DXK + Program.GralWest;
                                    double y = j * Program.DYK + Program.GralSouth;
                                    double xsi1 = x - Program.GrammWest;
                                    double eta1 = y - Program.GrammSouth;
                                    IUstern = Math.Clamp((int)(xsi1 / Program.DDX[1]) + 1, 1, Program.NX);
                                    JUstern = Math.Clamp((int)(eta1 / Program.DDY[1]) + 1, 1, Program.NY);
                                }
                                Z0 = Program.Z0Gramm[IUstern][JUstern];
                            }
                            else
                            {
                                Z0 = Program.Z0Gral[i][j];
                            }

                            int KKART_LL = Program.KKART[i][j];
                            HOKART_KKART_V = Vector512.Create(Program.HOKART[KKART_LL]);

                            int KSTART = 1;
                            if (CUTK_L == 0) // building heigth == 0 m
                            {
                                KSTART = KKART_LL + 1;
                            }

                            int KKART_LL_P1 = KKART_LL + 1;

                            int KEND = Vert_Index_LL + 1; // simpify for-loop
                            bool end = false; // flag for the last loop 
                            int k_real = 0;
                            bool VegetationExists = VEG_L != null;
                            
                            for (int k = KSTART; k < KEND; k += Vector512<float>.Count)
                            {
                                // to compute the top levels - reduce k, remember k_real and set end to true
                                k_real = k;
                                if (k > KEND - Vector512<float>.Count)
                                {
                                    k = KEND - Vector512<float>.Count;
                                    end = true;
                                }
                                int kM1 = k - 1;
                                int kP1 = k + 1;

                                WKS_V = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_L), (uint) k);
                                VKS_V = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_L), (uint) k);
                                UKS_V = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_L), (uint) k);
                                DZK_V = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(Program.DZK), (uint) k);

                                DXKDZK = Avx512F.Multiply(DXK_V, DZK_V);
                                DYKDZK = Avx512F.Multiply(DYK_V, DZK_V);

                                //turbulence modelling
                                zs = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(Program.HOKART), (uint) k) - Avx512F.FusedMultiplyAdd(DZK_V, Vect_05, HOKART_KKART_V);

                                VIS = Vector512<float>.Zero;

                                // Create Mask if KKART_LL is between k and k + Vector512<float>.Count
                                bool mask_v_enable = false;
                                if (KKART_LL_P1 >= k && KKART_LL_P1 < (k + Vector512<float>.Count))
                                {
                                    for (int ii = 0; ii < Mask.Length; ++ii)
                                    {
                                        if ((k + ii) > KKART_LL_P1)
                                        {
                                            Mask[ii] = 1;
                                        }
                                        else
                                        {
                                            Mask[ii] = 0;
                                        }
                                    }
                                    Mask_V = Vector512.Create<float>(Mask);
                                    mask_v_enable = true;
                                }
                                else
                                {
                                     Mask_V = Vector512<float>.One;
                                }

                                if ((k + Vector512<float>.Count) > KKART_LL_P1)
                                {
                                    //k-eps model
                                    //float VIS = (float)Math.Sqrt(TURB_L[k]) * zs * Cmueh;

                                    //mixing-length model
                                    mixL = Vector512.Min<float>(Avx512F.Multiply(Vect_0071, zs), Vect_100);

                                    //adapted mixing-leng th within vegetation layers
                                    if (COV_L > 0)
                                    {
                                        mixL = Avx512F.Multiply(mixL, (Vect_1 - Avx512F.Multiply(Vect_099, Vector512.Create<float>(COV_L))));
                                        //mixL = (float)Math.Min(0.071 * Math.Max(1 - Math.Min(2 * VEG_L[kM1], 1), 0.05) * zs, 100);
                                    }

                                    intern = Avx512F.Divide(Avx512F.Subtract(Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_L), (uint)kP1), Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_L), (uint)kM1)),
                                    Avx512F.Multiply(Vect_2, DZK_V));
                                    DUDZ = Avx512F.Multiply(intern, intern);

                                    intern = Avx512F.Divide(Avx512F.Subtract(Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_L), (uint)kP1), Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_L), (uint)kM1)),
                                    Avx512F.Multiply(Vect_2, DZK_V));
                                    DVDZ = Avx512F.Multiply(intern, intern);

                                    VIS = Vector512.Min<float>(Avx512F.Multiply(Avx512F.Multiply(mixL, mixL), Vector512.Sqrt<float>(Avx512F.Multiply(Vect_05, (DUDZ + DVDZ)))), Vect_15) * Mask_V;
                                }

                                //Diffusion terms
                                DE = Avx512F.Divide(Avx512F.Multiply(Vector512.Max<float>(VIS, VISHMIN_V), DYKDZK), DXK_V);
                                DW = DE;
                                DS = Avx512F.Divide(Avx512F.Multiply(Vector512.Max<float>(VIS, VISHMIN_V), DXKDZK), DYK_V);
                                DN = DS;
                                DT = Avx512F.Divide(Avx512F.Multiply(Vector512.Max<float>(VIS, VISHMIN_V), AREAxy_V), DZK_V);
                                DB = Vector512<float>.Zero;
                                if (mask_v_enable)
                                {
                                    DB = Avx512F.Multiply(DT, Mask_V);
                                }
                                else if (k > KKART_LL_P1) // otherwise k < KKART_LL_P1 and DB=0!
                                {
                                    DB = DT;
                                }

                                //BOUNDARY CONDITIONS FOR DIFFUSION TERMS
                                if (ADVDOM_S)
                                {
                                    DS = Vector512<float>.Zero;
                                }
                                if (ADVDOM_N)
                                {
                                    DN = Vector512<float>.Zero;
                                }
                                if (ADVDOM_W)
                                {
                                    DW = Vector512<float>.Zero;
                                }
                                if (ADVDOM_E)
                                {
                                    DE = Vector512<float>.Zero;
                                }

                                //ADVECTION TERMS
                                VKS_WN_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_WN_L), (uint) k);
                                VKS_E_LV =  Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_N_L), (uint) k);
                                VKS_W_LV =  Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_W_L), (uint) k);
                                VKS_WS_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_WS_L), (uint) k);
                                VKS_S_LV =  Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_S_L), (uint) k);
                                UKS_W_LV =  Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_W_L), (uint) k);
                                
                                WKS_B_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_L), (uint) kM1);
                                WKS_Bp_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_L), (uint) kP1);

                                WKS_W_LV =  Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_W_L), (uint) k);
                                WKS_WB_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_W_L), (uint) kM1);
                                WKS_WBm_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_W_L), (uint) kP1);

                                FE = Avx512F.Multiply(UKS_V, DYKDZK);
                                FW = Avx512F.Multiply(UKS_W_LV, DYKDZK);
                                FS = Avx512F.Multiply(Avx512F.Multiply(Vect_025,
                                     Avx512F.Add(Avx512F.Add(Avx512F.Add(VKS_W_LV, VKS_V), VKS_WS_LV), VKS_S_LV)), DXKDZK);
                                FN = Avx512F.Multiply(Avx512F.Multiply(Vect_025,
                                     Avx512F.Add(Avx512F.Add(Avx512F.Add(VKS_W_LV, VKS_V), VKS_WN_LV), VKS_E_LV)), DXKDZK);
                                FT = Avx512F.Multiply(Avx512F.Multiply(Vect_025,
                                     Avx512F.Add(Avx512F.Add(Avx512F.Add(WKS_W_LV, WKS_V), WKS_WBm_LV), WKS_Bp_LV)), AREAxy_V);
                                FB = Vector512<float>.Zero;

                                if (mask_v_enable)
                                {
                                    FB = Avx512F.Multiply(Avx512F.Multiply(Vect_025, Avx512F.Add(Avx512F.Add(Avx512F.Add(WKS_W_LV, WKS_V), WKS_WB_LV), WKS_B_LV)), AREAxy_V) * Mask_V;
                                }
                                else if (k > KKART_LL_P1) // otherwise k < KKART_LL_P1 and FB=0!
                                {
                                    FB = Avx512F.Multiply(Avx512F.Multiply(Vect_025, Avx512F.Add(Avx512F.Add(Avx512F.Add(WKS_W_LV, WKS_V), WKS_WB_LV), WKS_B_LV)), AREAxy_V);
                                }

                                //PECLET NUMBERS
                                DE = Vector512.Max<float>(DE, Vect_0001);
                                DB = Vector512.Max<float>(DB, Vect_0001);
                                DW = Vector512.Max<float>(DW, Vect_0001);
                                DS = Vector512.Max<float>(DS, Vect_0001);
                                DN = Vector512.Max<float>(DN, Vect_0001);
                                DT = Vector512.Max<float>(DT, Vect_0001);

                                PE = Vector512.Abs<float>(FE / DE);
                                PB = Vector512.Abs<float>(FB / DB);
                                PW = Vector512.Abs<float>(FW / DW);
                                PS = Vector512.Abs<float>(FS / DS);
                                PN = Vector512.Abs<float>(FN / DN);
                                PT = Vector512.Abs<float>(FT / DT);

                                //POWER LAW ADVECTION SCHEME
                                intern = Vect_1 - Vect_01 * PT;
                                BIM = Avx512F.FusedMultiplyAdd(DT, Vector512.Max<float>(Vect_0, Avx512F.Multiply(intern, Avx512F.Multiply(Avx512F.Multiply(intern, intern), Avx512F.Multiply(intern, intern)))),
                                                               Vector512.Max<float>(-FT, Vect_0));

                                intern = Vect_1 - Vect_01 * PB;
                                CIM = Avx512F.FusedMultiplyAdd(DB, Vector512.Max<float>(Vect_0, Avx512F.Multiply(intern, Avx512F.Multiply(Avx512F.Multiply(intern, intern), Avx512F.Multiply(intern, intern)))),
                                                               Vector512.Max<float>(FB, Vect_0));

                                intern = Vect_1 - Vect_01 * PE;
                                AE1 = Avx512F.FusedMultiplyAdd(DE, Vector512.Max<float>(Vect_0, Avx512F.Multiply(intern, Avx512F.Multiply(Avx512F.Multiply(intern, intern), Avx512F.Multiply(intern, intern)))),
                                                               Vector512.Max<float>(-FE, Vect_0));

                                intern = Vect_1 - Vect_01 * PW;
                                AW1 = Avx512F.FusedMultiplyAdd(DW, Vector512.Max<float>(Vect_0, Avx512F.Multiply(intern, Avx512F.Multiply(Avx512F.Multiply(intern, intern), Avx512F.Multiply(intern, intern)))),
                                                               Vector512.Max<float>(FW, Vect_0));

                                intern = Vect_1 - Vect_01 * PS;
                                AS1 = Avx512F.FusedMultiplyAdd(DS, Vector512.Max<float>(Vect_0, Avx512F.Multiply(intern, Avx512F.Multiply(Avx512F.Multiply(intern, intern), Avx512F.Multiply(intern, intern)))),
                                                               Vector512.Max<float>(FS, Vect_0));

                                intern = Vect_1 - Vect_01 * PN;
                                AN1 = Avx512F.FusedMultiplyAdd(DN, Vector512.Max<float>(Vect_0, Avx512F.Multiply(intern, Avx512F.Multiply(Avx512F.Multiply(intern, intern), Avx512F.Multiply(intern, intern)))),
                                                               Vector512.Max<float>(-FN, Vect_0));

                                //UPWIND SCHEME
                                /*
                                    double BIM = DT + Program.fl_max(-FT, 0);
                                    double CIM = DB + Program.fl_max(FB, 0);
                                    double AE1 = DE + Program.fl_max(-FE, 0);
                                    double AW1 = DW + Program.fl_max(FW, 0);
                                    double AS1 = DS + Program.fl_max(FS, 0);
                                    double AN1 = DN + Program.fl_max(-FN, 0);
                                 */

                                AP0_V = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(PrognosticFlowfield.AP0), (uint) k);
                                AIM = Avx512F.Add(Avx512F.Add(Avx512F.Add(Avx512F.Add(Avx512F.Add(Avx512F.Add(BIM, CIM), AW1), AS1), AE1), AN1), AP0_V);

                                //SOURCE TERMS
                                DDPX =     Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(DPMNEW_W_L), (uint) k) - Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(DPMNEW_L), (uint) k);
                                UK_W_LV =  Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_W_L), (uint) k);
                                UK_E_LV =  Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_E_L), (uint) k);
                                UKS_W_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_W_L), (uint) k);
                                UK_LV =    Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_L), (uint) k);

                                if (VegetationExists)
                                {
                                    intern2 = Avx512F.Multiply(Vect_05, (VKS_W_LV + VKS_V));
                                    intern = Avx512F.Multiply(Vect_05, (UKS_W_LV + UKS_V));
                                    windhilf = Vector512.Max<float>(Vector512.Sqrt<float>(Avx512F.Multiply(intern, intern) + Avx512F.Multiply(intern2, intern2)), Vect_001);
                                    DIM_U = Avx512F.FusedMultiplyAdd(AW1, UK_W_LV,
                                            AS1 * Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_S_L), (uint) k)) +
                                            Avx512F.FusedMultiplyAdd(AE1, UK_E_LV,
                                            AN1 * Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_N_L), (uint) k)) +
                                            AP0_V * Vect_05 * (UKS_W_LV + UKS_V) +
                                            DDPX * DYKDZK +
                                            (Avx512F.Multiply(Coriolis, Avx512F.Subtract(UG_V, UK_LV)) -
                                            Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VEG_L), (uint) kM1) * UK_LV * windhilf) * AREAxy_V * DZK_V;
                                }
                                else
                                {
                                    DIM_U = Avx512F.FusedMultiplyAdd(AW1, UK_W_LV,
                                            AS1 * Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_S_L), (uint) k)) +
                                            Avx512F.FusedMultiplyAdd(AE1, UK_E_LV,
                                            AN1 * Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UK_N_L), (uint) k)) +
                                            AP0_V * Vect_05 * (UKS_W_LV + UKS_V) +
                                            Avx512F.FusedMultiplyAdd(DDPX, DYKDZK,
                                            Avx512F.Multiply(Avx512F.Multiply(Avx512F.Multiply(Coriolis, Avx512F.Subtract(UG_V,UK_LV)), AREAxy_V), DZK_V));
                                }

                                //BOUNDARY CONDITION AT SURFACES (OBSTACLES AND TERRAIN)
                                if ((k <= KKART_LL_P1) && ((k + Vector512<float>.Count) > KKART_LL_P1))
                                {
                                    Mask2.Clear();
                                    //Array.Clear(Mask2, 0, Mask2.Length);
                                    int ii = KKART_LL_P1 - k;   // 0 to SIMD - 1
                                    Mask2[ii] = 1;              // Set the mask2 at SIMD position k == KKART_LL_P1 to 1

                                    intern = Avx512F.Multiply(Vect_05, (UKS_W_LV + UKS_V));
                                    intern2 = Avx512F.Multiply(Vect_05, (VKS_W_LV + VKS_V));
                                    windhilf = Vector512.Max<float>(Vector512.Sqrt<float>(Avx512F.Multiply(intern, intern) + Avx512F.Multiply(intern2, intern2)), Vect_01);

                                    if (CUTK_L < 1) // building heigth < 1 m
                                    {
                                        // above terrain
                                        float Ustern_Buildings = Ustern_terrain_helpterm * windhilf[ii];
                                        if (Z0 >= DZK_V[ii] * 0.1F)
                                        {
                                            int ind = k + ii + 1;
                                            Ustern_Buildings = Ustern_terrain_helpterm * MathF.Sqrt(Program.Pow2(0.5F * ((UKS_W_L[ind]) + UKS_L[ind])) + Program.Pow2(0.5F * ((VKS_W_L[ind]) + VKS_L[ind])));
                                        }

                                        Vector512<float> Ustern_Buildings_V = Vector512.Create(Ustern_Buildings * Ustern_Buildings);
                                        DIM_U -= UK_LV / windhilf * Ustern_Buildings_V * AREAxy_V * Vector512.Create<float>(Mask2);
                                    }
                                    else
                                    {
                                        //above a building
                                        float Ustern_Buildings = Ustern_obstacles_helpterm * windhilf[ii];
                                        Vector512<float> Ustern_Buildings_V = Vector512.Create(Ustern_Buildings * Ustern_Buildings);
                                        DIM_U -= UK_LV / windhilf * Ustern_Buildings_V * AREAxy_V * Vector512.Create<float>(Mask2);
                                    }
                                }

                                //additional terms of the eddy-viscosity model
                                if (k > KKART_LL_P1)
                                {
                                    DUDXE = (UK_E_LV - UK_LV) * DYKDZK;
                                    DUDXW = (UK_LV - UK_W_LV) * DYKDZK;
                                    DVDXN = (Vect_05 * (Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_EN_L), (uint) k) + VKS_E_LV) - Vect_05 * (VKS_E_LV + VKS_WN_LV)) * DXKDZK;
                                    DVDXS = (Vect_05 * (Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_ES_L), (uint) k) + VKS_S_LV) - Vect_05 * (VKS_S_LV + VKS_WS_LV)) * DXKDZK;
                                    DWDXT = (Vect_05 * (Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_E_L), (uint) k) + WKS_V) - Vect_05 * (WKS_V + WKS_W_LV)) * AREAxy_V;
                                    DWDXB = (Vect_05 * (Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_E_L), (uint) kM1) + WKS_B_LV) - Vect_05 * (WKS_B_LV + WKS_WB_LV)) * AREAxy_V;

                                    DIM_U += VIS / DXK_V * (DUDXE - DUDXW + DVDXN - DVDXS + DWDXT - DWDXB) * Mask_V;
                                }

                                //RECURRENCE FORMULA
                                int kint_s = 0;
                                if (end) // restore original indices 
                                {
                                    kint_s = k_real - k;
                                    k_real -= kint_s;
                                }

                                Vector512.StoreUnsafe<float>(AIM, ref AIM_A[0]);
                                Vector512.StoreUnsafe<float>(BIM, ref BIM_A[0]);
                                Vector512.StoreUnsafe<float>(CIM, ref CIM_A[0]);
                                Vector512.StoreUnsafe<float>(DIM_U, ref DIMU_A[0]);

                                for (int kint = kint_s; kint < AIM_A.Length; ++kint) // loop over all SIMD values
                                {
                                    int index_i = k_real + kint;

                                    if (index_i < KEND)
                                    {
                                        if (index_i > KKART_LL_P1)
                                        {
                                            float TERMP = 1 / (AIM_A[kint] - CIM_A[kint] * PIMU[index_i - 1]);
                                            PIMU[index_i] = BIM_A[kint] * TERMP;
                                            QIMU[index_i] = (DIMU_A[kint] + CIM_A[kint] * QIMU[index_i - 1]) * TERMP;
                                        }
                                        else
                                        {
                                            float TERMP = 1 / AIM_A[kint];
                                            PIMU[index_i] = BIM_A[kint] * TERMP;
                                            QIMU[index_i] = DIMU_A[kint] * TERMP;
                                        }
                                    }
                                }

                                if (end)
                                {
                                    k = KEND + 1;
                                }

                            } // loop over all k

                            //OBTAIN NEW U-COMPONENTS
                            int KKARTm_LL = Math.Max(KKART_LL, Program.KKART[i - 1][j]);
                            lock (UK_L)
                            {
                                for (int k = Vert_Index_LL; k >= KSTART; --k)
                                {
                                    if (KKARTm_LL < k)
                                    {
                                        UK_L[k] += relax * (PIMU[k] * UK_L[k + 1] + QIMU[k] - UK_L[k]);
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}
