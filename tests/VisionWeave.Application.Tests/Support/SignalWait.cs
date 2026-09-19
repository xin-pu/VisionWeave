using Shouldly;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// Awaits a signal a test is waiting for. A signal that never arrives is a defect
/// in the test or in the runtime it covers, and either way the test that waits for
/// it fails with what it was waiting for instead of leaving the suite to hang until
/// the machine running it gives up.
/// </summary>
internal static class SignalWait
{
    /// <summary>
    /// How long a signal is given. A signal a running runtime produces arrives in
    /// milliseconds, so this is a diagnostic for one that never arrives rather than
    /// the moment a test waits for.
    /// </summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Awaits a task a test treats as a signal.
    /// </summary>
    /// <param name="signal">The task that completes when the signal arrives.</param>
    /// <param name="because">What the test is waiting for, named in the failure.</param>
    /// <param name="limit">How long to give it, which a test shortens to reach the failure it asserts.</param>
    internal static async Task Within(this Task signal, string because, TimeSpan? limit = null)
    {
        ArgumentNullException.ThrowIfNull(signal);

        TimeSpan waited = limit ?? Limit;
        bool arrived = true;

        try
        {
            await signal.WaitAsync(waited).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            arrived = false;
        }

        arrived.ShouldBeTrue($"waited {waited.TotalSeconds:0.#}s for {because}");
    }
}
