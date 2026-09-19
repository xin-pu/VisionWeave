using System.Windows;
using Shouldly;
using VisionWeave.App.Canvas;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Tests.Canvas;

/// <summary>
/// Covers what a committed document looks like on the canvas: the title and ports a
/// node shows, the order the nodes are drawn in, the wires between them, and the
/// conditions the validator put on each element. The projection reads and writes
/// nothing, so every test builds a document and reads the answer it produces.
/// </summary>
public sealed class CanvasProjectionTests : IDisposable
{
    private static readonly NodeDefinitionCatalog Catalog =
        NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

    /// <summary>
    /// A catalog whose two node types carry incompatible port types, so a document
    /// can hold a wire the validator refuses. The built-in types are all image
    /// frames, which is why the mismatch needs its own pair.
    /// </summary>
    private static readonly NodeDefinitionCatalog MismatchCatalog = new(
    [
        new NodeDefinition(
            new NodeTypeId(ImageSourceTypeId),
            1,
            "Image source",
            "Input",
            [new PortDefinition("image", PortDirection.Output, BuiltInPortTypeIds.ImageFrame, PortMultiplicity.Single, false, "Image")],
            [],
            "visionweave.test.executor.image-source"),
        new NodeDefinition(
            new NodeTypeId(CountTypeId),
            1,
            "Count regions",
            "Analysis",
            [new PortDefinition("count", PortDirection.Input, BuiltInPortTypeIds.Number, PortMultiplicity.Single, false, "Count")],
            [],
            "visionweave.test.executor.count"),
    ]);

    private static readonly NodeTypeId UnknownType = new("visionweave.missing.enhance");

    private readonly TemporaryWorkflowDirectory _directory = new();
    private readonly EditorSession _session = TestSessions.Create(catalog: Catalog);

    [Fact]
    public void A_placed_node_shows_the_title_and_the_ports_its_definition_declares()
    {
        Guid instanceId = Add(OpenCvNodeIds.GaussianBlurTypeId, 10, 20);

        WorkflowNodeViewModel node = Project(_session).Nodes.ShouldHaveSingleItem();

        node.InstanceId.ShouldBe(instanceId);
        node.DisplayName.ShouldBe("Gaussian Blur");
        node.Caption.ShouldBe("visionweave.opencv.gaussian-blur v1");
        node.Location.ShouldBe(new Point(10, 20));
        node.IsSelected.ShouldBeFalse();

        PortViewModel input = node.Inputs.ShouldHaveSingleItem();
        input.PortId.ShouldBe(OpenCvNodeIds.ImagePortId);
        input.DisplayName.ShouldBe("Image");
        input.Direction.ShouldBe(PortDirection.Input);

        PortViewModel output = node.Outputs.ShouldHaveSingleItem();
        output.PortId.ShouldBe(OpenCvNodeIds.BlurredPortId);
        output.Direction.ShouldBe(PortDirection.Output);

        // Nothing feeds the blur yet, and a required input that nothing feeds is an
        // error: the node and the port carry it, in a word as well as in a colour.
        node.Severity.ShouldBe(DiagnosticSeverity.Error);
        node.SeverityWord.ShouldBe("Error");
        node.Condition.ShouldContain(DiagnosticCodes.InvalidGraph);
        node.Summary.ShouldStartWith(node.Caption);
        node.Summary.ShouldContain(DiagnosticCodes.InvalidGraph);

        input.Severity.ShouldBe(DiagnosticSeverity.Error);
        input.Condition.ShouldContain(DiagnosticCodes.InvalidGraph);
        input.Summary.ShouldStartWith("Image · Input");
        input.Summary.ShouldContain(DiagnosticCodes.InvalidGraph);

        output.Severity.ShouldBeNull();
        output.Summary.ShouldBe("Image · Output");
    }

    [Fact]
    public void A_node_type_this_build_does_not_provide_says_so_and_offers_no_ports()
    {
        EditorSession session = TestSessions.Create();
        var command = new AddNodeCommand(UnknownType, 4, new CanvasPosition(0, 0));
        session.Execute(command).IsAccepted.ShouldBeTrue();

        WorkflowNodeViewModel node = Project(session, NodeDefinitionCatalog.Empty).Nodes.ShouldHaveSingleItem();

        node.InstanceId.ShouldBe(command.InstanceId!.Value);
        node.DisplayName.ShouldBe("Unknown node type");
        node.Caption.ShouldBe("visionweave.missing.enhance v4 · not installed");
        node.Inputs.ShouldBeEmpty();
        node.Outputs.ShouldBeEmpty();
        node.Severity.ShouldBe(DiagnosticSeverity.Error);
        node.SeverityWord.ShouldBe("Error");
        node.Condition.ShouldContain(DiagnosticCodes.MissingNodeDefinition);
        node.Summary.ShouldContain(DiagnosticCodes.MissingNodeDefinition);
    }

    [Fact]
    public void A_node_falls_back_to_the_ports_a_document_of_an_older_build_remembered()
    {
        // A document saved by a build that knew the type keeps the ports that build
        // saw, so a plugin that arrives later has something to redraw against.
        string path = _directory.PathOf("remembers-ports.vwflow");
        DocumentRememberingPorts.Write(path);
        _session.Open(path).Session.ShouldNotBeNull();

        CanvasProjectedDocument projected = Project(_session);

        WorkflowNodeViewModel node = projected.Nodes
            .Single(item => item.InstanceId == DocumentRememberingPorts.UnknownNodeId);

        node.DisplayName.ShouldBe("Unknown node type");
        node.Caption.ShouldBe("visionweave.missing.enhance v4 · not installed");
        node.Severity.ShouldBe(DiagnosticSeverity.Error);
        node.Inputs.ShouldHaveSingleItem().DisplayName.ShouldBe("Image");

        // The remembered port that carried no label is named by its identifier.
        node.Outputs.ShouldHaveSingleItem().DisplayName.ShouldBe("result");

        // The wire is drawn even though neither end's definition resolves: the wire
        // needs the ports, and the document remembered them.
        WorkflowConnectionViewModel wire = projected.Connectors.ShouldHaveSingleItem();
        wire.Source.NodeInstanceId.ShouldBe(DocumentRememberingPorts.BlurNodeId);
        wire.Source.PortId.ShouldBe(OpenCvNodeIds.BlurredPortId);
        wire.Target.NodeInstanceId.ShouldBe(DocumentRememberingPorts.UnknownNodeId);
    }

    [Fact]
    public void Nodes_are_drawn_from_the_top_left_so_the_surface_does_not_depend_on_document_order()
    {
        Guid lower = Add(OpenCvNodeIds.ResizeTypeId, 0, 200);
        Guid upperRight = Add(OpenCvNodeIds.GaussianBlurTypeId, 200, 0);
        Guid upperLeft = Add(OpenCvNodeIds.ResizeTypeId, 100, 0);

        // The document holds its nodes in a dictionary, so the order it enumerates
        // them in is not part of its contract; the surface order is.
        Project(_session).Nodes.Select(node => node.InstanceId)
            .ShouldBe([upperLeft, upperRight, lower]);
    }

    [Fact]
    public void A_selected_instance_is_projected_as_a_selected_node()
    {
        Guid first = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid second = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);

        _session.Select([second]);

        CanvasProjectedDocument projected = Project(_session);
        projected.Nodes.Single(node => node.InstanceId == second).IsSelected.ShouldBeTrue();
        projected.Nodes.Single(node => node.InstanceId == first).IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void A_wire_reuses_the_port_presentations_of_the_nodes_it_joins()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid resize = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);
        Connect(blur, OpenCvNodeIds.BlurredPortId, resize, OpenCvNodeIds.ImagePortId)
            .ShouldBeTrue();

        CanvasProjectedDocument projected = Project(_session);
        WorkflowNodeViewModel blurNode = projected.Nodes.Single(node => node.InstanceId == blur);
        WorkflowNodeViewModel resizeNode = projected.Nodes.Single(node => node.InstanceId == resize);

        // The wire follows a node that moves because it holds the ports the nodes
        // already publish rather than a copy of where they were.
        WorkflowConnectionViewModel wire = projected.Connectors.ShouldHaveSingleItem();
        wire.Source.ShouldBeSameAs(blurNode.Outputs.ShouldHaveSingleItem());
        wire.Target.ShouldBeSameAs(resizeNode.Inputs.ShouldHaveSingleItem());
        wire.Severity.ShouldBeNull();
        wire.SeverityWord.ShouldBeEmpty();
        wire.Summary.ShouldBeNull("a wire with nothing to report opens no empty tooltip.");
    }

    [Fact]
    public void A_wire_whose_target_port_this_build_cannot_name_is_left_out()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid unknown = AddUnknown(300, 0);

        // The document is permissive, so a file can hold a wire to a port this build
        // cannot name. It is left out rather than drawn from the origin, and the
        // nodes themselves are still shown.
        _session.Document.AddConnection(blur, OpenCvNodeIds.BlurredPortId, unknown, "image");

        CanvasProjectedDocument projected = Project(_session);

        projected.Nodes.Count.ShouldBe(2);
        projected.Connectors.ShouldBeEmpty();
    }

    [Fact]
    public void A_wire_the_validator_refuses_is_drawn_marked_for_the_connection()
    {
        EditorSession session = TestSessions.Create(catalog: MismatchCatalog);
        Guid source = Add(session, MismatchCatalog, ImageSourceTypeId, 0, 0);
        Guid count = Add(session, MismatchCatalog, CountTypeId, 300, 0);

        // The document is permissive, so a file can hold a wire between ports whose
        // types do not match. The condition belongs to the connection itself.
        session.Document.AddConnection(source, "image", count, "count");

        WorkflowConnectionViewModel wire = Project(session, MismatchCatalog)
            .Connectors.ShouldHaveSingleItem();

        wire.Severity.ShouldBe(DiagnosticSeverity.Error);
        wire.SeverityWord.ShouldBe("Error");
        wire.Summary.ShouldNotBeNull();
        wire.Summary!.ShouldContain(DiagnosticCodes.IncompatiblePort);
    }

    [Fact]
    public void A_port_a_wire_cannot_use_is_marked_where_the_condition_belongs()
    {
        Guid blur = Add(OpenCvNodeIds.GaussianBlurTypeId, 0, 0);
        Guid resize = Add(OpenCvNodeIds.ResizeTypeId, 300, 0);
        _session.Document.AddConnection(
            blur,
            OpenCvNodeIds.BlurredPortId,
            resize,
            OpenCvNodeIds.ResizedPortId);

        WorkflowConnectionViewModel wire = Project(_session).Connectors.ShouldHaveSingleItem();

        // An output cannot be a target, and that condition is reported against the
        // port rather than against the wire, so the port is what carries the mark.
        wire.Target.Severity.ShouldBe(DiagnosticSeverity.Error);
        wire.Target.Condition.ShouldContain(DiagnosticCodes.IncompatiblePort);
        wire.Target.Summary.ShouldContain(DiagnosticCodes.IncompatiblePort);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    /// <summary>
    /// Projects the document the session holds, judged by the same catalog. The
    /// projection is built here rather than read from the session, so a test that
    /// edits the document directly still reads an answer for what it now holds.
    /// </summary>
    /// <param name="session">The session holding the document.</param>
    /// <param name="catalog">The catalog to resolve node types against, or the built-in one.</param>
    /// <returns>What the canvas would draw.</returns>
    private static CanvasProjectedDocument Project(EditorSession session, NodeDefinitionCatalog? catalog = null)
    {
        NodeDefinitionCatalog resolved = catalog ?? Catalog;

        return CanvasProjection.Project(
            session.Document,
            resolved,
            new WorkflowValidator(resolved).Project(session.Document),
            session.Selection);
    }

    private Guid Add(string typeId, double x, double y) => Add(_session, Catalog, typeId, x, y);

    /// <summary>Places a node, resolving the version from the catalog that defines it.</summary>
    /// <param name="session">The session to edit.</param>
    /// <param name="catalog">The catalog the type is resolved against.</param>
    /// <param name="typeId">The node type to place.</param>
    /// <param name="x">The graph-space column.</param>
    /// <param name="y">The graph-space row.</param>
    /// <returns>The instance that was placed.</returns>
    private static Guid Add(EditorSession session, NodeDefinitionCatalog catalog, string typeId, double x, double y)
    {
        var nodeTypeId = new NodeTypeId(typeId);
        catalog.TryResolveLatest(nodeTypeId, out NodeDefinition? definition).ShouldBeTrue();

        var command = new AddNodeCommand(nodeTypeId, definition!.TypeVersion, new CanvasPosition(x, y));
        session.Execute(command).IsAccepted.ShouldBeTrue();

        return command.InstanceId!.Value;
    }

    private Guid AddUnknown(double x, double y)
    {
        var command = new AddNodeCommand(UnknownType, 4, new CanvasPosition(x, y));
        _session.Execute(command).IsAccepted.ShouldBeTrue();

        return command.InstanceId!.Value;
    }

    private bool Connect(Guid sourceNodeId, string sourcePortId, Guid targetNodeId, string targetPortId)
        => _session.Execute(new ConnectPortsCommand(
            new WorkflowValidator(Catalog),
            sourceNodeId,
            sourcePortId,
            targetNodeId,
            targetPortId)).IsAccepted;

    private const string ImageSourceTypeId = "visionweave.test.image-source";

    private const string CountTypeId = "visionweave.test.count";
}
