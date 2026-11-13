using System.Text.Json;

namespace Relick.Core;

/// <summary>What the game reads from a SpacetimeAuth ID token once the server has verified it.</summary>
public static class AccountClaims
{
    public const string GuestName = "Invitado";
    public const string DefaultName = "Jugador";
    public const int MaxNameLength = 24;

    /// <summary>
    /// A guest is SpacetimeAuth's anonymous sign-in. Its token reports the magic link method but, unlike a real
    /// magic link, carries no email: there is nothing to recover the account with on another device.
    /// </summary>
    public static bool IsGuest(string payload)
    {
        if (!TryParse(payload, out var claims)) return false;
        using (claims)
        {
            var method = Text(claims.RootElement, "login_method").ToLowerInvariant();
            return method.Contains("anon") || method == "guest" ||
                method == "magic_link" && Text(claims.RootElement, "email").Length == 0;
        }
    }

    /// <summary>First name shown for a new account. Guests get a generic one: theirs is only an internal identifier.</summary>
    public static string SuggestedName(string payload)
    {
        if (IsGuest(payload)) return GuestName;
        if (!TryParse(payload, out var claims)) return DefaultName;
        using (claims)
        {
            var name = new string(Text(claims.RootElement, "preferred_username").Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (name.Length > MaxNameLength) name = name[..MaxNameLength].Trim();
            return name.Length > 0 ? name : DefaultName;
        }
    }

    private static string Text(JsonElement claims, string name) =>
        claims.ValueKind == JsonValueKind.Object && claims.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";

    private static bool TryParse(string payload, out JsonDocument claims)
    {
        try { claims = JsonDocument.Parse(payload); return true; }
        catch (JsonException) { claims = null!; return false; }
    }
}
