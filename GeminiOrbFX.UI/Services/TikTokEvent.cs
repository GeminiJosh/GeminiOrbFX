namespace GeminiOrbFX.UI.Services
{
    internal enum TikTokEventType
    {
        Follow,
        Gift,
        ChatCommand
    }

    internal struct TikTokEvent
    {
        public TikTokEventType Type;
        public string Username;
        public string Message;
        public int Amount;

        public string GiftName;
        public int TotalCoins;
        public bool IsFollower;
    }
}