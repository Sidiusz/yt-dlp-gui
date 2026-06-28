using System;
using System.Threading;

namespace Grabsy;

/// <summary>Custom entry point (replaces XAML-generated Main) enforcing a
/// single running instance per user session before XAML initializes.</summary>
public static class Program
{
    private const string MutexName = "Local\\Grabsy.SingleInstance.v1";

    [STAThread]
    public static int Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is live; let it own the foreground and exit.
            return 0;
        }

        try
        {
            global::WinRT.ComWrappersSupport.InitializeComWrappers();
            global::Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
            return 0;
        }
        finally
        {
            try { mutex.ReleaseMutex(); } catch { /* exiting */ }
        }
    }
}
