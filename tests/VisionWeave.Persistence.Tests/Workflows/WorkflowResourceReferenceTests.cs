using Shouldly;
using VisionWeave.Contracts.Ports;
using VisionWeave.Contracts.Workflows;

namespace VisionWeave.Persistence.Tests.Workflows;

/// <summary>
/// The invariants that let the writer hand a preserved resource fragment back to
/// the format without inspecting it again.
/// </summary>
public sealed class WorkflowResourceReferenceTests
{
    [Fact]
    public void Creating_an_unknown_resource_rejects_text_that_is_not_json()
    {
        Should.Throw<ArgumentException>(() => new UnknownResourceReference("camera", "kind: camera"));
    }

    [Fact]
    public void Creating_an_unknown_resource_keeps_a_complete_json_value()
    {
        UnknownResourceReference resource = new("camera", """{"kind":"camera","index":2}""");

        resource.Kind.ShouldBe("camera");
        resource.Json.ShouldBe("""{"kind":"camera","index":2}""");
    }

    [Fact]
    public void Creating_an_unknown_resource_accepts_an_entry_that_names_no_kind()
    {
        UnknownResourceReference resource = new(null, """{"index":2}""");

        resource.Kind.ShouldBeNull();
    }

    [Fact]
    public void Creating_a_file_resource_rejects_a_blank_path()
    {
        Should.Throw<ArgumentException>(() => new FileResourceReference("  "));
    }

    [Fact]
    public void Creating_a_port_schema_entry_rejects_a_blank_port()
    {
        Should.Throw<ArgumentException>(() => new PortSchemaEntry("", PortDirection.Input));
    }
}
