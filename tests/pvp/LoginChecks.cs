using System.Diagnostics;
using Relick.Network;
using Relick.Network.Types;
using SpacetimeDB;

// Interactive check of the real sign-in: it opens the browser and needs a person to log in.
//   dotnet run --project tests/pvp/Relick.Pvp.Tests.csproj -- --login <client-id> [server] [database]
// Add --anonymous to only report which issuer signs the server's own anonymous credentials.
static class LoginChecks
{
    public static async Task<int> Run(string[] args)
    {
        var anonymousOnly = args.Contains("--anonymous");
        // --fresh shows the login page again instead of reusing the browser's session, to try another method.
        var fresh = args.Contains("--fresh");
        args = args.Where(arg => arg is not ("--anonymous" or "--fresh")).ToArray();
        var clientId = args.ElementAtOrDefault(1) ?? "";
        var server = args.ElementAtOrDefault(2) ?? "http://127.0.0.1:3100";
        var database = args.ElementAtOrDefault(3) ?? "relick-dev";
        try
        {
            var (anonymous, anonymousToken) = await Connect(server, database, null);
            Console.WriteLine($"Credencial anónima de {server}: iss={Claim(anonymousToken, "iss")} identidad={anonymous}");
            if (anonymousOnly) return 0;
            if (clientId.Length == 0) throw new Exception("Indica el client ID de SpacetimeAuth.");

            var session = new AuthSession(clientId);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var tokens = await session.LoginAsync(url =>
            {
                Console.WriteLine("Inicia sesión en el navegador. Si no se abre, visita:\n" + url);
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            }, timeout.Token, fresh);

            var claims = AuthSession.Claims(tokens.IdToken);
            Console.WriteLine("Claims del ID token:");
            foreach (var claim in claims.EnumerateObject())
                Console.WriteLine($"  {claim.Name} = {(claim.Name is "email" or "picture" or "sid" ? "(presente)" : claim.Value.ToString())}");
            var issued = claims.TryGetProperty("iat", out var iat) ? DateTimeOffset.FromUnixTimeSeconds(iat.GetInt64()) : DateTimeOffset.UtcNow;
            Console.WriteLine($"Duración del ID token: {(tokens.ExpiresAt - issued).TotalMinutes:0} minutos");
            Console.WriteLine("Refresh token: " + (tokens.RefreshToken == null ? "NO se entregó" : "entregado"));

            var (identity, _) = await Connect(server, database, tokens.IdToken);
            Console.WriteLine($"SpacetimeDB aceptó el ID token. Identidad de la cuenta: {identity}");

            if (tokens.RefreshToken != null)
            {
                var renewed = await session.RefreshAsync(tokens.RefreshToken, timeout.Token);
                Console.WriteLine($"Renovación sin navegador: correcta; refresh token {(renewed.RefreshToken == tokens.RefreshToken ? "se conserva" : "rota")}; " +
                    $"nuevo vencimiento en {(renewed.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes:0} minutos");
                var (again, _) = await Connect(server, database, renewed.IdToken);
                Console.WriteLine(again == identity ? "La identidad se mantiene tras renovar." : "ATENCIÓN: la identidad cambió tras renovar.");
            }
            Console.WriteLine("LOGIN_PASS");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
    }

    private static string Claim(string token, string name) =>
        AuthSession.Claims(token).TryGetProperty(name, out var value) ? value.ToString() : "(sin " + name + ")";

    private static async Task<(Identity, string)> Connect(string server, string database, string? token)
    {
        Identity? identity = null; string received = ""; string? fatal = null;
        var builder = DbConnection.Builder().WithUri(server).WithDatabaseName(database)
            .OnConnect((_, id, value) => { identity = id; received = value; })
            .OnConnectError(error => fatal = error.Message);
        if (token != null) builder = builder.WithToken(token);
        var connection = builder.Build();
        var timer = Stopwatch.StartNew();
        while (identity == null && fatal == null && timer.Elapsed.TotalSeconds < 20)
        {
            connection.FrameTick();
            await Task.Delay(10);
        }
        if (connection.IsActive) connection.Disconnect();
        if (identity == null) throw new Exception("SpacetimeDB rechazó la conexión: " + (fatal ?? "tiempo agotado"));
        return (identity.Value, received);
    }
}
