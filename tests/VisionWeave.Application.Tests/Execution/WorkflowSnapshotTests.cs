using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Execution;

public sealed class WorkflowSnapshotTests
{
    [Fact]
    public void From_new_document_preserves_identity_and_revision()
    {
        WorkflowDocument document = WorkflowDocument.Create("New workflow");

        WorkflowSnapshot snapshot = WorkflowSnapshot.From(document);

        snapshot.DocumentId.ShouldBe(document.Id);
        snapshot.Revision.ShouldBe(document.Revision);
    }
}
