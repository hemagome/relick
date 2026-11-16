using Relick.Core;

namespace Relick.Network.Types;

// The server keeps each deck's sleeve and die in its own view (my_deck_styles), so servers that predate styles keep
// the original my_decks row. The client joins them here; these properties are never part of the row.
public sealed partial class DeckInfo
{
    public string Sleeve { get; set; } = CosmeticCatalog.DefaultSleeve;
    public string Dice { get; set; } = CosmeticCatalog.DefaultDice;
}
