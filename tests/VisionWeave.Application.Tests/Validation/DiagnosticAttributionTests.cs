using Shouldly;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Validation;

/// <summary>
/// Covers which document element the validator attributes a condition to. The
/// projection can only answer about a port, a parameter, or a connection if the
/// validator named it, so these tests hold the producing half of the contract.
/// </summary>
public sealed class DiagnosticAttributionTests
{
    private readonly NodeDefinitionCatalog _catalog = TestNodes.DefaultCatalog();
    private readonly WorkflowValidator _validator;

    public DiagnosticAttributionTests()
    {
        _validator = new WorkflowValidator(_catalog);
    }

    [Fact]
    public void Validate_unconnected_required_input_targets_that_port_and_still_names_its_node()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(0, 0));

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.InvalidGraph);

        diagnostic.Target.ShouldBe(new PortTarget(masking.InstanceId, "image"));

        // The narrow target refines the node scope rather than replacing it, so a
        // consumer that groups by node sees the condition where it always did.
        diagnostic.NodeInstanceId.ShouldBe(masking.InstanceId);
    }

    [Fact]
    public void Validate_port_the_definition_does_not_declare_targets_the_port_the_connection_names()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "blurred", blur.InstanceId, "image");

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.UnknownPort);

        diagnostic.Target.ShouldBe(new PortTarget(source.InstanceId, "blurred"));
    }

    [Fact]
    public void Validate_parameter_outside_its_range_targets_that_parameter()
    {
        WorkflowDocument document = ConnectedBlur(out NodeInstance blur);
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 99);

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.ParameterOutOfRange);

        diagnostic.Target.ShouldBe(new ParameterTarget(blur.InstanceId, "kernelSize"));
        diagnostic.NodeInstanceId.ShouldBe(blur.InstanceId);
    }

    [Fact]
    public void Validate_parameter_of_the_wrong_kind_targets_that_parameter()
    {
        WorkflowDocument document = ConnectedBlur(out NodeInstance blur);
        document.SetNodeParameter(blur.InstanceId, "kernelSize", "large");

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.InvalidParameterValue);

        diagnostic.Target.ShouldBe(new ParameterTarget(blur.InstanceId, "kernelSize"));
    }

    [Fact]
    public void Validate_parameter_the_definition_does_not_declare_targets_the_name_the_document_saved()
    {
        WorkflowDocument document = ConnectedBlur(out NodeInstance blur);
        document.SetNodeParameter(blur.InstanceId, "sharpness", 2);

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.UnknownParameter);

        diagnostic.Target.ShouldBe(new ParameterTarget(blur.InstanceId, "sharpness"));
    }

    [Fact]
    public void Validate_required_parameter_without_a_value_targets_that_parameter()
    {
        NodeDefinition withoutDefault = TestNodes.Blur() with
        {
            Parameters = [new ParameterDefinition("kernelSize", ParameterKind.Integer, true, "Kernel size", 1, 31)],
        };

        WorkflowValidator validator = new(TestNodes.Catalog(withoutDefault));
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));

        NodeDiagnostic diagnostic = validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.MissingRequiredParameter);

        diagnostic.Target.ShouldBe(new ParameterTarget(blur.InstanceId, "kernelSize"));
    }

    [Fact]
    public void Validate_data_type_the_target_port_cannot_accept_targets_the_connection()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance count = document.AddNode(TestNodes.CountType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        WorkflowConnection connection = document.AddConnection(count.InstanceId, "count", blur.InstanceId, "image");

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.IncompatiblePort);

        // A pairing the two port types cannot make is a property of the wire, not
        // of either port: the ports are each correct on their own.
        diagnostic.Target.ShouldBe(new ConnectionTarget(connection.ConnectionId));
    }

    [Fact]
    public void Validate_second_connection_into_a_single_input_targets_that_port()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance masking = document.AddNode(TestNodes.MaskingType, 1, new CanvasPosition(200, 0));
        document.AddConnection(first.InstanceId, "image", masking.InstanceId, "mask");
        document.AddConnection(second.InstanceId, "image", masking.InstanceId, "mask");

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.IncompatiblePort);

        // Fan-in is counted per target port, so the port that refuses the second
        // wire is the one the diagnostic belongs to.
        diagnostic.Target.ShouldBe(new PortTarget(masking.InstanceId, "mask"));
    }

    [Fact]
    public void Validate_node_connected_to_itself_targets_the_connection()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        WorkflowConnection connection =
            document.AddConnection(blur.InstanceId, "blurred", blur.InstanceId, "image");

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.InvalidGraph);

        diagnostic.Target.ShouldBe(new ConnectionTarget(connection.ConnectionId));
    }

    [Fact]
    public void ValidateConnection_connection_to_a_missing_node_targets_that_connection()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        var candidate = new WorkflowConnection(Guid.NewGuid(), Guid.NewGuid(), "image", blur.InstanceId, "image");

        NodeDiagnostic diagnostic = _validator.ValidateConnection(document, candidate).Diagnostics.Single();

        // A connection whose far end is gone has no node to belong to, so the
        // target is the only thing that still identifies it.
        diagnostic.Target.ShouldBe(new ConnectionTarget(candidate.ConnectionId));
        diagnostic.NodeInstanceId.ShouldBeNull();
    }

    [Fact]
    public void ValidateConnection_wire_that_closes_a_cycle_targets_that_connection()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(first.InstanceId, "blurred", second.InstanceId, "image");
        var candidate = new WorkflowConnection(Guid.NewGuid(), second.InstanceId, "blurred", first.InstanceId, "image");

        NodeDiagnostic diagnostic = _validator.ValidateConnection(document, candidate).Diagnostics.Single();

        // The rejected wire is what the canvas must mark, and the cycle it would
        // close is why; the whole-document cycle keeps its node scope, because a
        // path of several wires does not belong to any one of them.
        diagnostic.Target.ShouldBe(new ConnectionTarget(candidate.ConnectionId));
    }

    [Fact]
    public void Validate_condition_about_the_node_as_a_whole_names_no_target()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance absent = document.AddNode(TestNodes.AbsentType, 1, new CanvasPosition(0, 0));

        NodeDiagnostic diagnostic = _validator.Validate(document).Diagnostics
            .Single(item => item.Code == DiagnosticCodes.MissingNodeDefinition);

        // Nothing narrower than the node is known, so the node is the whole of
        // what this condition can be attributed to.
        diagnostic.NodeInstanceId.ShouldBe(absent.InstanceId);
        diagnostic.Target.ShouldBeNull();
    }

    [Fact]
    public void Validate_narrowed_targets_agree_with_the_node_they_name()
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance first = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance second = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 100));
        NodeInstance blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(first.InstanceId, "image", blur.InstanceId, "image");
        document.AddConnection(second.InstanceId, "image", blur.InstanceId, "image");
        document.SetNodeParameter(blur.InstanceId, "kernelSize", 99);

        IReadOnlyList<NodeDiagnostic> diagnostics = _validator.Validate(document).Diagnostics;

        diagnostics.ShouldContain(diagnostic => diagnostic.Target != null);

        List<string> disagreements = [];
        foreach (NodeDiagnostic diagnostic in diagnostics)
        {
            Guid? owner = diagnostic.Target switch
            {
                PortTarget port => port.NodeInstanceId,
                ParameterTarget parameter => parameter.NodeInstanceId,
                _ => null,
            };

            // A consumer that groups by node and a consumer that reads the target
            // must not see different owners for one condition.
            if (owner is { } nodeInstanceId && diagnostic.NodeInstanceId != nodeInstanceId)
            {
                disagreements.Add($"{diagnostic.Code} names {diagnostic.NodeInstanceId} but targets {nodeInstanceId}");
            }
        }

        disagreements.ShouldBeEmpty();
    }

    private static WorkflowDocument ConnectedBlur(out NodeInstance blur)
    {
        WorkflowDocument document = WorkflowDocument.Create("workflow");
        NodeInstance source = document.AddNode(TestNodes.SourceType, 1, new CanvasPosition(0, 0));
        blur = document.AddNode(TestNodes.BlurType, 1, new CanvasPosition(200, 0));
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");
        return document;
    }
}
