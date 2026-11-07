// Local lifecycle test only; this wrapper preserves the external sender.
public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void AttemptExpiryForTest(SpacetimeDB.ReducerContext ctx) => ExpireRoom(ctx, new RoomExpiry());
}
