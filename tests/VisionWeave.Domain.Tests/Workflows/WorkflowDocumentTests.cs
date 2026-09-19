using Shouldly;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Domain.Tests.Workflows;

public sealed class WorkflowDocumentTests
{
    [Fact]
    public void Create_valid_name_creates_initial_document()
    {
        WorkflowDocument document = WorkflowDocument.Create("New workflow");

        document.Id.ShouldNotBe(Guid.Empty);
        document.Name.ShouldBe("New workflow");
        document.Revision.ShouldBe(0);
    }
}
