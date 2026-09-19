namespace VisionWeave.Contracts.Diagnostics;

/// <summary>
/// The closed hierarchy of document elements a diagnostic can be attributed to.
/// A target names the element by the stable identifiers the document already
/// carries, so a presentation layer can look that element up without reading the
/// diagnostic message, and it holds no presentation type of its own.
/// </summary>
/// <remarks>
/// A diagnostic that names no target describes the document as a whole. A
/// diagnostic that names one still carries
/// <see cref="NodeDiagnostic.NodeInstanceId"/> when the element belongs to a node,
/// so a consumer that groups by node keeps working, and a target this build does
/// not know leaves the diagnostic at that broader scope rather than dropping it.
/// </remarks>
public abstract record DiagnosticTarget;
