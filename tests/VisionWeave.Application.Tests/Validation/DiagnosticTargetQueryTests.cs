using Shouldly;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Validation;

/// <summary>
/// Covers what the projection answers about a port, a parameter, and a
/// connection. This is the half a canvas or an inspector reads: it decides
/// whether a connector shows a failure of its own or the failure of the node
/// that owns it, and what it shows once the element it asked about is gone.
/// </summary>
public sealed class DiagnosticTargetQueryTests
{
    private readonly NodeDefinitionCatalog _catalog = TestNodes.DefaultCatalog();
    private readonly WorkflowValidator _validator;

    public DiagnosticTargetQueryTests()
    {
        _validator = new WorkflowValidator(_catalog);
    }

    [Fact]
    public void SeverityOfPort_does_not_inherit_a_parameter_out_of_range()
    {
        WorkflowDocument document = ConnectedBlur(out NodeInstance blur);
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 99);

        ValidationProjection projection = _validator.Project(document);

        projection.SeverityOfParameter(blur.InstanceId, "kernelSize").ShouldBe(DiagnosticSeverity.Error);

        // Both connectors of the node stay clean: a kernel size outside its range
        // says nothing about the image ports, which is the whole reason a port
        // needed a severity of its own.
        projection.SeverityOfPort(blur.InstanceId, "image").ShouldBeNull();
        projection.SeverityOfPort(blur.InstanceId, "blurred").ShouldBeNull();

        // The node still reports it, so nothing disappeared from the node's view.
        projection.SeverityOf(blur.InstanceId).ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void DiagnosticsForPort_reports_the_conditions_of_that_port_and_not_of_its_neighbours()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(200, 0));
        document.AddConnection(first.InstanceId, "image", masking.InstanceId, "mask");
        document.AddConnection(second.InstanceId, "image", masking.InstanceId, "mask");

        ValidationProjection projection = _validator.Project(document);

        // The mask port refuses the second wire; the image port has nothing
        // connected to it. Each condition belongs to the port that earned it.
        projection.DiagnosticsForPort(masking.InstanceId, "mask")
            .ShouldContain(diagnostic => diagnostic.Code == DiagnosticCodes.IncompatiblePort);
        projection.DiagnosticsForPort(masking.InstanceId, "mask")
            .ShouldNotContain(diagnostic => diagnostic.Code == DiagnosticCodes.InvalidGraph);

        projection.DiagnosticsForPort(masking.InstanceId, "image")
            .ShouldContain(diagnostic => diagnostic.Code == DiagnosticCodes.InvalidGraph);
        projection.DiagnosticsForPort(masking.InstanceId, "image")
            .ShouldNotContain(diagnostic => diagnostic.Code == DiagnosticCodes.IncompatiblePort);
    }

    [Fact]
    public void DiagnosticsForParameter_reports_the_conditions_of_that_parameter_and_not_of_another()
    {
        WorkflowDocument document = ConnectedBlur(out NodeInstance blur);
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 99);

        ValidationProjection projection = _validator.Project(document);

        projection.DiagnosticsForParameter(blur.InstanceId, "kernelSize")
            .ShouldContain(diagnostic => diagnostic.Code == DiagnosticCodes.ParameterOutOfRange);

        // A parameter the definition never declared is not in the projection, and
        // the node itself reports nothing, so there is nothing to show for it.
        projection.DiagnosticsForParameter(blur.InstanceId, "sigma").ShouldBeEmpty();
        projection.SeverityOfParameter(blur.InstanceId, "sigma").ShouldBeNull();
    }

    [Fact]
    public void DiagnosticsForPort_of_a_port_the_projection_never_saw_answers_with_the_nodes_own_conditions()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance absent = document.AddNode(TestNodes.AbsentType, 1, new CanvasPosition(0, 0));

        ValidationProjection projection = _validator.Project(document);

        // The definition is missing, so no port of this node was ever judged;
        // that is exactly why the port must not present itself as clean.
        projection.SeverityOfPort(absent.InstanceId, "image").ShouldBe(DiagnosticSeverity.Error);
        projection.DiagnosticsForPort(absent.InstanceId, "image")
            .ShouldContain(diagnostic => diagnostic.Code == DiagnosticCodes.MissingNodeDefinition);
    }

    [Fact]
    public void DiagnosticsForPort_of_an_unknown_node_reports_nothing()
    {
        WorkflowDocument document = ConnectedBlur(out _);

        ValidationProjection projection = _validator.Project(document);

        // A view that outlived the edit which removed its node shows nothing
        // rather than a badge that describes an element it can no longer name.
        projection.DiagnosticsForPort(Guid.NewGuid(), "image").ShouldBeEmpty();
        projection.SeverityOfPort(Guid.NewGuid(), "image").ShouldBeNull();
    }

    [Fact]
    public void DiagnosticsForConnection_reports_the_conditions_of_that_connection()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        WorkflowConnection connection =
            document.AddConnection(blur.InstanceId, "blurred", blur.InstanceId, "image");

        ValidationProjection projection = _validator.Project(document);

        projection.SeverityOfConnection(connection.ConnectionId).ShouldBe(DiagnosticSeverity.Error);
        projection.DiagnosticsForConnection(connection.ConnectionId)
            .ShouldContain(diagnostic => diagnostic.Code == DiagnosticCodes.InvalidGraph);

        projection.DiagnosticsForConnection(Guid.NewGuid()).ShouldBeEmpty();
        projection.SeverityOfConnection(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public void Project_reports_a_condition_that_names_no_node_as_a_document_diagnostic()
    {
        Guid connectionId = Guid.NewGuid();

        ValidationProjection projection = new(
            new ValidationResult(
            [
                new NodeDiagnostic(
                    DiagnosticCodes.InvalidGraph,
                    DiagnosticSeverity.Error,
                    "The connection names a node the document does not contain.",
                    null,
                    null,
                    new ConnectionTarget(connectionId)),
            ]),
            documentRevision: 1);

        // The condition has no node to belong to, so the document is the scope it
        // degrades to: a reader that only knows about document scope still sees
        // it, and a reader that knows the connection finds it there too.
        projection.DocumentDiagnostics.ShouldHaveSingleItem();
        projection.DiagnosticsForConnection(connectionId).ShouldHaveSingleItem();
        projection.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void DiagnosticsFor_target_kind_the_projection_does_not_index_keeps_the_condition_at_node_scope()
    {
        Guid node = Guid.NewGuid();

        ValidationProjection projection = new(
            new ValidationResult(
            [
                new NodeDiagnostic(
                    "VW-TEST-001",
                    DiagnosticSeverity.Error,
                    "A condition a later contract version attributed more narrowly.",
                    node,
                    null,
                    new UnrecognisedTarget()),
            ]),
            documentRevision: 1);

        // A target this build cannot index must not make its condition
        // unreachable: it stays where the node scope already reports it.
        projection.SeverityOf(node).ShouldBe(DiagnosticSeverity.Error);
        projection.DiagnosticsFor(node).ShouldHaveSingleItem();
        projection.DocumentDiagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void DiagnosticsForPort_an_unusable_identifier_keeps_the_condition_at_node_scope()
    {
        Guid node = Guid.NewGuid();

        ValidationProjection projection = new(
            new ValidationResult(
            [
                new NodeDiagnostic(
                    DiagnosticCodes.UnknownPort,
                    DiagnosticSeverity.Error,
                    "A port whose identifier cannot be used as a key.",
                    node,
                    null,
                    new PortTarget(node, string.Empty)),
            ]),
            documentRevision: 1);

        projection.SeverityOf(node).ShouldBe(DiagnosticSeverity.Error);

        // The condition reaches the node's ports because it could not be placed
        // on a port of its own; showing a node-level failure is the safe side.
        projection.SeverityOfPort(node, "image").ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Select_still_includes_a_port_condition_of_a_selected_node()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(200, 0));
        document.AddConnection(first.InstanceId, "image", masking.InstanceId, "mask");
        document.AddConnection(second.InstanceId, "image", masking.InstanceId, "mask");

        ValidationProjection projection = _validator.Project(document);

        // Narrowing the attribution must not narrow what a selection reports:
        // selecting the node still answers with everything about that node.
        projection.Select([masking.InstanceId]).Diagnostics
            .ShouldContain(diagnostic => diagnostic.Code == DiagnosticCodes.IncompatiblePort);
    }

    private static WorkflowDocument ConnectedBlur(out NodeInstance blur)
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        return document;
    }

    /// <summary>
    /// A target kind that stands in for one a later contract version adds, to
    /// prove an older projection reports it instead of losing it.
    /// </summary>
    private sealed record UnrecognisedTarget : DiagnosticTarget;
}
