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
/// placeable but unrunnable, and a required parameter with no usable default would
/// make a freshly placed node fail on its first run, so both are checked here
/// rather than discovered by a user. The one parameter a node cannot default is
/// the file it reads or writes, which is checked to be required of exactly the
/// nodes that name one.
/// </summary>
public sealed class OpenCvNodeCatalogTests
{
    private static readonly IReadOnlyList<NodeDefinition> Definitions =
        [.. new OpenCvNodeDefinitionProvider().GetDefinitions()];

    [Fact]
    public void Provider_publishes_node_types_that_enter_one_catalog()
    {
        NodeDefinitionCatalog catalog = NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);

        catalog.KnownTypeIds.Count.ShouldBe(Definitions.Count);
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.SaveImageTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.ResizeTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.CvtColorTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.CropTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.MedianBlurTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.ThresholdTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.BlurTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.BilateralFilterTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.AdaptiveThresholdTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.PyrDownTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.PyrUpTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.SobelTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.ScharrTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.LaplacianTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.CannyTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.ErodeTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.DilateTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.MorphologyExTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.DrawRectangleTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.DrawLineTypeId));
        catalog.KnownTypeIds.ShouldContain(new NodeTypeId(OpenCvNodeIds.DrawCircleTypeId));

        foreach (NodeDefinition definition in Definitions)
        {
            catalog.TryResolve(definition.TypeId, definition.TypeVersion, out NodeDefinition? resolved).ShouldBeTrue();
            resolved.ShouldBe(definition);
        }
    }

    [Fact]
    public void Every_definition_declares_at_most_one_image_input_and_one_image_output()
    {
        Definitions.ShouldNotBeEmpty();

        foreach (NodeDefinition definition in Definitions)
        {
            definition.Ports.ShouldNotBeEmpty($"{definition.TypeId} declares no port, so it can neither receive nor publish an image.");
            definition.Inputs.Count().ShouldBeLessThanOrEqualTo(1, $"{definition.TypeId} declares more than one input.");
            definition.Outputs.Count().ShouldBeLessThanOrEqualTo(1, $"{definition.TypeId} declares more than one output.");

            foreach (PortDefinition port in definition.Ports)
            {
                port.TypeId.ShouldBe(BuiltInPortTypeIds.ImageFrame);
                port.Multiplicity.ShouldBe(PortMultiplicity.Single);
                port.IsOptional.ShouldBeFalse();
            }

            // A node that reads a file starts a workflow with an image, one that
            // writes a file ends one, and a transform does both under a name that
            // says what it produced.
            foreach (PortDefinition input in definition.Inputs)
            {
                input.Id.ShouldBe(OpenCvNodeIds.ImagePortId);
            }

            foreach (PortDefinition output in definition.Outputs)
            {
                output.Id.ShouldBeOneOf(
                    OpenCvNodeIds.ImagePortId,
                    OpenCvNodeIds.BlurredPortId,
                    OpenCvNodeIds.ResizedPortId,
                    OpenCvNodeIds.ConvertedPortId,
                    OpenCvNodeIds.CroppedPortId,
                    OpenCvNodeIds.ThresholdedPortId,
                    OpenCvNodeIds.ReducedPortId,
                    OpenCvNodeIds.EnlargedPortId,
                    OpenCvNodeIds.GradientPortId,
                    OpenCvNodeIds.EdgesPortId,
                    OpenCvNodeIds.ErodedPortId,
                    OpenCvNodeIds.DilatedPortId,
                    OpenCvNodeIds.MorphedPortId,
                    OpenCvNodeIds.DrawnPortId);
            }
        }
    }

    [Fact]
    public void Every_required_parameter_declares_a_default_unless_it_names_a_file()
    {
        Definitions.ShouldNotBeEmpty();

        foreach (NodeDefinition definition in Definitions)
        {
            foreach (ParameterDefinition parameter in definition.Parameters.Where(item => item.IsRequired))
            {
                if (parameter.Kind == ParameterKind.Path)
                {
                    // A file a run reads or writes has to be one the user chose, so
                    // the definition requires it and cannot invent a default.
                    parameter.DefaultValue.ShouldBeNull(
                        $"{definition.TypeId} requires the path '{parameter.Name}' but declares a default for it.");
                    continue;
                }

                parameter.DefaultValue.ShouldNotBeNull(
                    $"{definition.TypeId} requires '{parameter.Name}', so a node placed without a saved value "
                    + "has nothing to run with.");
            }
        }
    }

    [Fact]
    public void Only_the_file_backed_nodes_declare_a_path_parameter()
    {
        foreach (NodeDefinition definition in Definitions)
        {
            bool namesAFile = definition.Parameters.Any(parameter => parameter.Kind == ParameterKind.Path);

            namesAFile.ShouldBe(
                definition.TypeId == new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId)
                    || definition.TypeId == new NodeTypeId(OpenCvNodeIds.SaveImageTypeId),
                $"{definition.TypeId} names a file only if it reads one or writes one.");
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
