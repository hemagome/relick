using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    [Table(Accessor = "InitialSkill")]
    public partial struct InitialSkill
    {
        [PrimaryKey] public Identity Player;
        public string Definition;
        public string CommandId;
    }

    [SpacetimeDB.Type]
    public partial struct InitialSkillInfo
    {
        public bool Selected;
        public string Chosen;
        public List<string> Options;
    }

    [View(Accessor = "MyInitialSkill", Public = true)]
    public static InitialSkillInfo? MyInitialSkill(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me ||
            ctx.Db.Room.Code.Find(me.RoomCode) is not Room room || !room.Dealt) return null;
        var choice = ctx.Db.InitialSkill.Player.Find(ctx.Sender);
        var cards = ctx.Db.Card.Owner.Filter(ctx.Sender).ToList();
        // Existing prepared rooms keep their hands; a deployment never redeals them.
        var selected = choice != null || cards.Any(c => c.InHand) || ctx.Db.Opening.RoomCode.Find(me.RoomCode) != null;
        return new InitialSkillInfo { Selected = selected, Chosen = choice?.Definition ?? "",
            Options = selected ? new() : cards.Where(c => CardCatalog.Get(c.Definition).Kind == CardKind.Skill)
                .Select(c => c.Definition).Distinct().Order(StringComparer.Ordinal).ToList() };
    }

    [Reducer]
    public static void ChooseInitialSkill(ReducerContext ctx, string roomCode, string definition, string commandId)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me || me.RoomCode != roomCode)
            throw new Exception("No perteneces a esta sala.");
        if (definition.Length > 128 || !Guid.TryParseExact(commandId, "N", out _)) throw new Exception("Elección de habilidad inválida.");
        if (ctx.Db.InitialSkill.Player.Find(ctx.Sender) is InitialSkill previous)
        {
            if (previous.CommandId == commandId && previous.Definition == definition) return;
            throw new Exception("Ya elegiste tu habilidad inicial.");
        }
        if (ctx.Db.Room.Code.Find(roomCode) is not Room room || !room.Dealt || ctx.Db.Opening.RoomCode.Find(roomCode) != null ||
            ctx.Db.Preparation.Player.Find(ctx.Sender) != null) throw new Exception("No puedes elegir una habilidad ahora.");
        var cards = ctx.Db.Card.Owner.Filter(ctx.Sender).OrderBy(c => c.Position).ToList();
        if (cards.Any(c => c.InHand)) throw new Exception("Tu mano inicial ya está repartida.");
        if (!CardCatalog.All.TryGetValue(definition, out var skill) || skill.Kind != CardKind.Skill ||
            !cards.Any(c => c.Definition == definition)) throw new Exception("Elige una habilidad que esté en tu mazo.");
        if (cards.Count < 5) throw new Exception("El mazo no tiene cartas suficientes para la mano inicial.");
        var chosen = cards.First(c => c.Definition == definition); cards.Remove(chosen);
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = ctx.Rng.Next(i + 1); (cards[i], cards[j]) = (cards[j], cards[i]);
        }
        cards.Insert(0, chosen);
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i]; card.Position = (uint)i; card.InHand = i < 5; ctx.Db.Card.Id.Update(card);
        }
        ctx.Db.InitialSkill.Insert(new InitialSkill { Player = ctx.Sender, Definition = definition, CommandId = commandId });
        TouchRoom(ctx, roomCode);
    }
}
