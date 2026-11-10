using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Relick.Network;

static class ConnectionHealthChecks
{
    public static async Task<int> Run(string[] args)
    {
        var server = new Uri(args.ElementAtOrDefault(1) ?? "http://127.0.0.1:3100");
        var database = args.ElementAtOrDefault(2) ?? "relick-dev";
        if (server.Scheme != "http" || !server.IsLoopback)
            throw new ArgumentException("Esta prueba requiere un servidor HTTP local.");
        using var relay = new SilentRelay(server.Host, server.Port);
        using var lobby = new LobbyConnection();
        string? token = null;
        lobby.TokenReceived += value => token = value;
        async Task Until(Func<bool> done, double seconds = 20)
        {
            var timer = Stopwatch.StartNew();
            while (true)
            {
                lobby.Tick();
                if (done()) return;
                if (timer.Elapsed.TotalSeconds > seconds) throw new Exception("Tiempo agotado: " + lobby.Message);
                await Task.Delay(10);
            }
        }
        async Task StayReady()
        {
            var timer = Stopwatch.StartNew();
            await Until(() =>
            {
                if (!lobby.Ready) throw new Exception("Una conexión sana se marcó desconectada: " + lobby.Message);
                return timer.Elapsed.TotalSeconds >= 11;
            });
        }
        string? roomCode = null;
        try
        {
            lobby.Connect(relay.Address, database, null);
            await Until(() => lobby.Ready);
            await StayReady();
            Console.WriteLine("PASS La sala vacía permanece conectada durante varias comprobaciones.");
            relay.DropTraffic = true;
            var outage = Stopwatch.StartNew();
            await Until(() => !lobby.Ready, 11);
            if (lobby.Connecting || !lobby.Message.Contains("Reconectar")) throw new Exception("El corte no ofrece recuperación.");
            Console.WriteLine($"PASS Corte silencioso sin sala detectado en {outage.Elapsed.TotalSeconds:F1}s.");
            relay.DropTraffic = false;
            lobby.Connect(relay.Address, database, token);
            await Until(() => lobby.Ready);
            lobby.Create("Conexión de prueba");
            await Until(() => lobby.Room != null && lobby.PendingRoomRequest == RoomRequest.None);
            roomCode = lobby.Room!.Code;
            relay.DropTraffic = true;
            await Until(() => !lobby.Ready, 11);
            if (lobby.Room?.Code != roomCode) throw new Exception("Se perdió la sala en caché.");
            lobby.Create("Solicitud bloqueada"); lobby.Join("ZZZZZZZZ", "Solicitud bloqueada"); lobby.Leave();
            if (lobby.PendingRoomRequest != RoomRequest.None) throw new Exception("Se aceptan operaciones sin conexión.");
            relay.DropTraffic = false;
            lobby.Connect(relay.Address, database, token);
            await Until(() => lobby.Ready);
            if (lobby.Room?.Code != roomCode) throw new Exception("La reconexión no recupera la misma sala.");
            Console.WriteLine("PASS Corte en sala bloquea operaciones y reconectar recupera la misma sala.");
            lobby.Leave(); await Until(() => lobby.Room == null); roomCode = null;

            relay.DropTraffic = true;
            lobby.Connect(relay.Address, database, token);
            await Until(() => !lobby.Connecting, 17);
            if (lobby.Ready || !lobby.Message.Contains("Reconectar")) throw new Exception("La conexión inicial queda bloqueada.");
            relay.DropTraffic = false;
            lobby.Connect(relay.Address, database, token);
            await Until(() => lobby.Ready);
            Console.WriteLine("PASS Un inicio sin respuesta vence y permite un nuevo intento.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            if (roomCode != null)
            {
                relay.DropTraffic = false;
                try
                {
                    lobby.Connect(relay.Address, database, token); await Until(() => lobby.Ready);
                    lobby.Leave(); await Until(() => lobby.Room == null);
                }
                catch (Exception error) { Console.Error.WriteLine("No se pudo cerrar la sala de prueba: " + error.Message); }
            }
        }
    }

    // Drops bytes without closing either socket, like a Wi-Fi outage whose TCP
    // connection has not yet timed out. Never changes the host's network settings.
    private sealed class SilentRelay : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly string _host;
        private readonly int _port;
        public volatile bool DropTraffic;
        public string Address => "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
        public SilentRelay(string host, int port)
        {
            _host = host; _port = port; _listener.Start(); _ = Accept();
        }
        private async Task Accept()
        {
            try
            {
                while (!_stop.IsCancellationRequested) _ = Forward(await _listener.AcceptTcpClientAsync(_stop.Token));
            }
            catch (OperationCanceledException) { }
        }
        private async Task Forward(TcpClient client)
        {
            using (client)
            using (var upstream = new TcpClient())
            {
                try
                {
                    await upstream.ConnectAsync(_host, _port, _stop.Token);
                    await Task.WhenAll(Pump(client.GetStream(), upstream.GetStream()), Pump(upstream.GetStream(), client.GetStream()));
                }
                catch (Exception) when (_stop.IsCancellationRequested) { }
                catch (IOException) { }
                catch (SocketException) { }
            }
        }
        private async Task Pump(NetworkStream source, NetworkStream target)
        {
            var bytes = new byte[8192];
            int count;
            while ((count = await source.ReadAsync(bytes, _stop.Token)) > 0)
                if (!DropTraffic) await target.WriteAsync(bytes.AsMemory(0, count), _stop.Token);
        }
        public void Dispose() { _stop.Cancel(); _listener.Stop(); }
    }
}
