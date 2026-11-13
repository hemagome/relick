using Godot;
using Relick.Network;

namespace Relick;

public partial class OnlineLobby
{
    // Offline: drives the sign-in states with finished tasks, without a browser or the account server.
    private async Task CheckAccountScreens()
    {
        async Task Frames() { for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Expect(bool ok, string reason) { if (!ok) throw new Exception("Cuenta: " + reason); }
        var contentWasVisible = _content.Visible;
        _auth = new AuthSession("client_test");
        _sessionPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "relick-account-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            ShowLogin("");
            await Frames();
            Expect(_loginScreen.Visible && !_content.Visible && !_signIn.Disabled && !_cancelSignIn.Visible && !UpdateAccount(),
                "sin sesión debe mostrarse solo la pantalla de inicio");
            Expect(_signIn.GetGlobalRect().Size.Y >= 48 && GetViewportRect().Encloses(_signIn.GetGlobalRect()), "el botón de inicio debe caber en pantalla");

            var browser = new TaskCompletionSource<AuthTokens>();
            _accountStage = AccountStage.WaitingBrowser; _authTask = browser.Task; PresentAccount();
            Expect(_signIn.Disabled && _cancelSignIn.Visible && !UpdateAccount(), "esperando al navegador solo se puede cancelar");
            browser.SetException(new AuthException("El inicio de sesión se canceló o fue rechazado."));
            Expect(!UpdateAccount() && _accountStage == AccountStage.SignedOut && _loginStatus.Text.Contains("canceló") &&
                !_signIn.Disabled && !_cancelSignIn.Visible, "un inicio fallido vuelve a ofrecer iniciar sesión con el motivo");

            var menu = _account.GetPopup();
            Expect(menu.ItemCount == 2 && menu.GetItemText(0) == "Cerrar sesión" && menu.GetItemText(1).StartsWith("Eliminar cuenta"),
                "el menú de cuenta debe ofrecer cerrar sesión y eliminar la cuenta");
            var warning = DeleteAccountExplanation(3);
            Expect(warning.Contains("3 mazos") && warning.Contains("permanente") && warning.Contains("SpacetimeAuth") && warning.Contains("cuenta nueva y vacía") &&
                DeleteAccountExplanation(1).Contains("Tu mazo guardado") && DeleteAccountExplanation(0).Contains("no tienes ninguno"),
                "el aviso de eliminación debe decir qué se borra, qué no y qué pasa al volver");

            SavePrivate(_sessionPath, "refresh");
            var ended = new TaskCompletionSource<AuthTokens>();
            _accountStage = AccountStage.Restoring; _authTask = ended.Task;
            ended.SetException(new AuthException("Tu sesión venció. Inicia sesión de nuevo.", sessionEnded: true));
            Expect(!UpdateAccount() && _loginScreen.Visible && !System.IO.File.Exists(_sessionPath) && _loginStatus.Text.Contains("venció"),
                "una sesión vencida se olvida y pide iniciar de nuevo");

            SavePrivate(_sessionPath, "refresh");
            var offline = new TaskCompletionSource<AuthTokens>();
            _accountStage = AccountStage.Restoring; _authTask = offline.Task;
            offline.SetException(new AuthException("No se pudo contactar al servidor de cuentas. Revisa tu conexión."));
            Expect(UpdateAccount() && !_loginScreen.Visible && System.IO.File.Exists(_sessionPath) && _localError.Contains("servidor de cuentas"),
                "un fallo de red conserva la sesión para reintentar");
        }
        finally
        {
            ForgetSession();
            _auth = null; _authTask = null; _tokens = null; _accountStage = AccountStage.Guest; _localError = "";
            _loginScreen.Hide(); _content.Visible = contentWasVisible;
        }
        GD.Print("PVP_ACCOUNT_PASS · inicio, espera, fallo, sesión vencida, reintento y aviso de eliminación.");
    }
}
