# ADR-0013 Execution timing ownership

- **Status:** Accepted
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

A run reports how long each node took and how long the whole run took. PL-2026-022
records that the two halves of that report came from different clocks. The
`WorkflowRunner` measures a node with the `TimeProvider` the run was given — the
same clock it measures the run with — but it preferred a non-zero
`NodeExecutionResult.Duration` an executor returned, and the four built-in OpenCV
executors filled that field from their own `Stopwatch`.

The behavior was safe, and it had two costs. One summary could combine a
deterministic injected clock with wall-clock numbers the run never observed, so a
node duration had no single meaning depending on which executor produced it. And a
built-in executor's duration could not be asserted exactly, because its
`Stopwatch` reads the machine, not the run.

PL-2026-010 introduced the seam that made this visible: once the run waits and
measures on an injected clock, the only timing a test can control is timing the
run took itself. The entry asked for one contract before duration-driven UX,
telemetry, or policy is built on the numbers, and named two ways: executors own
operation timing and the run treats their duration as authoritative, or the run
owns all reported timing and executors stop measuring. The maintainer chose the
second on 2026-09-19.

## Decision

1. **The run measures every node it executes, on the clock it was given.** The
   timestamp is taken immediately before the executor is called and the elapsed
   time immediately after it returns. That measurement is what the node's report
   and the run's summary carry. There is one clock per run and no second one
   behind it.

2. **`NodeExecutionResult` carries no duration.** An executor answers with a
   status, its output values, and the diagnostics it reported; timing is not part
   of its answer, so the field and the duration parameters of `Success`,
   `Failure`, and `Cancelled` are gone. An executor therefore cannot report a
   number the run did not observe, and a plugin has no second timing contract to
   satisfy — the same shape as ADR-0005's rule that ownership of a native handle
   is stated by handing the handle over rather than by describing it.

3. **The built-in executors stop measuring.** The `Stopwatch` in Image Source,
   Resize, Gaussian Blur, and Save Image is deleted rather than kept as a number
   nothing reads. What they measured around the native call is what the run
   measures around them, because the executor call is what the run is timing.

4. **A duration describes a completed execution.** A node that produced no result
   reports no duration: a cancelled node and one whose executor threw keep
   reporting `TimeSpan.Zero`, as they do today. Reporting the part of a node's
   time that ran before it was stopped would read as work the run never got, and
   the number that explains a slow or stopped run is the run's own duration plus
   the node's terminal state. This is the one part of the contract a later
   decision may revisit, and it is revisited only if telemetry asks for partial
   measurements.

5. **No clock reaches an executor.** With the run as the only measurer, an
   executor needs no time source at all, so no execution-context clock is added to
   `Contracts`, OpenCV stays independent of the clock the host composes, and an
   executor cannot become sensitive to which clock a test injected.

## Consequences

A node duration has one meaning: how long the run waited for that node, measured
on the run's clock. Two nodes of one summary are comparable even when one is a
plugin, and a run measured on a test clock reports durations a test can assert
exactly.

The run's measurement includes whatever the executor did around its own work —
queueing on the runtime's gate is outside it, since that happens before the call,
but a first-time native initialization inside the call is inside it. That number
is what a user watching a slow node wants to know, and it is not the same as the
time the native operation itself took. An executor that wants to know the latter
measures it locally for its own logs; the run does not report it.

A partial measurement is lost for a node that never returned a result: a node
cancelled after 30 seconds, or one that threw after 30 seconds, reports no
duration even though the run spent that time in it. The run's own duration still
carries it. If telemetry ever needs it, the fix is to report the elapsed time on
those paths too, which does not change who owns the clock.

## Alternatives considered

**Executors own operation timing and the run prefers their duration.** This is
what the code did, and it lets an executor report work the run cannot see — a
native call it timed more precisely than the runner can, or a plugin's own
accounting. It was rejected because two meanings in one summary outlive that
benefit: a summary becomes a mix of the run's clock and other clocks, no duration
is comparable across executors, and a test cannot assert a built-in executor's
duration at all. The case for it is speculative until a plugin needs to report a
measurement the run cannot make.

**Keeping the field but ignoring it in the run.** This keeps the contract
available for the plugin case at no cost today. It was rejected as a second
meaning waiting to be used: a field an executor fills and nothing reads is what
makes the ambiguity return, and removing it later would be the same breaking
change with a delayed cost.

**Giving executors the run's clock through an execution-context contract.** Only
an executor that must measure something the run cannot would need this, and the
chosen contract removes the reason to have it. Adding it now would couple the
OpenCV executors to a clock the host composes, which is the coupling the entry
asked to avoid.

## Standards impact

No deviation. The decision applies the repository's existing rule that one value
has one owner — stated for native handles in ADR-0005 and for the working
directory in ADR-0012 — to the timing a run reports, and it keeps the
deterministic-time seam of PL-2026-010 as the only clock in execution.
