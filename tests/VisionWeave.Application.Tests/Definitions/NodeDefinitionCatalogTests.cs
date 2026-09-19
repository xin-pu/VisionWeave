using Shouldly;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Tests.Support;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;

namespace VisionWeave.Application.Tests.Definitions;

public sealed class NodeDefinitionCatalogTests
{
    [Fact]
    public void Constructor_duplicate_type_and_version_is_rejected()
    {
        Should.Throw<ArgumentException>(() => TestNodes.Catalog(TestNodes.Blur(), TestNodes.Blur()));
    }

    [Fact]
    public void Constructor_same_type_in_two_versions_keeps_both()
    {
        NodeDefinitionCatalog catalog = TestNodes.Catalog(TestNodes.Blur(1), TestNodes.Blur(2));

        catalog.Definitions.Count.ShouldBe(2);
        catalog.TryResolve(TestNodes.BlurType, 1, out NodeDefinition? first).ShouldBeTrue();
        catalog.TryResolve(TestNodes.BlurType, 2, out NodeDefinition? second).ShouldBeTrue();
        first!.TypeVersion.ShouldBe(1);
        second!.TypeVersion.ShouldBe(2);
    }

    [Fact]
    public void TryResolve_saved_version_resolves_the_matching_definition()
    {
        NodeDefinitionCatalog catalog = TestNodes.DefaultCatalog();

        catalog.TryResolve(TestNodes.BlurType, 1, out NodeDefinition? definition).ShouldBeTrue();

        definition!.TypeId.ShouldBe(TestNodes.BlurType);
        definition.TypeVersion.ShouldBe(1);
    }

    [Fact]
    public void TryResolve_absent_version_fails_while_the_type_stays_known()
    {
        NodeDefinitionCatalog catalog = TestNodes.DefaultCatalog();

        catalog.TryResolve(TestNodes.BlurType, 9, out NodeDefinition? definition).ShouldBeFalse();
        catalog.TryResolveLatest(TestNodes.BlurType, out NodeDefinition? latest).ShouldBeTrue();

        definition.ShouldBeNull();
        latest!.TypeVersion.ShouldBe(1);
    }

    [Fact]
    public void TryResolve_unknown_type_fails()
    {
        NodeDefinitionCatalog catalog = TestNodes.DefaultCatalog();

        catalog.TryResolve(TestNodes.AbsentType, 1, out NodeDefinition? definition).ShouldBeFalse();

        definition.ShouldBeNull();
    }

    [Fact]
    public void TryResolveLatest_several_versions_prefers_the_newest()
    {
        NodeDefinitionCatalog catalog = TestNodes.Catalog(TestNodes.Blur(3), TestNodes.Blur(1), TestNodes.Blur(2));

        catalog.TryResolveLatest(TestNodes.BlurType, out NodeDefinition? latest).ShouldBeTrue();

        latest!.TypeVersion.ShouldBe(3);
    }

    [Fact]
    public void FromProviders_every_provider_contributes_its_definitions()
    {
        NodeDefinitionCatalog catalog = NodeDefinitionCatalog.FromProviders(
            [new StubProvider("test.input", [TestNodes.Source()]), new StubProvider("test.filter", [TestNodes.Blur(), TestNodes.Count()])]);

        catalog.Definitions.Count.ShouldBe(3);
        catalog.TryResolveLatest(TestNodes.CountType, out _).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_definition_with_duplicate_port_id_is_rejected()
    {
        NodeDefinition invalid = TestNodes.Source() with
        {
            Ports =
            [
                TestNodes.Output("image", BuiltInPortTypeIds.ImageFrame),
                TestNodes.Input("image", BuiltInPortTypeIds.ImageFrame),
            ],
        };

        Should.Throw<ArgumentException>(() => TestNodes.Catalog(invalid));
    }

    [Fact]
    public void Constructor_definition_with_duplicate_parameter_name_is_rejected()
    {
        NodeDefinition invalid = TestNodes.Source() with
        {
            Parameters =
            [
                new ParameterDefinition("radius", ParameterKind.Integer, true, "Radius"),
                new ParameterDefinition("radius", ParameterKind.Number, false, "Radius again"),
            ],
        };

        Should.Throw<ArgumentException>(() => TestNodes.Catalog(invalid));
    }

    [Fact]
    public void Empty_contains_no_definitions()
    {
        NodeDefinitionCatalog catalog = NodeDefinitionCatalog.Empty;

        catalog.Definitions.ShouldBeEmpty();
        catalog.TryResolve(TestNodes.BlurType, 1, out _).ShouldBeFalse();
    }

    private sealed class StubProvider(string providerId, IReadOnlyCollection<NodeDefinition> definitions) : INodeDefinitionProvider
    {
        public string ProviderId { get; } = providerId;

        public IReadOnlyCollection<NodeDefinition> GetDefinitions() => definitions;
    }
}
