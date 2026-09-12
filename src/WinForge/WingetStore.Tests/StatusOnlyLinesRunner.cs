namespace WingetStore.Tests;

public class StatusOnlyLinesRunner : IProcessRunner
{
    public async Task<int> RunStreamAsync(string fileName, string arguments, Action<string> onLineReceived, CancellationToken cancellationToken = default)
    {
        if (arguments != null && (arguments.Contains("install", StringComparison.OrdinalIgnoreCase) ||
                                  arguments.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ||
                                  arguments.Contains("uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            string[] ops = arguments.Contains("install", StringComparison.OrdinalIgnoreCase) ? TestHelper.InstallSteps
                : arguments.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ? TestHelper.UpgradeSteps
                : TestHelper.UninstallSteps;
            foreach (var op in ops)
            {
                onLineReceived(op);
                await Task.Delay(10, cancellationToken);
            }
            return 0;
        }
        return 0;
    }
}
