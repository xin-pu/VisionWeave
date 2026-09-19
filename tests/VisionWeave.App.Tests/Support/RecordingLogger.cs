using Microsoft.Extensions.Logging;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Keeps every entry a boundary writes, so a test can assert how many times a
/// failure was logged rather than infer it from what was presented.
/// </summary>
/// <typeparam name="T">The category the logger is created for.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    /// <summary>Gets the entries that were written, in order.</summary>
    internal IReadOnlyList<LogEntry> Entries => _entries;

    /// <summary>Counts the entries written at one level.</summary>
    /// <param name="level">The level to count.</param>
    /// <returns>The number of entries at that level.</returns>
    internal int Count(LogLevel level) => _entries.Count(entry => entry.Level == level);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    /// <summary>One written entry.</summary>
    /// <param name="Level">The level it was written at.</param>
    /// <param name="Message">The rendered message.</param>
    /// <param name="Exception">The exception it carried, if any.</param>
    internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
