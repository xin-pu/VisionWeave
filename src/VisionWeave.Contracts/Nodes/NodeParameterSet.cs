namespace VisionWeave.Contracts.Nodes;

/// <summary>
/// Holds the validated parameter values of one node instance. Values are
/// deserialized and validated at the definition boundary before execution, so an
/// executor reads them without re-checking their shape.
/// </summary>
public sealed class NodeParameterSet
{
    private readonly Dictionary<string, object?> _values;

    /// <summary>
    /// Initializes a set from already validated values.
    /// </summary>
    /// <param name="values">The parameter values keyed by parameter name.</param>
    public NodeParameterSet(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets an empty set.
    /// </summary>
    public static NodeParameterSet Empty { get; } = new(new Dictionary<string, object?>(StringComparer.Ordinal));

    /// <summary>
    /// Gets the parameter names present in the set.
    /// </summary>
    public IReadOnlyCollection<string> Names => _values.Keys;

    /// <summary>
    /// Gets the raw value of a parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The stored value, or <see langword="null"/> when absent.</returns>
    public object? this[string name] => _values.GetValueOrDefault(name);

    /// <summary>
    /// Determines whether a parameter is present.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns><see langword="true"/> when the parameter is present.</returns>
    public bool Contains(string name) => _values.ContainsKey(name);

    /// <summary>
    /// Reads a numeric parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value.</returns>
    /// <exception cref="KeyNotFoundException">The parameter is absent.</exception>
    /// <exception cref="InvalidCastException">The parameter is not numeric.</exception>
    public double GetDouble(string name) => Convert.ToDouble(Require(name), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a whole-number parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value.</returns>
    /// <exception cref="KeyNotFoundException">The parameter is absent.</exception>
    /// <exception cref="InvalidCastException">The parameter is not numeric.</exception>
    public int GetInt32(string name) => Convert.ToInt32(Require(name), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a boolean parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value.</returns>
    /// <exception cref="KeyNotFoundException">The parameter is absent.</exception>
    /// <exception cref="InvalidCastException">The parameter is not boolean.</exception>
    public bool GetBoolean(string name)
        => Require(name) is bool value
            ? value
            : throw new InvalidCastException($"Parameter '{name}' is not a boolean.");

    /// <summary>
    /// Reads a text parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value.</returns>
    /// <exception cref="KeyNotFoundException">The parameter is absent.</exception>
    /// <exception cref="InvalidCastException">The parameter is not text.</exception>
    public string GetString(string name)
        => Require(name) as string ?? throw new InvalidCastException($"Parameter '{name}' is not text.");

    private object Require(string name)
        => _values.TryGetValue(name, out object? value) && value is not null
            ? value
            : throw new KeyNotFoundException($"Parameter '{name}' is not present.");
}
