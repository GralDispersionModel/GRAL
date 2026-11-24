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
        internal static MeteoReadStatus ReportError(string filename, int lineNumber, Exception ex)
        {
            string error = $"Error when reading file {filename} in line {Math.Max(1, lineNumber)}: {ex.Message}";
            Console.WriteLine(error);
            ProgramWriters.LogfileProblemreportWrite(error);
            return MeteoReadStatus.InvalidInput;
        }

        internal static string[] ReadSituation(TextReader reader, int situation, ref int lineNumber)
        {
            string line = null;
            for (int n = 0; n < situation; n++)
            {
                line = ReadDataLine(reader, ref lineNumber);
                if (line == null) break;
            }
            return line?.Split(new char[] { ' ', ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // Empty lines at EOF are harmless. Skipping a gap inside the file would shift every later situation.
        internal static string ReadDataLine(TextReader reader, ref int lineNumber)
        {
            lineNumber++;
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
