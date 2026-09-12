namespace WingetStore.Tests;

public class SecurityAndSanitizationTests
{
    [Theory]
    [InlineData("Microsoft.VisualStudioCode", "Microsoft.VisualStudioCode.png")]
    [InlineData("Foo/Bar\\Baz", "Foo_Bar_Baz.png")]
    [InlineData("..\\..\\secret.txt", "____secret.txt.png")]
    [InlineData("Invalid:File*Name?Chars\"< >|", "Invalid_File_Name_Chars__ __.png")]
    [InlineData("", "unknown.png")]
    public void GetSafeIconFileName_SanitizesPathTraversalAndInvalidChars(string input, string expected)
    {
        string actual = IconService.GetSafeIconFileName(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("simple", "\"simple\"")]
    [InlineData("with space", "\"with space\"")]
    [InlineData("with\"quote", "\"with\\\"quote\"")]
    [InlineData("trailing\\", "\"trailing\\\\\"")]
    [InlineData("path\\with\\\"quote", "\"path\\with\\\\\\\"quote\"")]
    [InlineData("", "\"\"")]
    public void EscapeArgument_EscapesQuotesAndBackslashesCorrectly(string input, string expected)
    {
        string actual = WingetService.EscapeArgument(input);
        Assert.Equal(expected, actual);
    }
}
