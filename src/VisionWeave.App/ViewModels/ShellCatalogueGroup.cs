namespace VisionWeave.App.ViewModels;

/// <summary>
/// One category of the node catalogue and the node types it holds. The catalogue
/// region presents intent only — a category here says what exists, and adding a
/// node to a document stays an application command — so an entry names a type and
/// the canvas resolves what may be added from the catalog.
/// </summary>
/// <param name="Category">The category the definitions declare.</param>
/// <param name="Entries">The addable types in that category, in display order.</param>
internal sealed record ShellCatalogueGroup(string Category, IReadOnlyList<ShellCatalogueEntry> Entries);

/// <summary>
/// One addable node type in the catalogue.
/// </summary>
/// <param name="TypeId">The provider-qualified type identifier an add command names.</param>
/// <param name="DisplayName">The name the catalogue shows for it.</param>
internal sealed record ShellCatalogueEntry(string TypeId, string DisplayName);
