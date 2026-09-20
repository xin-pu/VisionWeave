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
/// <param name="TagLine">
/// The tags the definition carries, joined for the one line the entry shows them
/// on. They are drawn beside the name rather than kept behind the filter, because a
/// tag the user cannot see is a tag they cannot learn.
/// </param>
internal sealed record ShellCatalogueEntry(string TypeId, string DisplayName, string TagLine);

/// <summary>
/// One chip of the catalogue's tag filter: the word to narrow the catalogue by, or
/// the chip that clears the filter when the word is absent.
/// </summary>
/// <param name="Label">The word the chip shows.</param>
/// <param name="Tag">The tag choosing this chip toggles, or <see langword="null"/> to clear the filter.</param>
/// <param name="IsSelected">Whether this chip's tag is one of the tags in force.</param>
internal sealed record ShellCatalogueTag(string Label, string? Tag, bool IsSelected);
