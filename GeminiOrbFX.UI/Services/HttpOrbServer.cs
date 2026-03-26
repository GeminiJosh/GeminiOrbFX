using System;
using System.Net;
using System.Text;
using System.Threading;

namespace GeminiOrbFX.UI.Services
{
    internal class HttpOrbServer
    {
        private const int PORT = 6556;

        private readonly Plugin _plugin;
        private HttpListener _listener;
        private Thread _thread;

        public bool IsRunning => _listener != null && _listener.IsListening;

        public HttpOrbServer(Plugin plugin)
        {
            _plugin = plugin;
        }

        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{PORT}/");
                _listener.Start();

                _thread = new Thread(ListenLoop) { IsBackground = true };
                _thread.Start();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error("[GeminiOrbFX UI] HttpListener start failed: " + ex.Message);
            }
        }

        public void Stop()
        {
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }

            if (_thread != null && _thread.IsAlive)
            {
                try { _thread.Join(1000); } catch { }
            }

            _listener = null;
            _thread = null;
        }

        private void ListenLoop()
        {
            while (_listener != null)
            {
                try
                {
                    if (!_listener.IsListening)
                        break;

                    var ctx = _listener.GetContext();

                    string response = HandleRequest(ctx.Request);

                    ctx.Response.ContentType = "text/plain; charset=utf-8";

                    byte[] bytes = Encoding.UTF8.GetBytes(response ?? "");
                    ctx.Response.ContentLength64 = bytes.Length;

                    using (var stream = ctx.Response.OutputStream)
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_listener != null)
                        Plugin.Log?.Warn("[GeminiOrbFX UI] Listener error: " + ex.Message);
                }
            }
        }

        private string HandleRequest(HttpListenerRequest req)
        {
            string path = (req.Url.AbsolutePath ?? "").Trim().ToLowerInvariant();

            if (path == "/status")
                return BuildStatusText();

            if (path == "/orb")
            {
                OrbRequest r = _plugin.ParseOrbRequest(req.Url);
                bool ok = Plugin.EnqueueOrbRequest(r, out int qCount);
                return ok ? $"queued:{qCount}:{r.Name}" : "queue_full";
            }

            if (path == "/event/follow")
            {
                string username = GetQueryParam(req.Url, "name");
                bool ok = EventRouter.TriggerFollow(username);
                return ok ? $"follow_ok:{NormalizeName(username)}" : "follow_ignored";
            }

            if (path == "/event/chat")
            {
                string username = GetQueryParam(req.Url, "name");
                string message = GetQueryParam(req.Url, "message");
                bool isFollower = ParseBool(GetQueryParam(req.Url, "follower"));

                bool ok = EventRouter.TriggerChatCommand(username, message, isFollower);
                return ok ? $"chat_ok:{NormalizeName(username)}" : "chat_ignored";
            }

            if (path == "/event/gift")
            {
                string username = GetQueryParam(req.Url, "name");
                string giftName = GetQueryParam(req.Url, "gift");
                string countStr = GetQueryParam(req.Url, "count");
                string coinsStr = GetQueryParam(req.Url, "coins");
                bool isFollower = ParseBool(GetQueryParam(req.Url, "follower"));

                int count = 1;
                if (!int.TryParse(countStr, out count))
                    count = 1;

                if (count < 1)
                    count = 1;

                int coins = 0;
                if (!int.TryParse(coinsStr, out coins))
                    coins = 0;

                if (coins < 0)
                    coins = 0;

                bool ok = EventRouter.TriggerGift(username, giftName, count, coins, isFollower);
                return ok
                    ? $"gift_ok:{NormalizeName(username)}:{NormalizeName(giftName)}:{count}:{coins}"
                    : "gift_ignored";
            }

            if (path == "/relay/status")
            {
                string state = GetQueryParam(req.Url, "state");
                string username = GetQueryParam(req.Url, "user");
                string message = GetQueryParam(req.Url, "message");

                RelayStatusService.Update(state, username, message);
                return $"relay_status_ok:{RelayStatusService.State}:{RelayStatusService.Username}";
            }

            return "unknown_endpoint";
        }

        private static bool ParseBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (bool.TryParse(value, out bool result))
                return result;

            return value == "1";
        }

        private string BuildStatusText()
        {
            bool inGame = _plugin.IsInGameplay();
            int qCount = Plugin.GetQueueCount();
            int liveCount = Plugin.GetLiveOrbCount();

            return
                "GeminiOrbFX UI\n" +
                $"scene={_plugin.GetActiveSceneName()}\n" +
                $"inGameplay={(inGame ? "true" : "false")}\n" +
                $"orbsAlive={liveCount}\n" +
                $"queue={qCount}\n" +
                $"rate={PluginConfig.Instance.SpawnRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}/s\n" +
                $"relayState={RelayStatusService.State}\n" +
                $"relayUser={RelayStatusService.Username}\n" +
                $"relayMessage={RelayStatusService.Message}\n";
        }

        private static string GetQueryParam(Uri uri, string key)
        {
            try
            {
                string q = uri.Query;
                if (string.IsNullOrEmpty(q))
                    return null;

                if (q.StartsWith("?"))
                    q = q.Substring(1);

                string[] parts = q.Split('&');
                for (int i = 0; i < parts.Length; i++)
                {
                    string[] kv = parts[i].Split(new char[] { '=' }, 2);
                    if (kv.Length != 2)
                        continue;

                    string k = Uri.UnescapeDataString(kv[0] ?? "");
                    if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return Uri.UnescapeDataString(kv[1] ?? "");
                }
            }
            catch { }

            return null;
        }

        private static string NormalizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "USER";

            string trimmed = input.Trim();
            if (trimmed.Length > 18)
                trimmed = trimmed.Substring(0, 18);

            return trimmed.ToUpperInvariant();
        }
    }

    internal static class RelayStatusService
    {
        private static readonly object _lock = new object();

        public static string State { get; private set; } = "disconnected";
        public static string Username { get; private set; } = "";
        public static string Message { get; private set; } = "";

        public static void Update(string state, string username, string message)
        {
            lock (_lock)
            {
                State = string.IsNullOrWhiteSpace(state) ? "unknown" : state.Trim().ToLowerInvariant();
                Username = string.IsNullOrWhiteSpace(username) ? "" : username.Trim();
                Message = string.IsNullOrWhiteSpace(message) ? "" : message.Trim();
            }

            Plugin.NotifyRelayStatusChanged();
        }
    }
}