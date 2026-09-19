namespace VisionWeave.Contracts.Diagnostics;

/// <summary>
/// A parameter of a node instance, named by the instance that owns it and the
/// parameter name. The name is the one the document saves the value under, which
/// is also the name a definition declares, so a diagnostic about a parameter the
/// definition does not declare is still attributable to what the document holds.
/// </summary>
/// <param name="NodeInstanceId">The node instance that owns the parameter.</param>
/// <param name="ParameterName">The parameter name.</param>
public sealed record ParameterTarget(Guid NodeInstanceId, string ParameterName) : DiagnosticTarget;
