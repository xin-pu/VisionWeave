using Shouldly;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Validation;

public sealed class WorkflowValidatorTests
{
    private readonly NodeDefinitionCatalog _catalog = TestNodes.DefaultCatalog();
    private readonly WorkflowValidator _validator;

    public WorkflowValidatorTests()
    {
        _validator = new WorkflowValidator(_catalog);
    }

    [Fact]
    public void Validate_connected_source_and_filter_report_no_diagnostics()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_unknown_node_type_reports_missing_definition()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance node = document.AddNode(TestNodes.AbsentType, 1, new CanvasPosition(0, 0));

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.MissingNodeDefinition);
        result.Errors[0].NodeInstanceId.ShouldBe(node.InstanceId);
    }

    [Fact]
    public void Validate_unsupported_saved_version_reports_version_diagnostic()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        document.SetNodeTypeVersion(blur.InstanceId, 7);

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnsupportedNodeVersion);
    }

    [Fact]
    public void Validate_unknown_source_port_reports_unknown_port()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "renamed", blur.InstanceId, "image");

        ValidationResult result = _validator.Validate(document);

        result.HasCode(DiagnosticCodes.UnknownPort).ShouldBeTrue();
        result.Errors.ShouldContain(diagnostic => diagnostic.NodeInstanceId == source.InstanceId);
    }

    [Fact]
    public void Validate_mismatched_port_types_report_incompatible_port()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance count = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(200, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(400, 0));
        document.AddConnection(source.InstanceId, "image", count.InstanceId, "image");
        document.AddConnection(count.InstanceId, "count", blur.InstanceId, "image");

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeFalse();
        result.HasCode(DiagnosticCodes.IncompatiblePort).ShouldBeTrue();
        result.Errors.ShouldContain(diagnostic => diagnostic.NodeInstanceId == blur.InstanceId);
    }

    [Fact]
    public void Validate_output_port_used_as_target_reports_incompatible_port()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(200, 0));
        document.AddConnection(first.InstanceId, "image", second.InstanceId, "image");

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.IncompatiblePort);
        result.Errors[0].NodeInstanceId.ShouldBe(second.InstanceId);
    }

    [Fact]
    public void Validate_two_connections_to_single_port_report_incompatible_port()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance image = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance firstMask = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance secondMask = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 200));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(300, 0));
        document.AddConnection(image.InstanceId, "image", masking.InstanceId, "image");
        document.AddConnection(firstMask.InstanceId, "image", masking.InstanceId, "mask");
        document.AddConnection(secondMask.InstanceId, "image", masking.InstanceId, "mask");

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.IncompatiblePort);
        result.Errors[0].NodeInstanceId.ShouldBe(masking.InstanceId);
    }

    [Fact]
    public void Validate_two_connections_to_many_port_report_nothing()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance merge = document.AddNode(TestNodes.MergeType, 1, new CanvasPosition(300, 0));
        document.AddConnection(first.InstanceId, "image", merge.InstanceId, "frames");
        document.AddConnection(second.InstanceId, "image", merge.InstanceId, "frames");

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_cycle_reports_invalid_graph()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance first = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(200, 0));
        NodeInstance second = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(400, 0));
        document.AddConnection(source.InstanceId, "image", first.InstanceId, "image");
        document.AddConnection(first.InstanceId, "masked", second.InstanceId, "image");
        document.AddConnection(second.InstanceId, "masked", first.InstanceId, "mask");

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeFalse();
        result.HasCode(DiagnosticCodes.InvalidGraph).ShouldBeTrue();
        result.Errors.ShouldAllBe(diagnostic => diagnostic.Code == DiagnosticCodes.InvalidGraph);
    }

    [Fact]
    public void Validate_self_connection_reports_invalid_graph()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        document.AddConnection(blur.InstanceId, "blurred", blur.InstanceId, "image");

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidGraph);
        result.Errors[0].NodeInstanceId.ShouldBe(blur.InstanceId);
    }

    [Fact]
    public void Validate_unbound_required_input_reports_invalid_graph()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidGraph);
        result.Errors[0].NodeInstanceId.ShouldBe(blur.InstanceId);
    }

    [Fact]
    public void Validate_unbound_optional_input_reports_nothing()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", masking.InstanceId, "image");

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_disabled_node_without_inputs_reports_nothing()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        document.SetNodeEnabled(blur.InstanceId, false);

        ValidationResult result = _validator.Validate(document);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_same_document_twice_reports_the_same_sequence()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        NodeInstance merge = document.AddNode(TestNodes.MergeType, 1, new CanvasPosition(400, 0));
        document.AddConnection(first.InstanceId, "missing", second.InstanceId, "image");
        document.AddConnection(second.InstanceId, "image", blur.InstanceId, "image");
        document.AddConnection(second.InstanceId, "image", merge.InstanceId, "unknown");

        ValidationResult firstPass = _validator.Validate(document);
        ValidationResult secondPass = _validator.Validate(document);

        firstPass.Diagnostics.Count.ShouldBeGreaterThan(1);
        secondPass.Diagnostics.ShouldBe(firstPass.Diagnostics);
    }
}
