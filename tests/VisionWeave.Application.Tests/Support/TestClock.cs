namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// A clock the test moves by hand, so a coalescing window can be crossed without
/// waiting for real time to pass.
/// </summary>
/// <param name="start">The instant the clock starts at.</param>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan delta) => UtcNow += delta;
}
