namespace Relick.Core;

/// <summary>Official deck construction rules, shared by the deck builder and the server.</summary>
public static class DeckRules
{
    public const int MainDeckSize = 42;
    public const int AttackCount = 12;
    public const int MinSkills = 3;
    public const int MaxCopies = 2;
    public const int MaxTraps = 3;
    public const int MaxTrapCopies = 2;
    public const int MaxSide = 10;

    public static bool IsTrap(CardDefinition card) => card.CodeType == CodeType.Trap;

    /// <summary>Attack cards proper. A Trap that merely counts as an Attack in play is not one for deck building.</summary>
    public static bool IsAttack(CardDefinition card) => card.Kind == CardKind.Attack && !IsTrap(card);

    public static bool Compatible(CardDefinition card, string relikario) => card.Relikario == null || card.Relikario == relikario;

    public static string DefaultAttackArt(string relikario) =>
        RelikarioCatalog.Find(relikario)?.DefaultAttackArtId ?? CardCatalog.Attack.Id;

    /// <summary>Repairs older main decks that mixed Attack art or used the classic ID for Ironwing's UT art.</summary>
    public static List<string> NormalizeAttackArt(string relikario, IEnumerable<string> cards)
    {
        var defaultAttack = DefaultAttackArt(relikario);
        return cards.Select(id => id == CardCatalog.Attack.Id ||
            (CardCatalog.All.TryGetValue(id, out var card) && IsAttack(card) && !Compatible(card, relikario))
            ? defaultAttack : id).ToList();
    }

    /// <summary>Whether a card may wait in the side deck: anything but an Attack.</summary>
    public static bool AllowedInSide(CardDefinition card) => !IsAttack(card);

    /// <summary>Returns every broken rule in Spanish; an empty list means the deck is legal.
    /// Copy limits count the side deck together with the deck its cards would be swapped into.</summary>
    public static List<string> Validate(string relikario, IReadOnlyList<string> cards, IReadOnlyList<string> traps, IReadOnlyList<string>? side = null)
    {
        side ??= [];
        var errors = new List<string>();
        var relikarioName = RelikarioCatalog.Playable.FirstOrDefault(r => r.Id == relikario)?.Name;
        if (relikarioName == null) errors.Add("El Relikario del mazo no existe.");

        var unknown = cards.Concat(traps).Concat(side).Where(id => !CardCatalog.All.ContainsKey(id)).Distinct().ToArray();
        if (unknown.Length > 0)
        {
            errors.Add("Cartas desconocidas: " + string.Join(", ", unknown) + ".");
            return errors;
        }
        var main = cards.Select(CardCatalog.Get).ToArray();
        var extra = traps.Select(CardCatalog.Get).ToArray();
        var bench = side.Select(CardCatalog.Get).ToArray();
        bool Benched(string name) => bench.Any(card => card.Name == name);

        if (main.Length != MainDeckSize)
            errors.Add($"El mazo principal debe tener exactamente {MainDeckSize} cartas (tiene {main.Length}).");
        if (main.Any(IsTrap))
            errors.Add("Los Códigos Trampa van en su propio mazo, no en el principal.");
        var incompatible = main.Where(card => !Compatible(card, relikario)).Select(card => card.Name).Distinct().ToArray();
        if (incompatible.Length > 0)
            errors.Add($"No son compatibles con {relikarioName ?? "este Relikario"}: " + string.Join(", ", incompatible) + ".");

        var attacks = main.Count(IsAttack);
        if (attacks != AttackCount)
            errors.Add($"Debe incluir exactamente {AttackCount} cartas de Ataque (tiene {attacks}).");
        var skills = main.Count(card => card.Kind == CardKind.Skill && Compatible(card, relikario));
        if (skills < MinSkills)
            errors.Add($"Debe incluir al menos {MinSkills} habilidades compatibles (tiene {skills}).");

        // Copies are counted by printed name so alternate art of one card shares the limit.
        foreach (var group in main.Concat(bench).Where(card => !IsAttack(card) && !IsTrap(card)).GroupBy(card => card.Name).Where(group => group.Count() > MaxCopies))
            errors.Add($"Máximo {MaxCopies} copias de {group.Key}{(Benched(group.Key) ? " entre el mazo y el banquillo" : "")} (tiene {group.Count()}).");

        if (extra.Any(card => !IsTrap(card)))
            errors.Add("El mazo de Códigos Trampa solo admite Códigos Trampa.");
        if (extra.Length > MaxTraps)
            errors.Add($"El mazo de Códigos Trampa admite hasta {MaxTraps} cartas (tiene {extra.Length}).");
        foreach (var group in extra.Concat(bench.Where(IsTrap)).GroupBy(card => card.Name).Where(group => group.Count() > MaxTrapCopies))
            errors.Add($"Máximo {MaxTrapCopies} copias de {group.Key} {(Benched(group.Key) ? "entre el mazo de Códigos Trampa y el banquillo" : "en el mazo de Códigos Trampa")} (tiene {group.Count()}).");

        if (bench.Length > MaxSide)
            errors.Add($"El banquillo admite hasta {MaxSide} cartas (tiene {bench.Length}).");
        if (bench.Any(IsAttack))
            errors.Add("El banquillo no admite cartas de Ataque.");
        var benchIncompatible = bench.Where(card => !Compatible(card, relikario)).Select(card => card.Name).Distinct().ToArray();
        if (benchIncompatible.Length > 0)
            errors.Add($"El banquillo tiene cartas no compatibles con {relikarioName ?? "este Relikario"}: " + string.Join(", ", benchIncompatible) + ".");
        return errors;
    }
}
