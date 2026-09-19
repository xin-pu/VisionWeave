using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.IntegrationTests.Persistence;

/// <summary>
/// The persisted workflow vertical slice: a document is saved to a
/// <c>.vwflow</c> file, loaded back, and executed as saved. The tests also cover
/// the repair path, where a document still opens and saves although one of its
/// node types is not in the catalog.
/// </summary>
public sealed class WorkflowFileExecutionTests
{
    [Fact]
    public async Task Run_document_loaded_from_a_file_executes_the_saved_graph()
    {
        using var file = new TemporaryWorkflowFile("saved.vwflow");
        WorkflowDocumentWriter.Save(BuildDocument(), file.Path);

        WorkflowLoadResult loaded = WorkflowDocumentReader.Load(file.Path);
        loaded.Succeeded.ShouldBeTrue();
        loaded.IsReadOnly.ShouldBeFalse();
        loaded.Diagnostics.ShouldBeEmpty();

        LeaseLedger ledger = new();
        var sourceFrames = new NativeFrameSource(ledger);

        WorkflowRunSummary summary = await RunAsync(loaded.Document!, ledger, sourceFrames);

        summary.Status.ShouldBe(WorkflowRunStatus.Succeeded);
        summary.Nodes.ShouldAllBe(node => node.State == NodeRunState.Succeeded);
        summary.Diagnostics.ShouldBeEmpty();
        ledger.Created.ShouldBe(3);
        ledger.Released.ShouldBe(3);
        ledger.Outstanding.ShouldBe(0);
        ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public void Load_document_with_an_unknown_node_type_keeps_it_for_a_later_plugin()
    {
        using var file = new TemporaryWorkflowFile("unknown-node.vwflow");
        Guid knownNodeId = Guid.Parse("1b2c3d4e-5f60-4a7b-8c9d-0e1f2a3b4c5d");
        Guid unknownNodeId = Guid.Parse("6d7e8f90-1a2b-4c3d-9e4f-5a6b7c8d9e0f");

        File.WriteAllText(file.Path, """
        {
          "schemaVersion": 1,
          "documentId": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
          "name": "restored",
          "createdUtc": "2026-09-19T10:00:00+00:00",
          "modifiedUtc": "2026-09-19T10:00:00+00:00",
          "revision": 2,
          "nodes": [
            { "id": "1b2c3d4e-5f60-4a7b-8c9d-0e1f2a3b4c5d", "typeId": "visionweave.tests.image-source", "typeVersion": 1, "layout": { "x": 0, "y": 0 } },
            { "id": "6d7e8f90-1a2b-4c3d-9e4f-5a6b7c8d9e0f", "typeId": "visionweave.missing.enhance", "typeVersion": 4, "parameters": { "strength": 2 }, "portSchemaSnapshot": [{"portId":"image","direction":"Input"}], "layout": { "x": 200, "y": 0 } }
          ],
          "connections": [
            { "id": "0a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d", "sourceNodeId": "1b2c3d4e-5f60-4a7b-8c9d-0e1f2a3b4c5d", "sourcePortId": "image", "targetNodeId": "6d7e8f90-1a2b-4c3d-9e4f-5a6b7c8d9e0f", "targetPortId": "image" }
          ]
        }
        """);

        WorkflowLoadResult loaded = WorkflowDocumentReader.Load(file.Path);

        loaded.Succeeded.ShouldBeTrue();
        loaded.Diagnostics.ShouldBeEmpty();
        WorkflowDocument document = loaded.Document!;
        document.Nodes.Count.ShouldBe(2);
        document.Connections.Count.ShouldBe(1);
        document.GetNode(unknownNodeId).TypeVersion.ShouldBe(4);
        document.GetNode(unknownNodeId).ExtensionData["portSchemaSnapshot"]
            .ShouldBe("""[{"portId":"image","direction":"Input"}]""");

        SnapshotBuildResult captured = new WorkflowSnapshotFactory(NativeWorkflow.Catalog()).Build(document);

        captured.Succeeded.ShouldBeFalse();
        captured.Validation.Diagnostics.ShouldContain(item => item.Code == DiagnosticCodes.MissingNodeDefinition);

        WorkflowDocumentWriter.Save(document, file.Path);
        WorkflowDocument rewritten = WorkflowDocumentReader.Load(file.Path).Document!;
        rewritten.Nodes.Count.ShouldBe(2);
        rewritten.GetNode(unknownNodeId).Parameters["strength"].ShouldBe(2L);
        rewritten.Connections.Count.ShouldBe(1);
        rewritten.GetNode(knownNodeId).NodeTypeId.ShouldBe(NativeWorkflow.SourceType);
    }

    private static WorkflowDocument BuildDocument()
    {
        WorkflowDocument document = WorkflowDocument.Create("from file");
        NodeInstance source = document.AddNode(NativeWorkflow.SourceType, 1, new CanvasPosition(0, 0));
        NodeInstance blur = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.GaussianBlurTypeId, 200, 0);
        NodeInstance resize = NativeWorkflow.AddOpenCvNode(document, OpenCvNodeIds.ResizeTypeId, 400, 0);

        document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.KernelSizeParameter, 5);
        document.SetNodeParameter(blur.InstanceId, OpenCvNodeIds.SigmaParameter, 1d);
        document.SetNodeParameter(resize.InstanceId, OpenCvNodeIds.WidthParameter, 4);
        document.SetNodeParameter(resize.InstanceId, OpenCvNodeIds.HeightParameter, 4);
        document.AddConnection(source.InstanceId, OpenCvNodeIds.ImagePortId, blur.InstanceId, OpenCvNodeIds.ImagePortId);
        document.AddConnection(blur.InstanceId, OpenCvNodeIds.BlurredPortId, resize.InstanceId, OpenCvNodeIds.ImagePortId);

        return document;
    }

    private static Task<WorkflowRunSummary> RunAsync(
        WorkflowDocument document,
        LeaseLedger ledger,
        NativeFrameSource sourceFrames)
    {
        SnapshotBuildResult captured = new WorkflowSnapshotFactory(NativeWorkflow.Catalog()).Build(document);

        captured.Succeeded.ShouldBeTrue(
            string.Join(Environment.NewLine, captured.Validation.Diagnostics.Select(diagnostic => diagnostic.Message)));

        ExecutionPlan plan = new ExecutionPlanBuilder().Build(captured.Snapshot!);
        MapExecutorResolver executors = MapExecutorResolver.Create(
            ledger,
            (NativeWorkflow.SourceExecutorTypeId, new DelegateExecutor(sourceFrames.Executor)));

        var runner = new WorkflowRunner(executors, ledger);

        return runner.RunAsync(plan, CancellationToken.None);
    }
}
