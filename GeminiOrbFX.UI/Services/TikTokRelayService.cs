using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GeminiOrbFX.UI.Services
{
    internal sealed class TikTokRelayService : IDisposable
    {
        private CancellationTokenSource _liveCts;
        private bool _liveRunning;
        private bool _manualStopRequested;
        private bool _disposed;
        private string _liveUsername = string.Empty;
        private ClientWebSocket _ws;

        private readonly Dictionary<string, DateTime> _recentGiftIds = new Dictionary<string, DateTime>();
        private readonly HashSet<string> _knownFollowers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _knownFollowersLock = new object();
                
        public bool IsRunning => _liveRunning;
        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;
        public string Username => _liveUsername ?? string.Empty;

        public void Start(string username)
        {
            if (_disposed)
                return;

            if (_liveRunning)
                return;

            if (string.IsNullOrWhiteSpace(username))
                return;

            string apiKey = (PluginConfig.Instance.EulerApiKey ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Plugin.Log?.Warn("[TikTokRelay] Start skipped: Euler API key missing. Add it to the config file.");
                RelayStatusService.Update("error", username, "missing_api_key");
                return;
            }

            _liveUsername = username.Trim().TrimStart('@');
            ClearRecentGiftCache();
            ClearKnownFollowers();

            _liveCts = new CancellationTokenSource();
            _liveRunning = true;
            _manualStopRequested = false;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_liveCts.IsCancellationRequested)
                    {
                        try
                        {
                            await RunEulerWebSocketLoop(_liveUsername, _liveCts.Token);

                            if (_liveCts.IsCancellationRequested || _manualStopRequested)
                                break;

                            if (!PluginConfig.Instance.RelayAutoReconnect)
                                break;

                            int delaySeconds = Math.Max(1, PluginConfig.Instance.RelayReconnectDelaySeconds);

                            Plugin.Log?.Warn("[TikTokRelay] Connection dropped. Reconnecting in " + delaySeconds + "s...");
                            RelayStatusService.Update("connecting", _liveUsername, "Reconnecting...");

                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _liveCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (_liveCts.IsCancellationRequested || _manualStopRequested)
                                break;

                            Plugin.Log?.Error("[TikTokRelay] Relay loop error: " + ex.Message);
                            RelayStatusService.Update("error", _liveUsername, ex.Message);

                            if (!PluginConfig.Instance.RelayAutoReconnect)
                                break;

                            int delaySeconds = Math.Max(1, PluginConfig.Instance.RelayReconnectDelaySeconds);

                            Plugin.Log?.Warn("[TikTokRelay] Retry in " + delaySeconds + "s...");
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _liveCts.Token);
                        }
                    }
                }
                finally
                {
                    _liveRunning = false;
                    CleanupSocket();
                    ClearRecentGiftCache();
                    ClearKnownFollowers();
                    RelayStatusService.Update("disconnected", _liveUsername, "");
                }
            });
        }

        public async Task StopAsync()
        {
            if (!_liveRunning)
                return;

            _manualStopRequested = true;

            try
            {
                _liveCts?.Cancel();
            }
            catch { }

            CleanupSocket();

            await Task.Delay(100);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch { }
        }

        private async Task RunEulerWebSocketLoop(string username, CancellationToken token)
        {
            string apiKey = (PluginConfig.Instance.EulerApiKey ?? string.Empty).Trim();

            string wsUrl =
                $"wss://ws.eulerstream.com?uniqueId={Uri.EscapeDataString(username)}&apiKey={Uri.EscapeDataString(apiKey)}";

            _ws = new ClientWebSocket();

            RelayStatusService.Update("connecting", username, "");
            await _ws.ConnectAsync(new Uri(wsUrl), token);
            RelayStatusService.Update("connected", username, "");

            Plugin.Log?.Info("[TikTokRelay] Connected");

            while (!token.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                string json = await ReceiveFullMessage(_ws, token);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                await HandleEulerPayload(json);
            }
        }

        private async Task<string> ReceiveFullMessage(ClientWebSocket ws, CancellationToken token)
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);

            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    WebSocketReceiveResult result = await ws.ReceiveAsync(buffer, token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (PluginConfig.Instance.VerboseRelayLogging)
                        {
                            Plugin.Log?.Info("[TikTokRelay] WebSocket closed by server.");
                            Plugin.Log?.Info("[TikTokRelay] Close status: " + ws.CloseStatus);
                            Plugin.Log?.Info("[TikTokRelay] Close description: " + (ws.CloseStatusDescription ?? "(none)"));
                        }

                        try
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
                        }
                        catch { }

                        return null;
                    }

                    ms.Write(buffer.Array, buffer.Offset, result.Count);

                    if (result.EndOfMessage)
                        break;
                }

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private async Task HandleEulerPayload(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                if (root == null)
                    return;

                var messages = root["messages"] as JArray;
                if (messages != null)
                {
                    foreach (var token in messages)
                    {
                        var msg = token as JObject;
                        if (msg != null)
                            await HandleSingleEulerMessage(msg);
                    }

                    return;
                }

                await HandleSingleEulerMessage(root);
            }
            catch (Exception ex)
            {
                if (PluginConfig.Instance.VerboseRelayLogging)
                    Plugin.Log?.Warn("[TikTokRelay] Parse error: " + ex.Message);
            }
        }

        private async Task HandleSingleEulerMessage(JObject msg)
        {
            if (msg == null)
                return;

            string type = GetString(msg, "type");

            if (string.IsNullOrWhiteSpace(type))
                return;

            switch (type)
            {
                case "follow":
                case "WebcastSocialMessage":
                    await HandleFollow(msg);
                    break;

                case "chat":
                case "WebcastChatMessage":
                    await HandleChat(msg);
                    break;

                case "gift":
                case "WebcastGiftMessage":
                    await HandleGift(msg);
                    break;
            }
        }

        private async Task HandleFollow(JObject msg)
        {
            var data = GetNestedObject(msg, "data") ?? msg;
            var userIdentity = GetNestedObject(data, "userIdentity");

            string user =
                GetNestedString(data, "user", "uniqueId") ??
                GetNestedString(data, "user", "unique_id") ??
                GetNestedString(data, "user", "nickname") ??
                GetString(data, "uniqueId") ??
                GetString(data, "username") ??
                "user";

            bool isFollower = true;
            RememberFollower(user);

            if (PluginConfig.Instance.VerboseRelayLogging)
            {
                Plugin.Log?.Info("[TikTokRelay] Follow RX user=" + user + " follower=" + isFollower);
            }

            EventRouter.TriggerFollow(user);
            await Task.CompletedTask;
        }

        private async Task HandleChat(JObject msg)
        {
            var data = GetNestedObject(msg, "data") ?? msg;
            var userIdentity = GetNestedObject(data, "userIdentity");

            string user =
                GetNestedString(data, "user", "uniqueId") ??
                GetNestedString(data, "user", "unique_id") ??
                GetNestedString(data, "user", "nickname") ??
                GetString(data, "uniqueId") ??
                GetString(data, "username") ??
                "user";

            string comment =
                GetString(data, "comment") ??
                GetString(data, "content") ??
                GetString(data, "message") ??
                GetString(data, "text") ??
                "";

            bool isFollower = ResolveFollowerState(user, msg, data, userIdentity);

            if (PluginConfig.Instance.VerboseRelayLogging)
            {
                Plugin.Log?.Info("[TikTokRelay] Chat RX user=" + user +
                                  " follower=" + isFollower +
                                  " message=" + comment);
            }

            EventRouter.TriggerChatCommand(user, comment, isFollower);
            await Task.CompletedTask;
        }

        private async Task HandleGift(JObject msg)
        {
            var data = GetNestedObject(msg, "data") ?? msg;
            var common = GetNestedObject(data, "common");
            var giftDetails = GetNestedObject(data, "giftDetails");
            var userIdentity = GetNestedObject(data, "userIdentity");

            string msgId = GetString(common, "msgId") ?? "";
            if (IsDuplicateGiftMessage(msgId))
                return;

            string user =
                GetNestedString(data, "user", "uniqueId") ??
                GetNestedString(data, "user", "unique_id") ??
                GetNestedString(data, "user", "nickname") ??
                GetString(data, "uniqueId") ??
                GetString(data, "username") ??
                "user";

            string giftName =
                GetString(giftDetails, "giftName") ??
                GetNestedString(data, "gift", "name") ??
                GetString(data, "giftName") ??
                "gift";

            int repeatCount = GetInt(data, "repeatCount", 1);
            if (repeatCount < 1)
                repeatCount = 1;

            int diamondCount = GetInt(giftDetails, "diamondCount", 0);
            int totalCoins = diamondCount * repeatCount;

            bool isFollower = ResolveFollowerState(user, msg, data, userIdentity);

            if (PluginConfig.Instance.VerboseRelayLogging)
            {
                Plugin.Log?.Info("[TikTokRelay] Gift RX user=" + user +
                                  " follower=" + isFollower +
                                  " gift=" + giftName +
                                  " count=" + repeatCount +
                                  " coins=" + totalCoins);
            }

            EventRouter.TriggerGift(user, giftName, repeatCount, totalCoins, isFollower);
            await Task.CompletedTask;
        }

        private bool IsDuplicateGiftMessage(string msgId)
        {
            if (string.IsNullOrWhiteSpace(msgId))
                return false;

            lock (_recentGiftIds)
            {
                DateTime now = DateTime.UtcNow;

                var oldKeys = new List<string>();
                foreach (var kv in _recentGiftIds)
                {
                    if ((now - kv.Value).TotalSeconds > 10)
                        oldKeys.Add(kv.Key);
                }

                foreach (string key in oldKeys)
                    _recentGiftIds.Remove(key);

                if (_recentGiftIds.ContainsKey(msgId))
                    return true;

                _recentGiftIds[msgId] = now;
                return false;
            }
        }

        private static string NormalizeUserKey(string user)
        {
            if (string.IsNullOrWhiteSpace(user))
                return string.Empty;

            return user.Trim();
        }

        private void RememberFollower(string user)
        {
            string key = NormalizeUserKey(user);
            if (string.IsNullOrWhiteSpace(key))
                return;

            lock (_knownFollowersLock)
            {
                _knownFollowers.Add(key);
            }
        }

        private bool IsKnownFollower(string user)
        {
            string key = NormalizeUserKey(user);
            if (string.IsNullOrWhiteSpace(key))
                return false;

            lock (_knownFollowersLock)
            {
                return _knownFollowers.Contains(key);
            }
        }

        private bool ResolveFollowerState(string user, JObject msg, JObject data, JObject userIdentity)
        {
            bool parsedFollower = GetFollowerState(msg, data, userIdentity);

            if (parsedFollower)
            {
                RememberFollower(user);
                return true;
            }

            if (IsKnownFollower(user))
                return true;

            return false;
        }

        private void ClearKnownFollowers()
        {
            lock (_knownFollowersLock)
            {
                _knownFollowers.Clear();
            }
        }

        private void ClearRecentGiftCache()
        {
            lock (_recentGiftIds)
            {
                _recentGiftIds.Clear();
            }
        }

        private void CleanupSocket()
        {
            try
            {
                if (_ws != null)
                    _ws.Dispose();
            }
            catch { }

            _ws = null;
        }

        private static JObject GetNestedObject(JObject dict, string key)
        {
            if (dict == null)
                return null;

            return dict[key] as JObject;
        }

        private static string GetString(JObject dict, string key)
        {
            if (dict == null || dict[key] == null)
                return null;

            return dict[key].ToString();
        }

        private static string GetNestedString(JObject dict, string parentKey, string childKey)
        {
            if (dict == null)
                return null;

            var nested = dict[parentKey] as JObject;
            if (nested == null || nested[childKey] == null)
                return null;

            return nested[childKey].ToString();
        }

        private static int GetInt(JObject dict, string key, int fallback)
        {
            if (dict == null || dict[key] == null)
                return fallback;

            try
            {
                int parsed;
                if (int.TryParse(dict[key].ToString(), out parsed))
                    return parsed;
            }
            catch { }

            return fallback;
        }

        private static bool GetBool(JObject dict, string key, bool fallback)
        {
            if (dict == null || dict[key] == null)
                return fallback;

            try
            {
                bool parsed;
                string s = dict[key].ToString();

                if (bool.TryParse(s, out parsed))
                    return parsed;

                if (s == "1")
                    return true;

                if (s == "0")
                    return false;
            }
            catch { }

            return fallback;
        }

        private static bool GetFollowerState(JObject msg, JObject data, JObject userIdentity)
        {
            if (GetBool(userIdentity, "isFollowerOfAnchor", false))
                return true;

            if (GetBool(data, "isFollower", false))
                return true;

            if (GetBool(GetNestedObject(data, "user"), "isFollower", false))
                return true;

            if (GetBool(GetNestedObject(data, "user"), "isFollowerOfAnchor", false))
                return true;

            string followInfo =
                GetString(userIdentity, "followInfo") ??
                GetString(userIdentity, "relation") ??
                GetString(data, "followInfo") ??
                GetString(data, "relation") ??
                GetString(GetNestedObject(data, "user"), "followInfo") ??
                GetString(GetNestedObject(data, "user"), "relation");

            if (!string.IsNullOrWhiteSpace(followInfo))
            {
                string normalized = followInfo.Trim().ToLowerInvariant();

                if (normalized.Contains("follow"))
                    return true;
            }

            return false;
        }
    }
}