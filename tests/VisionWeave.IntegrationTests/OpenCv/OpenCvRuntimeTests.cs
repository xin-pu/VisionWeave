using OpenCvSharp;
using Shouldly;

namespace VisionWeave.IntegrationTests.OpenCv;

public sealed class OpenCvRuntimeTests
{
    [Fact]
    public void Mat_when_created_has_expected_dimensions()
    {
        using var image = new Mat(2, 3, MatType.CV_8UC1, Scalar.Black);

        image.Rows.ShouldBe(2);
        image.Cols.ShouldBe(3);
    }
}
