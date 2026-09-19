namespace VisionWeave.App.Tests.Support;

/// <summary>
/// A workflow document that remembers the ports of a node type this build does not
/// provide, which is what a document saved by a build that knew the type carries.
/// It is written as text rather than through the document model, because the whole
/// point is a file that holds something this build cannot produce.
/// </summary>
internal static class DocumentRememberingPorts
{
    /// <summary>The node this build can resolve, which publishes the wire's source.</summary>
    internal static readonly Guid BlurNodeId = Guid.Parse("1b2c3d4e-5f60-4a7b-8c9d-0e1f2a3b4c5d");

    /// <summary>The node whose type this build does not provide.</summary>
    internal static readonly Guid UnknownNodeId = Guid.Parse("6d7e8f90-1a2b-4c3d-9e4f-5a6b7c8d9e0f");

    /// <summary>
    /// Writes the document to a path. One remembered port carries no label, which is
    /// how a document records a port whose type never named it.
    /// </summary>
    /// <param name="path">The file to write.</param>
    internal static void Write(string path)
        => System.IO.File.WriteAllText(path, Json);

    private const string Json = """
        {
          "schemaVersion": 1,
          "documentId": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
          "name": "remembers ports",
          "createdUtc": "2026-09-19T10:00:00+00:00",
          "modifiedUtc": "2026-09-19T10:00:00+00:00",
          "revision": 2,
          "nodes": [
            {
              "id": "1b2c3d4e-5f60-4a7b-8c9d-0e1f2a3b4c5d",
              "typeId": "visionweave.opencv.gaussian-blur",
              "typeVersion": 1,
              "layout": { "x": 0, "y": 0 }
            },
            {
              "id": "6d7e8f90-1a2b-4c3d-9e4f-5a6b7c8d9e0f",
              "typeId": "visionweave.missing.enhance",
              "typeVersion": 4,
              "portSchemaSnapshot": [
                { "portId": "image", "direction": "Input", "displayName": "Image" },
                { "portId": "result", "direction": "Output" }
              ],
              "layout": { "x": 200, "y": 0 }
            }
          ],
          "connections": [
            {
              "id": "0a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d",
              "sourceNodeId": "1b2c3d4e-5f60-4a7b-8c9d-0e1f2a3b4c5d",
              "sourcePortId": "blurred",
              "targetNodeId": "6d7e8f90-1a2b-4c3d-9e4f-5a6b7c8d9e0f",
              "targetPortId": "image"
            }
          ]
        }
        """;
}
