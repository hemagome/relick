using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Relick.Network;

/// <summary>Tokens of a signed-in player. The ID token is what SpacetimeDB accepts as credential.</summary>
public sealed record AuthTokens(string IdToken, string? RefreshToken, DateTimeOffset ExpiresAt)
{
    public bool ExpiresWithin(TimeSpan margin) => DateTimeOffset.UtcNow + margin >= ExpiresAt;
}

/// <summary><see cref="SessionEnded"/> tells a saved session that is no longer valid from a failure worth retrying.</summary>
public sealed class AuthException(string message, bool sessionEnded = false) : Exception(message)
{
    public bool SessionEnded { get; } = sessionEnded;
}

/// <summary>
/// Sign-in against SpacetimeAuth for a desktop app: the system browser shows the hosted login page and
/// returns an authorization code to a loopback address, which is exchanged with PKCE and no client secret.
/// </summary>
public sealed class AuthSession(string clientId, int redirectPort = AuthSession.DefaultRedirectPort, HttpClient? http = null)
{
    public const string Issuer = "https://auth.spacetimedb.com/oidc";
    // SpacetimeAuth matches the redirect address exactly, so the port is fixed and registered with the client.
    public const int DefaultRedirectPort = 53682;
    private const string Scopes = "openid profile email offline_access";
    private readonly HttpClient _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public string RedirectUri => $"http://127.0.0.1:{redirectPort}/callback";

    /// <summary>Opens the login page and waits until the player finishes, cancels or the token expires.</summary>
    public async Task<AuthTokens> LoginAsync(Action<string> openBrowser, CancellationToken cancel, bool chooseAccount = false)
    {
        var verifier = Random(48);
        var state = Random(24);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{redirectPort}/");
        try { listener.Start(); }
        catch (Exception error) when (error is HttpListenerException or SocketException)
        {
            throw new AuthException("No se pudo esperar la respuesta del navegador. Cierra otras ventanas de Relick e inténtalo de nuevo.");
        }

        // The consent prompt is what makes the provider hand out a refresh token for offline_access.
        openBrowser(Issuer + "/auth?" + Query(new()
        {
            ["client_id"] = clientId, ["response_type"] = "code", ["scope"] = Scopes, ["redirect_uri"] = RedirectUri,
            ["code_challenge"] = challenge, ["code_challenge_method"] = "S256", ["state"] = state,
            // "login" makes the provider ask who is signing in even when the browser still holds a session.
            ["prompt"] = chooseAccount ? "login consent" : "consent"
        }));

        using var stop = cancel.Register(listener.Stop);
        while (true)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                cancel.ThrowIfCancellationRequested();
                throw new AuthException("Se interrumpió el inicio de sesión.");
            }
            var query = context.Request.QueryString;
            // Anything that is not our own redirect, such as a favicon request or another local page, is ignored.
            if (context.Request.Url?.AbsolutePath != "/callback" || query["state"] != state)
            {
                await Reply(context, 404, "Esta página no pertenece a un inicio de sesión de Relick.");
                continue;
            }
            if (query["code"] is not { Length: > 0 } code)
            {
                await Reply(context, 400, "No se completó el inicio de sesión. Vuelve a Relick e inténtalo de nuevo.");
                throw new AuthException("El inicio de sesión se canceló o fue rechazado.");
            }
            await Reply(context, 200, "Sesión iniciada. Ya puedes cerrar esta pestaña y volver a Relick.");
            return await Token(new()
            {
                ["grant_type"] = "authorization_code", ["client_id"] = clientId, ["code"] = code,
                ["redirect_uri"] = RedirectUri, ["code_verifier"] = verifier
            }, null, cancel);
        }
    }

    /// <summary>Gets a fresh ID token without showing the browser. Fails when the saved session is no longer valid.</summary>
    public Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken cancel) => Token(new()
    {
        ["grant_type"] = "refresh_token", ["client_id"] = clientId, ["refresh_token"] = refreshToken
    }, refreshToken, cancel);

    /// <summary>Address that ends the browser's own session, so the next login can pick another account.</summary>
    public string LogoutUrl(string idToken) => Issuer + "/session/end?" + Query(new() { ["id_token_hint"] = idToken, ["client_id"] = clientId });

    /// <summary>Claims of an ID token, for display only: SpacetimeDB is the one that verifies its signature.</summary>
    public static JsonElement Claims(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length != 3) throw new AuthException("El servidor de cuentas devolvió una credencial inválida.");
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        try { return JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.Clone(); }
        catch (Exception error) when (error is FormatException or JsonException)
        {
            throw new AuthException("El servidor de cuentas devolvió una credencial inválida.");
        }
    }

    private async Task<AuthTokens> Token(Dictionary<string, string> form, string? previousRefresh, CancellationToken cancel)
    {
        JsonElement body;
        try
        {
            using var response = await _http.PostAsync(Issuer + "/token", new FormUrlEncodedContent(form), cancel);
            body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel)).RootElement.Clone();
            if (!response.IsSuccessStatusCode)
            {
                // Any refusal of a refresh token ends the session; only a server fault is worth another try.
                var ended = (int)response.StatusCode < 500 && previousRefresh != null;
                throw new AuthException(ended ? "Tu sesión venció. Inicia sesión de nuevo." : "El servidor de cuentas rechazó la solicitud.", ended);
            }
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is TaskCanceledException && !cancel.IsCancellationRequested)
        {
            throw new AuthException("No se pudo contactar al servidor de cuentas. Revisa tu conexión.");
        }
        if (!body.TryGetProperty("id_token", out var id) || id.GetString() is not { Length: > 0 } idToken)
            throw new AuthException("El servidor de cuentas no devolvió una credencial.");
        var expires = Claims(idToken).TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds) : DateTimeOffset.UtcNow.AddMinutes(5);
        // A provider that does not rotate refresh tokens omits it; the one already held stays valid.
        var refresh = body.TryGetProperty("refresh_token", out var rotated) ? rotated.GetString() : previousRefresh;
        return new AuthTokens(idToken, refresh, expires);
    }

    private static async Task Reply(HttpListenerContext context, int status, string message)
    {
        var page = Encoding.UTF8.GetBytes("<!doctype html><html lang=\"es\"><meta charset=\"utf-8\"><title>Relick</title>" +
            "<body style=\"font-family:sans-serif;background:#12151c;color:#e8ecf4;display:grid;place-items:center;height:100vh;margin:0\">" +
            $"<p>{WebUtility.HtmlEncode(message)}</p></body></html>");
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = page.Length;
        await context.Response.OutputStream.WriteAsync(page);
        context.Response.Close();
    }

    private static string Query(Dictionary<string, string> values) =>
        string.Join("&", values.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
    private static string Random(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
