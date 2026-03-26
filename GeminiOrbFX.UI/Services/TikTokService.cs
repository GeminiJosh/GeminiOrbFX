using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace GeminiOrbFX.UI.Services
{
    internal class TikTokService
    {
        private readonly Plugin _plugin;

        private readonly ConcurrentQueue<TikTokEvent> _pendingEvents = new ConcurrentQueue<TikTokEvent>();

        private const int MaxEventsPerTick = 8;

        private const float GlobalTriggerCooldownSeconds = 0.35f;
        private const float PerUserTriggerCooldownSeconds = 1.5f;

        private float _lastAcceptedTriggerRealtime = -999f;
        private readonly Dictionary<string, float> _lastAcceptedTriggerByUser = new Dictionary<string, float>();

        public TikTokService(Plugin plugin)
        {
            _plugin = plugin;
        }

        public void Tick()
        {
            int processed = 0;

            while (processed < MaxEventsPerTick && _pendingEvents.TryDequeue(out TikTokEvent evt))
            {
                RouteEvent(evt);
                processed++;
            }
        }

        private void RouteEvent(TikTokEvent evt)
        {
            switch (evt.Type)
            {
                case TikTokEventType.Follow:
                    if (CanAcceptTrigger(evt.Username))
                        EventRouter.TriggerFollow(evt.Username);
                    break;

                case TikTokEventType.Gift:
                    if (CanAcceptTrigger(evt.Username))
                        EventRouter.TriggerGift(evt.Username, evt.GiftName, evt.Amount, evt.TotalCoins, evt.IsFollower);
                    break;

                case TikTokEventType.ChatCommand:
                    if (CanAcceptTrigger(evt.Username))
                        EventRouter.TriggerChatCommand(evt.Username, evt.Message, evt.IsFollower);
                    break;
            }
        }

        private bool CanAcceptTrigger(string username)
        {
            float now = Time.realtimeSinceStartup;

            if (now - _lastAcceptedTriggerRealtime < GlobalTriggerCooldownSeconds)
                return false;

            string key = string.IsNullOrWhiteSpace(username)
                ? "UNKNOWN"
                : username.Trim().ToUpperInvariant();

            if (_lastAcceptedTriggerByUser.TryGetValue(key, out float lastUserTime))
            {
                if (now - lastUserTime < PerUserTriggerCooldownSeconds)
                    return false;
            }

            _lastAcceptedTriggerRealtime = now;
            _lastAcceptedTriggerByUser[key] = now;

            CleanupOldUserCooldowns(now);
            return true;
        }

        private void CleanupOldUserCooldowns(float now)
        {
            if (_lastAcceptedTriggerByUser.Count <= 32)
                return;

            var toRemove = new List<string>();

            foreach (var kvp in _lastAcceptedTriggerByUser)
            {
                if (now - kvp.Value > PerUserTriggerCooldownSeconds * 4f)
                    toRemove.Add(kvp.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _lastAcceptedTriggerByUser.Remove(toRemove[i]);
        }

        private void ClearRateLimitState()
        {
            _lastAcceptedTriggerRealtime = -999f;
            _lastAcceptedTriggerByUser.Clear();
        }

        private string NormalizeCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "!orb";

            command = command.Trim();

            if (!command.StartsWith("!"))
                command = "!" + command;

            return command;
        }

        public void ClearPendingEvents()
        {
            while (_pendingEvents.TryDequeue(out _)) { }
        }

        public void ResetDebugState()
        {
            ClearPendingEvents();
            ClearRateLimitState();
        }

        public void OnFollow(string uniqueId, bool isFollower = true)
        {
            _pendingEvents.Enqueue(new TikTokEvent
            {
                Type = TikTokEventType.Follow,
                Username = uniqueId,
                Message = null,
                Amount = 0,
                TotalCoins = 0,
                GiftName = null,
                IsFollower = isFollower
            });
        }

        public void OnGift(string uniqueId, string giftName, int amount, int totalCoins, bool isFollower)
        {
            _pendingEvents.Enqueue(new TikTokEvent
            {
                Type = TikTokEventType.Gift,
                Username = uniqueId,
                Message = null,
                Amount = amount,
                TotalCoins = totalCoins,
                GiftName = giftName,
                IsFollower = isFollower
            });
        }

        public void OnChatMessage(string uniqueId, string message, bool isFollower)
        {
            _pendingEvents.Enqueue(new TikTokEvent
            {
                Type = TikTokEventType.ChatCommand,
                Username = uniqueId,
                Message = message,
                Amount = 0,
                TotalCoins = 0,
                GiftName = null,
                IsFollower = isFollower
            });
        }

        public void SimulateFollow(string uniqueId = "TIKTOK_USER")
        {
            OnFollow(uniqueId, true);
        }

        public void SimulateGift(string uniqueId = "TIKTOK_GIFTER", int amount = 1)
        {
            OnGift(uniqueId, "Heart Me", amount, amount, true);
        }

        public void SimulateChatCommand(string uniqueId = "TIKTOK_CHAT", string message = null)
        {
            string command = NormalizeCommand(
                string.IsNullOrWhiteSpace(message)
                    ? PluginConfig.Instance.TikTokChatCommand
                    : message);

            OnChatMessage(uniqueId, command, true);
        }
    }
}