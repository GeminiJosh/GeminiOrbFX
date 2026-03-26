using IPA.Config.Stores;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace GeminiOrbFX.UI
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }

        public virtual float OrbHeight { get; set; } = 0.80f;
        public virtual float SpawnDistance { get; set; } = 34.0f;
        public virtual float OrbSpeed { get; set; } = 10.5f;

        public virtual float TrailWidth { get; set; } = 0.11f;
        public virtual float TrailBrightness { get; set; } = 2.8f;
        public virtual float OrbBrightness { get; set; } = 1.0f;
        public virtual float NameTextScale { get; set; } = 1.55f;

        public virtual float SpawnRate { get; set; } = 0.75f;
        public virtual int MaxLiveOrbs { get; set; } = 10;
        public virtual int MaxQueue { get; set; } = 50;

        public virtual bool OrbTestEnabled { get; set; } = false;
        public virtual float OrbTestIntervalMs { get; set; } = 100f;

        // TikTok connection
        public virtual string TikTokUsername { get; set; } = string.Empty;

        // Relay mode
        // 0 = TikTok Relay, 1 = HTTP Only
        public virtual int RelayMode { get; set; } = 0;
        public virtual bool RelayAutoReconnect { get; set; } = true;
        public virtual int RelayReconnectDelaySeconds { get; set; } = 5;
        public virtual bool VerboseRelayLogging { get; set; } = false;

        public virtual string EulerApiKey { get; set; } = string.Empty;

        // Trigger selection
        // 0 = NewFollower, 1 = Gift, 2 = ChatCommand
        public virtual int TikTokTriggerMode { get; set; } = 0;

        // Chat settings
        public virtual string TikTokChatCommand { get; set; } = "!orb";
        // 0 = Everyone, 1 = FollowersOnly
        public virtual int TikTokChatPermission { get; set; } = 0;

        // Gift settings
        // 0 = Everyone, 1 = FollowersOnly
        public virtual int TikTokGiftPermission { get; set; } = 0;
        // 0 = AnyGift, 1 = SpecificGift, 2 = CoinThreshold
        public virtual int TikTokGiftMode { get; set; } = 0;
        public virtual string TikTokGiftName { get; set; } = "Heart Me";
        public virtual int TikTokGiftMinCoins { get; set; } = 100;
    }
}