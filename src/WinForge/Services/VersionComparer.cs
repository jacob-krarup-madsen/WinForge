using System.Numerics;

namespace WingetStore.Services;

public class VersionComparer : IComparer<string>
{
    public static VersionComparer Instance { get; } = new();

    public int Compare(string? x, string? y) => CompareVersions(x, y);

    internal static int CompareVersions(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        string cleanX = x.TrimStart('v', 'V').Trim();
        string cleanY = y.TrimStart('v', 'V').Trim();

        string[] noBuildX = cleanX.Split('+', 2);
        string[] noBuildY = cleanY.Split('+', 2);

        string[] preSplitX = noBuildX[0].Split('-', 2);
        string[] preSplitY = noBuildY[0].Split('-', 2);

        bool hasPreX = preSplitX.Length > 1;
        bool hasPreY = preSplitY.Length > 1;
        string preX = hasPreX ? preSplitX[1] : "";
        string preY = hasPreY ? preSplitY[1] : "";

        int coreCmp = CompareIdentifierSequence(preSplitX[0], preSplitY[0]);
        if (coreCmp != 0) return coreCmp;

        if (hasPreX && !hasPreY) return -1;
        if (!hasPreX && hasPreY) return 1;
        if (hasPreX && hasPreY)
        {
            int preCmp = CompareIdentifierSequence(preX, preY);
            if (preCmp != 0) return preCmp;
        }

        return 0;
    }

    private static int CompareIdentifierSequence(string a, string b)
    {
        string[] partsA = a.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        string[] partsB = b.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

        int minLen = Math.Min(partsA.Length, partsB.Length);
        for (int i = 0; i < minLen; i++)
        {
            int cmp = CompareIdentifier(partsA[i], partsB[i]);
            if (cmp != 0) return cmp;
        }

        return partsA.Length.CompareTo(partsB.Length);
    }

    private static int CompareIdentifier(string a, string b)
    {
        bool isNumA = BigInteger.TryParse(a, out BigInteger numA);
        bool isNumB = BigInteger.TryParse(b, out BigInteger numB);

        if (isNumA && isNumB) return numA.CompareTo(numB);
        if (isNumA) return -1;
        if (isNumB) return 1;
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
