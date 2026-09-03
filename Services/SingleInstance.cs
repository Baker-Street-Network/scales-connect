using System.Runtime.Versioning;

namespace BakerScaleConnect.Services
{
    /// <summary>
    /// Machine-wide single-instance guard.
    ///
    /// This app binds a fixed port (http://localhost:5000) and takes exclusive
    /// handles on the scanner and serial devices, so exactly one copy may run per
    /// machine. Three separate things launch it — the HKCU Run key written by
    /// <c>Program.AddToStartup</c>, the BakerStreetWatchdog service, and Velopack's
    /// <c>ApplyUpdatesAndRestart</c> — and none of them coordinate with the others,
    /// so the guard has to live here rather than in any one launcher.
    ///
    /// The mutex is deliberately never released. Windows abandons it when the owning
    /// process dies, and <see cref="TryAcquire"/> treats an abandoned mutex as a
    /// successful acquisition, so a crash can never wedge the machine.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class SingleInstance
    {
        private const string MutexName = @"Global\BakerScaleConnect.SingleInstance";
        private const string ShowWindowEventName = @"Global\BakerScaleConnect.ShowWindow";

        // Velopack restarts the app by launching the new copy while the old one is
        // still tearing down, so wait out the handoff before deciding we're a duplicate.
        private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(10);

        private static Mutex? _mutex;

        /// <summary>
        /// True when this process owns the single-instance mutex and is therefore
        /// the one copy allowed to bind the port, drive the devices, and self-update.
        /// </summary>
        public static bool IsOwner { get; private set; }

        /// <summary>
        /// Attempts to become the single running instance. Returns false when another
        /// copy already holds the mutex, in which case the caller should exit.
        /// </summary>
        public static bool TryAcquire()
        {
            try
            {
                _mutex = new Mutex(initiallyOwned: false, MutexName);
            }
            catch (UnauthorizedAccessException)
            {
                // The mutex exists but was created under another user account, so we
                // can't even open it. Another copy is running — that's all we need.
                SignalExistingInstance();
                return false;
            }

            // Fast path: nothing else is running.
            if (TryTakeOwnership(TimeSpan.Zero))
                return true;

            // Something holds it. Ask that copy to surface *now*, so a user who
            // double-clicked the shortcut sees the window immediately rather than
            // after the handoff wait below.
            SignalExistingInstance();

            // The holder may be a predecessor that Velopack is still tearing down,
            // so give the handoff a chance before concluding we're a duplicate.
            if (TryTakeOwnership(AcquireTimeout))
                return true;

            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        private static bool TryTakeOwnership(TimeSpan timeout)
        {
            try
            {
                IsOwner = _mutex!.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                // The previous owner died without releasing it. Ownership is ours.
                IsOwner = true;
            }

            return IsOwner;
        }

        /// <summary>
        /// Asks the copy that is already running to bring its window to the front, so
        /// that a user who double-clicks the shortcut sees the app rather than nothing
        /// happening. Silently does nothing when there is no one listening.
        /// </summary>
        private static void SignalExistingInstance()
        {
            try
            {
                using var showWindow = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showWindow.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The running copy hasn't created the event yet (or is mid-shutdown).
                // Nothing to do — we're exiting either way.
            }
            catch (UnauthorizedAccessException)
            {
                // Running under a different user account. Same story.
            }
        }

        /// <summary>
        /// Starts listening for <see cref="SignalExistingInstance"/> from duplicate
        /// launches. <paramref name="onShowRequested"/> is raised on a background
        /// thread, so the handler is responsible for marshalling to the UI thread.
        /// </summary>
        public static void ListenForShowRequests(Action onShowRequested)
        {
            if (!IsOwner) return;

            EventWaitHandle showWindow;
            try
            {
                showWindow = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
            }
            catch (UnauthorizedAccessException)
            {
                // Not fatal — we just lose "a second launch re-opens the window".
                return;
            }

            var listener = new Thread(() =>
            {
                using (showWindow)
                {
                    while (true)
                    {
                        try
                        {
                            showWindow.WaitOne();
                            onShowRequested();
                        }
                        catch
                        {
                            return; // Handle closed during shutdown.
                        }
                    }
                }
            })
            {
                IsBackground = true,
                Name = "SingleInstance.ShowWindowListener"
            };

            listener.Start();
        }
    }
}
