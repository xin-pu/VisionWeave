namespace VisionWeave.App.ViewModels;

/// <summary>
/// One category of the node catalogue and the node types it holds. The catalogue
/// region presents intent only — a category here says what exists, and adding a
/// node to a document stays an application command.
/// </summary>
/// <param name="Category">The category the definitions declare.</param>
/// <param name="NodeNames">The display names of the definitions in that category, in order.</param>
internal sealed record ShellCatalogueGroup(string Category, IReadOnlyList<string> NodeNames);
