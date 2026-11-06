using Godot;
using Relick.Core;

namespace Relick;

public static class SkillArtwork
{
    public static Texture2D? Load(CardDefinition? card) => card?.Kind == CardKind.Skill
        ? GD.Load<Texture2D>($"res://assets/cards/{card.Id}-clean.png") : null;
}
