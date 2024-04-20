#region Copyright
///<remarks>
/// <Graz Lagrangian Particle Dispersion Model>
/// Copyright (C) [2024] [Markus Kuntner] Predecessor: [2019]  [Dietmar Oettl, Markus Kuntner]
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
    public static class V_PrognosticMicroscaleV1_Vec512
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
        /// Momentum equations for the v wind component - algebraic mixing lenght model
        /// </summary>   
        /// <param name="IS">ADI direction for x cells</param>
        /// <param name="JS">ADI direction for y cells</param>
        /// <param name="Cmueh">Constant</param>
        /// <param name="VISHMIN">Minimum horizontal turbulent exchange coefficients </param>
        /// <param name="AREAxy">Area of a flow field cell</param>
        /// <param name="VG">Geostrophic wind</param>
        /// <param name="building_Z0">Roughness of buildings</param>
        /// <param name="relax">Relaxation factor</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        [SkipLocalsInit]
        public static void Calculate(int IS, int JS, float Cmueh, float VISHMIN, float AREAxy, Single VG, float relax)
        {
            float DXK = Program.DXK; float DYK = Program.DYK;
            Vector512<float> DXK_V = Vector512.Create(DXK);
            Vector512<float> DYK_V = Vector512.Create(DYK);
            Vector512<float> VISHMIN_V = Vector512.Create(VISHMIN);
            Vector512<float> AREAxy_V = Vector512.Create(AREAxy);
            Vector512<float> VG_V = Vector512.Create(VG);
            
            int maxTasks = Math.Max(1, Program.IPROC + Math.Abs(Environment.TickCount % 8));
            int minL = Math.Min(64, Program.NII - 4); // Avoid too large slices due to perf issues
            Parallel.ForEach(Partitioner.Create(2, Program.NII, Math.Min(Math.Max(4, (Program.NII / maxTasks)), minL)), Program.pOptions, range =>
            //Parallel.For(2, Program.NII, Program.pOptions, i1 =>
            {
                Span<float> PIMV = stackalloc float[Program.KADVMAX + 1];
                Span<float> QIMV = stackalloc float[Program.KADVMAX + 1];
                Span<float> Mask   = stackalloc float[Vector512<float>.Count];
                Span<float> Mask2  = stackalloc float[Vector512<float>.Count];
                Span<float> AIM_A  = stackalloc float[Vector512<float>.Count];
                Span<float> BIM_A  = stackalloc float[Vector512<float>.Count];
                Span<float> CIM_A  = stackalloc float[Vector512<float>.Count];
                Span<float> DIMV_A = stackalloc float[Vector512<float>.Count];

                Vector512<float> DVDYN, DVDYS, DUDYE, DUDYW, DWDYT, DWDYB;
                Vector512<float> DE, DW, DS, DN, DT, DB;
                Vector512<float> FE, FW, FS, FN, FT, FB;
                Vector512<float> PE, PW, PS, PN, PT, PB;
                Vector512<float> BIM, CIM, AE1, AW1, AS1, AN1, AIM;
                Vector512<float> DIMV, windhilf;
                Vector512<float> DDPY;

                Vector512<float> UKS_E_LV, UKS_N_LV, UKS_ES_LV, UKS_W_LV, UKS_WS_LV, VK_N_LV, VK_S_LV, VKS_W_LV;
                Vector512<float> VKS_S_LV, WKS_S_LV, WKS_Sm_LV, WKS_B_LV;

                Vector512<float> WKS_V, VKS_V, UKS_V, DZK_V;
                Vector512<float> DXKDZK, DYKDZK, AP0_V, intern, intern2;

                Vector512<float> VIS, zs, mixL;
                Vector512<float> VK_LV;
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

                    for (int j1 = 3; j1 <= Program.NJJ - 1; ++j1)
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

                            float[] VK_L = Program.VK[i][j];
                            
                            Single[] UKS_L = Program.UKS[i][j];
                            Single[] VKS_L = Program.VKS[i][j];
                            Single[] WKS_L = Program.WKS[i][j];
                            Single[] DPMNEW_L = Program.DPMNEW[i][j];
                            Single[] VK_W_L = Program.VK[iM1][j];
                            Single[] VK_E_L = Program.VK[iP1][j];
                            Single[] VK_S_L = Program.VK[i][jM1];
                            Single[] VK_N_L = Program.VK[i][jP1];
                            Single[] UKS_W_L = Program.UKS[iM1][j];
                            Single[] UKS_E_L = Program.UKS[iP1][j];
                            Single[] UKS_S_L = Program.UKS[i][jM1];
                            Single[] UKS_ES_L = Program.UKS[iP1][jM1];
                            Single[] UKS_WS_L = Program.UKS[iM1][jM1];
                            Single[] VKS_S_L = Program.VKS[i][jM1];
                            Single[] VKS_W_L = Program.VKS[iM1][j];
                            Single[] WKS_S_L = Program.WKS[i][jM1];
                            Single[] DPMNEW_S_L = Program.DPMNEW[i][jM1];

                            Single[] UKS_EN_L = Program.UKS[iP1][jP1];
                            Single[] UKS_WN_L = Program.UKS[iM1][jP1];
                            Single[] WKS_N_L = Program.WK[i][jP1];
                            Single[] UK_L = Program.UK[i][j];

                            int KKART_LL = Program.KKART[i][j];
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

                            HOKART_KKART_V = Vector512.Create(Program.HOKART[KKART_LL]);

                            int KSTART = 1;
                            if (CUTK_L == 0) // building heigth == 0 m
                            {
                                KSTART = KKART_LL + 1;
                            }

                            int KKART_LL_P1 = KKART_LL + 1;

                            int KEND = Vert_Index_LL + 1;
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
                                VK_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_L), (uint) k);
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
                                // To do : Set a Mask to take the right values
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
                                UKS_E_LV  = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_E_L), (uint) k);
                                UKS_N_LV  = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_S_L), (uint) k);
                                UKS_ES_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_ES_L), (uint) k);
                                UKS_W_LV  = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_W_L), (uint) k);
                                VKS_W_LV  = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_W_L), (uint) k);
                                UKS_WS_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_WS_L), (uint) k);
                                VKS_S_LV  = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VKS_S_L), (uint) k);
                                WKS_S_LV  = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_S_L), (uint) k);
                                WKS_Sm_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_S_L), (uint) kM1);
                                WKS_B_LV  = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_L), (uint) kM1);

                                FE = Avx512F.Multiply(Avx512F.Multiply(Vect_025,
                                     Avx512F.Add(Avx512F.Add(Avx512F.Add(UKS_V, UKS_E_LV), UKS_N_LV), UKS_ES_LV)), DYKDZK);
                                FW = Avx512F.Multiply(Avx512F.Multiply(Vect_025,
                                     Avx512F.Add(Avx512F.Add(Avx512F.Add(UKS_V, UKS_W_LV), UKS_N_LV), UKS_WS_LV)), DYKDZK);
                                FS = Avx512F.Multiply(VKS_S_LV, DXKDZK);
                                FN = Avx512F.Multiply(VKS_V, DXKDZK);
                                FT = Avx512F.Multiply(Avx512F.Multiply(Vect_025, (WKS_S_LV + WKS_V + Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_S_L), (uint) kP1) + 
                                     Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_L), (uint) kP1))), AREAxy_V);
                                FB = Vector512<float>.Zero;

                                if (mask_v_enable)
                                {
                                    FB = Avx512F.Multiply(Avx512F.Multiply(Vect_025, 
                                        Avx512F.Add(Avx512F.Add(Avx512F.Add(WKS_S_LV, WKS_V), WKS_Sm_LV), WKS_B_LV)),
                                        AREAxy_V) * Mask_V;
                                }
                                else if (k > KKART_LL_P1) // otherwise k < KKART_LL_P1 and FB=0!
                                {
                                    FB = Avx512F.Multiply(Avx512F.Multiply(Vect_025, 
                                        Avx512F.Add(Avx512F.Add(Avx512F.Add(WKS_S_LV, WKS_V), WKS_Sm_LV), WKS_B_LV)),
                                        AREAxy_V);
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
                                DDPY = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(DPMNEW_S_L), (uint) k) - Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(DPMNEW_L), (uint) k);
                                VK_N_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_N_L), (uint) k);
                                VK_S_LV = Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_S_L), (uint) k);

                                if (VegetationExists)
                                {
                                    intern2 = Avx512F.Multiply(Vect_05, (VKS_W_LV + VKS_V));
                                    intern = Avx512F.Multiply(Vect_05, (UKS_W_LV + UKS_V));
                                    windhilf = Vector512.Max<float>(Vector512.Sqrt<float>(Avx512F.Multiply(intern, intern) + Avx512F.Multiply(intern2, intern2)), Vect_001);
                                    DIMV = Avx512F.FusedMultiplyAdd(AW1, Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_W_L), (uint) k),
                                           AS1 * VK_S_LV) +
                                           Avx512F.FusedMultiplyAdd(AE1, Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_E_L), (uint) k),
                                           AN1 * VK_N_LV) +
                                           AP0_V * Vect_05 * (VKS_S_LV + VKS_V) + DDPY * DXKDZK +
                                           (Avx512F.Multiply(Coriolis, Avx512F.Subtract(VG_V, VK_LV)) -
                                           Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VEG_L), (uint) kM1) * VK_LV * windhilf) * AREAxy_V * DZK_V;
                                }
                                else
                                {
                                    DIMV = AW1 * Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_W_L), (uint) k) +
                                           AS1 * VK_S_LV +
                                           AE1 * Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(VK_E_L), (uint) k) +
                                           AN1 * VK_N_LV +
                                           AP0_V * Vect_05 * (VKS_S_LV + VKS_V) + DDPY * DXKDZK +
                                           Avx512F.Multiply(Avx512F.Multiply(Avx512F.Multiply(Coriolis, Avx512F.Subtract(VG_V, VK_LV)), AREAxy_V), DZK_V);
                                }
                                //BOUNDARY CONDITION AT SURFACES (OBSTACLES AND TERRAIN)
                                if ((k <= KKART_LL_P1) && ((k + Vector512<float>.Count) > KKART_LL_P1))
                                {
                                    Mask2.Clear();
                                    //Array.Clear(Mask2, 0, Mask2.Length);
                                    int ii = KKART_LL_P1 - k;   // 0 to SIMD - 1
                                    Mask2[ii] = 1;              // Set the mask2 at SIMD position k == KKART_LL_P1 to 1
                                    intern = Vect_05 * (UKS_W_LV + UKS_V);
                                    intern2 = Vect_05 * (VKS_W_LV + VKS_V);
                                    windhilf = Vector512.Max<float>(Vector512.Sqrt<float>(Avx512F.Multiply(intern, intern) + Avx512F.Multiply(intern2, intern2)), Vect_01);

                                    if (CUTK_L < 1) // building heigth < 1 m
                                    {
                                        //above terrain
                                        float Ustern_Buildings = Ustern_terrain_helpterm * windhilf[ii];
                                        if (Z0 >= DZK_V[ii] * 0.1F)
                                        {
                                            int ind = k + ii + 1;
                                            Ustern_Buildings = Ustern_terrain_helpterm * MathF.Sqrt(Program.Pow2(0.5F * ((UKS_W_L[ind]) + UKS_L[ind])) + Program.Pow2(0.5F * ((VKS_W_L[ind]) + VKS_L[ind])));
                                        }
                                        Vector512<float>  Ustern_Buildings_V = Vector512.Create(Ustern_Buildings * Ustern_Buildings);
                                        DIMV -= VK_LV / windhilf * Ustern_Buildings_V * AREAxy_V * Vector512.Create<float>(Mask2);
                                    }
                                    else
                                    {
                                        //above a building
                                        float Ustern_Buildings = Ustern_obstacles_helpterm * windhilf[ii];
                                        Vector512<float>  Ustern_Buildings_V = Vector512.Create(Ustern_Buildings * Ustern_Buildings);
                                        DIMV -= VK_LV / windhilf * Ustern_Buildings_V * AREAxy_V * Vector512.Create<float>(Mask2);
                                    }
                                }

                                //additional terms of the eddy-viscosity model
                                if (k > KKART_LL_P1)
                                {
                                    DVDYN = (VK_N_LV - VK_LV) * DXKDZK;
                                    DVDYS = (VK_LV - VK_S_LV) * DXKDZK;
                                    DUDYE = (Vect_05 * (Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_EN_L), (uint) k) + UKS_E_LV) - Vect_05 * (UKS_E_LV + UKS_ES_LV)) * DXKDZK;
                                    DUDYW = (Vect_05 * (Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(UKS_WN_L), (uint) k) + UKS_W_LV) - Vect_05 * (UKS_W_LV + UKS_WS_LV)) * DXKDZK;
                                    DWDYT = (Vect_05 * (Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_N_L), (uint) k) + WKS_V) - Vect_05 * (WKS_V + WKS_S_LV)) * AREAxy_V;
                                    DWDYB = (Vect_05 * (Vector512.LoadUnsafe<float>(ref MemoryMarshal.GetArrayDataReference(WKS_N_L), (uint) kM1) + WKS_B_LV) - Vect_05 * (WKS_B_LV + WKS_S_LV)) * AREAxy_V;

                                    DIMV += VIS / DXK_V * (DUDYE - DUDYW + DVDYN - DVDYS + DWDYT - DWDYB) * Mask_V;
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
                                Vector512.StoreUnsafe<float>(DIMV, ref DIMV_A[0]);
                           
                                for (int kint = kint_s; kint < AIM_A.Length; ++kint) // loop over all SIMD values
                                {
                                    int index_i = k_real + kint;

                                    if (index_i < KEND)
                                    {
                                        if (index_i > KKART_LL_P1)
                                        {
                                            float TERMP = 1 / (AIM_A[kint] - CIM_A[kint] * PIMV[index_i - 1]);
                                            PIMV[index_i] = BIM_A[kint] * TERMP;
                                            QIMV[index_i] = (DIMV_A[kint] + CIM_A[kint] * QIMV[index_i - 1]) * TERMP;
                                        }
                                        else
                                        {
                                            float TERMP = 1 / AIM_A[kint];
                                            PIMV[index_i] = BIM_A[kint] * TERMP;
                                            QIMV[index_i] = DIMV_A[kint] * TERMP;
                                        }
                                    }
                                }

                                if (end)
                                {
                                    k = KEND + 1;
                                }

                            } // loop over all k

                            //OBTAIN NEW V-COMPONENTS
                            int KKARTm_LL = Math.Max(KKART_LL, Program.KKART[i][j - 1]);
                            lock (VK_L)
                            {
                                for (int k = Vert_Index_LL; k >= KSTART; --k)
                                {
                                    if (KKARTm_LL < k)
                                    {
                                        VK_L[k] += relax * (PIMV[k] * VK_L[k + 1] + QIMV[k] - VK_L[k]);
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
