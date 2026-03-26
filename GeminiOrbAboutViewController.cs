using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using UnityEngine;

namespace GeminiOrbFX.UI
{
    [ViewDefinition("GeminiOrbFX.UI.UI_Views.GeminiOrbAboutView.bsml")]
    internal class GeminiOrbAboutViewController : BSMLAutomaticViewController
    {
        internal GeminiOrbFlowCoordinator FlowCoordinator { get; set; }

        [UIValue("main-tab-text")]
        public string MainTabText => "MAIN";

        [UIValue("orbcontrols-tab-text")]
        public string OrbControlsTabText => "ORB CONTROLS";

        [UIValue("tiktok-tab-text")]
        public string TikTokTabText => "TIKTOK";

        [UIValue("about-tab-text")]
        public string AboutTabText => "<color=#00D8FF><b>ABOUT</b></color>";

        [UIValue("title-text")]
        public string TitleText => "<color=#00D8FF><b>GEMINIORBFX</b></color>";

        [UIValue("subtitle-text")]
        public string SubtitleText => "Interactive Orb Effects for Beat Saber.";

        [UIValue("version-label")]
        public string VersionLabel => "<color=#FFD966><b>Version</b></color>";

        [UIValue("version-value")]
        public string VersionValue => "2.0.0-ui";

        [UIValue("features-label")]
        public string FeaturesLabel => "<color=#00D8FF><b>Built-In TikTok Features</b></color>";

        [UIValue("feature-1")]
        public string Feature1 => "• Chat command triggers";

        [UIValue("feature-2")]
        public string Feature2 => "• Follow triggers";

        [UIValue("feature-3")]
        public string Feature3 => "• Gift triggers";

        [UIValue("feature-4")]
        public string Feature4 => "• Followers-only filtering";

        [UIValue("feature-5")]
        public string Feature5 => "• Specific gift filtering";

        [UIValue("feature-6")]
        public string Feature6 => "• Coin threshold filtering";

        [UIValue("feature-7")]
        public string Feature7 => "• Relay auto-start support";

        [UIValue("how-label")]
        public string HowLabel => "<color=#00D8FF><b>How It Works</b></color>";

        [UIValue("how-1")]
        public string How1 => "• Enter your TikTok username in the TikTok Connection tab";

        [UIValue("how-2")]
        public string How2 => "• The relay connects automatically once your username is saved";

        [UIValue("how-3")]
        public string How3 => "• Events are forwarded locally to the mod";

        [UIValue("how-4")]
        public string How4 => "• Orbs spawn during gameplay from your selected trigger setting";

        [UIValue("credits-label")]
        public string CreditsLabel => "<color=#00D8FF><b>Credits</b></color>";

        [UIValue("credit-1")]
        public string Credit1 => "Created by Josh / GeminiJoshVR";

        [UIValue("discord-button-text")]
        public string DiscordButtonText => "<color=#00D8FF><b>JOIN DISCORD</b></color>";

        [UIAction("open-discord")]
        private void OpenDiscord()
        {
            Application.OpenURL("https://discord.gg/swiftyandjoshiesparadise");
        }

        [UIAction("show-main")]
        private void ShowMain()
        {
            FlowCoordinator?.ShowMain();
        }

        [UIAction("show-orbcontrols")]
        private void ShowOrbControls()
        {
            FlowCoordinator?.ShowOrbControls();
        }

        [UIAction("show-tiktok")]
        private void ShowTikTok()
        {
            FlowCoordinator?.ShowTikTok();
        }

        [UIAction("show-about")]
        private void ShowAbout()
        {
            FlowCoordinator?.ShowAbout();
        }
    }
}