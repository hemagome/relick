using Relick.Core;
static class AccountClaimsTests
{
    // The shapes SpacetimeAuth actually issued for this project, with personal values replaced.
    private const string Discord = """{"sub":"user_a","login_method":"discord","provider_id":"1","preferred_username":"Hector Gomez","email":"a@example.com","email_verified":true}""";
    private const string Anonymous = """{"sub":"user_0Sc3","login_method":"magic_link","provider_id":"user_0Sc3","given_name":"Anonymous","family_name":"User","name":"Anonymous User","preferred_username":"0Sc3tw1N5mDE4r4Zjesyc6"}""";
    private const string MagicLink = """{"sub":"user_b","login_method":"magic_link","preferred_username":"ana","email":"ana@example.com","email_verified":true}""";

    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Cuenta: solo el inicio anónimo es invitado; correo y redes son cuentas completas", () =>
        {
            Check(AccountClaims.IsGuest(Anonymous) && !AccountClaims.IsGuest(Discord) && !AccountClaims.IsGuest(MagicLink));
            Check(AccountClaims.IsGuest("""{"login_method":"anonymous","email":"x@example.com"}""") && AccountClaims.IsGuest("""{"login_method":"magic_link","email":""}"""));
            // A social account without a public email is still a real account.
            Check(!AccountClaims.IsGuest("""{"login_method":"twitch","preferred_username":"ana"}""") && !AccountClaims.IsGuest("""{"login_method":"magic_link","email":"a@b.c"}"""));
            Check(!AccountClaims.IsGuest("") && !AccountClaims.IsGuest("no es json") && !AccountClaims.IsGuest("[]") && !AccountClaims.IsGuest("""{"login_method":5}"""));
        }),
        ("Cuenta: nombre inicial del proveedor, genérico para invitados y saneado", () =>
        {
            Check(AccountClaims.SuggestedName(Discord) == "Hector Gomez" && AccountClaims.SuggestedName(MagicLink) == "ana");
            Check(AccountClaims.SuggestedName(Anonymous) == AccountClaims.GuestName);
            Check(AccountClaims.SuggestedName("""{"login_method":"discord","preferred_username":"  \u0007  "}""") == AccountClaims.DefaultName);
            Check(AccountClaims.SuggestedName("""{"login_method":"discord"}""") == AccountClaims.DefaultName && AccountClaims.SuggestedName("roto") == AccountClaims.DefaultName);
            var longName = AccountClaims.SuggestedName("""{"login_method":"discord","preferred_username":"Un nombre demasiado largo para la sala de duelos"}""");
            Check(longName.Length <= AccountClaims.MaxNameLength && longName == longName.Trim() && longName.StartsWith("Un nombre"));
        })
    };

    private static void Check(bool condition) { if (!condition) throw new Exception("Comprobación de cuenta fallida"); }
}
