namespace WingetStore.Tests;

public class SettingsPageDiagnosticsTests
{
    [Theory]
    [InlineData(true, "Connected to Windows Package Manager", "\uE73E")]
    [InlineData(false, "Winget not found on this system", "\uEA39")]
    public void GetDiagnosticsData_ReturnsCorrectStatus(bool available, string expectedStatus, string expectedGlyph)
    {
        var (statusText, isAvailable, glyph, formatted) = SettingsPage.GetDiagnosticsData(available, DateTime.Today + new TimeSpan(14, 30, 0));
        Assert.Equal(expectedStatus, statusText);
        Assert.Equal(available, isAvailable);
        Assert.Equal(expectedGlyph, glyph);
        Assert.Contains("Checked today", formatted);
    }

    [Fact]
    public void GetDiagnosticsData_PreviousDate_ShowsDate()
    {
        var (_, _, _, formatted) = SettingsPage.GetDiagnosticsData(true, new DateTime(2026, 7, 22, 10, 0, 0));
        Assert.Contains("Checked ", formatted);
        Assert.Contains("22", formatted);
    }

    [Fact]
    public void GetDiagnosticsData_DefaultNotAvailable_UsesNotConnected()
    {
        var (statusText, isAvailable, _, _) = SettingsPage.GetDiagnosticsData(false, DateTime.Now);
        Assert.Equal("Winget not found on this system", statusText);
        Assert.False(isAvailable);
    }
}
