namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// A clock the test moves by hand, so a coalescing window can be crossed, a
/// cancellation grace period can expire, and a run's duration can be read without
/// waiting for real time to pass. Timers it is asked for fire when the test
/// advances the clock past the moment they were armed for, and never on their own.
/// </summary>
/// <param name="start">The instant the clock starts at.</param>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<TestTimer> _timers = [];
    private TaskCompletionSource _armed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _ticks;

    public DateTimeOffset UtcNow { get; private set; } = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return UtcNow;
        }
    }

    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _ticks;
        }
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>
    /// Gets the number of timers that are armed and have neither fired nor been
    /// disposed, which is how a test asserts that a wait the runtime took on was
    /// taken off again.
    /// </summary>
    internal int PendingTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => timer.IsArmed);
            }
        }
    }

    /// <summary>
    /// Gets a task that completes the next time the runtime arms a timer, so a
    /// test can move the clock knowing the wait exists rather than guessing that
    /// it does.
    /// </summary>
    internal Task NextTimer
    {
        get
        {
            lock (_gate)
            {
                return _armed.Task;
            }
        }
    }

    /// <summary>
    /// Moves the clock forward and fires every timer that falls due on the way.
    /// </summary>
    /// <param name="delta">How far to move.</param>
    public void Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "A clock cannot be moved backwards.");
        }

        long target;

        lock (_gate)
        {
            target = _ticks + delta.Ticks;
        }

        while (true)
        {
            TestTimer due;

            lock (_gate)
            {
                TestTimer? next = _timers
                    .Where(timer => timer.IsArmed && timer.DueTicks <= target)
                    .MinBy(timer => timer.DueTicks);

                if (next is null)
                {
                    Move(target);
                    return;
                }

                Move(next.DueTicks);
                due = next.Fire();
            }

            due.Callback(due.State);
        }
    }

    /// <summary>
    /// Moves the instant and the timestamp together, so a run that reads either
    /// sees the same moment pass.
    /// </summary>
    /// <param name="ticks">The timestamp to move to.</param>
    private void Move(long ticks)
    {
        UtcNow = UtcNow.AddTicks(ticks - _ticks);
        _ticks = ticks;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);

        TestTimer timer = new(this, callback, state);

        lock (_gate)
        {
            _timers.Add(timer);
            timer.Change(dueTime, period);
            _armed.TrySetResult();
            _armed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        return timer;
    }

    private sealed class TestTimer(TestClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long _dueTicks = long.MaxValue;
        private TimeSpan _period = Timeout.InfiniteTimeSpan;
        private bool _disposed;

        internal TimerCallback Callback => callback;

        internal object? State => state;

        internal bool IsArmed => !_disposed && _dueTicks != long.MaxValue;

        internal long DueTicks => _dueTicks;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                if (_disposed)
                {
                    return false;
                }

                _period = period;
                _dueTicks = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : clock._ticks + Math.Max(0, dueTime.Ticks);
            }

            return true;
        }

        public void Dispose()
        {
            lock (clock._gate)
            {
                _disposed = true;
                _dueTicks = long.MaxValue;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Takes the timer off the clock and returns it when its callback is due to
        /// run, so the caller can invoke it once the clock's lock is released.
        /// </summary>
        /// <returns>This timer.</returns>
        internal TestTimer Fire()
        {
            if (_period == Timeout.InfiniteTimeSpan || _period <= TimeSpan.Zero)
            {
                _dueTicks = long.MaxValue;
                return this;
            }

            _dueTicks += _period.Ticks;
            return this;
        }
    }
}
