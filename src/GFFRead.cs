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
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;


namespace GRAL_2001
{
    class ReadGralFlowFields
    {
        private const int ZIP_LEAD_BYTES = 0x04034b50;

        /// <summary>
        ///Read a GRAL flow field file
        /// </summary>
        public static bool Read()
        {
            bool ReadingOK = false;      
            string GRALflowfield = Path.Combine(Program.GFFFilePath, Convert.ToString(Program.IDISP).PadLeft(5, '0') + ".gff");
            //reading *.gff-file and GRAL geometries
            if (File.Exists(GRALflowfield) == true && ReadGRALGeometries())
            {
                try
                {
                    // Check if file is zipped
                    if (CheckIfZippedFile(GRALflowfield))
                    {
                        using (FileStream zipToOpen = new FileStream(GRALflowfield, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            using (BufferedStream bs = new BufferedStream(zipToOpen, 32768))
                            {
                                using (ZipArchive archive = new ZipArchive(bs, ZipArchiveMode.Read))
                                {
                                    string filename = archive.Entries[0].FullName;
                                    using (BinaryReader reader = new BinaryReader(archive.Entries[0].Open()))
                                    {
                                        ReadingOK = ReadGffData(reader, GRALflowfield);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        using (BinaryReader reader = new BinaryReader(File.Open(GRALflowfield, FileMode.Open, FileAccess.Read, FileShare.Read)))
                        {
                            ReadingOK = ReadGffData(reader, GRALflowfield);
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Error when reading file " + GRALflowfield);
                    ReadingOK = false;
                }         
            }

            return ReadingOK; //GRAL geometries and reading geometry (at the very 1st loop) is OK
        }

        /// <summary>
        ///Read the GRAL geometry data
        /// </summary>
        private static bool ReadGRALGeometries()
        {
            bool ReadingOK = true;
            //read geometry data
            if (Program.GeometryAlreadyRead == false)
            {
                string GRALgeom = "GRAL_geometries.txt";
                try
                {
                    using (BinaryReader read2 = new BinaryReader(File.Open(GRALgeom, FileMode.Open, FileAccess.Read, FileShare.Read)))
                    {
                        int dummy = read2.ReadInt32();
                        if (dummy != Program.NKK)
                        {
                            throw new IOException("Error when reading file " + GRALgeom + ": field dimension in z-direction do not match");
                        }
                        dummy = read2.ReadInt32();
                        if (dummy != Program.NJJ)
                        {
                            throw new IOException("Error when reading file " + GRALgeom + ": field dimension in y-direction do not match");
                        }
                        dummy = read2.ReadInt32();
                        if (dummy != Program.NII)
                        {
                            throw new IOException("Error when reading file " + GRALgeom + ": field dimension in x-direction do not match");
                        }
                        dummy = read2.ReadInt32();
                        if (dummy != Program.IKOOAGRAL)
                        {
                            throw new IOException("Error when reading file " + GRALgeom + ": western border do not match");
                        }
                        dummy = read2.ReadInt32();
                        if (dummy != Program.JKOOAGRAL)
                        {
                            throw new IOException("Error when reading file " + GRALgeom + ": southern borders do not match");
                        }

                        float dzk1 = read2.ReadSingle();
                        if (Math.Abs(dzk1 - Program.DZK[1]) > 0.001)
                        {
                            throw new IOException("Vertical cell height does not match");
                        }
                        float stretch = read2.ReadSingle();

                        // Check if *.gff file matches the recent GRAL.geb settings for the vertical grid
                        if (Math.Abs(stretch - Program.StretchFF) > 0.001)
                        {
                            throw new IOException("Stretching factor");
                        }
                        if (stretch < 0.1)
                        {
                            int sliceCount = read2.ReadInt32();
                            if (sliceCount != Program.StretchFlexible.Count)
                            {
                                throw new IOException("Slice count does not match");
                            }

                            for (int i = 0; i < sliceCount; i++)
                            {
                                if (Math.Abs(read2.ReadSingle() - Program.StretchFlexible[i][0]) > 0.001)
                                {
                                    throw new IOException("Stretch height does not match at number " + (i + 2).ToString());
                                }
                                if (Math.Abs(read2.ReadSingle() - Program.StretchFlexible[i][1]) > 0.001)
                                {
                                    throw new IOException("Stretch factor  does not match at number " + (i + 2).ToString());
                                }
                            }
                        }

                        Program.AHMIN = read2.ReadSingle();
                        Single Rdummy = 0;
                        for (int i = 1; i <= Program.NII + 1; i++)
                        {
                            for (int j = 1; j <= Program.NJJ + 1; j++)
                            {
                                Rdummy = read2.ReadSingle();
                                Program.AHK[i][j] = Rdummy;
                                int dummy1 = read2.ReadInt32();
                                Program.KKART[i][j] = (short)dummy1;
                                Rdummy = read2.ReadSingle();
                                Program.BUI_HEIGHT[i][j] = Rdummy;
                            }
                        }
                    }
                    Program.GeometryAlreadyRead = true; // set flag to true -> read geometry 1 times!
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    try
                    {
                        File.Delete(GRALgeom);
                    }
                    catch { }
                    ReadingOK = false;
                }
            }
            return ReadingOK;
        }

        /// <summary>
        ///Read the GRAL flow field files
        /// </summary>
        private static bool ReadGffData(BinaryReader reader, string GRALflowfield)
        {
            try
            {
                Int32 dummy = Math.Abs(reader.ReadInt32()); // negative values at strong compressed files
                if (dummy != Program.NKK)
                {
                    throw new IOException("Error when reading file " + GRALflowfield + ": field dimension in z-direction does not match");
                }
                dummy = reader.ReadInt32();
                if (dummy != Program.NJJ)
                {
                    throw new IOException("Error when reading file " + GRALflowfield + ": field dimension in y-direction does not match");
                }
                dummy = reader.ReadInt32();
                if (dummy != Program.NII)
                {
                    throw new IOException("Error when reading file " + GRALflowfield + ": field dimension in x-direction does not match");
                }

                Single direction = reader.ReadSingle();
                Single speed = reader.ReadSingle();
                int ak = reader.ReadInt32();
                Single GRAL_cellsize_ff = reader.ReadSingle();
                if (GRAL_cellsize_ff != Program.DXK)
                {
                    throw new IOException("Error when reading file " + GRALflowfield + ": horizontal grid size does not match");
                }
                int header = reader.ReadInt32();

                if (header == -1)
                {
                    byte[] readData;
                    for (int i = 1; i <= Program.NII + 1; i++)
                    {
                        for (int j = 1; j <= Program.NJJ + 1; j++)
                        {
                            readData = reader.ReadBytes((Program.NKK + 1) * 6); // read all vertical bytes of one point
                            int index = 0;

                            for (int k = 1; k <= Program.NKK + 1; k++)
                            {
                                Program.UK[i][j][k - 1] = (float)(BitConverter.ToInt16(readData, index) * 0.01F); // 2 Bytes  = word integer value
                                index += 2;
                                Program.VK[i][j][k - 1] = (float)(BitConverter.ToInt16(readData, index) * 0.01F);
                                index += 2;
                                Program.WK[i][j][k] = (float)(BitConverter.ToInt16(readData, index) * 0.01F);
                                index += 2;
                            }
                        }
                    }
                }
                else if (header == -2) // new format - better compression factor!
                {
                    byte[] readData;
                    for (int k = 1; k <= Program.NKK + 1; k++)
                    {
                        for (int i = 1; i <= Program.NII + 1; i++)
                        {
                            float[][] UK_L = Program.UK[i];
                            float[][] VK_L = Program.VK[i];
                            float[][] WK_L = Program.WK[i];

                            readData = reader.ReadBytes((Program.NJJ + 1) * 6); // read one vertical column 
                            int offset = (Program.NJJ + 1) * 2;
                            Parallel.For(1, Program.NJJ + 1, Program.pOptions, j =>
                            {
                                int index = (j - 1) * 2;
                                UK_L[j][k - 1] = (float)((Int16)(readData[index] | (readData[index + 1] << 8))) * 0.01F; // 19.06.03 Ku: use Bitshift instead of Bitconverter 2 Bytes  = word integer value
                                index += offset;
                                VK_L[j][k - 1] = (float)((Int16)(readData[index] | (readData[index + 1] << 8))) * 0.01F;
                                index += offset;
                                WK_L[j][k] = (float)((Int16)(readData[index] | (readData[index + 1] << 8))) * 0.01F;
                            });
                        }
                    }
                }
                else if (header == -3) // Compression format for terrain
                {
                    for (int i = 1; i < Program.NII + 1; i++)
                    {
                        for (int j = 1; j < Program.NJJ + 1; j++)
                        {
                            Int16 _kkart = reader.ReadInt16();
                            if (Program.KKART[i][j] != _kkart)
                            {
                                throw new Exception("Building or terrain of flow field does not match");
                            }
                        }
                    }

                    byte[] readData;

                    for (int i = 1; i <= Program.NII + 1; i++)
                    {
                        for (int j = 1; j <= Program.NJJ + 1; j++)
                        {
                            int KKART = Program.KKART[i][j];
                            if (KKART < 1)
                            {
                                KKART = 1;
                            }
                            readData = reader.ReadBytes((Program.NKK + 2 - KKART) * 6); // read all vertical bytes of one point
                            int index = 0;

                            float[] UK_L = Program.UK[i][j];
                            float[] VK_L = Program.VK[i][j];
                            float[] WK_L = Program.WK[i][j];

                            for (int k = 1; k < KKART; k++)
                            {
                                UK_L[k - 1] = 0;
                                VK_L[k - 1] = 0;
                                WK_L[k] = 0;
                            }
                            for (int k = KKART; k <= Program.NKK + 1; k++)
                            {
                                UK_L[k - 1] = (float)(BitConverter.ToInt16(readData, index) * 0.01F); // 2 Bytes  = word integer value
                                index += 2;
                            }
                            for (int k = KKART; k <= Program.NKK + 1; k++)
                            {
                                VK_L[k - 1] = (float)(BitConverter.ToInt16(readData, index) * 0.01F);
                                index += 2;
                            }
                            for (int k = KKART; k <= Program.NKK + 1; k++)
                            {
                                WK_L[k] = (float)(BitConverter.ToInt16(readData, index) * 0.01F);
                                index += 2;
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public static bool CheckIfZippedFile(string filename)
        {
            try
            {
                int zipheader = 0;

                if (File.Exists(filename) == false)
                {
                    return false;
                }

                using (BinaryReader header = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    zipheader = header.ReadInt32();
                    if (zipheader == ZIP_LEAD_BYTES)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

    }
}
