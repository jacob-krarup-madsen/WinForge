namespace WingetStore.Tests;

public class NullLineRunner : IProcessRunner
{
    public async Task<int> RunStreamAsync(string fileName, string arguments, Action<string> onLineReceived, CancellationToken cancellationToken = default)
    {
        onLineReceived(null!);
        if (arguments != null && (arguments.Contains("install", StringComparison.OrdinalIgnoreCase) ||
                                  arguments.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ||
                                  arguments.Contains("uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            string[] ops = arguments.Contains("install", StringComparison.OrdinalIgnoreCase) ? TestHelper.InstallSteps
                : arguments.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ? TestHelper.UpgradeSteps
                : TestHelper.UninstallSteps;
            for (int i = 0; i < ops.Length; i++)
            {
                onLineReceived($"Progress: {10 + i * 25}%");
                onLineReceived(ops[i]);
                await Task.Delay(10, cancellationToken);
            }
            return 0;
        }
        return 0;
    }
}
