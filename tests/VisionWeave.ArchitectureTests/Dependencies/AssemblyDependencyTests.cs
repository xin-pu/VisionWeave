using System.Reflection;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Frames;
using VisionWeave.Persistence.Composition;
using VisionWeave.PluginSdk.Plugins;

namespace VisionWeave.ArchitectureTests.Dependencies;

/// <summary>
/// Enforces the dependency direction of ADR-0001 across every project of the
/// solution, so a reference that would couple a stable layer to an unstable one
/// fails the build instead of surviving review.
/// </summary>
public sealed class AssemblyDependencyTests
{
    [Fact]
    public void Contracts_references_only_framework_assemblies()
    {
        AssertNoForbiddenReference(
            typeof(NodeTypeId).Assembly,
            forbiddenPrefixes: ["VisionWeave.", "Nodify", "OpenCvSharp", "Wpf.Ui"]);
    }

    [Fact]
    public void Domain_references_only_contracts()
    {
        AssertVisionWeaveReferences(typeof(WorkflowDocument).Assembly, "VisionWeave.Contracts");
    }

    [Fact]
    public void Application_references_only_domain_and_contracts()
    {
        AssertVisionWeaveReferences(
            typeof(WorkflowRunner).Assembly,
            "VisionWeave.Contracts",
            "VisionWeave.Domain");
    }

    [Fact]
    public void OpenCv_references_only_contracts()
    {
        AssertVisionWeaveReferences(typeof(MatFrameLease).Assembly, "VisionWeave.Contracts");
    }

    [Fact]
    public void PluginSdk_references_only_contracts()
    {
        AssertVisionWeaveReferences(typeof(IVisionWeavePlugin).Assembly, "VisionWeave.Contracts");
    }

    [Fact]
    public void Persistence_references_only_domain_and_contracts()
    {
        AssertVisionWeaveReferences(
            typeof(PersistenceModule).Assembly,
            "VisionWeave.Contracts",
            "VisionWeave.Domain");
    }

    [Fact]
    public void No_project_references_a_test_assembly()
    {
        Assembly[] projects =
        [
            typeof(NodeTypeId).Assembly,
            typeof(WorkflowDocument).Assembly,
            typeof(WorkflowRunner).Assembly,
            typeof(MatFrameLease).Assembly,
            typeof(IVisionWeavePlugin).Assembly,
            typeof(PersistenceModule).Assembly,
        ];

        foreach (Assembly project in projects)
        {
            AssertNoForbiddenReference(project, forbiddenPrefixes: ["VisionWeave.ArchitectureTests", "VisionWeave.Application.Tests", "VisionWeave.Domain.Tests", "VisionWeave.IntegrationTests", "VisionWeave.Persistence.Tests"]);
        }
    }

    [Fact]
    public void Domain_does_not_reference_ui_or_opencv_assemblies()
    {
        AssertNoForbiddenReference(
            typeof(WorkflowDocument).Assembly,
            forbiddenPrefixes: ["Nodify", "OpenCvSharp", "Presentation", "Wpf.Ui"]);
    }

    private static void AssertVisionWeaveReferences(Assembly assembly, params string[] allowed)
    {
        string[] references = VisionWeaveReferences(assembly);

        foreach (string reference in references)
        {
            allowed.ShouldContain(
                reference,
                $"'{assembly.GetName().Name}' must not reference '{reference}'. Allowed: {string.Join(", ", allowed)}.");
        }
    }

    private static void AssertNoForbiddenReference(Assembly assembly, string[] forbiddenPrefixes)
    {
        string[] forbidden = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        forbidden.ShouldBeEmpty(
            $"'{assembly.GetName().Name}' references {string.Join(", ", forbidden)}.");
    }

    private static string[] VisionWeaveReferences(Assembly assembly)
        => [.. assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("VisionWeave.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
}
