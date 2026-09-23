using System;

namespace GRAL_2001
{
    /// <summary>Find the first source whose inclusive endpoint contains a particle.</summary>
    internal static class ParticleSourceLookup
    {
        internal static int[] BuildEnds(int[] counts, int sourceCount)
        {
            if (sourceCount == 0) return Array.Empty<int>();
            int[] ends = new int[sourceCount];
            int sum = 0;
            for (int i = 0; i < sourceCount; i++)
            {
                sum = checked(sum + counts[i + 1]);
                ends[i] = sum;
            }
            return ends;
        }

        internal static int FindSource(int[] ends, int particle)
        {
            if (particle <= 0 || ends.Length == 0 || particle > ends[ends.Length - 1])
                return 0;
            int low = 0;
            int high = ends.Length - 1;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (ends[mid] < particle)
                    low = mid + 1;
                else
                    high = mid;
            }
            return low + 1;
        }
    }
}
