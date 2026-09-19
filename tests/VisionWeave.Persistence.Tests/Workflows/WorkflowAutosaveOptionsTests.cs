using Shouldly;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.Persistence.Tests.Workflows;

public sealed class WorkflowAutosaveOptionsTests
{
    [Fact]
    public void Validate_of_the_default_options_reports_nothing()
    {
        WorkflowAutosaveOptions.Default.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_of_an_interval_at_each_bound_reports_nothing()
    {
        WorkflowAutosaveOptions shortest = WorkflowAutosaveOptions.Default with
        {
            Interval = WorkflowAutosaveOptions.MinInterval,
        };
        WorkflowAutosaveOptions longest = WorkflowAutosaveOptions.Default with
        {
            Interval = WorkflowAutosaveOptions.MaxInterval,
        };

        shortest.Validate().ShouldBeEmpty();
        longest.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_of_an_interval_below_the_minimum_while_enabled_reports_an_invalid_setting()
    {
        WorkflowAutosaveOptions options = WorkflowAutosaveOptions.Default with
        {
            Interval = WorkflowAutosaveOptions.MinInterval - TimeSpan.FromMilliseconds(1),
        };

        NodeDiagnostic diagnostic = options.Validate().ShouldHaveSingleItem();

        diagnostic.Code.ShouldBe(DiagnosticCodes.InvalidSetting);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldContain(nameof(WorkflowAutosaveOptions.Interval));
    }

    [Fact]
    public void Validate_of_an_interval_above_the_maximum_while_enabled_reports_an_invalid_setting()
    {
        WorkflowAutosaveOptions options = WorkflowAutosaveOptions.Default with
        {
            Interval = WorkflowAutosaveOptions.MaxInterval + TimeSpan.FromSeconds(1),
        };

        options.Validate().ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidSetting);
    }

    [Fact]
    public void Validate_of_an_unusable_interval_while_disabled_reports_nothing()
    {
        WorkflowAutosaveOptions options = new()
        {
            Enabled = false,
            Interval = TimeSpan.Zero,
        };

        options.Validate().ShouldBeEmpty();
    }
}
