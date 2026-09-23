// Source-group filename tokens. GPL-3.0, same license as GRAL.
// Keep this file identical in core and GUI apart from the namespace.
using System;
using System.Globalization;

namespace GRAL_2001
{
    public static class SourceGroupFileName
    {
        public const int MaximumId = 1295;
        public static bool IsSupported(int id) => id >= 1 && id <= MaximumId;

        public static string Encode(string id) => Encode(int.Parse(id, CultureInfo.InvariantCulture));

        public static string Encode(int id)
        {
            if (!IsSupported(id)) throw new ArgumentOutOfRangeException(nameof(id), "Source groups must be in 1..1295.");
            if (id < 100) return id.ToString("D2", CultureInfo.InvariantCulture);
            int offset = id - 100;
            if (offset < 260) return new string(new[] { (char)('A' + offset / 10), (char)('0' + offset % 10) });
            offset -= 260;
            if (offset < 260) return new string(new[] { (char)('0' + offset % 10), (char)('A' + offset / 10) });
            offset -= 260;
            return new string(new[] { (char)('A' + offset / 26), (char)('A' + offset % 26) });
        }

        public static bool TryDecode(string token, out int id)
        {
            id = 0;
            if (token == null || token.Length != 2) return false;
            char a = token[0], b = token[1];
            if (a >= 'a' && a <= 'z') a = (char)(a - 'a' + 'A');
            if (b >= 'a' && b <= 'z') b = (char)(b - 'a' + 'A');
            bool ad = a >= '0' && a <= '9', bd = b >= '0' && b <= '9';
            bool al = a >= 'A' && a <= 'Z', bl = b >= 'A' && b <= 'Z';
            if (ad && bd) id = (a - '0') * 10 + b - '0';
            else if (al && bd) id = 100 + (a - 'A') * 10 + b - '0';
            else if (ad && bl) id = 360 + (b - 'A') * 10 + a - '0';
            else if (al && bl) id = 620 + (a - 'A') * 26 + b - 'A';
            return IsSupported(id);
        }

        // The GUI historically used emissions001.dat, not emissions01.dat.
        public static string ModulationToken(int id) => "0" + Encode(id);
        public static string ModulationToken(string id) => ModulationToken(int.Parse(id, CultureInfo.InvariantCulture));
        public static bool TryModulationStem(string stem, out int id)
        {
            const string prefix = "emissions0";
            id = 0;
            return stem != null && stem.Length == prefix.Length + 2
                && stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && TryDecode(stem.Substring(prefix.Length), out id);
        }
    }
}
