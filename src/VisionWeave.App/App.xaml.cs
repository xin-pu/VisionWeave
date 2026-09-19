using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SettingsLoadResult load = VisionWeaveSettings.Load(AppContext.BaseDirectory);
        if (!load.Succeeded)
        {
            using ILoggerFactory bootstrapFactory = LoggerFactory.Create(
                builder => builder.AddDebug().SetMinimumLevel(LogLevel.Information));
            ReportRejectedSettings(bootstrapFactory.CreateLogger<App>(), load.Diagnostics);
            Shutdown(1);
            return;
        }

        VisionWeaveSettings settings = load.Settings!;
        IReadOnlyList<NodeDiagnostic> problems = settings.Validate();

        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Information));
        services.AddVisionWeave(settings);
        _services = services.BuildServiceProvider();

        ILogger logger = _services.GetRequiredService<ILoggerFactory>().CreateLogger<App>();
        LogSettings(logger, settings);

        if (problems.Count > 0)
        {
            ReportRejectedSettings(logger, problems);
            Shutdown(1);
            return;
        }

        WorkflowSession session = WorkflowSession.New("Untitled", _services.GetRequiredService<TimeProvider>());
        NodeDefinitionCatalog catalog = _services.GetRequiredService<NodeDefinitionCatalog>();
        logger.LogInformation(
            "Started with the empty workflow document {DocumentId}; {NodeTypeCount} node types are available.",
            session.Document.Id,
            catalog.Definitions.Count);

        MainWindow shell = new(new MainWindowViewModel(session, catalog, OpenDocumentCommand()));
        MainWindow = shell;
        shell.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Creates the command the shell opens documents with.
    /// </summary>
    private OpenDocumentCommand OpenDocumentCommand()
        => new(
            _services!.GetRequiredService<AsyncCommandBoundary>(),
            _services!.GetRequiredService<IDocumentLoader>());

    private static void LogSettings(ILogger logger, VisionWeaveSettings settings)
    {
        logger.LogInformation(
            "Execution settings: {MaxDegreeOfParallelism} node(s) in parallel, {CancellationGracePeriod} cancellation grace, {CacheBudgetBytes} byte cache budget.",
            settings.Execution.MaxDegreeOfParallelism,
            settings.Execution.CancellationGracePeriod,
            settings.Execution.CacheBudgetBytes);
        logger.LogInformation(
            "Preview settings: {MaxPixelArea} pixel preview bound.",
            settings.Preview.MaxPixelArea);
        logger.LogInformation(
            "Autosave settings: enabled {AutosaveEnabled}, interval {AutosaveInterval}.",
            settings.Autosave.Enabled,
            settings.Autosave.Interval);
    }

    /// <summary>
    /// Refuses to start rather than running with a setting the user wrote and the
    /// runtime cannot honor: a silently substituted limit would only resurface as
    /// behaviour nobody can explain from the file they edited.
    /// </summary>
    private static void ReportRejectedSettings(ILogger logger, IReadOnlyList<NodeDiagnostic> problems)
    {
        foreach (NodeDiagnostic problem in problems)
        {
            logger.LogError(
                "Rejected setting: {DiagnosticCode} {DiagnosticMessage}",
                problem.Code,
                problem.Message);
        }

        logger.LogError(
            "Startup stopped because {ProblemCount} setting(s) were rejected.",
            problems.Count);

        string details = string.Join(
            Environment.NewLine,
            problems.Select(problem => $"{problem.Code}  {problem.Message}"));

        MessageBox.Show(
            $"VisionWeave cannot start because {problems.Count} setting(s) were rejected:{Environment.NewLine}{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}Correct them in appsettings.json and start again.",
            "VisionWeave",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
