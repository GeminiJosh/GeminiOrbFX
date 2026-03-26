using System;
using UnityEngine;

namespace GeminiOrbFX.UI.Services
{
    internal static class EventRouter
    {
        private const int TriggerModeFollow = 0;
        private const int TriggerModeGift = 1;
        private const int TriggerModeChatCommand = 2;

        private const int PermissionEveryone = 0;
        private const int PermissionFollowersOnly = 1;

        private const int GiftModeAnyGift = 0;
        private const int GiftModeSpecificGift = 1;
        private const int GiftModeCoinThreshold = 2;

        public static bool TriggerOrb(
            string username,
            int? lane = null,
            float? speed = null,
            Color? color = null,
            float? h = null,
            float? s = null,
            float? v = null,
            bool isTest = false)
        {
            if (string.IsNullOrWhiteSpace(username))
                username = "EVENT";

            OrbRequest req = new OrbRequest
            {
                Name = username.ToUpperInvariant(),
                Lane = lane,
                Speed = speed,
                Color = color,
                H = h,
                S = s,
                V = v,
                IsTest = isTest
            };

            return Plugin.EnqueueOrbRequest(req, out _);
        }

        public static bool TriggerFollow(string username)
        {
            if (PluginConfig.Instance.TikTokTriggerMode != TriggerModeFollow)
                return false;

            return TriggerOrb(username);
        }

        public static bool TriggerGift(string username, string giftName, int amount, int totalCoins, bool isFollower)
        {
            if (PluginConfig.Instance.TikTokTriggerMode != TriggerModeGift)
                return false;

            if (PluginConfig.Instance.TikTokGiftPermission == PermissionFollowersOnly && !isFollower)
                return false;

            if (amount < 1)
                amount = 1;

            if (totalCoins < 0)
                totalCoins = 0;

            int giftMode = PluginConfig.Instance.TikTokGiftMode;

            if (giftMode == GiftModeSpecificGift)
            {
                string expectedGift = NormalizeGiftName(PluginConfig.Instance.TikTokGiftName);
                string incomingGift = NormalizeGiftName(giftName);

                if (string.IsNullOrWhiteSpace(expectedGift))
                    return false;

                if (!string.Equals(incomingGift, expectedGift, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else if (giftMode == GiftModeCoinThreshold)
            {
                int minCoins = PluginConfig.Instance.TikTokGiftMinCoins;
                if (minCoins < 1)
                    minCoins = 1;

                if (totalCoins < minCoins)
                    return false;
            }

            return TriggerOrb(username);
        }

        public static bool TriggerChatCommand(string username, string message, bool isFollower)
        {
            if (PluginConfig.Instance.TikTokTriggerMode != TriggerModeChatCommand)
                return false;

            if (PluginConfig.Instance.TikTokChatPermission == PermissionFollowersOnly && !isFollower)
                return false;

            if (string.IsNullOrWhiteSpace(message))
                return false;

            string expectedCommand = NormalizeCommand(PluginConfig.Instance.TikTokChatCommand);
            string incomingMessage = message.Trim();

            if (incomingMessage.StartsWith(expectedCommand, StringComparison.OrdinalIgnoreCase))
                return TriggerOrb(username);

            return false;
        }

        private static string NormalizeCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "!orb";

            command = command.Trim();

            if (!command.StartsWith("!"))
                command = "!" + command;

            return command;
        }

        private static string NormalizeGiftName(string giftName)
        {
            if (string.IsNullOrWhiteSpace(giftName))
                return string.Empty;

            return giftName.Trim();
        }
    }
}