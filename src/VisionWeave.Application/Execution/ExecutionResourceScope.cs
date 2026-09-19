using VisionWeave.Contracts.Execution;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Owns the private resources one executor creates. Disposal happens whatever
/// the node's outcome, and a resource that fails to release is recorded instead
/// of replacing the node's own result, so a successful node never turns into a
/// failed one because of a cleanup defect.
/// </summary>
public sealed class ExecutionResourceScope : IExecutionResourceScope
{
    private readonly List<IDisposable> _owned = [];
    private readonly List<Exception> _failures = [];
    private bool _completed;

    /// <summary>
    /// Gets a value indicating whether the scope has been disposed and no longer
    /// accepts resources.
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Gets the failures observed while releasing the owned resources.
    /// </summary>
    public IReadOnlyList<Exception> DisposalFailures => _failures;

    /// <inheritdoc />
    public void Own(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (_completed)
        {
            throw new InvalidOperationException(
                "The execution resource scope has completed and no longer owns resources.");
        }

        _owned.Add(resource);
    }

    /// <summary>
    /// Releases every owned resource, most recently added first.
    /// </summary>
    public void Dispose()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;

        for (int index = _owned.Count - 1; index >= 0; index--)
        {
            try
            {
                _owned[index].Dispose();
            }
            catch (Exception exception)
            {
                _failures.Add(exception);
            }
        }

        _owned.Clear();
    }
}
