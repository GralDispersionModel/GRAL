// Source group configuration. GPL-3.0, same license as GRAL.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GRAL_2001
{
    public partial class Program
    {
        public static void ConfigureSourceGroups(string line)
        {
            if (line == null) throw new InvalidDataException("Missing source groups in GRAL.geb");
            var groups = new List<int>();
            var indices = new Dictionary<int, int>();
            foreach (string token in line.Split('!')[0].Split(new char[] { ',', ';' }))
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                if (!int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                    || !SourceGroupFileName.IsSupported(id) || indices.ContainsKey(id))
                {
                    string error = "Source groups must be unique IDs in 1..1295: " + token;
                    Console.Error.WriteLine(error);
                    throw new InvalidDataException(error);
                }
                indices.Add(id, groups.Count);
                groups.Add(id);
            }
            if (groups.Count == 0) throw new InvalidDataException("At least one source group is required");
            SourceGroups = groups;
            SourceGroupIndices.Clear();
            foreach (var entry in indices) SourceGroupIndices.Add(entry.Key, entry.Value);
            DecayRate = new double[groups.Count];
        }
    }
}
