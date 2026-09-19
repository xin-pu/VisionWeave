using Shouldly;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Validation;

public sealed class ValidationProjectionTests
{
    private readonly NodeDefinitionCatalog _catalog = TestNodes.DefaultCatalog();
    private readonly WorkflowValidator _validator;

    public ValidationProjectionTests()
    {
        _validator = new WorkflowValidator(_catalog);
    }

    [Fact]
    public void Project_tags_the_outcome_with_the_validated_revision()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));

        ValidationProjection projection = _validator.Project(document);

        projection.DocumentRevision.ShouldBe(document.Revision);
        projection.Matches(document.Revision).ShouldBeTrue();
        projection.Matches(document.Revision + 1).ShouldBeFalse();
    }

    [Fact]
    public void Project_of_a_connected_graph_reports_no_diagnostics()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        ValidationProjection projection = _validator.Project(document);

        projection.IsValid.ShouldBeTrue();
        projection.Diagnostics.ShouldBeEmpty();
        projection.DocumentDiagnostics.ShouldBeEmpty();
        projection.SeverityOf(blur.InstanceId).ShouldBeNull();
    }

    [Fact]
    public void SeverityOf_a_node_reports_its_highest_severity()
    {
        Guid node = Guid.NewGuid();

        ValidationProjection projection = new(
            new ValidationResult(
            [
                new NodeDiagnostic("VW-TEST-001", DiagnosticSeverity.Information, "context", node),
                new NodeDiagnostic("VW-TEST-002", DiagnosticSeverity.Warning, "attention", node),
                new NodeDiagnostic("VW-TEST-003", DiagnosticSeverity.Information, "more context", node),
            ]),
            3);

        projection.SeverityOf(node).ShouldBe(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DiagnosticsFor_keeps_an_unknown_node_empty_instead_of_failing()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 100);

        ValidationProjection projection = _validator.Project(document);

        projection.DiagnosticsFor(Guid.NewGuid()).ShouldBeEmpty();
        projection.SeverityOf(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public void DiagnosticsFor_returns_only_the_diagnostics_of_that_node()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 100);

        ValidationProjection projection = _validator.Project(document);

        projection.DiagnosticsFor(source.InstanceId).ShouldBeEmpty();
        NodeDiagnostic diagnostic = projection.DiagnosticsFor(blur.InstanceId).ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe(DiagnosticCodes.ParameterOutOfRange);
        diagnostic.NodeInstanceId.ShouldBe(blur.InstanceId);
        projection.SeverityOf(blur.InstanceId).ShouldBe(DiagnosticSeverity.Error);
        projection.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void DiagnosticsFor_preserves_the_validator_order()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 100);
        document.SetNodeParameter(blur.InstanceId, "notDeclared", 1);

        ValidationProjection projection = _validator.Project(document);

        projection.DiagnosticsFor(blur.InstanceId).Select(diagnostic => diagnostic.Code)
            .ShouldBe([DiagnosticCodes.ParameterOutOfRange, DiagnosticCodes.UnknownParameter]);
    }

    [Fact]
    public void Select_reports_the_selected_nodes_and_the_document_level_diagnostics()
    {
        Guid selected = Guid.NewGuid();
        Guid notSelected = Guid.NewGuid();

        ValidationProjection projection = new(
            new ValidationResult(
            [
                new NodeDiagnostic("VW-TEST-001", DiagnosticSeverity.Error, "document scope"),
                new NodeDiagnostic("VW-TEST-002", DiagnosticSeverity.Error, "selected node", selected),
                new NodeDiagnostic("VW-TEST-003", DiagnosticSeverity.Error, "another node", notSelected),
            ]),
            0);

        SelectionValidation selection = projection.Select([selected]);

        selection.Severity.ShouldBe(DiagnosticSeverity.Error);
        selection.IsValid.ShouldBeFalse();
        selection.Diagnostics.Select(diagnostic => diagnostic.Code)
            .ShouldBe(["VW-TEST-001", "VW-TEST-002"]);
    }

    [Fact]
    public void Select_of_a_clean_selection_in_a_clean_document_is_valid()
    {
        Guid node = Guid.NewGuid();

        ValidationProjection projection = new(
            new ValidationResult(
            [
                new NodeDiagnostic("VW-TEST-001", DiagnosticSeverity.Warning, "attention", node),
            ]),
            0);

        SelectionValidation selection = projection.Select([node]);

        selection.Severity.ShouldBe(DiagnosticSeverity.Warning);
        selection.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Select_does_not_depend_on_the_order_or_multiplicity_of_identifiers()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        ValidationProjection projection = new(
            new ValidationResult(
            [
                new NodeDiagnostic("VW-TEST-001", DiagnosticSeverity.Error, "first node", first),
                new NodeDiagnostic("VW-TEST-002", DiagnosticSeverity.Error, "second node", second),
            ]),
            0);

        IReadOnlyList<NodeDiagnostic> forward = projection.Select([first, second]).Diagnostics;
        IReadOnlyList<NodeDiagnostic> reversed = projection.Select([second, first]).Diagnostics;
        IReadOnlyList<NodeDiagnostic> repeated = projection.Select([first, first, second]).Diagnostics;

        reversed.ShouldBe(forward);
        repeated.ShouldBe(forward);
    }

    [Fact]
    public void Select_of_an_empty_selection_reports_the_document_level_diagnostics()
    {
        ValidationProjection projection = new(
            new ValidationResult(
            [
                new NodeDiagnostic("VW-TEST-001", DiagnosticSeverity.Error, "document scope"),
                new NodeDiagnostic("VW-TEST-002", DiagnosticSeverity.Error, "node scope", Guid.NewGuid()),
            ]),
            0);

        SelectionValidation selection = projection.Select([]);

        selection.Diagnostics.Select(diagnostic => diagnostic.Code).ShouldBe(["VW-TEST-001"]);
    }

    [Fact]
    public void Select_keeps_answering_from_a_stale_projection_instead_of_failing()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 100);

        ValidationProjection projection = _validator.Project(document);
        projection.SeverityOf(blur.InstanceId).ShouldBe(DiagnosticSeverity.Error);

        document.RemoveNode(blur.InstanceId);

        // A stale projection still describes the document it was computed from,
        // which is why the caller has to notice the mismatch and replace it.
        projection.Matches(document.Revision).ShouldBeFalse();
        projection.Select([blur.InstanceId]).Diagnostics.ShouldHaveSingleItem();
    }

    [Fact]
    public void Project_after_an_edit_forgets_a_removed_node()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 100);

        document.RemoveNode(blur.InstanceId);
        ValidationProjection projection = _validator.Project(document);

        projection.Matches(document.Revision).ShouldBeTrue();
        projection.SeverityOf(blur.InstanceId).ShouldBeNull();
        projection.Select([blur.InstanceId]).Diagnostics.ShouldBeEmpty();
    }
}
