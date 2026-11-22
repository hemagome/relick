using Godot;
using Relick.Network;

namespace Relick;

// Sign-in, the saved session and sign-out. Without an account client configured the lobby keeps the
// anonymous device identity used by local development servers.
public partial class OnlineLobby
{
    private enum AccountStage { Guest, SignedOut, WaitingBrowser, Restoring, SignedIn }

    private AccountStage _accountStage = AccountStage.Guest;
    private AuthSession? _auth;
    private AuthTokens? _tokens;
    private Task<AuthTokens>? _authTask;
    private CancellationTokenSource? _authCancel;
    private string _sessionPath = "";
    private Control _loginScreen = null!;
    private Label _loginStatus = null!;
    private Button _signIn = null!;
    private Button _cancelSignIn = null!;

    private bool AccountBusy => _accountStage is AccountStage.Restoring or AccountStage.WaitingBrowser;

    private void BuildLoginScreen()
    {
        var screen = new CenterContainer { Name = "LoginScreen", Visible = false };
        _loginScreen = screen; AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = Surface(screen, 32);
        panel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; panel.CustomMinimumSize = new(Math.Min(460, Math.Max(280, Size.X - 48)), 0);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 18); panel.AddChild(column);
        var logo = RelickVisuals.Logo(); logo.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; column.AddChild(logo);
        var title = Text("Inicia sesión", MenuVisuals.Type.Title); title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontOverride("font", RelickVisuals.Display); column.AddChild(title);
        _signIn = AddButton(column, "Iniciar sesión", StartSignIn);
        _signIn.Name = "SignIn"; RelickVisuals.Primary(_signIn);
        _loginStatus = Description(""); _loginStatus.Name = "LoginStatus"; _loginStatus.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_loginStatus);
        _cancelSignIn = AddButton(column, "Cancelar", () => _authCancel?.Cancel());
        _cancelSignIn.Name = "CancelSignIn"; _cancelSignIn.Visible = false;
    }

    /// <summary>Chooses between accounts and the anonymous development identity, then starts whichever applies.</summary>
    private void StartSession(ConfigFile config, string profileKey)
    {
        var client = OS.GetEnvironment("RELICK_AUTH_CLIENT");
        // The configured client belongs to the configured server; another server needs its own or none.
        if (client.Length == 0 && OS.GetEnvironment("RELICK_SERVER").Length == 0)
            client = config.GetValue("auth", "client_id", "").AsString();
        if (client.Length == 0)
        {
            Connect();
            return;
        }
        _auth = new AuthSession(client);
        _sessionPath = ProjectSettings.GlobalizePath("user://auth-" + profileKey + ".session");
        if (System.IO.File.Exists(_sessionPath)) Connect();
        else ShowLogin("");
    }

    private void ShowLogin(string message, bool failed = false)
    {
        _accountStage = AccountStage.SignedOut;
        _tokens = null;
        _lobby.Dispose();
        _loginStatus.Text = message;
        _loginStatus.Modulate = failed ? ArenaPresentation.Coral : Colors.White;
        PresentAccount();
    }

    private void StartSignIn()
    {
        if (_auth == null || AccountBusy) return;
        _authCancel = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        _accountStage = AccountStage.WaitingBrowser;
        _loginStatus.Text = "Esperando a que termines en el navegador…";
        _loginStatus.Modulate = Colors.White;
        _authTask = _auth.LoginAsync(url => OS.ShellOpen(url), _authCancel.Token);
        PresentAccount();
    }

    /// <summary>Makes sure the credential is fresh before connecting; it lasts minutes, so every reconnection may need a new one.</summary>
    private bool ConnectWithAccount()
    {
        if (_auth == null) return false;
        if (AccountBusy) return true;
        if (_tokens != null && !_tokens.ExpiresWithin(TimeSpan.FromMinutes(1)))
        {
            _lobby.Connect(_server, _database, _tokens.IdToken);
            return true;
        }
        string? refresh = _tokens?.RefreshToken;
        try { refresh ??= System.IO.File.Exists(_sessionPath) ? System.IO.File.ReadAllText(_sessionPath).Trim() : null; }
        catch (Exception) { }
        if (string.IsNullOrEmpty(refresh)) { ShowLogin(""); return true; }
        _authCancel = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _accountStage = AccountStage.Restoring;
        _authTask = _auth.RefreshAsync(refresh, _authCancel.Token);
        PresentAccount();
        return true;
    }

    /// <summary>Completes a pending sign-in or renewal on the main thread. Returns false while there is no session to play with.</summary>
    private bool UpdateAccount()
    {
        if (_accountStage == AccountStage.Guest) return true;
        if (_authTask is { IsCompleted: true } task)
        {
            var renewing = _accountStage == AccountStage.Restoring;
            _authTask = null; _authCancel?.Dispose(); _authCancel = null;
            if (task.IsCompletedSuccessfully)
            {
                _tokens = task.Result;
                // The provider replaces the refresh token on every use; losing the new one would end the session.
                if (_tokens.RefreshToken != null) SavePrivate(_sessionPath, _tokens.RefreshToken);
                _accountStage = AccountStage.SignedIn;
                _localError = "";
                try { _lobby.Connect(_server, _database, _tokens.IdToken); }
                catch (Exception) { _lobby.Dispose(); _localError = "No se pudo conectar. Comprueba tu conexión y pulsa Reconectar."; }
            }
            else if (task.Exception?.InnerException is AuthException { SessionEnded: false } error && renewing)
            {
                // The saved session may still be good: stay signed in and let Reconectar try again.
                _accountStage = AccountStage.SignedIn;
                _localError = error.Message;
            }
            else
            {
                var failure = task.Exception?.InnerException as AuthException;
                if (failure?.SessionEnded == true) ForgetSession();
                ShowLogin(task.IsCanceled || task.Exception?.InnerException is OperationCanceledException ? "" : failure?.Message ?? "No se pudo iniciar sesión. Inténtalo de nuevo.", failed: true);
            }
            PresentAccount();
        }
        return _accountStage is AccountStage.SignedIn or AccountStage.Restoring;
    }

    private void SignOut()
    {
        if (_auth == null || _accountStage != AccountStage.SignedIn || _lobby.Room != null || _lobby.Searching) return;
        var idToken = _tokens?.IdToken;
        ForgetSession();
        ShowLogin("Cerraste sesión.");
        // The browser keeps its own session; ending it lets the next login choose another account.
        if (idToken != null) OS.ShellOpen(_auth.LogoutUrl(idToken));
    }

    /// <summary>Asks before erasing the account, spelling out what is lost and what the game cannot remove.</summary>
    private void ConfirmDeleteAccount()
    {
        if (_auth == null || _accountStage != AccountStage.SignedIn || !_lobby.Ready || _lobby.Room != null || _lobby.Searching) return;
        ShowDeleteAccount(_lobby.Decks.Count);
    }

    private void ShowDeleteAccount(int decks)
    {
        var dialog = new ConfirmationDialog { Name = "DeleteAccountDialog", Title = "Eliminar cuenta",
            OkButtonText = "Eliminar mi cuenta", CancelButtonText = "Conservar mi cuenta" };
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 12);
        var explanation = Text(DeleteAccountExplanation(decks), MenuVisuals.Type.Body);
        explanation.Name = "DeleteAccountExplanation";
        column.AddChild(explanation);
        var confirm = new LineEdit { Name = "DeleteAccountConfirm", PlaceholderText = "Escribe ELIMINAR para confirmar", CustomMinimumSize = new(0, 48) };
        column.AddChild(confirm);
        MenuDialog.Fill(dialog, column, 540, destructive: true);
        dialog.GetOkButton().Disabled = true;
        confirm.TextChanged += typed => dialog.GetOkButton().Disabled = typed.Trim() != "ELIMINAR";
        dialog.Confirmed += () => { _localError = ""; _lobby.DeleteAccount(); };
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        AddChild(dialog);
        MenuDialog.Show(dialog);
    }

    internal static string DeleteAccountExplanation(int decks) =>
        "Se borrará de Relick, de forma permanente y sin posibilidad de recuperarlo:\n" +
        "• Tu cuenta, tu nombre de jugador, tu perfil y tus estadísticas.\n" +
        $"• {(decks == 1 ? "Tu mazo guardado" : $"Tus {decks} mazos guardados")}{(decks == 0 ? " (ahora no tienes ninguno)" : "")}.\n\n" +
        "Lo que Relick no puede borrar desde el juego: el registro de tu inicio de sesión en el servicio de cuentas (SpacetimeAuth), " +
        "que conserva tu correo y el método con el que entraste, por ejemplo Discord o Google. Para eliminarlo también, pídeselo al equipo de Relick.\n\n" +
        "Si más adelante vuelves a iniciar sesión con el mismo método, empezarás con una cuenta nueva y vacía.";

    private bool AccountWasDeleted()
    {
        if (_auth == null || !_lobby.AccountDeleted) return false;
        var idToken = _tokens?.IdToken;
        ForgetSession();
        ShowLogin("Tu cuenta y tus mazos fueron eliminados.");
        // End the browser's session too, so the next login does not silently recreate the account.
        if (idToken != null) OS.ShellOpen(_auth.LogoutUrl(idToken));
        return true;
    }

    private void ForgetSession()
    {
        try { System.IO.File.Delete(_sessionPath); } catch (Exception) { }
    }

    private void PresentAccount()
    {
        var signedOut = _accountStage is AccountStage.SignedOut or AccountStage.WaitingBrowser;
        _loginScreen.Visible = signedOut;
        if (signedOut) { ReturnToPlay(); _joinDialog.Hide(); }
        if (_arena == null) _content.Visible = !signedOut;
        if (signedOut) _connectionNotice.Hide();
        var waiting = _accountStage == AccountStage.WaitingBrowser;
        _signIn.Disabled = waiting;
        _signIn.Text = waiting ? "Abriendo el navegador…" : "Iniciar sesión";
        _cancelSignIn.Visible = waiting;
    }

    /// <summary>The account owns the name: the field shows it and saves edits instead of sending a name per room.</summary>
    private void PresentAccountName(bool editable)
    {
        if (_auth == null) return;
        var account = _lobby.Account;
        if (_lobby.TakeAccountError() is { Length: > 0 } refused) _localError = refused;
        if (account == null || account.Name.Length == 0) return;
        if (!_name.HasFocus() && _name.Text != account.Name && editable) _name.Text = account.Name;
    }

    private void SaveAccountName()
    {
        if (_auth != null) _lobby.SetDisplayName(_name.Text);
    }

    private static void SavePrivate(string path, string text)
    {
        try
        {
            // Create with user-only permissions from the start, including on Linux.
            var options = new System.IO.FileStreamOptions { Mode = System.IO.FileMode.Create, Access = System.IO.FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite;
            using var writer = new System.IO.StreamWriter(new System.IO.FileStream(path, options));
            writer.Write(text);
        }
        catch (Exception) { GD.PushWarning("No se pudo guardar la sesión para la próxima vez."); }
    }
}
