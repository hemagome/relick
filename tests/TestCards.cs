using System.Runtime.CompilerServices;
using Relick.Core;

// Generic code-effect fixtures exist only in this test executable. They are never
// compiled into the game, included in its catalogue, or dealt in a real deck.
static class TestCards
{
    public static readonly CardDefinition Recover = new("test-recover", "Test heal", CardKind.Code, "Test fixture", Heal: 2);
    public static readonly CardDefinition ObjectCode = new("test-object-code", "Object code fixture", CardKind.Code, "Test fixture draws 2", Draw: 2, CodeType: CodeType.Object);
    [ModuleInitializer]
    internal static void Register()
    {
        var catalogue = (Dictionary<string, CardDefinition>)CardCatalog.All;
        catalogue.Add(Recover.Id, Recover); catalogue.Add(ObjectCode.Id, ObjectCode);
    }
}
