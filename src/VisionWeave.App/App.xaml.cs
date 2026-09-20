using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VisionWeave.App.Commands;
using VisionWeave.App.Inspector;
using VisionWeave.App.Composition;
using VisionWeave.App.Notifications;
using VisionWeave.App.Preview;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A published build can be asked to prove it starts instead of opening a
        // window: the same composition, one native operation, one line, and a code.
        if (ReleaseCheck.IsRequested(e.Args))
        {
            Shutdown(ReleaseCheck.Run(Console.Out, AppContext.BaseDirectory));
            return;
        }

        HostStartup startup = HostStartup.Compose(AppContext.BaseDirectory);

        if (startup.Services is null)
        {
            using ILoggerFactory bootstrapFactory = LoggerFactory.Create(
                builder => builder.AddDebug().SetMinimumLevel(LogLevel.Information));
            ReportRejectedSettings(bootstrapFactory.CreateLogger<App>(), startup.Problems);
            Shutdown(1);
            return;
        }

        _services = startup.Services;

        ILogger logger = _services.GetRequiredService<ILoggerFactory>().CreateLogger<App>();
        LogSettings(logger, startup.Settings!);

        if (!startup.Succeeded)
        {
            ReportRejectedSettings(logger, startup.Problems);
            Shutdown(1);
            return;
        }

        EditorSession session = _services.GetRequiredService<EditorSession>();
        NodeDefinitionCatalog catalog = _services.GetRequiredService<NodeDefinitionCatalog>();
        ShellStatus status = _services.GetRequiredService<ShellStatus>();
        logger.LogInformation(
            "Started with the empty workflow document {DocumentId}; {NodeTypeCount} node types are available.",
            session.Document.Id,
            catalog.Definitions.Count);

        // The shell is composed here rather than resolved, because the view model is
        // built from objects the container already owns: the session, the catalog,
        // the validator the canvas judges a wire with, the status area, the commands
        // that open, write, and run a document in that session, and the managed
        // preview a run publishes.
        AsyncCommandBoundary boundary = _services.GetRequiredService<AsyncCommandBoundary>();
        var openDocument = new OpenDocumentCommand(boundary, session, status);
        var fileChooser = _services.GetRequiredService<IWorkflowFileChooser>();
        var saveDocument = new SaveDocumentCommand(boundary, session, status, fileChooser);

        MainWindow shell = new(
            new MainWindowViewModel(
                session,
                catalog,
                _services.GetRequiredService<WorkflowValidator>(),
                openDocument,
                saveDocument,
                _services.GetRequiredService<RunWorkflowCommand>(),
                _services.GetRequiredService<PreviewViewModel>(),
                fileChooser,
                new ShellPromptViewModel(),
                status,
                _services.GetRequiredService<IParameterPathChooser>()),
            _services.GetRequiredService<SnackbarNotificationPresenter>());

        MainWindow = shell;
        shell.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

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
