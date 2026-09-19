namespace VisionWeave.Contracts.Workflows;

/// <summary>
/// One stable reference a document declares that a node depends on. A reference is
/// never a machine fingerprint: the document records what the workflow depends on
/// so it stays portable between machines, and the runtime computes the actual
/// fingerprint when it builds a snapshot (ADR-0004 decision 7).
/// </summary>
public abstract record ResourceReference;
