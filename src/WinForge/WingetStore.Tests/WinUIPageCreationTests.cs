namespace WingetStore.Tests;

[Trait("Category", "WinUIIntegration")]
public class WinUIPageCreationTests
{
    public WinUIPageCreationTests() { }

    [Fact(Skip = "Requires live WinUI desktop message pump; executed via WinForge.exe --run-ui-tests")]
    public void CanCreateSettingsPage()
    {
        SettingsPage? page = null;
        WinUIApp.Run(() => { page = new SettingsPage(); });
        Assert.NotNull(page);
    }

    [Fact(Skip = "Requires live WinUI desktop message pump; executed via WinForge.exe --run-ui-tests")]
    public void CanCreateHomePage()
    {
        HomePage? page = null;
        WinUIApp.Run(() => { page = new HomePage(); });
        Assert.NotNull(page);
    }

    [Fact(Skip = "Requires live WinUI desktop message pump; executed via WinForge.exe --run-ui-tests")]
    public void CanCreateInstalledPage()
    {
        InstalledPage? page = null;
        WinUIApp.Run(() => { page = new InstalledPage(); });
        Assert.NotNull(page);
    }

    [Fact(Skip = "Requires live WinUI desktop message pump; executed via WinForge.exe --run-ui-tests")]
    public void CanCreateUpdatesPage()
    {
        UpdatesPage? page = null;
        WinUIApp.Run(() => { page = new UpdatesPage(); });
        Assert.NotNull(page);
    }

    [Fact(Skip = "Requires live WinUI desktop message pump; executed via WinForge.exe --run-ui-tests")]
    public void CanCreateDetailsPage()
    {
        DetailsPage? page = null;
        WinUIApp.Run(() => { page = new DetailsPage(); });
        Assert.NotNull(page);
    }

    [Fact(Skip = "Requires live WinUI desktop message pump; executed via WinForge.exe --run-ui-tests")]
    public void CanCreateAboutPage()
    {
        AboutPage? page = null;
        WinUIApp.Run(() => { page = new AboutPage(); });
        Assert.NotNull(page);
    }

    [Fact(Skip = "Requires live WinUI desktop message pump; executed via WinForge.exe --run-ui-tests")]
    public void CanCreateNoWingetPage()
    {
        NoWingetPage? page = null;
        WinUIApp.Run(() => { page = new NoWingetPage(); });
        Assert.NotNull(page);
    }
}
