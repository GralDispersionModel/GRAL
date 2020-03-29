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
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GRAL_2001
{
    public class MicroscaleTerrain
    {

        /// <summary>
        /// Get the microscale terrain from GRAL_topofile.txt or interpolate GRAMM terrain
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Calculate(bool firstloop)
        {
            Console.WriteLine();
            Console.WriteLine("FLOW FIELD COMPUTATION WITH TOPOGRAPHY");
            float DXK = Program.DXK;
            float DYK = Program.DYK;
            float DDX1 = Program.DDX[1];
            float DDY1 = Program.DDY[1];
            int Delta_IKOO = Program.IKOOAGRAL - Program.IKOOA;
            int Delta_JKOO = Program.JKOOAGRAL - Program.JKOOA;
            double vec_x = 0; double vec_y = 0; int vec_numb = 0;
            bool[,] vec_count = new bool[Program.NII + 2, Program.NJJ + 2];
            Array.Clear(vec_count, 0, vec_count.Length);

            //read vegetation only once
            if (File.Exists("vegetation.dat") == true)
            {
                //read vegetation only one times
                if (Program.VEG[0][0][0] < 0)
                {
                    ProgramReaders Readclass = new ProgramReaders();
                    Readclass.ReadVegetationDomain();
                }
            }

            // Search the reference point within the prognostic sub domains
            if (Program.SubDomainRefPos.X == 0 && Program.SubDomainRefPos.Y == 0)
            {
                MicroscaleTerrainSearchRefPoint mtsp = new MicroscaleTerrainSearchRefPoint();
                Program.SubDomainRefPos = mtsp.SearchReferencePoint(Program.ADVDOM);
                //No prognostic sub array - warning message to the user
                if (Program.SubDomainRefPos.X == 0 && Program.SubDomainRefPos.Y == 0) 
                {
                    Program.SubDomainRefPos = new IntPoint(1, 1);
                    string err = "Prognostic approach selected but there are no buildings or no vegetation areas and therefore no prognostic sub domains and no prognostic wind field calculation \nAre the absolute building heights below the surface?";
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);
                    Console.WriteLine("Press a key to continue");
                    if (Program.IOUTPUT <= 0 && Program.WaitForConsoleKey) // not for Soundplan or no keystroke
                    {
                        Console.ReadKey(true);
                    }
                }
            }

            //optional: orographical data as used in the GRAMM model is used
            if (File.Exists("GRAL_topofile.txt") == false || Program.AHKOriMin > 999999) // No GRAL topography or GRAL topography = corrupt
            {
                Program.KADVMAX = 1;

                object obj = new object();

                Parallel.For(1, Program.NII + 1, Program.pOptions, i =>
                {
                    double vec_x_loc = 0; double vec_y_loc = 0; int vec_numb_loc = 0;
                    Span<float> Zellhoehe = stackalloc float[Program.NK + 1];
                    Span<float> Zellhoehe_p = stackalloc float[Program.NK + 1];

                    for (int j = 1; j <= Program.NJJ; j++)
                    {
                        //Pointers (to speed up the computations)
                        Single[] UK_L = Program.UK[i][j];
                        Single[] VK_L = Program.VK[i][j];
                        Single[] WK_L = Program.WK[i][j];
                        Single[] UKip_L = Program.UK[i + 1][j];
                        Single[] VKjp_L = Program.VK[i][j + 1];

                        Program.AHK[i][j] = Program.AHMIN;
                        Program.KKART[i][j] = 0;
                        int indi = (int)((Delta_IKOO + DXK * (float)i - DXK * 0.5) / DDX1) + 1;
                        int indj = (int)((Delta_JKOO + DYK * (float)j - DYK * 0.5) / DDY1) + 1;
                        float xwert = ((DXK * (float)i - DXK * 0.5F + Delta_IKOO) - (indi - 1) * DDX1);
                        float ywert = ((DYK * (float)j - DYK * 0.5F + Delta_JKOO) - (indj - 1) * DDY1);
                        float[] AHE_L = Program.AHE[indi][indj];

                        if ((indi <= Program.NX - 1) && (indi >= 2) && (indj <= Program.NY - 1) && (indj >= 2))
                        {
                            float[] AHEJP_L = Program.AHE[indi][indj + 1];
                            float[] AHEIPJP = Program.AHE[indi + 1][indj + 1];
                            float[] AHEIP = Program.AHE[indi + 1][indj];
                            float[] AHEIPJM = Program.AHE[indi + 1][indj - 1];
                            float[] AHEIP2JM = Program.AHE[indi + 2][indj - 1];
                            float[] AHEIP2J = Program.AHE[indi + 2][indj];
                            float[] AHEIP2JP = Program.AHE[indi + 2][indj + 1];
                            float[] AHEIP2JP2 = Program.AHE[indi + 2][indj + 2];
                            float[] AHEIPJP2 = Program.AHE[indi + 1][indj + 2];
                            float[] AHEIJP2 = Program.AHE[indi][indj + 2];
                            float[] AHEIMJP2 = Program.AHE[indi - 1][indj + 2];
                            float[] AHEIMJP = Program.AHE[indi - 1][indj + 1];
                            float[] AHEIMJ = Program.AHE[indi - 1][indj];
                            float[] AHEIJM = Program.AHE[indi][indj - 1];

                            for (int ik = Program.NK; ik >= 1; ik--)
                            {
                                int ik_p = Math.Min(ik + 1, Program.NK);

                                double xdist = DDX1 - xwert;
                                double ydist = DDY1 - ywert;

                                float r1 = (float)(1 / (Program.Pow2(Program.Pow2(xwert) + Program.Pow2(ywert) + .1F)));
                                float r2 = (float)(1 / (Program.Pow2(Program.Pow2(xwert) + Program.Pow2(ydist) + .1F)));
                                float r3 = (float)(1 / (Program.Pow2(Program.Pow2(xdist) + Program.Pow2(ydist) + .1F)));
                                float r4 = (float)(1 / (Program.Pow2(Program.Pow2(xdist) + Program.Pow2(ywert) + .1F)));
                                float r5 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xwert) + Program.Pow2(DDY1 + ywert) + .1F)));
                                float r6 = (float)(1 / (Program.Pow2(Program.Pow2(xwert) + Program.Pow2(DDY1 + ywert) + .1F)));
                                float r7 = (float)(1 / (Program.Pow2(Program.Pow2(xdist) + Program.Pow2(DDY1 + ywert) + .1F)));
                                float r8 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xdist) + Program.Pow2(DDY1 + ywert) + .1F)));
                                float r9 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xdist) + Program.Pow2(ywert) + .1F)));
                                float r10 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xdist) + Program.Pow2(ydist) + .1F)));
                                float r11 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xdist) + Program.Pow2(DDY1 + ydist) + .1F)));
                                float r12 = (float)(1 / (Program.Pow2(Program.Pow2(xdist) + Program.Pow2(DDY1 + ydist) + .1F)));
                                float r13 = (float)(1 / (Program.Pow2(Program.Pow2(xwert) + Program.Pow2(DDY1 + ydist) + .1F)));
                                float r14 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xwert) + Program.Pow2(DDY1 + ydist) + .1F)));
                                float r15 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xwert) + Program.Pow2(ydist) + .1F)));
                                float r16 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xwert) + Program.Pow2(ywert) + .1F)));

                                float gew = r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10 + r11 + r12 + r13 + r14 + r15 + r16;

                                Zellhoehe[ik] = (AHE_L[ik] * r1 + AHEJP_L[ik] * r2 + AHEIPJP[ik] * r3 +
                                    AHEIP[ik] * r4 + Program.AHE[indi - 1][indj - 1][ik] * r5 + AHEIJM[ik] * r6 +
                                    AHEIPJM[ik] * r7 + AHEIP2JM[ik] * r8 + AHEIP2J[ik] * r9 +
                                    AHEIP2JP[ik] * r10 + AHEIP2JP2[ik] * r11 + AHEIPJP2[ik] * r12 +
                                    AHEIJP2[ik] * r13 + AHEIMJP2[ik] * r14 + AHEIMJP[ik] * r15 +
                                    AHEIMJ[ik] * r16) / gew - Program.AHMIN;

                                Zellhoehe_p[ik] = (AHE_L[ik_p] * r1 + AHEJP_L[ik_p] * r2 + AHEIPJP[ik_p] * r3 +
                                    AHEIP[ik_p] * r4 + Program.AHE[indi - 1][indj - 1][ik_p] * r5 + AHEIJM[ik_p] * r6 +
                                    AHEIPJM[ik_p] * r7 + AHEIP2JM[ik_p] * r8 + AHEIP2J[ik_p] * r9 +
                                    AHEIP2JP[ik_p] * r10 + AHEIP2JP2[ik_p] * r11 + AHEIPJP2[ik_p] * r12 +
                                    AHEIJP2[ik_p] * r13 + AHEIMJP2[ik_p] * r14 + AHEIMJP[ik_p] * r15 +
                                    AHEIMJ[ik_p] * r16) / gew - Program.AHMIN;
                            }
                        }

                        for (int k = Program.NKK; k >= 1; k--)
                        {
                            double vertk = Program.HOKART[k - 1] + Program.DZK[k] * 0.5 + Program.AHMIN;
                            int indk = 0;
                            int indk_p = 0;
                            int ik_p = 0;
                            float zellhoehe = 0;
                            float zellhoehe_p = 0;
                            float delta_z = 0;

                            for (int ik = Program.NK; ik >= 1; ik--)
                            {
                                ik_p = Math.Min(ik + 1, Program.NK);
                                if ((indi > Program.NX - 1) || (indi < 2))
                                {
                                    zellhoehe = AHE_L[ik] - Program.AHMIN;
                                    zellhoehe_p = AHE_L[ik_p] - Program.AHMIN;
                                }
                                else if ((indj > Program.NY - 1) || (indj < 2))
                                {
                                    zellhoehe = AHE_L[ik] - Program.AHMIN;
                                    zellhoehe_p = AHE_L[ik_p] - Program.AHMIN;
                                }
                                else
                                {
                                    zellhoehe = Zellhoehe[ik];
                                    zellhoehe_p = Zellhoehe_p[ik];
                                }

                                if (vertk - Program.AHMIN > zellhoehe + Program.CUTK[i][j])
                                {
                                    indk = ik;
                                    indk_p = Math.Min(indk + 1, Program.NK);
                                    delta_z = (float)(vertk - Program.AHMIN) - (zellhoehe + Program.CUTK[i][j]);
                                    break;
                                }
                            }

                            if (indk > 0)
                            {
                                if (indi > Program.NX) indi = Program.NX;
                                if (indj > Program.NY) indj = Program.NY;
                                int ixm = 0;
                                int iym = 0;
                                if (xwert < DDX1 * 0.5)
                                {
                                    ixm = indi - 1;
                                    ixm = Math.Max(ixm, 1);
                                    ixm = Math.Min(ixm, Program.NX);
                                }
                                else
                                {
                                    ixm = indi + 1;
                                    ixm = Math.Max(ixm, 1);
                                    ixm = Math.Min(ixm, Program.NX);
                                }
                                if (ywert < DDY1 * 0.5F)
                                {
                                    iym = indj - 1;
                                    iym = Math.Max(iym, 1);
                                    iym = Math.Min(iym, Program.NY);
                                }
                                else
                                {
                                    iym = indj + 1;
                                    iym = Math.Max(iym, 1);
                                    iym = Math.Min(iym, Program.NY);
                                }

                                float ux1 = Program.UWIN[indi][indj][indk] + (Program.UWIN[ixm][indj][indk] - Program.UWIN[indi][indj][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float ux2 = Program.UWIN[indi][iym][indk] + (Program.UWIN[ixm][iym][indk] - Program.UWIN[indi][iym][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                //vertical interpolation
                                float ux3 = Program.UWIN[indi][indj][indk_p] + (Program.UWIN[ixm][indj][indk_p] - Program.UWIN[indi][indj][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float ux4 = Program.UWIN[indi][iym][indk_p] + (Program.UWIN[ixm][iym][indk_p] - Program.UWIN[indi][iym][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float ux5 = ux1 + (ux3 - ux1) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                float ux6 = ux2 + (ux4 - ux2) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                UK_L[k] = (float)(ux5 + (ux6 - ux5) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));
                                //UK_L[k] = (float)(ux1 + (ux2 - ux1) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));

                                float vx1 = Program.VWIN[indi][indj][indk] + (Program.VWIN[ixm][indj][indk] - Program.VWIN[indi][indj][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float vx2 = Program.VWIN[indi][iym][indk] + (Program.VWIN[ixm][iym][indk] - Program.VWIN[indi][iym][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                //vertical interpolation
                                float vx3 = Program.VWIN[indi][indj][indk_p] + (Program.VWIN[ixm][indj][indk_p] - Program.VWIN[indi][indj][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float vx4 = Program.VWIN[indi][iym][indk_p] + (Program.VWIN[ixm][iym][indk_p] - Program.VWIN[indi][iym][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float vx5 = vx1 + (vx3 - vx1) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                float vx6 = vx2 + (vx4 - vx2) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                VK_L[k] = (float)(vx5 + (vx6 - vx5) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));
                                //VK_L[k] = (float)(vx1 + (vx2 - vx1) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));

                                float wx1 = Program.WWIN[indi][indj][indk] + (Program.WWIN[ixm][indj][indk] - Program.WWIN[indi][indj][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float wx2 = Program.WWIN[indi][iym][indk] + (Program.WWIN[ixm][iym][indk] - Program.WWIN[indi][iym][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                //vertical interpolation
                                float wx3 = Program.WWIN[indi][indj][indk_p] + (Program.WWIN[ixm][indj][indk_p] - Program.WWIN[indi][indj][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float wx4 = Program.WWIN[indi][iym][indk_p] + (Program.WWIN[ixm][iym][indk_p] - Program.WWIN[indi][iym][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float wx5 = wx1 + (wx3 - wx1) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                float wx6 = wx2 + (wx4 - wx2) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                WK_L[k] = (float)(wx5 + (wx6 - wx5) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));
                                //WK_L[k] = (float)(wx1 + (wx2 - wx1) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));

                                // compute windvector in about 10m
                                if (Program.ZSP[indi][indj][indk] - Program.AH[indi][indj] < 10 + (Program.ZKO[2] - Program.ZKO[1]) * 0.5F && vec_count[i, j] == false)
                                {
                                    vec_count[i, j] = true;
                                    vec_x_loc += UK_L[k];
                                    vec_y_loc += VK_L[k];
                                    vec_numb_loc++;
                                }

                                if (i > 1)
                                    if (k <= Program.KKART[i - 1][j]) UK_L[k] = 0;
                                if (j > 1)
                                    if (k <= Program.KKART[i][j - 1]) VK_L[k] = 0;

                                Program.UK[Program.NII + 1][j][k] = Program.UK[Program.NII][j][k];
                                Program.VK[i][Program.NJJ + 1][k] = Program.VK[i][Program.NJJ][k];
                            }
                            else
                            {
                                UK_L[k] = 0;
                                VK_L[k] = 0;
                                WK_L[k] = 0;
                                if (i < Program.NII) UKip_L[k] = 0;
                                if (j < Program.NJJ) VKjp_L[k] = 0;
                                if (k < Program.NKK) WK_L[k + 1] = 0;
                                Program.AHK[i][j] = Math.Max(Program.HOKART[k] + Program.AHMIN, Program.AHK[i][j]);
                                if (Program.CUTK[i][j] > 0)
                                {
                                    Program.BUI_HEIGHT[i][j] = (float)Math.Max(Program.AHK[i][j] - Program.AHMIN - zellhoehe, Program.BUI_HEIGHT[i][j]);
                                }
                                Program.KKART[i][j] = Convert.ToInt16(Math.Max(k, Program.KKART[i][j]));
                            }
                        }

                        if (Program.ADVDOM[i][j] > 0)
                        {
                            lock (obj)
                            {
                                Program.KADVMAX = Math.Max(Program.KKART[i][j], Program.KADVMAX);
                            }
                        }
                    }
                    lock (obj)
                    {
                        vec_numb += vec_numb_loc;
                        vec_x += vec_x_loc;
                        vec_y += vec_y_loc;
                    }
                });
            }

            //optional: original orographical data is used
            else
            {
                Program.KADVMAX = 1;
                object obj = new object();
                Parallel.For(1, Program.NII + 1, Program.pOptions, i =>
                {
                    int KADVMAX1 = 1;
                    double vec_x_loc = 0; float vec_y_loc = 0; int vec_numb_loc = 0;
                    Span<float> Zellhoehe = stackalloc float[Program.NK + 1];
                    Span<float> Zellhoehe_p = stackalloc float[Program.NK + 1];

                    for (int j = 1; j <= Program.NJJ; j++)
                    {
                        //Pointers (to speed up the computations)
                        Single[] UK_L = Program.UK[i][j];
                        Single[] VK_L = Program.VK[i][j];
                        Single[] WK_L = Program.WK[i][j];
                        Single[] UKip_L = Program.UK[i + 1][j];
                        Single[] VKjp_L = Program.VK[i][j + 1];

                        Program.AHK[i][j] = Program.AHKOriMin;
                        Program.KKART[i][j] = 0;
                        int indi = (int)((Delta_IKOO + DXK * (float)i - DXK * 0.5F) / DDX1) + 1;
                        int indj = (int)((Delta_JKOO + DYK * (float)j - DYK * 0.5F) / DDY1) + 1;
                        float xwert = ((DXK * (float)i - DXK * 0.5F + Delta_IKOO) - (indi - 1) * DDX1);
                        float ywert = ((DYK * (float)j - DYK * 0.5F + Delta_JKOO) - (indj - 1) * DDY1);
                        float[] AHE_L = Program.AHE[indi][indj];

                        if ((indi <= Program.NX - 1) && (indi >= 2) && (indj <= Program.NY - 1) && (indj >= 2))
                        {
                            float[] AHEJP_L = Program.AHE[indi][indj + 1];
                            float[] AHEIPJP = Program.AHE[indi + 1][indj + 1];
                            float[] AHEIP = Program.AHE[indi + 1][indj];
                            float[] AHEIPJM = Program.AHE[indi + 1][indj - 1];
                            float[] AHEIP2JM = Program.AHE[indi + 2][indj - 1];
                            float[] AHEIP2J = Program.AHE[indi + 2][indj];
                            float[] AHEIP2JP = Program.AHE[indi + 2][indj + 1];
                            float[] AHEIP2JP2 = Program.AHE[indi + 2][indj + 2];
                            float[] AHEIPJP2 = Program.AHE[indi + 1][indj + 2];
                            float[] AHEIJP2 = Program.AHE[indi][indj + 2];
                            float[] AHEIMJP2 = Program.AHE[indi - 1][indj + 2];
                            float[] AHEIMJP = Program.AHE[indi - 1][indj + 1];
                            float[] AHEIMJ = Program.AHE[indi - 1][indj];
                            float[] AHEIJM = Program.AHE[indi][indj - 1];

                            for (int ik = Program.NK; ik >= 1; ik--)
                            {
                                int ik_p = Math.Min(ik + 1, Program.NK);

                                double xdist = DDX1 - xwert;
                                double ydist = DDY1 - ywert;

                                float r1 = (float)(1 / (Program.Pow2(Program.Pow2(xwert) + Program.Pow2(ywert) + .1F)));
                                float r2 = (float)(1 / (Program.Pow2(Program.Pow2(xwert) + Program.Pow2(ydist) + .1F)));
                                float r3 = (float)(1 / (Program.Pow2(Program.Pow2(xdist) + Program.Pow2(ydist) + .1F)));
                                float r4 = (float)(1 / (Program.Pow2(Program.Pow2(xdist) + Program.Pow2(ywert) + .1F)));
                                float r5 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xwert) + Program.Pow2(DDY1 + ywert) + .1F)));
                                float r6 = (float)(1 / (Program.Pow2(Program.Pow2(xwert) + Program.Pow2(DDY1 + ywert) + .1F)));
                                float r7 = (float)(1 / (Program.Pow2(Program.Pow2(xdist) + Program.Pow2(DDY1 + ywert) + .1F)));
                                float r8 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xdist) + Program.Pow2(DDY1 + ywert) + .1F)));
                                float r9 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xdist) + Program.Pow2(ywert) + .1F)));
                                float r10 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xdist) + Program.Pow2(ydist) + .1F)));
                                float r11 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xdist) + Program.Pow2(DDY1 + ydist) + .1F)));
                                float r12 = (float)(1 / (Program.Pow2(Program.Pow2(xdist) + Program.Pow2(DDY1 + ydist) + .1F)));
                                float r13 = (float)(1 / (Program.Pow2(Program.Pow2(xwert) + Program.Pow2(DDY1 + ydist) + .1F)));
                                float r14 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xwert) + Program.Pow2(DDY1 + ydist) + .1F)));
                                float r15 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xwert) + Program.Pow2(ydist) + .1F)));
                                float r16 = (float)(1 / (Program.Pow2(Program.Pow2(DDX1 + xwert) + Program.Pow2(ywert) + .1F)));

                                float gew = r1 + r2 + r3 + r4 + r5 + r6 + r7 + r8 + r9 + r10 + r11 + r12 + r13 + r14 + r15 + r16;

                                Zellhoehe[ik] = (AHE_L[ik] * r1 + AHEJP_L[ik] * r2 + AHEIPJP[ik] * r3 +
                                    AHEIP[ik] * r4 + Program.AHE[indi - 1][indj - 1][ik] * r5 + AHEIJM[ik] * r6 +
                                    AHEIPJM[ik] * r7 + AHEIP2JM[ik] * r8 + AHEIP2J[ik] * r9 +
                                    AHEIP2JP[ik] * r10 + AHEIP2JP2[ik] * r11 + AHEIPJP2[ik] * r12 +
                                    AHEIJP2[ik] * r13 + AHEIMJP2[ik] * r14 + AHEIMJP[ik] * r15 +
                                    AHEIMJ[ik] * r16) / gew;

                                Zellhoehe_p[ik] = (AHE_L[ik_p] * r1 + AHEJP_L[ik_p] * r2 + AHEIPJP[ik_p] * r3 +
                                    AHEIP[ik_p] * r4 + Program.AHE[indi - 1][indj - 1][ik_p] * r5 + AHEIJM[ik_p] * r6 +
                                    AHEIPJM[ik_p] * r7 + AHEIP2JM[ik_p] * r8 + AHEIP2J[ik_p] * r9 +
                                    AHEIP2JP[ik_p] * r10 + AHEIP2JP2[ik_p] * r11 + AHEIPJP2[ik_p] * r12 +
                                    AHEIJP2[ik_p] * r13 + AHEIMJP2[ik_p] * r14 + AHEIMJP[ik_p] * r15 +
                                    AHEIMJ[ik_p] * r16) / gew;
                            }
                        }

                        for (int k = Program.NKK; k >= 1; k--)
                        {
                            //vertk = mean absolute Height of the GRAL flow field grid
                            float vertk = Program.HOKART[k - 1] + Program.DZK[k] * 0.5F + Program.AHKOriMin;
                            int indk = 1;
                            int indz = 1;
                            int ik_p = 0;
                            int indk_p = 0;
                            float zellhoehe_p = 0;
                            float delta_z = 0;
                            float zellhoehe = 0;

                            for (int ik = Program.NK; ik >= 1; ik--)
                            {
                                ik_p = Math.Min(ik + 1, Program.NK);

                                if ((indi > Program.NX - 1) || (indi < 2))
                                {
                                    zellhoehe = AHE_L[ik] - Program.AHMIN;
                                    zellhoehe_p = AHE_L[ik_p] - Program.AHMIN;
                                }
                                else if ((indj > Program.NY - 1) || (indj < 2))
                                {
                                    zellhoehe = AHE_L[ik] - Program.AHMIN;
                                    zellhoehe_p = AHE_L[ik_p] - Program.AHMIN;
                                }
                                else
                                {
                                    zellhoehe = Zellhoehe[ik];
                                    zellhoehe_p = Zellhoehe_p[ik];
                                }

                                // GRAL cell height below GRAMM surface -> break
                                if (vertk <= Program.AHKOri[i][j] + Program.CUTK[i][j])
                                {
                                    indz = 0; // GRAL cell below GRAMM surface
                                    indk = ik;
                                    break;
                                }
                                // GRAL cell height above GRAMM surface -> generate UK, VK, WK
                                if (vertk > zellhoehe + Program.CUTK[i][j])
                                {
                                    indk = ik;
                                    indk_p = Math.Min(indk + 1, Program.NK);
                                    delta_z = vertk - (zellhoehe + Program.CUTK[i][j]);
                                    break;
                                }
                            }

                            // gerenate UK, VK, WK from GRAMM wind field inclusive vertical interpolation of GRAMM wind fields
                            if (indz > 0)
                            {
                                if (indi > Program.NX) indi = Program.NX;
                                if (indj > Program.NY) indj = Program.NY;
                                int ixm = 0;
                                int iym = 0;
                                if (xwert < DDX1 * 0.5F)
                                {
                                    ixm = indi - 1;
                                    ixm = Math.Max(ixm, 1);
                                    ixm = Math.Min(ixm, Program.NX);
                                }
                                else
                                {
                                    ixm = indi + 1;
                                    ixm = Math.Max(ixm, 1);
                                    ixm = Math.Min(ixm, Program.NX);
                                }
                                if (ywert < DDY1 * 0.5F)
                                {
                                    iym = indj - 1;
                                    iym = Math.Max(iym, 1);
                                    iym = Math.Min(iym, Program.NY);
                                }
                                else
                                {
                                    iym = indj + 1;
                                    iym = Math.Max(iym, 1);
                                    iym = Math.Min(iym, Program.NY);
                                }

                                float ux1 = Program.UWIN[indi][indj][indk] + (Program.UWIN[ixm][indj][indk] - Program.UWIN[indi][indj][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float ux2 = Program.UWIN[indi][iym][indk] + (Program.UWIN[ixm][iym][indk] - Program.UWIN[indi][iym][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                //vertical interpolation
                                float ux3 = Program.UWIN[indi][indj][indk_p] + (Program.UWIN[ixm][indj][indk_p] - Program.UWIN[indi][indj][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float ux4 = Program.UWIN[indi][iym][indk_p] + (Program.UWIN[ixm][iym][indk_p] - Program.UWIN[indi][iym][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float ux5 = ux1 + (ux3 - ux1) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                float ux6 = ux2 + (ux4 - ux2) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                UK_L[k] = (float)(ux5 + (ux6 - ux5) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));
                                //UK_L[k] = (float)(ux1 + (ux2 - ux1) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));
                               
                                float vx1 = Program.VWIN[indi][indj][indk] + (Program.VWIN[ixm][indj][indk] - Program.VWIN[indi][indj][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float vx2 = Program.VWIN[indi][iym][indk] + (Program.VWIN[ixm][iym][indk] - Program.VWIN[indi][iym][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                //vertical interpolation
                                float vx3 = Program.VWIN[indi][indj][indk_p] + (Program.VWIN[ixm][indj][indk_p] - Program.VWIN[indi][indj][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float vx4 = Program.VWIN[indi][iym][indk_p] + (Program.VWIN[ixm][iym][indk_p] - Program.VWIN[indi][iym][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float vx5 = vx1 + (vx3 - vx1) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                float vx6 = vx2 + (vx4 - vx2) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                VK_L[k] = (float)(vx5 + (vx6 - vx5) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));
                                //VK_L[k] = (float)(vx1 + (vx2 - vx1) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));

                                float wx1 = Program.WWIN[indi][indj][indk] + (Program.WWIN[ixm][indj][indk] - Program.WWIN[indi][indj][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float wx2 = Program.WWIN[indi][iym][indk] + (Program.WWIN[ixm][iym][indk] - Program.WWIN[indi][iym][indk]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                //vertical interpolation
                                float wx3 = Program.WWIN[indi][indj][indk_p] + (Program.WWIN[ixm][indj][indk_p] - Program.WWIN[indi][indj][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float wx4 = Program.WWIN[indi][iym][indk_p] + (Program.WWIN[ixm][iym][indk_p] - Program.WWIN[indi][iym][indk_p]) / DDX1 * (ixm - indi) * (xwert - DDX1 * 0.5F);
                                float wx5 = wx1 + (wx3 - wx1) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                float wx6 = wx2 + (wx4 - wx2) / (Math.Max(zellhoehe_p - zellhoehe, 0.1F)) * delta_z;
                                WK_L[k] = (float)(wx5 + (wx6 - wx5) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));
                                //WK_L[k] = (float)(wx1 + (wx2 - wx1) / DDY1 * (iym - indj) * (ywert - DDY1 * 0.5F));

                                if (i > 1)
                                    if (k <= Program.KKART[i - 1][j]) UK_L[k] = 0;
                                if (j > 1)
                                    if (k <= Program.KKART[i][j - 1]) VK_L[k] = 0;


                                if (i == 1)
                                {
                                    Program.UK[Program.NII + 1][j][k] = Program.UK[Program.NII][j][k];
                                }
                                if (j == 1)
                                {
                                    Program.VK[i][Program.NJJ + 1][k] = Program.VK[i][Program.NJJ][k];
                                }

                                // compute windvector in about 10m
                                if (vertk < Program.AHKOri[i][j] + 10 + 0.5F * Program.DZK[k] && vec_count[i, j] == false)
                                {
                                    vec_count[i, j] = true;
                                    vec_x_loc += UK_L[k];
                                    vec_y_loc += VK_L[k];
                                    vec_numb_loc++;
                                }

                            }
                            else // below GRAMM surface
                            {
                                UK_L[k] = 0;
                                VK_L[k] = 0;
                                WK_L[k] = 0;
                                if (i < Program.NII) UKip_L[k] = 0;
                                if (j < Program.NJJ) VKjp_L[k] = 0;
                                if (k < Program.NKK) WK_L[k + 1] = 0;

                                Program.AHK[i][j] = Math.Max(Program.HOKART[k] + Program.AHKOriMin, Program.AHK[i][j]);

                                Program.KKART[i][j] = Convert.ToInt16(Math.Max(k, Program.KKART[i][j]));
                                
                                // Inside the prognostic sub domain?
                                if (Program.ADVDOM[i][j] > 0)
                                {
                                    KADVMAX1 = Math.Max(Program.KKART[i][j], KADVMAX1);
                                }
                                if ((Program.CUTK[i][j] > 0) && (vertk >= Program.AHKOri[i][j]) && (k > 1))
                                {
                                    Program.BUI_HEIGHT[i][j] = (float)Math.Max(Program.AHK[i][j] - (Program.HOKART[k - 1] + Program.AHKOriMin), Program.BUI_HEIGHT[i][j]);
                                }
                            }
                        }
                    }
                    lock (obj)
                    {
                        if (KADVMAX1 > Program.KADVMAX)
                        {
                            Program.KADVMAX = Math.Max(Program.KADVMAX, KADVMAX1);
                        }                 
                        vec_numb += vec_numb_loc;
                        vec_x += vec_x_loc;
                        vec_y += vec_y_loc;
                    }
                });
                obj = null;
            }
            //maximum z-index up to the microscale flow field is calculated
            Program.KADVMAX = Math.Min(Program.NKK, Program.KADVMAX + Program.VertCellsFF);

            //final computation of the minimum elevation of the GRAL orography
            Program.AHMIN = 100000;
            for (int i = 1; i <= Program.NII; i++)
            {
                for (int j = 1; j <= Program.NJJ; j++)
                {
                    Program.AHMIN = Math.Min(Program.AHMIN, Program.AHK[i][j]);
                }
            }

            Program.WindDirGramm = (float)(Math.Atan2(vec_x, vec_y) * 180 / Math.PI + 180);
            if (Program.WindDirGramm < 0)
                Program.WindDirGramm += 360;
            if (Program.WindDirGramm > 360)
                Program.WindDirGramm -= 360;

            if (vec_numb > 0)
                Program.WindVelGramm = (float)Math.Sqrt(Math.Pow(vec_x, 2) + Math.Pow(vec_y, 2)) / vec_numb;

            //optional: write GRAL orography
            if (Program.IDISP == 1 || firstloop)
            {
                if (File.Exists("GRAL_Topography.txt") == true)
                {
                    Console.WriteLine("Write file GRAL_Topography.txt");

                    try
                    {
                        using (StreamWriter wt = new StreamWriter("GRAL_Topography.txt"))
                        {
                            wt.WriteLine("ncols         " + Program.NII.ToString(CultureInfo.InvariantCulture));
                            wt.WriteLine("nrows         " + Program.NJJ.ToString(CultureInfo.InvariantCulture));
                            wt.WriteLine("xllcorner     " + Program.IKOOAGRAL.ToString(CultureInfo.InvariantCulture));
                            wt.WriteLine("yllcorner     " + Program.JKOOAGRAL.ToString(CultureInfo.InvariantCulture));
                            wt.WriteLine("cellsize      " + DXK.ToString(CultureInfo.InvariantCulture));
                            wt.WriteLine("NODATA_value  " + "-9999");

                            for (int jj = Program.NJJ; jj >= 1; jj--)
                            {
                                for (int o = 1; o <= Program.NII; o++)
                                {
                                    wt.Write((Program.AHK[o][jj] - Program.BUI_HEIGHT[o][jj]).ToString(CultureInfo.InvariantCulture) + " ");
                                }
                                wt.WriteLine();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        string err = ex.Message + " / Error writing GRAL_Topography.txt";
                        Console.WriteLine(err);
                        ProgramWriters.LogfileGralCoreWrite(err);
                    }
                }
            }

            //in case of the diagnostic approach, a boundary layer is established near the obstacle's walls
            if ((Program.FlowFieldLevel == 1) && (Program.BuildingTerrExist == true))
            {
                Parallel.For((1 + Program.IGEB), (Program.NII - Program.IGEB + 1), Program.pOptions, i =>
                {
                    int IGEB = Program.IGEB;
                    for (int j = 1 + IGEB; j <= Program.NJJ - IGEB; j++)
                    {
                        double entf = 0;
                        float[] UK_L = Program.UK[i][j];
                        float[] VK_L = Program.VK[i][j];
                        float[] WK_L = Program.WK[i][j];
                        int KKART = Program.KKART[i][j];

                        for (int k = 1; k < Program.NKK; k++)
                        {
                            double abmind = 1;
                            double abmind1 = 1;
                            double abmind2 = 1;
                            double abmind3 = 1;
                            double abmind4 = 1;
                            double abmind5 = 1;
                            double abmind6 = 1;
                            double abmind7 = 1;
                            double abmind8 = 1;
                            double vertk = Program.HOKART[k - 1] + Program.DZK[k] * 0.5 + Program.AHMIN;

                            //search towards west for obstacles
                            for (int ig = i - IGEB; ig < i; ig++)
                            {
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][j]) && (Program.CUTK[ig][j] > 0) && (k > KKART))
                                    entf = Math.Abs((i - ig) * DXK);
                            }
                            if (entf <= 20)
                                abmind1 *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards east for obstacles
                            for (int ig = i + IGEB; ig >= i + 1; ig--)
                            {
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][j]) && (Program.CUTK[ig][j] > 0) && (k > KKART))
                                    entf = Math.Abs((i - ig) * DXK);
                            }
                            if (entf <= 20)
                                abmind2 *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards south for obstacles
                            for (int jg = j - IGEB; jg < j; jg++)
                            {
                                entf = 21;
                                if ((vertk <= Program.AHK[i][jg]) && (Program.CUTK[i][jg] > 0) && (k > KKART))
                                    entf = Math.Abs((j - jg) * DYK);
                            }
                            if (entf <= 20)
                                abmind3 *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards north for obstacles
                            for (int jg = j + IGEB; jg >= j + 1; jg--)
                            {
                                entf = 21;
                                if ((vertk <= Program.AHK[i][jg]) && (Program.CUTK[i][jg] > 0) && (k > KKART))
                                    entf = Math.Abs((j - jg) * DYK);
                            }
                            if (entf <= 20)
                                abmind4 *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards north/east for obstacles
                            for (int ig = i + IGEB; ig >= i + 1; ig--)
                            {
                                int jg = j + ig - i;
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][jg]) && (Program.CUTK[ig][jg] > 0) && (k > KKART))
                                    entf = Math.Sqrt(Program.Pow2((i - ig) * DXK) + Program.Pow2((j - jg) * DYK));
                            }
                            if (entf <= 20)
                                abmind5 *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards south/west for obstacles
                            for (int ig = i - IGEB; ig < i; ig++)
                            {
                                int jg = j + ig - i;
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][jg]) && (Program.CUTK[ig][jg] > 0) && (k > KKART))
                                    entf = Math.Sqrt(Program.Pow2((i - ig) * DXK) + Program.Pow2((j - jg) * DYK));
                            }
                            if (entf <= 20)
                                abmind6 *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards south/east for obstacles
                            for (int ig = i + IGEB; ig >= i + 1; ig--)
                            {
                                int jg = j - ig + i;
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][jg]) && (Program.CUTK[ig][jg] > 0) && (k > KKART))
                                    entf = Math.Sqrt(Program.Pow2((i - ig) * DXK) + Program.Pow2((j - jg) * DYK));
                            }
                            if (entf <= 20)
                                abmind7 *= 0.19 * Math.Log((entf + 0.5) * 10);

                            //search towards north/west for obstacles
                            for (int ig = i - IGEB; ig < i; ig++)
                            {
                                int jg = j - ig + i;
                                entf = 21;
                                if ((vertk <= Program.AHK[ig][jg]) && (Program.CUTK[ig][jg] > 0) && (k > KKART))
                                    entf = Math.Sqrt(Program.Pow2((i - ig) * DXK) + Program.Pow2((j - jg) * DYK));
                            }
                            if (entf <= 20)
                                abmind8 *= 0.19 * Math.Log((entf + 0.5) * 10);

                            abmind = abmind1 * abmind2 * abmind3 * abmind4 * abmind5 * abmind6 * abmind7 * abmind8;

                            UK_L[k] *= (float)abmind;
                            VK_L[k] *= (float)abmind;
                            WK_L[k] *= (float)abmind;
                        }
                    }
                });
            }

            //prognostic approach
            if ((Program.FlowFieldLevel == 2) && ((Program.BuildingTerrExist == true) || (File.Exists("vegetation.dat") == true)))
            {
                //read vegetation only once
                if (Program.VEG[0][0][0] < 0)
                {
                    ProgramReaders Readclass = new ProgramReaders();
                    Readclass.ReadVegetation();
                }
                Console.WriteLine("PROGNOSTIC WIND FIELD AROUND OBSTACLES");
                PrognosticFlowfield.Calculate();
            }

            //Final mass conservation using poisson equation for pressure
            if (Program.FlowFieldLevel == 1)
                Console.WriteLine("DIAGNOSTIC WIND FIELD AROUND OBSTACLES");
            else if (Program.FlowFieldLevel == 0)
                Console.WriteLine("DIAGNOSTIC WIND FIELD CALCULATION");

            DiagnosticFlowfield.Calculate();

            //in diagnostic mode mass-conservation is finally achieved by adjusting the vertical velocity in each cell
            Parallel.For(2, Program.NII, Program.pOptions, i =>
            {
                for (int j = 2; j < Program.NJJ; j++)
                {
                    //Pointers (to speed up the computations)
                    Single[] UK_L = Program.UK[i][j];
                    Single[] UKi_L = Program.UK[i + 1][j];
                    Single[] VK_L = Program.VK[i][j];
                    Single[] VKj_L = Program.VK[i][j + 1];
                    Single[] WK_L = Program.WK[i][j];
                    double fwo1 = 0;
                    double fwo2 = 0;
                    double fsn1 = 0;
                    double fsn2 = 0;
                    double fbt1 = 0;
                    float KKART = Program.KKART[i][j];

                    for (int k = 1; k < Program.NKK; k++)
                    {
                        if (k > KKART)
                        {
                            if (k <= Program.KKART[i - 1][j])
                                fwo1 = 0;
                            else
                                fwo1 = Program.DZK[k] * DYK * UK_L[k];

                            if (k <= Program.KKART[i + 1][j])
                                fwo2 = 0;
                            else
                                fwo2 = Program.DZK[k] * DYK * UKi_L[k];

                            if (k <= Program.KKART[i][j - 1])
                                fsn1 = 0;
                            else
                                fsn1 = Program.DZK[k] * DYK * VK_L[k];

                            if (k <= Program.KKART[i][j + 1])
                                fsn2 = 0;
                            else
                                fsn2 = Program.DZK[k] * DYK * VKj_L[k];

                            if ((k <= KKART + 1) || (k == 1))
                                fbt1 = 0;
                            else
                                fbt1 = DXK * DYK * WK_L[k];

                            WK_L[k + 1] = (float)((fwo1 - fwo2 + fsn1 - fsn2 + fbt1) / (DXK * DYK));
                        }
                    }
                }
            });
        }
    }
}
