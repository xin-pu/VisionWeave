using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionWeave.App.Presentation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;

namespace VisionWeave.App.Inspector;

/// <summary>
/// Presents one parameter of one node: what the definition declares about it, the
/// value the document holds or the default it falls back to, and the condition it
/// currently carries. It holds text, a switch, or an option — one projection per
/// declared kind, never all three at once — and it does not edit anything: the
/// inspector turns what the user committed here into an application command.
/// </summary>
internal sealed partial class ParameterEditorViewModel : ObservableObject
{
    private readonly ParameterDefinition _definition;

    /// <summary>
    /// True while the document's value is being written into the field, so a value
    /// the shell projects is never mistaken for the user committing one.
    /// </summary>
    private bool _projecting;

    /// <summary>
    /// Creates an editor for one parameter.
    /// </summary>
    /// <param name="definition">The definition that declares the parameter.</param>
    /// <param name="value">The value the document holds, or <see langword="null"/> when it holds none.</param>
    /// <param name="severity">The worst condition the parameter carries, or <see langword="null"/>.</param>
    /// <param name="condition">The condition that severity belongs to, or an empty string.</param>
    internal ParameterEditorViewModel(
        ParameterDefinition definition,
        object? value,
        DiagnosticSeverity? severity,
        string condition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(condition);

        _definition = definition;
        HasStoredValue = value is not null;
        Severity = severity;
        Condition = condition;

        Name = definition.Name;
        DisplayName = definition.DisplayName;
        Kind = definition.Kind;
        Options = definition.Options ?? [];
        IsBoolean = definition.Kind == ParameterKind.Boolean;
        HasOptions = Options.Count > 0;
        Caption = string.Empty;
        Summary = string.Empty;
        Reword();

        Project(value ?? definition.DefaultValue);
    }

    /// <summary>Gets the parameter name the definition declares and the document stores.</summary>
    public string Name { get; }

    /// <summary>Gets the label the inspector shows.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the declared kind, which decides what the row offers.</summary>
    public ParameterKind Kind { get; }

    /// <summary>Gets the values an option parameter accepts.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>Gets a value indicating whether the row offers a switch.</summary>
    public bool IsBoolean { get; }

    /// <summary>Gets a value indicating whether the row offers a list of options.</summary>
    public bool HasOptions { get; }

    /// <summary>
    /// Gets the line under the field: what the parameter is, the range it accepts,
    /// and the default it falls back to, so a stored value and a default are not
    /// mistaken for each other.
    /// </summary>
    public string Caption { get; private set; }

    /// <summary>Gets the worst condition this parameter carries, which also picks its colour.</summary>
    public DiagnosticSeverity? Severity { get; private set; }

    /// <summary>
    /// Gets the severity as a word, so a marked field says what is wrong with it
    /// rather than only colouring itself.
    /// </summary>
    public string SeverityWord
        => Severity is { } severity ? SeverityText.Of(severity) : string.Empty;

    /// <summary>Gets the condition this parameter carries, or an empty string.</summary>
    public string Condition { get; private set; }

    /// <summary>Gets what the field says about itself when the pointer rests on it.</summary>
    public string Summary { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the document holds a value of its own. The
    /// caption says which of the two the field is showing, so this is read whenever
    /// the document moves rather than only when the field is first built.
    /// </summary>
    public bool HasStoredValue { get; private set; }

    /// <summary>
    /// Gets or sets what the field asks when the user says the value is ready. The
    /// inspector sets it, so a committed field becomes an application command while
    /// the field itself still edits nothing and owns no rule about parameters.
    /// </summary>
    internal Action<ParameterEditorViewModel>? Commit { get; set; }

    /// <summary>
    /// Gets or sets the value as the text field shows and edits it. It is used by
    /// every kind except a switch and an option, which have a shape of their own.
    /// </summary>
    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>Gets or sets the switch, which is the projection a boolean uses.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    /// <summary>Gets or sets the chosen option, which is the projection an option uses.</summary>
    [ObservableProperty]
    public partial string? SelectedOption { get; set; }

    /// <summary>
    /// Applies what the user committed. It is the one act the field offers, and the
    /// markup reaches it with Enter while a switch and an option list reach it by the
    /// change itself, because a change to either of those is a whole gesture where
    /// typing a number is not.
    /// </summary>
    [RelayCommand]
    private void Apply() => Ask();

    partial void OnIsCheckedChanged(bool value) => Ask();

    partial void OnSelectedOptionChanged(string? value) => Ask();

    /// <summary>
    /// Asks for the value to be applied. A value the shell projected into the field is
    /// not the user committing one, so writing the document's value here never asks
    /// for an edit of it.
    /// </summary>
    private void Ask()
    {
        if (!_projecting)
        {
            Commit?.Invoke(this);
        }
    }

    /// <summary>
    /// Reads what the user left in the field as the value the document can store.
    /// The shape of a value is the editor's business — only the field knows the text
    /// a person typed — while the value's legality stays the validator's, so a text
    /// that is not a number is refused here and a number outside the declared range
    /// is committed and reported by the projection.
    /// </summary>
    /// <param name="value">The value to store, when the field holds one.</param>
    /// <param name="refusal">The condition that explains a field that holds no readable value.</param>
    /// <returns><see langword="true"/> when the field holds a value the document accepts.</returns>
    internal bool TryReadValue(out object? value, out NodeDiagnostic? refusal)
    {
        refusal = null;

        switch (Kind)
        {
            case ParameterKind.Boolean:
                value = IsChecked;
                return true;
            case ParameterKind.Option:
                if (string.IsNullOrEmpty(SelectedOption))
                {
                    refusal = Refused("no option is chosen");
                    value = null;
                    return false;
                }

                value = SelectedOption;
                return true;
            case ParameterKind.Integer:
                if (long.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                {
                    value = integer;
                    return true;
                }

                refusal = Refused("a whole number");
                value = null;
                return false;
            case ParameterKind.Number:
                if (double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    value = number;
                    return true;
                }

                refusal = Refused("a number");
                value = null;
                return false;
            default:
                value = Text;
                return true;
        }
    }

    /// <summary>
    /// Re-reads what the definition and the projection say about this parameter
    /// after the document moved. The value is refreshed as well unless the field is
    /// the one the user is editing, because rewriting the text under a caret would
    /// take the caret with it; whether the document holds a value of its own is
    /// re-read either way, because the caption says which of the two is on screen
    /// and a commit changes that.
    /// </summary>
    /// <param name="value">The value the document holds now.</param>
    /// <param name="severity">The worst condition the parameter carries now.</param>
    /// <param name="condition">The condition that severity belongs to.</param>
    /// <param name="refreshValue">Whether the value the user sees is replaced too.</param>
    internal void Refresh(object? value, DiagnosticSeverity? severity, string condition, bool refreshValue)
    {
        ArgumentNullException.ThrowIfNull(condition);

        Severity = severity;
        Condition = condition;
        HasStoredValue = value is not null;

        if (refreshValue)
        {
            Project(value ?? _definition.DefaultValue);
        }

        Reword();
        OnPropertyChanged(nameof(Severity));
        OnPropertyChanged(nameof(SeverityWord));
        OnPropertyChanged(nameof(Condition));
    }

    /// <summary>
    /// Marks the field with a condition the value currently in it earned. This is
    /// the field's own answer to text or to a choice the definition cannot accept,
    /// so nothing about the document is reported here and the value on screen stays
    /// exactly as the user left it.
    /// </summary>
    /// <param name="refusal">The condition that explains the field.</param>
    internal void Refuse(NodeDiagnostic refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);

        Severity = refusal.Severity;
        Condition = DiagnosticText.Of(refusal);

        Reword();
        OnPropertyChanged(nameof(Severity));
        OnPropertyChanged(nameof(SeverityWord));
        OnPropertyChanged(nameof(Condition));
    }

    /// <summary>
    /// Re-words the caption and the tooltip from the state the field holds now. The
    /// caption is what tells a stored value and a default apart, so a commit, an
    /// undo, and a refusal all have to re-word it rather than only re-read the value
    /// they carry.
    /// </summary>
    private void Reword()
    {
        Caption = Describe(_definition, HasStoredValue);
        Summary = Condition.Length == 0
            ? $"{DisplayName} ({Name}) · {Caption}"
            : $"{DisplayName} ({Name}) · {Caption}{Environment.NewLine}{Condition}";

        OnPropertyChanged(nameof(HasStoredValue));
        OnPropertyChanged(nameof(Caption));
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>
    /// Places a value into the projection its kind uses, so text, a switch, and an
    /// option never describe the same parameter at once.
    /// </summary>
    private void Project(object? value)
    {
        _projecting = true;
        try
        {
            ProjectInto(value);
        }
        finally
        {
            _projecting = false;
        }
    }

    /// <summary>Writes one value into the field its kind uses.</summary>
    /// <param name="value">The value to place in the field.</param>
    private void ProjectInto(object? value)
    {
        switch (Kind)
        {
            case ParameterKind.Boolean:
                IsChecked = value is bool flag && flag;
                break;
            case ParameterKind.Option:
                SelectedOption = value?.ToString();
                break;
            case ParameterKind.Integer:
                Text = value is null
                    ? string.Empty
                    : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0", CultureInfo.InvariantCulture);
                break;
            default:
                Text = value switch
                {
                    null => string.Empty,
                    double number => number.ToString(CultureInfo.InvariantCulture),
                    _ => value.ToString() ?? string.Empty,
                };
                break;
        }
    }

    /// <summary>
    /// Words a field that holds nothing the definition can accept. It names no
    /// document element, because no document condition is being reported: the text
    /// never reached the document.
    /// </summary>
    private NodeDiagnostic Refused(string expected)
        => new(
            DiagnosticCodes.InvalidParameterValue,
            DiagnosticSeverity.Error,
            $"Parameter '{Name}' accepts only {expected}, so the document was left unchanged.");

    /// <summary>
    /// Describes what the parameter is: its kind, the range it accepts, the options
    /// it offers, and the default it falls back to. A parameter the document stores
    /// a value for says so, because the value on screen is then the document's and
    /// not the definition's.
    /// </summary>
    private static string Describe(ParameterDefinition definition, bool stored)
    {
        List<string> parts = [KindWord(definition.Kind)];

        if (Range(definition) is { Length: > 0 } range)
        {
            parts.Add(range);
        }

        if (definition.Options is { Count: > 0 } options)
        {
            parts.Add(string.Join(", ", options));
        }

        if (!stored && definition.DefaultValue is not null)
        {
            parts.Add($"default {definition.DefaultValue}");
        }
        else if (!stored && definition.IsRequired)
        {
            parts.Add("no default");
        }

        // Typing is not a whole gesture the way toggling a switch or picking an option
        // is, so a field that has to be typed in says how it is applied rather than
        // leaving the user to find out that what they typed was never used.
        if (IsTyped(definition.Kind))
        {
            parts.Add("press Enter to apply");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Determines whether a kind is edited by typing rather than by a change of its own.</summary>
    /// <param name="kind">The declared kind.</param>
    /// <returns><see langword="true"/> when the field holds text the user applied.</returns>
    private static bool IsTyped(ParameterKind kind)
        => kind is not (ParameterKind.Boolean or ParameterKind.Option);

    private static string Range(ParameterDefinition definition)
        => (definition.Minimum, definition.Maximum) switch
        {
            ({ } minimum, { } maximum) => $"{Format(minimum)} to {Format(maximum)}",
            ({ } minimum, null) => $"at least {Format(minimum)}",
            (null, { } maximum) => $"at most {Format(maximum)}",
            _ => string.Empty,
        };

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string KindWord(ParameterKind kind)
        => kind switch
        {
            ParameterKind.Number => "number",
            ParameterKind.Integer => "whole number",
            ParameterKind.Boolean => "switch",
            ParameterKind.Option => "option",
            ParameterKind.Path => "path",
            _ => "text",
        };
}
