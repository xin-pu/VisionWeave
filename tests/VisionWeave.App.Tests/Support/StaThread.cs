using System.Runtime.ExceptionServices;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Runs one action on a single-threaded-apartment thread. Building a WPF element
/// installs the input manager for the thread it is built on, and that requires an
/// STA thread, so a test that needs a real control builds it here — the way the
/// shell builds it on its dispatcher thread — instead of on the runner's thread.
/// </summary>
internal static class StaThread
{
    /// <summary>Runs an action on an STA thread and returns what it produced.</summary>
    /// <typeparam name="T">The type the action produces.</typeparam>
    /// <param name="action">The action to run.</param>
    /// <returns>What the action produced.</returns>
    internal static T Run<T>(Func<T> action)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;

        System.Threading.Thread thread = new(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                // The failure is rethrown on the calling thread, so a broken test
                // reports what went wrong rather than only where it ran.
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();

        return result;
    }

    /// <summary>Runs an action on an STA thread.</summary>
    /// <param name="action">The action to run.</param>
    internal static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Run(() =>
        {
            action();
            return true;
        });
    }
}
