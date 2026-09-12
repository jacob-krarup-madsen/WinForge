namespace WingetStore.Tests;

public static class TestHelper
{
    public static readonly string[] InstallSteps = ["Downloading package...", "Verifying hash...", "Running installer...", "Finalizing..."];
    public static readonly string[] UpgradeSteps = ["Downloading update...", "Verifying hash...", "Running upgrade installer...", "Finalizing..."];
    public static readonly string[] UninstallSteps = ["Locating registry entries...", "Running uninstaller...", "Cleaning user data...", "Finalizing..."];

    public static void RunWithDispatcher(Action action)
    {
        App.DispatcherOverride = act => act();
        try { action(); }
        finally { App.DispatcherOverride = null; }
    }

    public static async Task RunWithDispatcherAsync(Func<Task> action)
    {
        App.DispatcherOverride = act => act();
        try { await action(); }
        finally { App.DispatcherOverride = null; }
    }

    public static async Task WaitWhileAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        int waited = 0;
        while (condition() && waited < timeoutMs)
        {
            await Task.Delay(50);
            waited += 50;
        }
    }

    public static void RunWithSetting<T>(Func<T> getter, Action<T> setter, T testValue, Action action)
    {
        var original = getter();
        try { setter(testValue); action(); }
        finally { setter(original); }
    }
}
