using System.Text;
using OpenCvSharp;
using Shouldly;
using VisionWeave.OpenCv.Frames;

namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// Reads a frame back as a picture of what a node marked: a full stop for a zero and
/// a hash for anything else. A node that marks a shape writes only the pixels its
/// shape covers on a frame that starts dark, so the picture is the smallest way to
/// state the whole result of one, rasterisation and clipping included.
/// </summary>
internal static class MarkedFrame
{
    /// <summary>
    /// Compares the frame with the picture the test states. The picture is written in
    /// a raw string literal, whose rows end the way the file's lines do, so both sides
    /// are compared with one newline between the rows.
    /// </summary>
    /// <param name="frame">The lease whose frame is read.</param>
    /// <param name="expected">The expected picture.</param>
    internal static void ShouldBe(MatFrameLease frame, string expected)
    {
        ArgumentNullException.ThrowIfNull(frame);

        // The pixels are read through a clone of the frame, because the buffer the
        // lease holds belongs to the lease.
        using Mat pixels = frame.CloneWritable();

        Of(pixels).ShouldBe(expected.ReplaceLineEndings("\n"));
    }

    private static string Of(Mat frame)
    {
        int rows = frame.Rows;
        int cols = frame.Cols;
        var lines = new List<string>(rows);

        for (int row = 0; row < rows; row++)
        {
            var line = new StringBuilder(cols);

            for (int column = 0; column < cols; column++)
            {
                line.Append(frame.At<byte>(row, column) == 0 ? '.' : '#');
            }

            lines.Add(line.ToString());
        }

        return string.Join('\n', lines);
    }
}
