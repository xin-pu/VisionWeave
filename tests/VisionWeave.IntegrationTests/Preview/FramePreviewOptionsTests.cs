using Shouldly;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.IntegrationTests.Preview;

public sealed class FramePreviewOptionsTests
{
    [Fact]
    public void Validate_of_the_default_options_reports_nothing()
    {
        FramePreviewOptions.Default.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_of_a_pixel_area_at_each_bound_reports_nothing()
    {
        FramePreviewOptions smallest = FramePreviewOptions.Default with
        {
            MaxPixelArea = FramePreviewOptions.MinPixelArea,
        };
        FramePreviewOptions largest = FramePreviewOptions.Default with
        {
            MaxPixelArea = FramePreviewOptions.MaxPixelAreaLimit,
        };

        smallest.Validate().ShouldBeEmpty();
        largest.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void Validate_of_a_zero_pixel_area_reports_an_invalid_setting()
    {
        FramePreviewOptions options = FramePreviewOptions.Default with { MaxPixelArea = 0 };

        NodeDiagnostic diagnostic = options.Validate().ShouldHaveSingleItem();

        diagnostic.Code.ShouldBe(DiagnosticCodes.InvalidSetting);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldContain(nameof(FramePreviewOptions.MaxPixelArea));
    }

    [Fact]
    public void Validate_of_a_pixel_area_above_the_limit_reports_an_invalid_setting()
    {
        FramePreviewOptions options = FramePreviewOptions.Default with
        {
            MaxPixelArea = FramePreviewOptions.MaxPixelAreaLimit + 1,
        };

        options.Validate().ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.InvalidSetting);
    }
}
