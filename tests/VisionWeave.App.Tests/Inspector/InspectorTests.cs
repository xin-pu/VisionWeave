using Shouldly;
using VisionWeave.App.Inspector;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Tests.Inspector;

/// <summary>
/// The inspector driven headlessly: which fields a selection offers, what a
/// committed field does to the document, and what the panel says about a node
/// that carries conditions. The window is not built here — the markup tests hold
/// the templates — so every step here is about the words and the commands a user
/// reaches through them.
/// </summary>
public sealed class InspectorTests
{
    private const string SwitchTypeId = "visionweave.test.switch";
    private const string SwitchParameter = "invert";
    private const string SwitchInputPort = "image";

    private static readonly NodeDefinitionCatalog Catalog =
        NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

    [Fact]
    public void The_inspector_offers_one_field_per_parameter_the_selection_declares()
    {
        (EditorSession session, InspectorViewModel inspector, _) = Select(OpenCvNodeIds.GaussianBlurTypeId);

        inspector.NodeTitle.ShouldBe("Gaussian Blur");
        inspector.NodeCaption.ShouldBe("visionweave.opencv.gaussian-blur v1");
        inspector.IsEditable.ShouldBeTrue();

        inspector.Parameters.Select(parameter => parameter.Name)
            .ShouldBe([OpenCvNodeIds.KernelSizeParameter, OpenCvNodeIds.SigmaParameter]);

        ParameterEditorViewModel kernel = inspector.Parameters[0];
        kernel.DisplayName.ShouldBe("Kernel size");
        kernel.Kind.ShouldBe(ParameterKind.Integer);

        // Nothing is stored yet, so the field shows the default and says so. Numeric
        // fields commit through their dedicated editor and need no typing hint.
        kernel.Text.ShouldBe("5");
        kernel.HasStoredValue.ShouldBeFalse();
        kernel.Caption.ShouldBe("whole number · 1 to 99 · default 5");
        kernel.Condition.ShouldBeEmpty();

        session.Document.Nodes.ShouldHaveSingleItem();
    }

    [Fact]
    public void A_field_shows_the_value_the_document_holds_rather_than_the_default()
    {
        (EditorSession session, InspectorViewModel inspector, Guid instance) =
            Select(OpenCvNodeIds.GaussianBlurTypeId);
        session.Execute(new SetNodeParameterCommand(instance, OpenCvNodeIds.KernelSizeParameter, 7L));

        ParameterEditorViewModel kernel = inspector.Parameters[0];
        kernel.Text.ShouldBe("7");
        kernel.HasStoredValue.ShouldBeTrue();

        // The caption keeps the range and drops the default, because the value on
        // screen is the document's rather than the definition's.
        kernel.Caption.ShouldBe("whole number · 1 to 99");
    }

    [Fact]
    public void A_committed_field_edits_the_document_through_the_command_history()
    {
        (EditorSession session, InspectorViewModel inspector, Guid instance) =
            Select(OpenCvNodeIds.GaussianBlurTypeId);
        ParameterEditorViewModel kernel = inspector.Parameters[0];

        // The field holds text and reports the commit; the inspector is what turns
        // it into a document command. Committing twice is one undo step, because a
        // parameter is the unit a user thinks in and a keystroke is not.
        kernel.Text = "7";
        kernel.ApplyCommand.Execute(null);
        kernel.Text = "9";
        kernel.ApplyCommand.Execute(null);

        session.Document.GetNode(instance).Parameters[OpenCvNodeIds.KernelSizeParameter].ShouldBe(9L);
        session.CanUndo.ShouldBeTrue();

        session.Undo();

        // The undo returns the parameter to what it held before the first commit:
        // the document never stored a value, so the definition's default applies.
        session.Document.GetNode(instance).Parameters.ShouldNotContainKey(OpenCvNodeIds.KernelSizeParameter);

        // The field follows the document rather than the text that was typed.
        inspector.Parameters[0].Text.ShouldBe("5");
        inspector.Parameters[0].HasStoredValue.ShouldBeFalse();

        // Two commits, one undo step: a second undo removes the node the test
        // placed, which is what makes a parameter the unit rather than a keystroke.
        session.Undo();
        session.Document.Nodes.ShouldBeEmpty();
        session.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void A_field_that_holds_no_number_is_refused_and_leaves_the_document_alone()
    {
        (EditorSession session, InspectorViewModel inspector, Guid instance) =
            Select(OpenCvNodeIds.GaussianBlurTypeId);
        long revision = session.Document.Revision;
        ParameterEditorViewModel kernel = inspector.Parameters[0];

        kernel.Text = "not a number";
        kernel.ApplyCommand.Execute(null);

        session.Document.Revision.ShouldBe(revision);
        session.Document.GetNode(instance).Parameters.ShouldNotContainKey(OpenCvNodeIds.KernelSizeParameter);

        // The refusal is shown on the field it belongs to and reported where the
        // shell reports conditions, so it is not a message that vanishes.
        kernel.Severity.ShouldBe(DiagnosticSeverity.Error);
        kernel.Condition.ShouldContain(DiagnosticCodes.InvalidParameterValue);

        // A refusal is not an undo step either: undoing once removes the node the
        // test placed, so nothing else was ever pushed onto the history.
        session.Undo();
        session.Document.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void A_value_outside_the_declared_range_is_committed_and_reported_at_the_parameter()
    {
        (EditorSession session, InspectorViewModel inspector, Guid instance) =
            Select(OpenCvNodeIds.GaussianBlurTypeId);
        ParameterEditorViewModel kernel = inspector.Parameters[0];

        kernel.Text = "4096";
        kernel.ApplyCommand.Execute(null);

        // The shape of a value is the field's business and its legality is the
        // document's, so this commit is applied and the validator reports it.
        session.Document.GetNode(instance).Parameters[OpenCvNodeIds.KernelSizeParameter].ShouldBe(4096L);

        inspector.Diagnostics.ShouldContain(entry =>
            entry.Code == DiagnosticCodes.ParameterOutOfRange
            && entry.Target == $"parameter {OpenCvNodeIds.KernelSizeParameter}");
        kernel.Severity.ShouldBe(DiagnosticSeverity.Error);
        kernel.Condition.ShouldContain(DiagnosticCodes.ParameterOutOfRange);
    }

    [Fact]
    public void An_option_field_offers_the_values_the_definition_declares()
    {
        (EditorSession _, InspectorViewModel inspector, _) = Select(OpenCvNodeIds.ResizeTypeId);

        ParameterEditorViewModel interpolation = inspector.Parameters
            .Single(parameter => parameter.Name == OpenCvNodeIds.InterpolationParameter);

        interpolation.HasOptions.ShouldBeTrue();
        interpolation.Options.ShouldBe(OpenCvNodeIds.InterpolationOptions);
        interpolation.SelectedOption.ShouldBe(OpenCvNodeIds.InterpolationArea);
        interpolation.Caption.ShouldBe("option · nearest, linear, cubic, area · default area");
    }

    [Fact]
    public void An_option_field_commits_the_option_the_user_picks()
    {
        (EditorSession session, InspectorViewModel inspector, Guid instance) =
            Select(OpenCvNodeIds.ResizeTypeId);

        ParameterEditorViewModel interpolation = inspector.Parameters
            .Single(parameter => parameter.Name == OpenCvNodeIds.InterpolationParameter);

        // Choosing an option is a whole gesture by itself, so the pick is the commit
        // and the document already holds it by the time the user looks up.
        interpolation.SelectedOption = OpenCvNodeIds.InterpolationCubic;

        session.Document.GetNode(instance).Parameters[OpenCvNodeIds.InterpolationParameter]
            .ShouldBe(OpenCvNodeIds.InterpolationCubic);
        interpolation.HasStoredValue.ShouldBeTrue();
        interpolation.Caption.ShouldBe("option · nearest, linear, cubic, area");
    }

    [Fact]
    public void An_option_field_that_chose_nothing_is_refused()
    {
        (EditorSession session, InspectorViewModel inspector, Guid instance) =
            Select(OpenCvNodeIds.ResizeTypeId);

        ParameterEditorViewModel interpolation = inspector.Parameters
            .Single(parameter => parameter.Name == OpenCvNodeIds.InterpolationParameter);

        interpolation.SelectedOption = null;

        session.Document.GetNode(instance).Parameters.ShouldNotContainKey(OpenCvNodeIds.InterpolationParameter);
        interpolation.Condition.ShouldContain(DiagnosticCodes.InvalidParameterValue);
    }

    [Fact]
    public void A_boolean_field_is_presented_as_a_switch_and_commits_its_state()
    {
        (EditorSession session, InspectorViewModel inspector, Guid instance) =
            Select(SwitchTypeId, NodeDefinitionCatalogWithASwitch());

        ParameterEditorViewModel invert = inspector.Parameters.ShouldHaveSingleItem();
        invert.IsBoolean.ShouldBeTrue();
        invert.Caption.ShouldBe("switch · default False");

        // Flipping the switch is the commit: there is nothing else the user has to
        // do to apply it.
        invert.IsChecked = true;

        session.Document.GetNode(instance).Parameters[SwitchParameter].ShouldBe(true);
        invert.Caption.ShouldBe("switch");
    }

    [Fact]
    public void A_node_type_this_build_does_not_hold_offers_no_fields_and_says_why()
    {
        EditorSession session = TestSessions.Create(catalog: NodeDefinitionCatalog.Empty);
        NodeInstance instance = session.Document.AddNode(
            new NodeTypeId("visionweave.missing.enhance"),
            4,
            new CanvasPosition(0, 0));
        var inspector = new InspectorViewModel(session, NodeDefinitionCatalog.Empty, new ShellStatus(session));

        session.Select([instance.InstanceId]);

        inspector.Parameters.ShouldBeEmpty();
        inspector.IsEditable.ShouldBeFalse();
        inspector.ParameterNote.ShouldBe(
            "This build does not provide this node type, so its parameters cannot be shown or edited.");
        inspector.NodeTitle.ShouldBe("Unknown node type");
        inspector.NodeCaption.ShouldBe("visionweave.missing.enhance v4 · not installed");
    }

    [Fact]
    public void A_definition_that_declares_no_parameters_says_so_rather_than_showing_an_empty_panel()
    {
        (EditorSession _, InspectorViewModel inspector, _) =
            Select(SwitchTypeId, NodeDefinitionCatalogWithParameters([]));

        inspector.Parameters.ShouldBeEmpty();
        inspector.IsEditable.ShouldBeFalse();
        inspector.ParameterNote.ShouldBe("This node type declares no parameters.");
    }

    [Fact]
    public void Nothing_selected_leaves_the_inspector_explaining_rather_than_offering_fields()
    {
        EditorSession session = TestSessions.Create(catalog: Catalog);
        var inspector = new InspectorViewModel(session, Catalog, new ShellStatus(session));

        inspector.NodeTitle.ShouldBe("Nothing is selected");
        inspector.NodeCaption.ShouldBe("Nothing is selected.");
        inspector.Parameters.ShouldBeEmpty();
        inspector.IsEditable.ShouldBeFalse();
        inspector.DiagnosticCountText.ShouldBe("No conditions");
    }

    [Fact]
    public void A_selection_of_more_than_one_node_offers_no_fields()
    {
        EditorSession session = TestSessions.Create(catalog: Catalog);
        Guid first = Place(session, OpenCvNodeIds.GaussianBlurTypeId);
        Guid second = Place(session, OpenCvNodeIds.ResizeTypeId);
        var inspector = new InspectorViewModel(session, Catalog, new ShellStatus(session));

        session.Select([first, second]);

        inspector.NodeTitle.ShouldBe("2 nodes are selected");
        inspector.IsEditable.ShouldBeFalse();
        inspector.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void A_condition_is_listed_with_the_element_it_points_at_and_only_for_the_selection()
    {
        EditorSession session = TestSessions.Create(catalog: Catalog);
        Guid first = Place(session, OpenCvNodeIds.GaussianBlurTypeId);
        Place(session, OpenCvNodeIds.GaussianBlurTypeId);
        var inspector = new InspectorViewModel(session, Catalog, new ShellStatus(session));

        // Two nodes, each with a required input nothing is connected to, so the
        // panel has to list one node's condition and not the other's, and it has to
        // name the port the condition points at rather than the node that owns it.
        session.Select([first]);

        DiagnosticEntryViewModel entry = inspector.Diagnostics.ShouldHaveSingleItem();
        entry.Code.ShouldBe(DiagnosticCodes.InvalidGraph);
        entry.Severity.ShouldBe(DiagnosticSeverity.Error);
        entry.SeverityWord.ShouldBe("Error");
        entry.Target.ShouldBe($"port {OpenCvNodeIds.ImagePortId}");
        inspector.DiagnosticCountText.ShouldBe("1 condition");
    }

    [Fact]
    public void A_read_only_document_offers_the_fields_as_read_only()
    {
        using TemporaryWorkflowDirectory directory = new();
        WorkflowDocument stored = WorkflowDocument.Create("A newer workflow");
        stored.AddNode(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0));

        EditorSession session = TestSessions.Create(catalog: Catalog);
        session.Open(directory.SaveUnsupportedSchemaDocument(stored));

        NodeInstance instance = session.Document.Nodes.ShouldHaveSingleItem();
        var inspector = new InspectorViewModel(session, Catalog, new ShellStatus(session));

        session.Select([instance.InstanceId]);

        // The document still describes itself, so the fields are present and read
        // only: hiding them would claim the shell cannot say what the node holds.
        // Why the document is read-only is the session's answer, and the shell shows
        // it in the notice beside the panel rather than as a condition of the node.
        inspector.Parameters.ShouldNotBeEmpty();
        inspector.Parameters[0].Text.ShouldBe("5");
        inspector.IsEditable.ShouldBeFalse();

        // A read-only document refuses every edit, so the promise the panel makes is
        // the one the session keeps.
        session.Execute(new SetNodeParameterCommand(instance.InstanceId, OpenCvNodeIds.KernelSizeParameter, 7L))
            .IsAccepted.ShouldBeFalse();
        session.Document.GetNode(instance.InstanceId).Parameters
            .ShouldNotContainKey(OpenCvNodeIds.KernelSizeParameter);
    }

    /// <summary>
    /// Selects the node a test drives, so the test reads as the flow a user takes:
    /// place a node, select it, and read the inspector. The node is placed through
    /// the command history rather than into the document, because that is what keeps
    /// the session's projection describing the document the inspector reads.
    /// </summary>
    /// <param name="typeId">The node type to place.</param>
    /// <param name="definitions">The catalog to place it in, or the OpenCV one.</param>
    /// <returns>The session, the inspector over it, and the placed instance.</returns>
    private static (EditorSession Session, InspectorViewModel Inspector, Guid InstanceId) Select(
        string typeId,
        NodeDefinitionCatalog? definitions = null)
    {
        NodeDefinitionCatalog catalog = definitions ?? Catalog;
        EditorSession session = TestSessions.Create(catalog: catalog);
        Guid instanceId = Place(session, typeId);
        var inspector = new InspectorViewModel(session, catalog, new ShellStatus(session));

        session.Select([instanceId]);

        return (session, inspector, instanceId);
    }

    /// <summary>Places one node of a type and returns the identifier it was given.</summary>
    /// <param name="session">The session to edit.</param>
    /// <param name="typeId">The node type to place.</param>
    /// <returns>The placed instance identifier.</returns>
    private static Guid Place(EditorSession session, string typeId)
    {
        var place = new AddNodeCommand(new NodeTypeId(typeId), 1, new CanvasPosition(0, 0));

        session.Execute(place).IsAccepted.ShouldBeTrue();

        return place.InstanceId!.Value;
    }

    /// <summary>
    /// A catalog holding one node type that declares a switch and a port, so the
    /// boolean field is covered by a definition rather than by the OpenCV nodes,
    /// none of which declares one.
    /// </summary>
    /// <returns>The catalog.</returns>
    private static NodeDefinitionCatalog NodeDefinitionCatalogWithASwitch()
        => NodeDefinitionCatalogWithParameters(
        [
            new ParameterDefinition(
                SwitchParameter,
                ParameterKind.Boolean,
                IsRequired: false,
                DisplayName: "Invert",
                DefaultValue: false),
        ]);

    private static NodeDefinitionCatalog NodeDefinitionCatalogWithParameters(
        IReadOnlyList<ParameterDefinition> parameters)
        => new(
        [
            new NodeDefinition(
                new NodeTypeId(SwitchTypeId),
                1,
                "Switch",
                "Test",
                [
                    new PortDefinition(
                        SwitchInputPort,
                        PortDirection.Input,
                        BuiltInPortTypeIds.ImageFrame,
                        PortMultiplicity.Single,
                        IsOptional: false,
                        DisplayName: "Image"),
                ],
                parameters,
                "visionweave.test.switch.executor"),
        ]);
}
