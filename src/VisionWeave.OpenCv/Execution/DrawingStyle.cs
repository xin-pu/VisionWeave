using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reads the style and the geometry a draw node marks with. The three nodes draw
/// different shapes and share the rest, so a colour means the same on all of them.
/// <para>
/// A colour is three numbers in the order a drawing call reads them, blue first. The
/// frame decides what they mean: one of three or four channels takes all three, and
/// one of a single channel takes the first, which is what a scalar means to such an
/// image rather than a rule this layer invents.
/// </para>
/// </summary>
internal static class DrawingStyle
{
    /// <summary>
    /// Reads the rectangle a draw node marks.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <param name="rectangle">The rectangle, when every parameter holds one.</param>
    /// <param name="failure">The diagnostic that names the parameter, when one does not.</param>
    /// <returns><see langword="true"/> when the parameters state a rectangle.</returns>
    internal static bool TryReadRectangle(
        NodeExecutionRequest request,
        out Rect rectangle,
        out NodeDiagnostic? failure)
    {
        if (!TryReadInteger(request, OpenCvNodeIds.XParameter, OpenCvParameterBounds.MinOrigin, OpenCvParameterBounds.MaxDimension, out int x, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.YParameter, OpenCvParameterBounds.MinOrigin, OpenCvParameterBounds.MaxDimension, out int y, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.WidthParameter, OpenCvParameterBounds.MinDimension, OpenCvParameterBounds.MaxDimension, out int width, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.HeightParameter, OpenCvParameterBounds.MinDimension, OpenCvParameterBounds.MaxDimension, out int height, out failure))
        {
            rectangle = default;
            return false;
        }

        rectangle = new Rect(x, y, width, height);
        return true;
    }

    /// <summary>
    /// Reads the two ends of the line a draw node marks.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <param name="start">The end the line starts at, when every parameter holds one.</param>
    /// <param name="end">The end the line reaches, when every parameter holds one.</param>
    /// <param name="failure">The diagnostic that names the parameter, when one does not.</param>
    /// <returns><see langword="true"/> when the parameters state a line.</returns>
    internal static bool TryReadLine(
        NodeExecutionRequest request,
        out Point start,
        out Point end,
        out NodeDiagnostic? failure)
    {
        if (!TryReadInteger(request, OpenCvNodeIds.StartXParameter, OpenCvParameterBounds.MinOrigin, OpenCvParameterBounds.MaxDimension, out int startX, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.StartYParameter, OpenCvParameterBounds.MinOrigin, OpenCvParameterBounds.MaxDimension, out int startY, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.EndXParameter, OpenCvParameterBounds.MinOrigin, OpenCvParameterBounds.MaxDimension, out int endX, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.EndYParameter, OpenCvParameterBounds.MinOrigin, OpenCvParameterBounds.MaxDimension, out int endY, out failure))
        {
            start = default;
            end = default;
            return false;
        }

        start = new Point(startX, startY);
        end = new Point(endX, endY);
        return true;
    }

    /// <summary>
    /// Reads the circle a draw node marks.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <param name="centre">The centre of the circle, when every parameter holds one.</param>
    /// <param name="radius">The radius of the circle, when every parameter holds one.</param>
    /// <param name="failure">The diagnostic that names the parameter, when one does not.</param>
    /// <returns><see langword="true"/> when the parameters state a circle.</returns>
    internal static bool TryReadCircle(
        NodeExecutionRequest request,
        out Point centre,
        out int radius,
        out NodeDiagnostic? failure)
    {
        if (!TryReadInteger(request, OpenCvNodeIds.XParameter, OpenCvParameterBounds.MinOrigin, OpenCvParameterBounds.MaxDimension, out int x, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.YParameter, OpenCvParameterBounds.MinOrigin, OpenCvParameterBounds.MaxDimension, out int y, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.RadiusParameter, OpenCvParameterBounds.MinRadius, OpenCvParameterBounds.MaxDimension, out int readRadius, out failure))
        {
            centre = default;
            radius = 0;
            return false;
        }

        centre = new Point(x, y);
        radius = readRadius;
        return true;
    }

    /// <summary>
    /// Reads the colour the node marks with.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <param name="colour">The colour, when every component holds a level.</param>
    /// <param name="failure">The diagnostic that names the parameter, when one does not.</param>
    /// <returns><see langword="true"/> when the parameters state a colour.</returns>
    internal static bool TryReadColour(
        NodeExecutionRequest request,
        out Scalar colour,
        out NodeDiagnostic? failure)
    {
        if (!TryReadInteger(request, OpenCvNodeIds.BlueParameter, OpenCvParameterBounds.MinLevel, OpenCvParameterBounds.MaxLevel, out int blue, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.GreenParameter, OpenCvParameterBounds.MinLevel, OpenCvParameterBounds.MaxLevel, out int green, out failure)
            || !TryReadInteger(request, OpenCvNodeIds.RedParameter, OpenCvParameterBounds.MinLevel, OpenCvParameterBounds.MaxLevel, out int red, out failure))
        {
            colour = default;
            return false;
        }

        // The fourth component is opaque, which only a frame that carries an alpha
        // channel reads and the call ignores on every other one.
        colour = new Scalar(blue, green, red, OpenCvParameterBounds.MaxLevel);
        return true;
    }

    /// <summary>
    /// Reads the width the node draws its outline with.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <param name="filled">Whether the shape is filled rather than outlined.</param>
    /// <param name="thickness">The width the call draws with, when the parameter holds one.</param>
    /// <param name="failure">The diagnostic that names the parameter, when it does not.</param>
    /// <returns><see langword="true"/> when the parameter is a usable width.</returns>
    internal static bool TryReadThickness(
        NodeExecutionRequest request,
        bool filled,
        out int thickness,
        out NodeDiagnostic? failure)
    {
        if (!TryReadInteger(
            request,
            OpenCvNodeIds.ThicknessParameter,
            OpenCvParameterBounds.MinThickness,
            OpenCvParameterBounds.MaxThickness,
            out int outline,
            out failure))
        {
            thickness = 0;
            return false;
        }

        // A width of minus one is how a drawing call fills a shape. It is derived
        // from the switch rather than set by the user, so that no parameter's value
        // says another parameter is unused.
        thickness = filled ? -1 : outline;
        return true;
    }

    /// <summary>
    /// Reads whether the node fills its shape rather than outlining it.
    /// </summary>
    /// <param name="request">The node request that carries the parameters.</param>
    /// <returns><see langword="true"/> when the shape is filled.</returns>
    internal static bool ReadFilled(NodeExecutionRequest request)
        => request.Parameters.Contains(OpenCvNodeIds.FilledParameter)
            && request.Parameters.GetBoolean(OpenCvNodeIds.FilledParameter);

    private static bool TryReadInteger(
        NodeExecutionRequest request,
        string parameter,
        double minimum,
        double maximum,
        out int value,
        out NodeDiagnostic? failure)
    {
        value = request.Parameters.GetInt32(parameter);

        if (value < minimum || value > maximum)
        {
            failure = FrameInput.Rejected(
                request,
                $"Parameter '{parameter}' must be between {minimum} and {maximum}, but it is {value}.");
            return false;
        }

        failure = null;
        return true;
    }
}
