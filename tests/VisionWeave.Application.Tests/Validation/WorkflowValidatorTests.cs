using Shouldly;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
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
    public void Validate_parameter_of_the_wrong_kind_reports_vw_param_001()
    {
        ValidationResult result = ValidateProbe(
            Probe(),
            (document, probe) => document.SetNodeParameter(probe.InstanceId, "count", "five"));

        result.IsValid.ShouldBeFalse();
        result.HasCode(DiagnosticCodes.InvalidParameterValue).ShouldBeTrue();
    }

    [Fact]
    public void Validate_fractional_value_for_an_integer_parameter_reports_vw_param_001()
    {
        ValidationResult result = ValidateProbe(
            Probe(),
            (document, probe) => document.SetNodeParameter(probe.InstanceId, "count", 2.5));

        result.HasCode(DiagnosticCodes.InvalidParameterValue).ShouldBeTrue();
        result.HasCode(DiagnosticCodes.ParameterOutOfRange).ShouldBeFalse();
    }

    [Fact]
    public void Validate_parameter_outside_the_declared_range_reports_vw_param_002()
    {
        ValidationResult result = ValidateProbe(
            Probe(),
            (document, probe) => document.SetNodeParameter(probe.InstanceId, "sigma", 250d));

        result.HasCode(DiagnosticCodes.ParameterOutOfRange).ShouldBeTrue();
        result.Diagnostics.Single(item => item.Code == DiagnosticCodes.ParameterOutOfRange)
            .Message.ShouldContain("[0, 100]");
    }

    [Fact]
    public void Validate_option_outside_the_declared_set_reports_vw_param_003()
    {
        ValidationResult result = ValidateProbe(
            Probe(),
            (document, probe) => document.SetNodeParameter(probe.InstanceId, "mode", "turbo"));

        result.HasCode(DiagnosticCodes.ParameterOptionNotDeclared).ShouldBeTrue();
    }

    [Fact]
    public void Validate_undeclared_parameter_name_reports_vw_param_004()
    {
        ValidationResult result = ValidateProbe(
            Probe(),
            (document, probe) => document.SetNodeParameter(probe.InstanceId, "kernelSize", 5));

        result.HasCode(DiagnosticCodes.UnknownParameter).ShouldBeTrue();
    }

    [Fact]
    public void Validate_required_parameter_without_a_value_reports_vw_param_005()
    {
        NodeDefinition withoutDefault = TestNodes.Blur() with
        {
            Parameters = [new ParameterDefinition("kernelSize", ParameterKind.Integer, true, "Kernel size", 1, 31)],
        };

        ValidationResult result = ValidateProbe(withoutDefault);

        result.HasCode(DiagnosticCodes.MissingRequiredParameter).ShouldBeTrue();
    }

    [Fact]
    public void Validate_disabled_node_without_a_required_value_reports_nothing()
    {
        NodeDefinition withoutDefault = TestNodes.Blur() with
        {
            Parameters = [new ParameterDefinition("kernelSize", ParameterKind.Integer, true, "Kernel size", 1, 31)],
        };

        ValidationResult result = ValidateProbe(
            withoutDefault,
            (document, probe) => document.SetNodeEnabled(probe.InstanceId, false));

        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_parameter_cleared_with_null_is_not_a_kind_error()
    {
        ValidationResult result = ValidateProbe(
            Probe(),
            (document, probe) => document.SetNodeParameter(probe.InstanceId, "count", null));

        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_parameter_of_every_declared_kind_reports_nothing()
    {
        ValidationResult result = ValidateProbe(
            Probe(),
            (document, probe) =>
            {
                document.SetNodeParameter(probe.InstanceId, "count", 4);
                document.SetNodeParameter(probe.InstanceId, "sigma", 1.5);
                document.SetNodeParameter(probe.InstanceId, "flag", false);
                document.SetNodeParameter(probe.InstanceId, "title", "outer");
                document.SetNodeParameter(probe.InstanceId, "mode", "precise");
            });

        result.Diagnostics.ShouldBeEmpty();
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

    /// <summary>
    /// A blur definition that declares one parameter of every kind the validator
    /// distinguishes, so a parameter rule can be exercised without a native node.
    /// </summary>
    private static NodeDefinition Probe()
        => TestNodes.Blur() with
        {
            Parameters =
            [
                new ParameterDefinition("count", ParameterKind.Integer, true, "Count", 1, 10, null, 5),
                new ParameterDefinition("sigma", ParameterKind.Number, false, "Sigma", 0, 100, null, 1d),
                new ParameterDefinition("flag", ParameterKind.Boolean, false, "Flag", null, null, null, true),
                new ParameterDefinition("title", ParameterKind.Text, false, "Title", null, null, null, "untitled"),
                new ParameterDefinition("mode", ParameterKind.Option, false, "Mode", null, null, ["fast", "precise"], "fast"),
            ],
        };

    private static ValidationResult ValidateProbe(
        NodeDefinition definition,
        Action<WorkflowDocument, NodeInstance>? configure = null)
    {
        var validator = new WorkflowValidator(TestNodes.Catalog(TestNodes.Source(), definition));

        WorkflowDocument document = WorkflowDocument.Create("probe");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance probe = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", probe.InstanceId, "image");
        configure?.Invoke(document, probe);

        return validator.Validate(document);
    }
}
