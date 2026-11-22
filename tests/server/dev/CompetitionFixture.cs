// Local test modules only. The published module lets only the database owner administer competitions, and a local
// database has another owner; these wrappers stand in for that person and are never part of the real module.
public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void OpenSeasonForTest(SpacetimeDB.ReducerContext ctx, string name, uint days) => StartSeason(ctx, name, days);
    [SpacetimeDB.Reducer]
    public static void CloseSeasonForTest(SpacetimeDB.ReducerContext ctx) => CloseOpenSeason(ctx);
    [SpacetimeDB.Reducer]
    public static void OpenTournamentForTest(SpacetimeDB.ReducerContext ctx, string name, uint capacity, uint minutes) => CreateTournament(ctx, name, capacity, minutes);
    [SpacetimeDB.Reducer]
    public static void StartTournamentForTest(SpacetimeDB.ReducerContext ctx) => BeginSignedUpTournament(ctx);
    [SpacetimeDB.Reducer]
    public static void CancelTournamentForTest(SpacetimeDB.ReducerContext ctx) => CancelLiveTournament(ctx);
}
