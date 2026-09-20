using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.App.Composition;

/// <summary>
/// What a published build answers when it is asked to prove it starts:
/// <c>VisionWeave.App.exe --check</c> composes the host the way startup does,
/// resolves the catalog, runs one node through OpenCV's native entry point, writes
/// one line, and exits with a code instead of opening a window.
/// <para>
/// A folder that was copied is not yet a folder that runs, and a CI runner cannot
/// see a window. This is the entry point's own path through composition, so a
/// settings file, a catalog contribution, or a native library that did not travel
/// with the publish fails here, once, instead of in front of a user.
/// </para>
/// </summary>
internal static class ReleaseCheck
{
    /// <summary>
    /// The switch a published build is asked with.
    /// </summary>
    internal const string Argument = "--check";

    /// <summary>
    /// Determines whether the command line asks for the release check.
    /// </summary>
    /// <param name="arguments">The arguments the application was started with.</param>
    /// <returns><see langword="true"/> when the check was requested.</returns>
    internal static bool IsRequested(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return arguments.Any(argument => string.Equals(argument, Argument, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Composes the host and runs one native operation, writing exactly one line.
    /// </summary>
    /// <param name="output">The writer the line is written to.</param>
    /// <param name="baseDirectory">The folder that holds the settings files.</param>
    /// <returns>The exit code the application ends with.</returns>
    internal static int Run(TextWriter output, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(output);

        HostStartup startup = HostStartup.Compose(baseDirectory);

        if (startup.Services is null || !startup.Succeeded)
        {
            return Report(output, ReleaseCheckExitCode.SettingsRejected, Describe(startup.Problems));
        }

        using ServiceProvider services = startup.Services;

        CheckOutcome outcome;

        try
        {
            outcome = RunOperation(services);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The first call into a native library that did not travel with the
            // publish throws here, and that is the failure this check turns into a
            // line and a code rather than a window that never appears.
            outcome = CheckOutcome.Failed($"{exception.GetType().Name}: {exception.Message}");
        }

        return Report(output, outcome.Code, outcome.Detail);
    }

    /// <summary>
    /// Runs the node this build is least likely to ship without: a threshold over a
    /// frame the check wrote itself, read back through the preview converter, which
    /// is the only path from a native frame to pixels a test of the host can read.
    /// </summary>
    private static CheckOutcome RunOperation(ServiceProvider services)
    {
        NodeDefinitionCatalog catalog = services.GetRequiredService<NodeDefinitionCatalog>();
        INodeExecutorResolver resolver = services.GetRequiredService<INodeExecutorResolver>();
        ILeaseLedger ledger = services.GetRequiredService<ILeaseLedger>();
        FramePreviewConverter converter = services.GetRequiredService<FramePreviewConverter>();

        var typeId = new NodeTypeId(OpenCvNodeIds.ThresholdTypeId);

        if (!catalog.TryResolveLatest(typeId, out NodeDefinition? definition) || definition is null)
        {
            return CheckOutcome.Failed($"the catalog does not hold '{OpenCvNodeIds.ThresholdTypeId}'.");
        }

        if (!resolver.TryResolve(definition.ExecutorTypeId, out INodeExecutor? executor) || executor is null)
        {
            return CheckOutcome.Failed($"no executor is registered for '{definition.ExecutorTypeId}'.");
        }

        var source = new Mat(2, 2, MatType.CV_8UC1, Scalar.All(200));
        MatFrameLease input = MatFrameLease.Create(source, ledger);
        MatFrameLease? produced = null;
        var resources = new ExecutionResourceScope();
        byte sample;

        try
        {
            var request = new NodeExecutionRequest
            {
                NodeTypeId = typeId,
                TypeVersion = definition.TypeVersion,
                NodeInstanceId = Guid.NewGuid(),
                OperationId = Guid.NewGuid(),
                Parameters = new NodeParameterSet(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.ThresholdParameter] = 128d,
                    [OpenCvNodeIds.MaxValueParameter] = 255d,
                }),
                Inputs = new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.ImagePortId] = new ImageFrameValue(input),
                },
                Resources = resources,
            };

            NodeExecutionResult result = executor
                .ExecuteAsync(request, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (result.Status != NodeExecutionStatus.Succeeded)
            {
                return CheckOutcome.Failed(
                    $"the threshold node reported {result.Status}: {Describe(result.Diagnostics)}");
            }

            if (!result.Outputs.TryGetValue(OpenCvNodeIds.ThresholdedPortId, out PortValue? value)
                || value is not ImageFrameValue frame
                || frame.Lease is not MatFrameLease lease)
            {
                return CheckOutcome.Failed(
                    $"the threshold node published no frame on '{OpenCvNodeIds.ThresholdedPortId}'.");
            }

            produced = lease;
            PreviewFrame preview = converter.Convert(lease);
            sample = preview.Pixels.Length > 0 ? preview.Pixels[0] : (byte)0;

            if (sample != byte.MaxValue)
            {
                return CheckOutcome.Failed(
                    $"thresholding a frame of 200 published {sample} instead of {byte.MaxValue}.");
            }
        }
        finally
        {
            produced?.Dispose();
            input.Dispose();
            resources.Dispose();
        }

        // The frames this check created are the only ones the ledger ever saw, so a
        // frame still outstanding here is a lease the published build cannot release.
        return ledger.Outstanding == 0
            ? CheckOutcome.Passed(
                $"the catalog holds {catalog.Definitions.Count} node types, thresholding a 2 by 2 frame of 200 published {sample}, "
                + "and every frame it created was released.")
            : CheckOutcome.Failed($"the run left {ledger.Outstanding} frame lease(s) outstanding.");
    }

    /// <summary>
    /// Writes the one line this check reports and answers with the code it carries.
    /// </summary>
    private static int Report(TextWriter output, ReleaseCheckExitCode code, string detail)
    {
        string state = code == ReleaseCheckExitCode.Passed ? "passed" : "failed";

        output.WriteLine($"VisionWeave {Version()} check {state}: {detail}");
        output.Flush();

        return (int)code;
    }

    private static string Describe(IReadOnlyList<NodeDiagnostic> diagnostics)
        => diagnostics.Count == 0
            ? "no diagnostic was reported."
            : string.Join("; ", diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Message}"));

    private static string Version()
    {
        string version = typeof(ReleaseCheck).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ReleaseCheck).Assembly.GetName().Version?.ToString()
            ?? "unknown";

        // A source-linked build appends the revision to the informational version,
        // and the folder this package came from is named after the version alone.
        int metadata = version.IndexOf('+', StringComparison.Ordinal);

        return metadata < 0 ? version : version[..metadata];
    }

    private readonly record struct CheckOutcome(ReleaseCheckExitCode Code, string Detail)
    {
        internal static CheckOutcome Passed(string detail) => new(ReleaseCheckExitCode.Passed, detail);

        internal static CheckOutcome Failed(string detail) => new(ReleaseCheckExitCode.OperationFailed, detail);
    }
}
