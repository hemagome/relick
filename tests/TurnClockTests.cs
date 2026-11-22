using System.Text.Json;
using Relick.Core;

static class TurnClockTests
{
    private const long Second = 1_000_000;

    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Reloj: cada asiento acumula solo mientras decide", () =>
        {
            var clock = TurnClock.Next(null, 1, 0, 100 * Second);
            Check(clock.TurnTime(0, 130 * Second) == 30 * Second && clock.TurnTime(1, 130 * Second) == 0);
            // The rival answers during the same turn: the owner's clock waits.
            clock = TurnClock.Next(clock, 1, 1, 130 * Second);
            Check(clock.TurnTime(0, 150 * Second) == 30 * Second && clock.TurnTime(1, 150 * Second) == 20 * Second);
            clock = TurnClock.Next(clock, 1, 0, 150 * Second);
            Check(clock.TurnTime(0, 160 * Second) == 40 * Second && clock.TurnTime(1, 160 * Second) == 20 * Second);
        }),
        ("Reloj: un turno nuevo reinicia su tiempo y conserva el del duelo", () =>
        {
            var clock = TurnClock.Next(TurnClock.Next(null, 1, 0, 0), 2, 1, 45 * Second);
            Check(clock.TurnTime(0, 50 * Second) == 0 && clock.DuelTime(0, 50 * Second) == 45 * Second);
            Check(clock.TurnTime(1, 50 * Second) == 5 * Second && clock.DuelTime(1, 50 * Second) == 5 * Second);
        }),
        ("Reloj: pasar la referencia solo muestra tiempo extra", () =>
        {
            var clock = TurnClock.Next(null, 1, 0, 0);
            Check(clock.Remaining(0, TurnClock.DefaultReference + 12 * Second) == -12 * Second);
            Check(clock.Running == 0 && clock.Turn == 1);
        }),
        ("Reloj: se detiene al terminar el duelo", () =>
        {
            var clock = TurnClock.Next(TurnClock.Next(null, 4, 1, 0), 4, null, 8 * Second);
            Check(clock.TurnTime(1, 900 * Second) == 8 * Second && clock.DuelTime(1, 900 * Second) == 8 * Second);
        }),
        ("Reloj: una hora anterior nunca resta tiempo", () =>
        {
            var first = TurnClock.Next(null, 1, 0, 100 * Second);
            var clock = TurnClock.Next(first, 1, 0, 90 * Second);
            Check(first.TurnTime(0, 90 * Second) == 0 && clock.UpdatedAt == 100 * Second);
        }),
        ("Reloj: continúa desde el paquete guardado y tolera los anteriores", () =>
        {
            var game = new GameEngine(42);
            game.ChooseInitialSkill(0, CardCatalog.Magnet.Id); game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);
            // A complete board as the server writes it, so a change of property order fails here.
            var packet = new DuelBoardPacket { Board = DuelSnapshot.For(game, -1, 7) };
            string Json() => JsonSerializer.Serialize(packet, DuelJson.Default.DuelBoardPacket);
            Check(!Json().Contains("\"Clock\"") && TurnClock.FromBoard(Json()) == null);
            packet.Board.Clock = TurnClock.Next(TurnClock.Next(null, 3, 1, 10), 3, 0, 25);
            var read = TurnClock.FromBoard(Json())!;
            Check(read.Running == 0 && read.Turn == 3 && read.UpdatedAt == 25 && read.TurnTime(1, 99) == 15 && read.Reference == TurnClock.DefaultReference);
            packet.Board.Clock = TurnClock.Next(read, 3, null, 40);
            var stopped = TurnClock.FromBoard(Json())!;
            Check(stopped.Running == null && stopped.TurnTime(0, 99) == 15);
            Check(JsonSerializer.Deserialize(Json(), DuelJson.Default.DuelBoardPacket)!.Board.Clock!.UpdatedAt == 40);
        })
    };

    private static void Check(bool condition) { if (!condition) throw new Exception("Comprobación del reloj fallida"); }
}
