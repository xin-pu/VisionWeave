using Shouldly;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Workflows;
using VisionWeave.Domain.Workflows;
using VisionWeave.Persistence.Tests.Support;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.Persistence.Tests.Workflows;

public sealed class WorkflowDocumentRoundTripTests
{
    private static readonly NodeTypeId BlurType = new("visionweave.opencv.gaussian-blur");

    [Fact]
    public void Save_and_load_round_trip_every_document_field()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("round-trip.vwflow");
        WorkflowDocument saved = SampleDocument.Build();

        WorkflowDocumentWriter.Save(saved, path);
        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeTrue();
        result.IsReadOnly.ShouldBeFalse();
        result.SchemaVersion.ShouldBe(WorkflowFileFormat.CurrentSchemaVersion);
        result.Diagnostics.ShouldBeEmpty();

        WorkflowDocument loaded = result.Document!;
        loaded.Id.ShouldBe(saved.Id);
        loaded.Name.ShouldBe(saved.Name);
        loaded.Revision.ShouldBe(saved.Revision);
        loaded.CreatedUtc.ShouldBe(saved.CreatedUtc);
        loaded.ModifiedUtc.ShouldBe(saved.ModifiedUtc);
        loaded.AppVersion.ShouldBe(saved.AppVersion);
        loaded.RequiredPlugins.ShouldBe(saved.RequiredPlugins, ignoreOrder: true);
        loaded.Resources.ShouldBe(saved.Resources);
        loaded.ExtensionData["futureField"].ShouldBe("""{"a":1}""");
        loaded.Nodes.Count.ShouldBe(2);

        NodeInstance blur = loaded.GetNode(saved.Nodes.Single(node => node.NodeTypeId == BlurType).InstanceId);
        blur.TypeVersion.ShouldBe(3);
        blur.Position.ShouldBe(new CanvasPosition(480.5, -120));
        blur.Label.ShouldBe("Blur A");
        blur.IsEnabled.ShouldBeFalse();
        blur.ExtensionData["futureNodeField"].ShouldBe("""{"b":[1,2]}""");
        blur.PortSchemaSnapshot.ShouldBe(saved.GetNode(blur.InstanceId).PortSchemaSnapshot);

        NodeParameterSet parameters = new(blur.Parameters);
        parameters.GetInt32("kernelSize").ShouldBe(5);
        parameters.GetDouble("sigmaX").ShouldBe(1.5);
        parameters.GetBoolean("normalize").ShouldBeFalse();
        parameters.GetString("title").ShouldBe("outer");
        blur.Parameters["kernelSize"].ShouldBe(5L);

        loaded.Connections.Count.ShouldBe(1);
        loaded.Connections[0].ShouldBe(saved.Connections[0]);
    }

    [Fact]
    public void Save_and_load_round_trip_a_document_that_carries_repairable_conditions()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("repairable.vwflow");

        // The validator, not the reader, is what judges these: a document may
        // save a parameter its definition does not declare and connect a port its
        // definition no longer has, and both must survive a round trip intact so
        // the attribution can be recomputed from the reloaded document.
        WorkflowDocument document = WorkflowDocument.Create("repairable");
        NodeInstance blur = document.AddNode(BlurType, 1, new CanvasPosition(0, 0));
        NodeInstance other = document.AddNode(BlurType, 1, new CanvasPosition(200, 0));
        document.SetNodeParameter(blur.InstanceId, "sharpness", 2);
        document.AddConnection(other.InstanceId, "gone", blur.InstanceId, "missing");

        WorkflowDocumentWriter.Save(document, path);
        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();

        WorkflowDocument loaded = result.Document!;
        loaded.GetNode(blur.InstanceId).Parameters["sharpness"].ShouldBe(2L);
        WorkflowConnection connection = loaded.Connections.ShouldHaveSingleItem();
        connection.SourcePortId.ShouldBe("gone");
        connection.TargetPortId.ShouldBe("missing");
    }

    [Fact]
    public void Save_and_load_round_trip_a_resource_of_a_kind_this_build_does_not_model()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("unmodelled-resource.vwflow");
        WorkflowDocument document = WorkflowDocument.Create("unmodelled resource");
        document.AddResource(new UnknownResourceReference("camera", """{"kind":"camera","index":2}"""));

        WorkflowDocumentWriter.Save(document, path);
        WorkflowDocument loaded = WorkflowDocumentReader.Load(path).Document!;

        UnknownResourceReference resource = loaded.Resources.ShouldHaveSingleItem()
            .ShouldBeOfType<UnknownResourceReference>();
        resource.Kind.ShouldBe("camera");
        resource.Json.ShouldBe("""{"kind":"camera","index":2}""");

        // A declared resource is stored state, so it survives at the revision the
        // document had when it was saved.
        loaded.Revision.ShouldBe(document.Revision);
    }

    [Fact]
    public void Save_writes_the_same_content_for_the_same_document()
    {
        using var directory = new TemporaryDirectory();
        WorkflowDocument document = SampleDocument.Build();

        WorkflowDocumentWriter.Save(document, directory.File("first.vwflow"));
        WorkflowDocumentWriter.Save(document, directory.File("second.vwflow"));

        File.ReadAllText(directory.File("first.vwflow"))
            .ShouldBe(File.ReadAllText(directory.File("second.vwflow")));
    }

    [Fact]
    public void Working_copy_round_trips_beside_the_document()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("working-copy.vwflow");
        WorkflowDocument document = SampleDocument.Build();

        WorkflowDocumentWriter.SaveWorkingCopy(document, path);

        string workingCopy = WorkflowDocumentWriter.GetWorkingCopyPath(path);
        workingCopy.ShouldBe(path + WorkflowFileFormat.WorkingCopySuffix);
        File.Exists(path).ShouldBeFalse();
        File.Exists(workingCopy).ShouldBeTrue();
        WorkflowDocumentReader.Load(workingCopy).Document!.Id.ShouldBe(document.Id);
    }
}
