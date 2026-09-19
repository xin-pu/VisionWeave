# ADR-0008 Async command and error boundary

- **Status:** Accepted
- **Date:** 2026-09-19
- **Decision owners:** VisionWeave maintainers

## Context

Every editor gesture becomes an application command, and most of those commands
do work that can fail for a reason the user can act on, fail for a reason nobody
anticipated, or be cancelled halfway. ADR-0006 requires that a command reports
its outcome as diagnostics rather than throwing into the binding layer, and
ADR-0007 records the asynchronous command and error boundary of the shell as the
piece it left unbuilt. Without one boundary, each new canvas, file, preview, or
run command would decide for itself what to catch, what to log, what to show, and
which failures to treat as cancellation — and an exception that escaped one of
them would surface as an unobserved fault on whichever thread finished the work.

## Decision

1. **The MVVM Toolkit owns command state.** `CommunityToolkit.Mvvm` — the MVVM
   package ADR-0002 approved — supplies `AsyncRelayCommand`, which owns
   `IsRunning`, `CanExecute`, the token source behind `Cancel()`, and the
   notifications that report them. Concurrent executions stay disabled, so a
   command that is still running is not available to the shell, and a view model
   exposes its state with `[ObservableProperty]` rather than a hand-written
   setter. Nothing in this repository re-implements an `ICommand` state machine.

2. **One boundary runs the work.** `AsyncCommandBoundary.RunAsync` in
   `VisionWeave.App.Commands` is the only path from a command to the thing it
   does. It returns a `CommandExecutionResult` and its task never faults, which
   is what keeps a failure off the UI thread: `AsyncRelayCommand` rethrows a
   faulted execution on the context that started it, so a fault that escaped the
   boundary would become an unhandled exception rather than a diagnostic.

3. **Expected failures belong to the command, not to the boundary.** The
   boundary does not classify domain errors: an operation reports what it
   observed as diagnostics and the boundary passes them through. A file that
   cannot be opened is therefore mapped to the stable `VW-FILE-001` diagnostic by
   `DocumentLoader`, which knows that this is an expected outcome of asking for a
   file, and not by shared infrastructure that would have to learn what every
   command considers normal.

4. **Unexpected failures are handled in one place, once.** An exception the
   operation did not anticipate becomes `VW-UI-001` with a message safe to show,
   is logged exactly once with the original exception as context, and is handed
   to the presenter as one diagnostic. The diagnostic keeps the exception for the
   log and carries a message that never contains an exception message, a path, or
   a payload.

5. **Cancellation is an outcome, not a failure.** Cancellation is recognised by
   filtering on the boundary's own token, the way `WorkflowRunner` already does,
   so an `OperationCanceledException` raised for some unrelated token is still
   treated as unexpected. A cancelled command produces no diagnostic, no message,
   and no error-level log record, and it does not go on to raise its result: a
   stopped document open cannot replace the document the shell is editing.

6. **One presenter shows user-facing messages.** `IUserNotificationPresenter`
   takes a diagnostic rather than an exception, so a caller cannot hand the UI a
   message built from internal state. The WPF implementation shows the stable
   code and the safe message only, and marshals to the dispatcher itself. Only
   the first error-severity diagnostic is presented; warnings and information
   travel in the result for the status area and the diagnostics panel of the
   later shell packages.

7. **Threading is a documented property of an operation, not hand-rolled
   marshalling.** The toolkit raises its state notifications on the thread that
   completes the operation, so an operation's final await returns to the context
   that invoked it — it does not use `ConfigureAwait(false)`. Blocking work runs
   inside `Task.Run(..., cancellationToken)`, which is where `ConfigureAwait(false)`
   belongs.

8. **A command's work is not started twice.** The toolkit's `CanExecute` reports
   a running command as unavailable, which is what the shell binds. A programmatic
   second `ExecuteAsync` supersedes the first by cancelling it, which is the
   toolkit's own semantics and is accepted rather than re-implemented.

9. **Startup keeps its own message box.** A rejected settings file is reported
   before the container exists, as ADR-0007 decision 4 records, so it cannot use
   the presenter. That remains the single documented exception.

## Consequences

The App layer gains one small vocabulary — a completion, a result, a boundary, a
presenter, and one diagnostic code — and every later command reuses it instead of
deciding its own failure policy. The boundary holds no WPF or Nodify type, so all
of its behaviour is covered headlessly in `VisionWeave.App.Tests`, which needs no
window, no dispatcher, and no STA thread. Three costs are accepted and recorded
rather than hidden: cancellation of a synchronous file read cannot interrupt the
read itself, only stop the command from reporting its result, so a cancelled open
still finishes its I/O; the diagnostics panel, the status area, and the snackbar
presenter that will replace the message box belong to later packages, so a
warning is currently visible only in the result a caller holds; and the run
boundary fields of ADR-0007 — `OperationId`, `WorkflowId`, `NodeId`,
`NodeTypeId`, and elapsed milliseconds — are still unbuilt on this boundary.

## Alternatives considered

A hand-written `ICommand` implementation was rejected: the approved MVVM package
already provides the running state, the availability predicate, the linked token
source, and the completion notifications, so a hand-written copy would duplicate
tested behaviour and drift from it. Letting `AsyncRelayCommand` handle a failure
through its own exception flow was rejected because it either rethrows on the
captured context or routes the fault to the task scheduler, and neither produces
a user-facing message with one log record. Classifying expected failures inside
the boundary was rejected because it would move per-command knowledge into shared
infrastructure and duplicate the diagnostics that Persistence already produces.
Reporting cancellation as a warning diagnostic was rejected because a user who
stopped an operation did not experience a failure. Queuing a second execution
instead of reporting the command as unavailable was rejected because the shell
has no queue policy and finishing a superseded open would be surprising.

## Standards impact

Implements the observability rules linked from
[the standards reference](../standards-reference.md): one boundary that logs a
defect once with the original exception as context, structured named log fields
rather than interpolated prose, and a user-facing message that carries no unsafe
payload. It also implements the directed-dependency rule by keeping the boundary
free of WPF types and the presenter free of domain work. No deviation is
required.
