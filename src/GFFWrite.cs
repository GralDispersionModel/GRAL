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
using System.IO;
using System.IO.Compression;

namespace GRAL_2001
{
    class WriteGRALFlowFields
    {
        private static int writingMode = 0;
        /// <summary>
        ///Write GRAL microscale wind fields in *.gff format
        /// </summary>
        public static void Write()
        {
            if (File.Exists("GRAL_FlowFields.txt") == true)
            {
                // read GRAL_Flowfields.txt
                try
                {
                    using (StreamReader reader = new StreamReader("GRAL_FlowFields.txt"))
                    {
                        writingMode = Convert.ToInt32(reader.ReadLine());
                    }
                }
                catch { }

                //write *.gff-file
                string GRALflowfield = Path.Combine(Program.GFFFilePath, Convert.ToString(Program.IDISP).PadLeft(5, '0') + ".gff");

                // delete corrupt or invalid gff file
                if (File.Exists(GRALflowfield))
                {
                    try
                    {
                        File.Delete(GRALflowfield);
                    }
                    catch { }
                }

                if (File.Exists(GRALflowfield) == false)
                {
                    if (writingMode == 0)
                    {
                        WriteCompressedGFFFiles(GRALflowfield);
                    }
                    if (writingMode == 1)
                    {
                        WriteStrongCompressedGFFFiles(GRALflowfield);
                    }
                    if (writingMode == 2)
                    {
                        WriteTerrainCompressedGFFFiles(GRALflowfield);
                    }
                }
            } // GFF -Files

            //write turbulence fields in case k-eps model is used
            int TURB_MODEL = 1;
            if (File.Exists("turbulence_model.txt") == true)
            {
                try
                {
                    using (StreamReader sr = new StreamReader("turbulence_model.txt"))
                    {
                        TURB_MODEL = Convert.ToInt32(sr.ReadLine());
                    }
                }
                catch
                { }
            }

            if (TURB_MODEL == 2)
            {
                if (File.Exists("GRAL_FlowFields.txt") == true)
                {
                    //write *.tke-file
                    string GRALtkefield = Convert.ToString(Program.IDISP).PadLeft(5, '0') + ".tke";

                    // delete corrupt or invalid tke file
                    if (File.Exists(GRALtkefield))
                    {
                        try
                        {
                            File.Delete(GRALtkefield);
                        }
                        catch { }
                    }

                    if (File.Exists(GRALtkefield) == false)
                    {
                        try
                        {
                            using (FileStream zipToOpen = new FileStream(GRALtkefield, FileMode.Create))
                            {
                                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                                {
                                    ZipArchiveEntry write_entry = archive.CreateEntry(GRALtkefield);
                                    using (BinaryWriter writer = new BinaryWriter(write_entry.Open()))
                                    {
                                        writer.Write((Int32)Program.NKK);
                                        writer.Write((Int32)Program.NJJ);
                                        writer.Write((Int32)Program.NII);
                                        writer.Write((float)Program.WindDirGral);
                                        writer.Write((float)Program.WindVelGral);
                                        writer.Write((Int32)Program.StabClass);
                                        writer.Write((float)Program.DXK);
                                        int header = -1;
                                        writer.Write(header);

                                        for (int i = 1; i <= Program.NII; i++)
                                        {
                                            for (int j = 1; j <= Program.NJJ; j++)
                                            {
                                                for (int k = 1; k <= Program.NKK; k++)
                                                {
                                                    writer.Write((Int16)Convert.ToInt16(
                                                        Math.Max(Int16.MinValue,
                                                                 Math.Min(Int16.MaxValue, Program.TURB[i][j][k] * 100))));
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                        }
                        catch (Exception e)
                        {
                            string err = "Error when writing file " + GRALtkefield + " / " + e.Message.ToString();
                            Console.WriteLine(err);
                            ProgramWriters.LogfileProblemreportWrite(err);

                            // delete corrupt tke-field file
                            if (File.Exists(GRALtkefield))
                            {
                                try
                                {
                                    File.Delete(GRALtkefield);
                                }
                                catch { }
                            }
                        }

                    }
                }
            }
        } // write


        /// <summary>
        /// Write the classical *.gff file writing vertcal rows 
        /// </summary>
        /// <param name="GRALflowfield">File name</param>
        /// <returns></returns>
        private static bool WriteCompressedGFFFiles(string GRALflowfield)
        {
            try
            {
                using (FileStream zipToOpen = new FileStream(GRALflowfield, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry write_entry = archive.CreateEntry(Path.GetFileName(GRALflowfield));
                        using (BinaryWriter writer = new BinaryWriter(write_entry.Open()))
                        {
                            writer.Write((Int32)Program.NKK);
                            writer.Write((Int32)Program.NJJ);
                            writer.Write((Int32)Program.NII);
                            writer.Write((float)Program.WindDirGral);
                            writer.Write((float)Program.WindVelGral);
                            writer.Write((Int32)Program.StabClass);
                            writer.Write((float)Program.DXK);
                            int header = -1;
                            writer.Write(header);

                            for (int i = 1; i <= Program.NII + 1; i++)
                            {
                                for (int j = 1; j <= Program.NJJ + 1; j++)
                                {
                                    byte[] writeData = new byte[(Program.NKK + 1) * 6]; // byte-buffer
                                    int z = 0;

                                    for (int k = 1; k <= Program.NKK + 1; k++)
                                    {
                                        byte[] intbyte = BitConverter.GetBytes((Int16)(
                                            Math.Max(Int16.MinValue,
                                                     Math.Min(Int16.MaxValue, Program.UK[i][j][k - 1] * 100))));
                                        writeData[z] = intbyte[0];
                                        ++z;
                                        writeData[z] = intbyte[1];
                                        ++z;

                                        intbyte = BitConverter.GetBytes((Int16)(
                                            Math.Max(Int16.MinValue,
                                                     Math.Min(Int16.MaxValue, Program.VK[i][j][k - 1] * 100))));
                                        writeData[z] = intbyte[0];
                                        ++z;
                                        writeData[z] = intbyte[1];
                                        ++z;

                                        intbyte = BitConverter.GetBytes((Int16)(
                                            Math.Max(Int16.MinValue,
                                                     Math.Min(Int16.MaxValue, Program.WK[i][j][k] * 100))));
                                        writeData[z] = intbyte[0];
                                        ++z;
                                        writeData[z] = intbyte[1];
                                        ++z;
                                    }
                                    writer.Write(writeData); // write buffer
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                string err = "Error when writing file " + GRALflowfield + " / " + e.Message.ToString();
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                // delete corrupt flowfield file
                if (File.Exists(GRALflowfield))
                {
                    try
                    {
                        File.Delete(GRALflowfield);
                    }
                    catch { }
                }
                return false;
            }
        }

        /// <summary>
        /// Write a stronger compressable *.gff file writing horizontal grids  
        /// </summary>
        /// <param name="GRALflowfield">File name</param>
        /// <returns></returns>
        private static bool WriteStrongCompressedGFFFiles(string GRALflowfield)
        {
            try
            {
                using (FileStream zipToOpen = new FileStream(GRALflowfield, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry write_entry = archive.CreateEntry(Path.GetFileName(GRALflowfield));
                        using (BinaryWriter writer = new BinaryWriter(write_entry.Open()))
                        {
                            writer.Write((Int32)(-1) * Program.NKK); // write negative value -> throw an error at old GRAL versions to avoid erroneous wind fields
                            writer.Write((Int32)Program.NJJ);
                            writer.Write((Int32)Program.NII);
                            writer.Write((float)Program.WindDirGral);
                            writer.Write((float)Program.WindVelGral);
                            writer.Write((Int32)Program.StabClass);
                            writer.Write((float)Program.DXK);
                            int header = -2;
                            writer.Write(header);

                            byte[] writeData = new byte[(Program.NJJ + 1) * 6]; // Byte - buffer
                            byte[] intbyte = BitConverter.GetBytes((Int16)(0)); // Bitconverter buffer

                            for (int k = 1; k <= Program.NKK + 1; k++)
                            {
                                for (int i = 1; i <= Program.NII + 1; i++)
                                {
                                    float[][] UK_L = Program.UK[i];
                                    float[][] VK_L = Program.VK[i];
                                    float[][] WK_L = Program.WK[i];

                                    // write each component after the other one -> better compression
                                    // loop unroll for better performance
                                    int km = k - 1;
                                    int bytecounter = 0;
                                    float _val = 0;
                                    Int16 _valInt16 = 0;
                                    for (int j = 1; j < Program.NJJ + 2; j++)
                                    {
                                        _val = UK_L[j][km] * 100;
                                        if (_val > Int16.MaxValue)
                                        {
                                            _valInt16 = Int16.MaxValue;
                                        }
                                        else if (_val < Int16.MinValue)
                                        {
                                            _valInt16 = Int16.MinValue;
                                        }
                                        else
                                        {
                                            _valInt16 = (Int16)_val;
                                        }

                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x000000FF));
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x0000FF00) >> 8);
                                    }

                                    for (int j = 1; j < Program.NJJ + 2; j++)
                                    {
                                        _val = VK_L[j][km] * 100;
                                        if (_val > Int16.MaxValue)
                                        {
                                            _valInt16 = Int16.MaxValue;
                                        }
                                        else if (_val < Int16.MinValue)
                                        {
                                            _valInt16 = Int16.MinValue;
                                        }
                                        else
                                        {
                                            _valInt16 = (Int16)_val;
                                        }

                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x000000FF));
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x0000FF00) >> 8);
                                    }

                                    for (int j = 1; j < Program.NJJ + 2; j++)
                                    {
                                        _val = WK_L[j][k] * 100;
                                        if (_val > Int16.MaxValue)
                                        {
                                            _valInt16 = Int16.MaxValue;
                                        }
                                        else if (_val < Int16.MinValue)
                                        {
                                            _valInt16 = Int16.MinValue;
                                        }
                                        else
                                        {
                                            _valInt16 = (Int16)_val;
                                        }

                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x000000FF));
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x0000FF00) >> 8);
                                    }
                                    writer.Write(writeData); // write buffer
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                string err = "Error when writing file " + GRALflowfield + " / " + e.Message.ToString();
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                // delete corrupt flowfield file
                if (File.Exists(GRALflowfield))
                {
                    try
                    {
                        File.Delete(GRALflowfield);
                    }
                    catch { }
                }
                return false;
            }
        }

        /// <summary>
        /// Write a stronger compressable *.gff file for areas with terrain
        /// </summary>
        /// <param name="GRALflowfield">File name</param>
        /// <returns></returns>
        private static bool WriteTerrainCompressedGFFFiles(string GRALflowfield)
        {
            try
            {
                using (FileStream zipToOpen = new FileStream(GRALflowfield, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry write_entry = archive.CreateEntry(Path.GetFileName(GRALflowfield));
                        using (BinaryWriter writer = new BinaryWriter(write_entry.Open()))
                        {
                            writer.Write((Int32)(-1) * Program.NKK); // write negative value -> throw an error at old GRAL versions to avoid erroneous wind fields
                            writer.Write((Int32)Program.NJJ);
                            writer.Write((Int32)Program.NII);
                            writer.Write((float)Program.WindDirGral);
                            writer.Write((float)Program.WindVelGral);
                            writer.Write((Int32)Program.StabClass);
                            writer.Write((float)Program.DXK);
                            int header = -3; //flag for *.gff mode 3
                            writer.Write(header);

                            //Write KKART
                            for (int i = 1; i < Program.NII + 1; i++)
                            {
                                Span<byte> _kkartBuffer = new byte[Program.NJJ * 2];
                                int bytecounter = 0;
                                for (int j = 1; j < Program.NJJ + 1; j++)
                                {
                                    Int16 _valInt16 = Program.KKART[i][j];
                                    _kkartBuffer[bytecounter++] = (byte)((_valInt16 & 0x000000FF));
                                    _kkartBuffer[bytecounter++] = (byte)((_valInt16 & 0x0000FF00) >> 8);
                                }
                                writer.Write(_kkartBuffer);
                            }

                            //Write the vectors
                            Span<byte> WriteBuffer = new byte[(Program.NKK + 1) * 6]; // byte-buffer
                            for (int i = 1; i <= Program.NII + 1; i++)
                            {
                                for (int j = 1; j <= Program.NJJ + 1; j++)
                                {
                                    float[] UK_L = Program.UK[i][j];
                                    float[] VK_L = Program.VK[i][j];
                                    float[] WK_L = Program.WK[i][j];

                                    int KKART = Program.KKART[i][j];
                                    if (KKART < 1)
                                    {
                                        KKART = 1;
                                    }

                                    Span<byte> writeData = WriteBuffer.Slice((KKART - 1) * 6);  // sliced byte-buffer

                                    //keep each wind component together to get a better compression rate
                                    int bytecounter = 0;
                                    for (int k = KKART; k <= Program.NKK + 1; k++)
                                    {
                                        Int16 _valInt16 = 0;
                                        float _val = MathF.Round(UK_L[k - 1] * 100);
                                        if (_val > Int16.MaxValue)
                                        {
                                            _valInt16 = Int16.MaxValue;
                                        }
                                        else if (_val < Int16.MinValue)
                                        {
                                            _valInt16 = Int16.MinValue;
                                        }
                                        else
                                        {
                                            _valInt16 = (Int16)_val;
                                        }
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x000000FF));
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x0000FF00) >> 8);
                                    }

                                    for (int k = KKART; k <= Program.NKK + 1; k++)
                                    {
                                        Int16 _valInt16 = 0;
                                        float _val = MathF.Round(VK_L[k -1] * 100);
                                        if (_val > Int16.MaxValue)
                                        {
                                            _valInt16 = Int16.MaxValue;
                                        }
                                        else if (_val < Int16.MinValue)
                                        {
                                            _valInt16 = Int16.MinValue;
                                        }
                                        else
                                        {
                                            _valInt16 = (Int16)_val;
                                        }
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x000000FF));
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x0000FF00) >> 8);
                                    }

                                    for (int k = KKART; k <= Program.NKK + 1; k++)
                                    {
                                        Int16 _valInt16 = 0;
                                        float _val = MathF.Round(WK_L[k] * 100);
                                        if (_val > Int16.MaxValue)
                                        {
                                            _valInt16 = Int16.MaxValue;
                                        }
                                        else if (_val < Int16.MinValue)
                                        {
                                            _valInt16 = Int16.MinValue;
                                        }
                                        else
                                        {
                                            _valInt16 = (Int16)_val;
                                        }
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x000000FF));
                                        writeData[bytecounter++] = (byte)((_valInt16 & 0x0000FF00) >> 8);
                                    }
                                    // if (i==56 && j == 30)
                                    // {
                                    //     int km=11; int k = 12;
                                    //     Console.WriteLine(UK_L[km]+"/"+VK_L[km] + "/" + WK_L[k]);
                                    // }
                                    writer.Write(writeData); // write buffer
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                string err = "Error when writing file " + GRALflowfield + " / " + e.Message.ToString();
                Console.WriteLine(err);
                ProgramWriters.LogfileProblemreportWrite(err);

                // delete corrupt flowfield file
                if (File.Exists(GRALflowfield))
                {
                    try
                    {
                        File.Delete(GRALflowfield);
                    }
                    catch { }
                }
                return false;
            }
        }

        public static void WriteGRALGeometries()
        {
            if (File.Exists("GRAL_FlowFields.txt") == true)
            {
                //write geometry data
                string GRALgeom = "GRAL_geometries.txt";
                try
                {
                    using (BinaryWriter writer = new BinaryWriter(File.Open(GRALgeom, FileMode.Create)))
                    {
                        writer.Write((Int32)Program.NKK);
                        writer.Write((Int32)Program.NJJ);
                        writer.Write((Int32)Program.NII);
                        writer.Write((Int32)Program.IKOOAGRAL);
                        writer.Write((Int32)Program.JKOOAGRAL);
                        writer.Write((float)Program.DZK[1]);
                        writer.Write((float)Program.StretchFF);
                        if (Program.StretchFF < 0.1)
                        {
                            writer.Write((int)Program.StretchFlexible.Count);
                            foreach (float[] stretchflex in Program.StretchFlexible)
                            {
                                writer.Write((float)stretchflex[0]);
                                writer.Write((float)stretchflex[1]);
                            }
                        }
                        writer.Write((float)Program.AHMIN);

                        for (int i = 1; i <= Program.NII + 1; i++)
                        {
                            for (int j = 1; j <= Program.NJJ + 1; j++)
                            {
                                writer.Write((float)Program.AHK[i][j]);
                                writer.Write((Int32)Program.KKART[i][j]);
                                writer.Write((float)Program.BUI_HEIGHT[i][j]);
                            }
                        }
                    }

                }
                catch
                {
                    string err = "Error when writing file " + GRALgeom;
                    Console.WriteLine(err);
                    ProgramWriters.LogfileProblemreportWrite(err);

                    //                    while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)) ;
                    //                    Environment.Exit(0);
                }
            }
        }


    }
}
