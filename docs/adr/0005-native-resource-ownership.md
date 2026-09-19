# ADR-0005 Native image ownership and lease lifetime

- **Status:** Proposed
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

OpenCV image buffers are unmanaged. The detailed design requires that no node
disposes an input, that a preview never retains a full-size native image, that
cancellation and executor failures release everything they reserved, and that
each scenario can be proven by a test that asserts outstanding native leases
return to zero. The Aries codebase shows the failure modes this avoids: ROI
blocks returned shared buffers, and blocks wrote output files during execution
without an ownership contract.

## Decision

1. **A port never exposes a mutable native buffer.** An image port carries
   `ImageFrameLease` from [ADR-0003](0003-port-value-types.md), which offers
   read-only access through the OpenCV project's internal surface. The producer
   owns the native resource; consumers never dispose it.

2. **Reservations precede publication.** Before a producer output is published
   to its consumers, the scheduler creates one consumer reservation per
   scheduled downstream input. A lease is released only when the producer
   releases its own ownership, every reservation has been released, every cache
   entry that holds the lease has been evicted, and any preview conversion fence
   registered against it has completed.

3. **Every path releases through one cleanup routine.** Normal completion,
   validation failure, cancellation, blocked branches, and executor exceptions
   all funnel into the same release path. A lease that is still outstanding when
   a run ends is a defect and is reported as a lease-leak diagnostic with the
   node and operation identifiers.

4. **Executors declare their private resources.** `NodeExecutionRequest`
   provides an execution-scoped resource scope. Any temporary buffer an executor
   creates — including the result of `CloneWritable()` — is registered in that
   scope and is disposed by the runtime when the node finishes, fails, or is
   cancelled. An executor transfers ownership only of the values it returns.

5. **Preview conversion is fenced and downscaled.** Preview conversion registers
   a completion fence before the source lease can be released, runs off the UI
   thread, and produces a managed, downscaled bitmap bounded by a configured
   pixel area. Full-resolution inspection performs its own fenced conversion and
   does not hold a native lease beyond it.

6. **Cache entries own independent leases with an explicit budget.** A cached
   output holds its own lease, is evicted only by the cache budget policy, and is
   never evicted by canvas activity. An output that alone exceeds the budget is
   not cached and is reported as a diagnostic rather than failing the run.

7. **Cancellation has a grace period and a quarantine rule.** After cancellation
   is signalled, the runtime waits a configured grace period for started work.
   An executor that does not return within it is recorded in the run summary
   with a dedicated diagnostic, its leases are reported as leaked, and its node
   type or plugin is quarantined for the session so the application can exit
   without waiting for it. Quarantine never masks the leak: the run summary
   records both.

8. **OpenCV internal threading is configured explicitly.** The scheduler's
   parallelism and OpenCV's internal worker count are set together so that
   parallel node execution does not oversubscribe the machine. Both are typed
   options with validated ranges.

9. **Lease accounting is observable and testable.** The runtime maintains a lease
   ledger (created, reserved, released, outstanding) that feeds both the
   diagnostics above and the tests required by the detailed design: fan-out,
   cancellation during preview conversion, cache eviction, and a throwing
   executor, each asserting zero outstanding leases at the end of the scenario.

## Consequences

Node executors gain a small obligation (register private resources, never
dispose inputs) in exchange for a lifetime rule that can be proven by tests. The
scheduler owns more bookkeeping, and the ledger is the single evidence source
for native leaks. Quarantining an executor trades a possible permanent leak in a
broken plugin for an application that remains closable, and the trade is always
visible in the run summary.

## Alternatives considered

Copying every image at each edge was rejected because it multiplies peak memory
for fan-out graphs. Letting executors dispose inputs was rejected because
fan-out makes the first consumer destroy the value for the others. Reference
counting without a ledger was rejected because the required tests could not then
observe the invariant.

## Standards impact

Implements the directed-dependency rule and the test-tier requirements linked
from [the standards reference](../standards-reference.md). No deviation is
required.
