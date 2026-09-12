namespace WingetStore.Tests;

public static class WinUIApp
{
    private static Thread? _uiThread;
    private static DispatcherQueue? _dispatcher;
    private static readonly ManualResetEventSlim _ready = new();

    public static void EnsureStarted()
    {
        if (_dispatcher != null) return;

        _uiThread = new Thread(() =>
        {
            Application.Start((args) =>
            {
                _dispatcher = DispatcherQueue.GetForCurrentThread();
                _ready.Set();
            });
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Name = "WinUI";
        _uiThread.IsBackground = true;
        _uiThread.Start();
        if (!_ready.Wait(30000)) throw new TimeoutException("WinUI Application.Start failed to initialize");
    }

    public static void Run(Action action)
    {
        EnsureStarted();
        Exception? captured = null;
        var done = new ManualResetEventSlim();
        if (_dispatcher == null || !_dispatcher.TryEnqueue(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
            finally { done.Set(); }
        }))
        {
            throw new InvalidOperationException("Failed to dispatch work to WinUI thread");
        }
        if (!done.Wait(60000)) throw new TimeoutException("WinUI operation timed out");
        if (captured != null) throw captured;
    }

    public static T Run<T>(Func<T> func)
    {
        T? result = default;
        Run(() => { result = func(); });
        return result!;
    }
}
