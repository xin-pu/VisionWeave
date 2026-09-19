using Shouldly;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.ArchitectureTests.Dependencies;

public sealed class AssemblyDependencyTests
{
    [Fact]
    public void Contracts_references_only_framework_assemblies()
    {
        string[] forbiddenPrefixes = ["VisionWeave.", "Nodify", "OpenCvSharp", "Wpf.Ui"];

        string[] forbiddenReferences = typeof(NodeTypeId).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        forbiddenReferences.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_does_not_reference_ui_or_opencv_assemblies()
    {
        string[] forbiddenPrefixes = ["Nodify", "OpenCvSharp", "Presentation", "Wpf.Ui"];

        string[] forbiddenReferences = typeof(WorkflowDocument).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        forbiddenReferences.ShouldBeEmpty();
    }
}
