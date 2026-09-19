using Shouldly;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.OpenCv.Execution;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The contract between the definitions the OpenCV provider publishes and the
/// registrations that execute them. A definition with no executor would be
/// placeable but unrunnable, and a required parameter with no usable default
/// would make a freshly placed node fail on its first run, so both are checked
/// here rather than discovered by a user.
/// </summary>
public sealed class OpenCvNodeCatalogTests
{
    private static readonly IReadOnlyList<NodeDefinition> Definitions =
        [.. new OpenCvNodeDefinitionProvider().GetDefinitions()];

    [Fact]
    public void Provider_publishes_node_types_that_enter_one_catalog()
    {
        NodeDefinitionCatalog catalog = NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

        catalog.KnownTypeIds.Count.ShouldBe(2);
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.ResizeTypeId));
        catalog.Definitions.Count.ShouldBe(Definitions.Count);

        foreach (NodeDefinition definition in Definitions)
        {
            catalog.TryResolve(definition.TypeId, definition.TypeVersion, out NodeDefinition? resolved).ShouldBeTrue();
            resolved.ShouldBe(definition);
        }
    }

    [Fact]
    public void Every_definition_declares_one_image_input_and_one_image_output()
    {
        Definitions.ShouldNotBeEmpty();

        foreach (NodeDefinition definition in Definitions)
        {
            PortDefinition input = definition.Inputs.ShouldHaveSingleItem();
            input.Id.ShouldBe(OpenCvNodeIds.ImagePortId);
            input.TypeId.ShouldBe(BuiltInPortTypeIds.ImageFrame);
            input.Multiplicity.ShouldBe(PortMultiplicity.Single);
            input.IsOptional.ShouldBeFalse();

            PortDefinition output = definition.Outputs.ShouldHaveSingleItem();
            output.Id.ShouldBeOneOf(OpenCvNodeIds.BlurredPortId, OpenCvNodeIds.ResizedPortId);
            output.TypeId.ShouldBe(BuiltInPortTypeIds.ImageFrame);
            output.Multiplicity.ShouldBe(PortMultiplicity.Single);
            output.IsOptional.ShouldBeFalse();
        }
    }

    [Fact]
    public void Every_required_parameter_declares_a_default_so_a_placed_node_can_run()
    {
        Definitions.ShouldNotBeEmpty();

        foreach (NodeDefinition definition in Definitions)
        {
            foreach (ParameterDefinition parameter in definition.Parameters)
            {
                if (!parameter.IsRequired)
                {
                    continue;
                }

                parameter.DefaultValue.ShouldNotBeNull(
                    $"{definition.TypeId} requires '{parameter.Name}', and parameter validation is not implemented "
                    + "yet (PL-2026-006), so a node placed without a saved value has nothing to run with.");
            }
        }
    }

    [Fact]
    public void Every_declared_default_matches_its_kind_and_bounds()
    {
        Definitions.ShouldNotBeEmpty();

        foreach (NodeDefinition definition in Definitions)
        {
            foreach (ParameterDefinition parameter in definition.Parameters)
            {
                if (parameter.DefaultValue is null)
                {
                    continue;
                }

                string where = $"{definition.TypeId} parameter '{parameter.Name}'";

                switch (parameter.Kind)
                {
                    case ParameterKind.Integer:
                        AssertInBounds(where, (int)parameter.DefaultValue, parameter);
                        break;
                    case ParameterKind.Number:
                        AssertInBounds(where, (double)parameter.DefaultValue, parameter);
                        break;
                    case ParameterKind.Boolean:
                        (parameter.DefaultValue is bool).ShouldBeTrue($"{where} is not a boolean default.");
                        break;
                    case ParameterKind.Text:
                    case ParameterKind.Path:
                        (parameter.DefaultValue is string).ShouldBeTrue($"{where} is not a text default.");
                        break;
                    case ParameterKind.Option:
                        (parameter.DefaultValue is string).ShouldBeTrue($"{where} is not a text default.");
                        parameter.Options.ShouldNotBeNull($"{where} is an option without a declared option list.")
                            .ShouldContain((string)parameter.DefaultValue);
                        break;
                }
            }
        }
    }

    [Fact]
    public void Every_definition_resolves_to_a_registered_executor()
    {
        IReadOnlyDictionary<string, INodeExecutor> executors = OpenCvExecutors.CreateDefaults(new LeaseLedger());

        Definitions.Select(definition => definition.ExecutorTypeId).ShouldBeUnique();
        executors.Keys.ShouldBe(Definitions.Select(definition => definition.ExecutorTypeId), ignoreOrder: true);
    }

    private static void AssertInBounds(string where, double value, ParameterDefinition parameter)
    {
        parameter.Minimum.ShouldNotBeNull($"{where} declares a default but no lower bound.");
        parameter.Maximum.ShouldNotBeNull($"{where} declares a default but no upper bound.");

        value.ShouldBeGreaterThanOrEqualTo(parameter.Minimum!.Value, $"{where} default is below its minimum.");
        value.ShouldBeLessThanOrEqualTo(parameter.Maximum!.Value, $"{where} default is above its maximum.");
    }
}
