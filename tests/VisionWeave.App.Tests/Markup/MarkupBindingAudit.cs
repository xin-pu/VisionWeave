using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace VisionWeave.App.Tests.Markup;

/// <summary>
/// Watches a window the shell smoke test built for the data-binding faults WPF reports,
/// and inspects the tree for the one fault it reports quietly. It lives here rather than
/// in a test of its own because WPF allows one <see cref="System.Windows.Application"/> per
/// process, so the smoke test is the only place a real window exists.
/// <para>
/// Two faults are covered. WPF reports a binding whose path resolves to nothing as a
/// trace message and then leaves the target at its default, which is how a region goes
/// blank without anything failing. And a control drawn from a data template is templated
/// by its item container, so a template trigger that binds one of the control's own
/// properties through the templated parent binds it against the container instead: a
/// property the container does not declare never applies and never reports.
/// </para>
/// </summary>
internal sealed class MarkupBindingAudit : IDisposable
{
    private readonly RecordingTraceListener _listener;

    private MarkupBindingAudit(RecordingTraceListener listener) => _listener = listener;

    /// <summary>
    /// Starts listening. It is called before the window under audit is built, because a
    /// binding that resolves to nothing reports the moment it is activated.
    /// </summary>
    internal static MarkupBindingAudit Start()
    {
        var listener = new RecordingTraceListener();

        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

        return new MarkupBindingAudit(listener);
    }

    /// <summary>
    /// Every fault the window has reported since the audit started, and every hazard its
    /// tree holds now, so an empty result is the shell having resolved what it declares.
    /// </summary>
    /// <param name="window">The window to read, which must be built and shown.</param>
    internal IReadOnlyList<string> Faults(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Bindings activate while the window is laid out, so the audit lays it out again
        // rather than trusting that the caller's own pass already covered every element.
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.UpdateLayout();

        return [.. _listener.Messages, .. PathErrors(window), .. TemplateParentHazards(window)];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Off;
    }

    /// <summary>
    /// Finds every control drawn from a data template whose template binds one of the
    /// control's own properties through the templated parent, which is the state a
    /// framework control's pressed or hovered look silently fails in.
    /// </summary>
    private static List<string> TemplateParentHazards(DependencyObject root)
    {
        List<string> lines = [];

        foreach (FrameworkElement element in Descendants<FrameworkElement>(root))
        {
            if (element is not Control control || element.TemplatedParent is not { } parent || control.Template is not { } template)
            {
                continue;
            }

            foreach (TriggerBase trigger in template.Triggers)
            {
                foreach (Setter setter in Setters(trigger))
                {
                    if (setter.Value is not Binding { Path.Path: { Length: > 0 } path } binding
                        || binding.RelativeSource?.Mode != RelativeSourceMode.TemplatedParent
                        || !IsPlainPropertyName(path)
                        || parent.GetType().GetProperty(path) is not null)
                    {
                        continue;
                    }

                    lines.Add(
                        $"{element.GetType().Name} drawn by a data template of '{parent.GetType().Name}': "
                        + $"{setter.Property?.OwnerType.Name}.{setter.Property?.Name} binds "
                        + $"TemplatedParent.{path}, which '{parent.GetType().Name}' does not declare");
                }
            }
        }

        return lines;
    }

    /// <summary>Reports a foreground binding that was attached and resolved to nothing.</summary>
    private static List<string> PathErrors(DependencyObject root)
    {
        List<string> lines = [];

        foreach (ButtonBase button in Descendants<ButtonBase>(root))
        {
            if (BindingOperations.GetBindingExpression(button, Control.ForegroundProperty) is { } binding
                && binding.Status == BindingStatus.PathError)
            {
                lines.Add(
                    $"PATH ERROR {button.GetType().Name} context={button.DataContext?.GetType().Name} "
                    + $"path={binding.ParentBinding.Path.Path}");
            }
        }

        return lines;
    }

    /// <summary>An attached property or a member path names something this check cannot resolve by name.</summary>
    private static bool IsPlainPropertyName(string path)
        => !path.Contains('(', StringComparison.Ordinal)
            && !path.Contains('.', StringComparison.Ordinal)
            && !path.Contains('[', StringComparison.Ordinal);

    private static IEnumerable<Setter> Setters(TriggerBase trigger)
        => trigger switch
        {
            Trigger typed => typed.Setters.OfType<Setter>(),
            DataTrigger typed => typed.Setters.OfType<Setter>(),
            MultiTrigger typed => typed.Setters.OfType<Setter>(),
            _ => [],
        };

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);

        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Collects what the binding trace source reported.</summary>
    private sealed class RecordingTraceListener : TraceListener
    {
        private readonly List<string> _messages = [];
        private readonly System.Text.StringBuilder _pending = new();

        internal List<string> Messages => [.. _messages];

        public override void Write(string? message) => _pending.Append(message);

        public override void WriteLine(string? message)
        {
            _pending.Append(message);
            _messages.Add(_pending.ToString());
            _pending.Clear();
        }
    }
}
