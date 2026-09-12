namespace WingetStore.Tests;

public class MockProcessRunner : IProcessRunner
{
    public static bool ShouldThrow { get; set; }

    public async Task<int> RunStreamAsync(string fileName, string arguments, Action<string> onLineReceived, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated general command failure");

        await Task.Delay(100, cancellationToken);

        if (arguments != null && (arguments.Contains("install", StringComparison.OrdinalIgnoreCase) ||
                                  arguments.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ||
                                  arguments.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) &&
            arguments.Contains("Mock.", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Contains("Mock.Throw", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Simulated task exception");
            if (arguments.Contains("Mock.Fail", StringComparison.OrdinalIgnoreCase)) return 2;

            string[] statusSteps = arguments.Contains("install", StringComparison.OrdinalIgnoreCase) ? TestHelper.InstallSteps
                : arguments.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ? TestHelper.UpgradeSteps
                : TestHelper.UninstallSteps;

            for (int i = 0; i < statusSteps.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onLineReceived($"Progress: {10 + i * 25}%");
                onLineReceived(statusSteps[i]);
                await Task.Delay(50, cancellationToken);
            }
            return 0;
        }

        if (arguments != null)
        {
            if (arguments.Contains("source list", StringComparison.OrdinalIgnoreCase))
            {
                onLineReceived("Name    Argument");
                onLineReceived("-----------------------------------------");
                onLineReceived("winget  https://cdn.winget.microsoft.com/cache");
                onLineReceived("msstore https://storeedgefd.dsx.mp.microsoft.com/v9.0");
                return 0;
            }

            if (arguments.StartsWith("list", StringComparison.OrdinalIgnoreCase))
            {
                onLineReceived("(1/4) Git [Git.Git]");
                onLineReceived("  Publisher: Software Corp");
                onLineReceived("  Version: 2.40.0");
                onLineReceived("  Origin Source: winget");
                onLineReceived("");
                onLineReceived("(2/4) Visual Studio Code [Microsoft.VisualStudioCode]");
                onLineReceived("  Publisher: Microsoft Corporation");
                onLineReceived("  Version: 1.79.0");
                onLineReceived("  Origin Source: winget");
                onLineReceived("");
                onLineReceived("(3/4) Mock Installed Package [Mock.App.Installed]");
                onLineReceived("  Publisher: Mock Publisher");
                onLineReceived("  Version: 1.0.0");
                onLineReceived("  Origin Source: winget");
                onLineReceived("");
                onLineReceived("(4/4) Mock Upgradable Package [Mock.App.Upgradable]");
                onLineReceived("  Publisher: Mock Publisher");
                onLineReceived("  Version: 1.0.0");
                onLineReceived("  Origin Source: winget");
                return 0;
            }

            if (arguments.StartsWith("upgrade", StringComparison.OrdinalIgnoreCase) && !arguments.Contains("--all", StringComparison.OrdinalIgnoreCase))
            {
                onLineReceived("Name                           Id                                       Version          Available        Source");
                onLineReceived("----------------------------------------------------------------------------------------------------------------");
                onLineReceived("Git                            Git.Git                                  2.40.0           2.41.0           winget");
                onLineReceived("Visual Studio Code             Microsoft.VisualStudioCode               1.79.0           1.80.0           winget");
                onLineReceived("Mock Upgradable Package        Mock.App.Upgradable                      1.0.0            1.1.0            winget");
                return 0;
            }

            if (arguments.StartsWith("search", StringComparison.OrdinalIgnoreCase))
            {
                onLineReceived("Name                           Id                                       Version          Source");
                onLineReceived("------------------------------------------------------------------------------------------------");
                onLineReceived("Git                            Git.Git                                  2.41.0           winget");
                onLineReceived("GitHub Desktop                 GitHub.GitHubDesktop                     3.2.3            winget");
                onLineReceived("GitLab Runner                  GitLab.GitLabRunner                      16.1.0           winget");
                return 0;
            }

            if (arguments.StartsWith("show", StringComparison.OrdinalIgnoreCase))
            {
                if (arguments.Contains("Mock.NotExist", StringComparison.OrdinalIgnoreCase)) return 1;
                onLineReceived("Found Git [Git.Git]");
                onLineReceived("Version: 2.41.0");
                onLineReceived("Publisher: Software Corp");
                onLineReceived("Author: Git Contributors");
                onLineReceived("Publisher Support Url: https://git-scm.com/support");
                onLineReceived("Description: Git is a free and open source distributed version control system.");
                onLineReceived("AppMoniker: git");
                onLineReceived("Tags: git, vcs, version-control");
                if (arguments.Contains("Mock.Race.App", StringComparison.OrdinalIgnoreCase))
                    onLineReceived("Homepage: https://www.example.com/product");
                return 0;
            }
        }
        return 0;
    }
}
