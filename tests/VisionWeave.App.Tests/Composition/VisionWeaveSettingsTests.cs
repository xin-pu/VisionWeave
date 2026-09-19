using Shouldly;
using VisionWeave.App.Composition;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Tests.Composition;

public sealed class VisionWeaveSettingsTests : IDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"visionweave-settings-{Guid.NewGuid():N}");

    public VisionWeaveSettingsTests() => System.IO.Directory.CreateDirectory(_directory);

    [Fact]
    public void Load_missing_deployment_file_returns_a_safe_diagnostic()
    {
        SettingsLoadResult result = VisionWeaveSettings.Load(_directory);

        result.Succeeded.ShouldBeFalse();
        result.Settings.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidSetting);
    }

    [Fact]
    public void Load_malformed_json_returns_a_safe_diagnostic()
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(_directory, "appsettings.json"), "{ invalid");

        SettingsLoadResult result = VisionWeaveSettings.Load(_directory);

        result.Succeeded.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidSetting);
    }

    [Fact]
    public void Load_unbindable_value_returns_a_safe_diagnostic()
    {
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(_directory, "appsettings.json"),
            """
            {
              "VisionWeave": {
                "Execution": {
                  "CacheBudgetBytes": "not-a-number"
                }
              }
            }
            """);

        SettingsLoadResult result = VisionWeaveSettings.Load(_directory);

        result.Succeeded.ShouldBeFalse();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidSetting);
    }

    [Fact]
    public void Load_valid_file_preserves_defaults_for_omitted_values()
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(_directory, "appsettings.json"), "{}");

        SettingsLoadResult result = VisionWeaveSettings.Load(_directory);

        result.Succeeded.ShouldBeTrue();
        result.Settings.ShouldBe(VisionWeaveSettings.Default);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_directory))
        {
            System.IO.Directory.Delete(_directory, recursive: true);
        }
    }
}
