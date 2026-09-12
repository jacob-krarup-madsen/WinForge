namespace WingetStore.Tests;

public class VersionComparerEdgeCaseTests
{
    [Fact]
    public void Compare_NullArguments_HandlesNulls()
    {
        var comparer = VersionComparer.Instance;
        Assert.Equal(0, comparer.Compare(null, null));
        Assert.True(comparer.Compare(null, "1.0") < 0);
        Assert.True(comparer.Compare("1.0", null) > 0);
    }

    [Fact]
    public void Compare_PrereleaseVsNonPrerelease_PrereleaseIsLower()
    {
        var comparer = VersionComparer.Instance;
        Assert.True(comparer.Compare("1.0.0-alpha", "1.0.0") < 0);
        Assert.True(comparer.Compare("1.0.0", "1.0.0-alpha") > 0);
    }

    [Fact]
    public void Compare_PrereleaseAlphabeticalOrdering_SortsCorrectly()
    {
        var comparer = VersionComparer.Instance;
        Assert.True(comparer.Compare("1.0.0-alpha", "1.0.0-beta") < 0);
        Assert.True(comparer.Compare("1.0.0-rc1", "1.0.0-beta") > 0);
    }

    [Fact]
    public void Compare_DifferentSectionLengths_ShorterIsLower()
    {
        var comparer = VersionComparer.Instance;
        Assert.True(comparer.Compare("1.0", "1.0.0") < 0);
        Assert.True(comparer.Compare("1.0.0.1", "1.0.0") > 0);
    }

    [Fact]
    public void Compare_NonNumericParts_UsesCaseInsensitiveStringComparison()
    {
        var comparer = VersionComparer.Instance;
        Assert.True(comparer.Compare("1.0.0.a", "1.0.0.b") < 0);
        Assert.Equal(0, comparer.Compare("1.0.0.A", "1.0.0.a"));
    }

    [Fact]
    public void Compare_LeadingVPrefix_IgnoresPrefixCaseInsensitively()
    {
        var comparer = VersionComparer.Instance;
        Assert.Equal(0, comparer.Compare("v2.1.0", "V2.1.0"));
        Assert.Equal(0, comparer.Compare("v2.1.0", "2.1.0"));
    }

    [Fact]
    public void Compare_BuildMetadataIsIgnoredForPrecedence()
    {
        var comparer = VersionComparer.Instance;
        Assert.Equal(0, comparer.Compare("1.0.0+build1", "1.0.0+build2"));
        Assert.Equal(0, comparer.Compare("1.0.0+build1", "1.0.0"));
        Assert.Equal(0, comparer.Compare("1.0.0-alpha.1+build5", "1.0.0-alpha.1+build9"));
    }

    [Fact]
    public void Compare_NumericPrereleaseIdentifiers_ComparedNumerically()
    {
        var comparer = VersionComparer.Instance;
        Assert.True(comparer.Compare("1.0.0-alpha.10", "1.0.0-alpha.9") > 0);
        Assert.True(comparer.Compare("1.0.0-alpha.9", "1.0.0-alpha.10") < 0);
        Assert.True(comparer.Compare("1.0.0-rc.2", "1.0.0-rc.10") < 0);
    }

    [Fact]
    public void Compare_NumericPrereleaseIdentifier_LowerThanAlphanumeric()
    {
        var comparer = VersionComparer.Instance;
        Assert.True(comparer.Compare("1.0.0-rc.1", "1.0.0-rc.a") < 0);
        Assert.True(comparer.Compare("1.0.0-rc.a", "1.0.0-rc.1") > 0);
    }

    [Fact]
    public void Compare_OverflowLengthNumericParts_ComparedNumerically()
    {
        var comparer = VersionComparer.Instance;
        string hugeA = "123456789012345678901234567890";
        string hugeB = "123456789012345678901234567891";
        Assert.True(comparer.Compare("1.0.0." + hugeA, "1.0.0." + hugeB) < 0);
        Assert.True(comparer.Compare("1.0.0." + hugeA, "1.0.0." + hugeA) == 0);
        Assert.True(comparer.Compare("1.0.0.1000000000000000000000000", "1.0.0.999") > 0);
        Assert.True(comparer.Compare("1.0.0.999", "1.0.0.1000000000000000000000000") < 0);
    }

    [Fact]
    public void CompareVersions_Static_MatchesInstanceBehavior()
    {
        Assert.True(VersionComparer.CompareVersions("1.0.0-alpha.10", "1.0.0-alpha.9") > 0);
        Assert.Equal(0, VersionComparer.CompareVersions("1.0.0+build1", "1.0.0"));
        Assert.True(VersionComparer.CompareVersions(null, "1.0") < 0);
        Assert.Equal(0, VersionComparer.CompareVersions(null, null));
    }

    [Fact]
    public void CompareVersions_MixedCoreSegments_FallbackToAlphanumeric()
    {
        Assert.True(VersionComparer.CompareVersions("1.0.0.a", "1.0.0.b") < 0);
        Assert.True(VersionComparer.CompareVersions("1.0.0.1", "1.0.0.a") < 0);
    }

    [Fact]
    public void Compare_SameReferenceInstance_ReturnsZero()
    {
        var comparer = VersionComparer.Instance;
        string v = "1.0.0";
        Assert.Equal(0, comparer.Compare(v, v));
    }

    [Fact]
    public void Compare_IdenticalVersions_ReturnsZero()
    {
        var comparer = VersionComparer.Instance;
        string x = new string("1.0.0".ToCharArray());
        string y = new string("1.0.0".ToCharArray());
        Assert.NotSame(x, y);
        Assert.Equal(0, comparer.Compare(x, y));
    }

    [Fact]
    public void Compare_IdenticalPrerelease_ReturnsZero()
    {
        var comparer = VersionComparer.Instance;
        string x = new string("1.0.0-alpha".ToCharArray());
        string y = new string("1.0.0-alpha".ToCharArray());
        Assert.NotSame(x, y);
        Assert.Equal(0, comparer.Compare(x, y));
    }

    [Fact]
    public void Compare_EmptyPrereleaseSuffix_IsLowerThanStable()
    {
        var comparer = VersionComparer.Instance;
        Assert.True(comparer.Compare("1.0.0-", "1.0.0") < 0);
        Assert.True(comparer.Compare("1.0.0", "1.0.0-") > 0);
        Assert.True(comparer.Compare("1.0.0-", "1.0.0-rc1") < 0);
    }

    [Fact]
    public void Compare_IdenticalBuildMetadata_ReturnsZero()
    {
        var comparer = VersionComparer.Instance;
        string x = new string("1.0.0+build1".ToCharArray());
        string y = new string("1.0.0+build1".ToCharArray());
        Assert.NotSame(x, y);
        Assert.Equal(0, comparer.Compare(x, y));
    }
}
