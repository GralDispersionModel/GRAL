using System;
using System.IO;

namespace GRAL_2001
{
    internal enum MeteoReadStatus
    {
        Success,
        EndOfInput,
        InvalidInput
    }

    internal static class MeteoRows
    {
        // Empty lines at EOF are harmless. Skipping a gap inside the file would shift every later situation.
        internal static string ReadDataLine(TextReader reader)
        {
            string line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line)) return line;

            while (line != null)
            {
                line = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    throw new FormatException("blank line is followed by more data");
            }
            return null;
        }
    }
}
