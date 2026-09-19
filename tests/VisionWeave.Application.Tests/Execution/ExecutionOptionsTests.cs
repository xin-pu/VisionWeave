using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Tests.Execution;

public sealed class ExecutionOptionsTests
{
    [Fact]
    public void Validate_of_the_default_options_reports_nothing()
    {
        ExecutionOptions.Default.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_of_zero_parallelism_reports_an_invalid_setting()
    {
        ExecutionOptions options = ExecutionOptions.Default with { MaxDegreeOfParallelism = 0 };

        NodeDiagnostic diagnostic = options.Validate().ShouldHaveSingleItem();

        diagnostic.Code.ShouldBe(DiagnosticCodes.InvalidSetting);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldContain(nameof(ExecutionOptions.MaxDegreeOfParallelism));
        diagnostic.NodeInstanceId.ShouldBeNull();
    }

    [Fact]
    public void Validate_of_parallelism_above_the_limit_reports_an_invalid_setting()
    {
        ExecutionOptions options = ExecutionOptions.Default with
        {
            MaxDegreeOfParallelism = ExecutionOptions.MaxDegreeOfParallelismLimit + 1,
        };

        options.Validate().ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidSetting);
    }

    [Fact]
    public void Validate_of_parallelism_at_the_limit_reports_nothing()
    {
        ExecutionOptions options = ExecutionOptions.Default with
        {
            MaxDegreeOfParallelism = ExecutionOptions.MaxDegreeOfParallelismLimit,
        };

        options.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_of_a_negative_grace_period_reports_an_invalid_setting()
    {
        ExecutionOptions options = ExecutionOptions.Default with
        {
            CancellationGracePeriod = TimeSpan.FromSeconds(-1),
        };

        options.Validate().ShouldHaveSingleItem().Message
            .ShouldContain(nameof(ExecutionOptions.CancellationGracePeriod));
    }

    [Fact]
    public void Validate_of_a_grace_period_above_the_maximum_reports_an_invalid_setting()
    {
        ExecutionOptions options = ExecutionOptions.Default with
        {
            CancellationGracePeriod = ExecutionOptions.MaxCancellationGracePeriod + TimeSpan.FromSeconds(1),
        };

        options.Validate().ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidSetting);
    }

    [Fact]
    public void Validate_of_a_negative_cache_budget_reports_an_invalid_setting()
    {
        ExecutionOptions options = ExecutionOptions.Default with { CacheBudgetBytes = -1 };

        options.Validate().ShouldHaveSingleItem().Message
            .ShouldContain(nameof(ExecutionOptions.CacheBudgetBytes));
    }

    [Fact]
    public void Validate_of_a_zero_cache_budget_reports_nothing_because_it_disables_caching()
    {
        ExecutionOptions options = ExecutionOptions.Default with { CacheBudgetBytes = 0 };

        options.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_of_several_invalid_values_reports_them_in_declaration_order()
    {
        ExecutionOptions options = new()
        {
            MaxDegreeOfParallelism = 0,
            CancellationGracePeriod = TimeSpan.FromDays(1),
            CacheBudgetBytes = -1,
        };

        options.Validate()
            .Select(diagnostic => diagnostic.Message)
            .ShouldBe(
            [
                $"Setting '{nameof(ExecutionOptions)}.{nameof(ExecutionOptions.MaxDegreeOfParallelism)}' is 0, but it must be a count between 1 and 1024.",
                $"Setting '{nameof(ExecutionOptions)}.{nameof(ExecutionOptions.CancellationGracePeriod)}' is 1.00:00:00, but it must be a duration between 00:00:00 and 00:05:00.",
                $"Setting '{nameof(ExecutionOptions)}.{nameof(ExecutionOptions.CacheBudgetBytes)}' is -1, but it must be a byte count between 0 and 68719476736.",
            ]);
    }
}
