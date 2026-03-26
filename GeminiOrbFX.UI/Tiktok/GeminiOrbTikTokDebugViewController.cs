using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace GeminiOrbFX.UI
{
    [ViewDefinition("GeminiOrbFX.UI.Tiktok.GeminiOrbTikTokDebugView.bsml")]
    internal class GeminiOrbTikTokDebugViewController : BSMLAutomaticViewController
    {
        private enum TikTokSubTab
        {
            Connection,
            Triggers,
            GiftSettings,
            Debug
        }

        private enum TriggerMode
        {
            NewFollower = 0,
            Gift = 1,
            ChatCommand = 2
        }

        private TikTokSubTab _currentSubTab = TikTokSubTab.Debug;

        internal GeminiOrbFlowCoordinator FlowCoordinator { get; set; }

        private TriggerMode CurrentTriggerMode
        {
            get
            {
                switch (PluginConfig.Instance.TikTokTriggerMode)
                {
                    case 1:
                        return TriggerMode.Gift;
                    case 2:
                        return TriggerMode.ChatCommand;
                    default:
                        return TriggerMode.NewFollower;
                }
            }
        }

        [UIValue("main-tab-text")]
        public string MainTabText => "MAIN";

        [UIValue("orbcontrols-tab-text")]
        public string OrbControlsTabText => "ORB CONTROLS";

        [UIValue("tiktok-tab-text")]
        public string TikTokTabText => "<color=#00D8FF><b>TIKTOK</b></color>";

        [UIValue("about-tab-text")]
        public string AboutTabText => "ABOUT";

        [UIValue("connection-subtab-text")]
        public string ConnectionSubTabText
        {
            get
            {
                return _currentSubTab == TikTokSubTab.Connection
                    ? "<color=#00D8FF><b>CONNECTION</b></color>"
                    : "CONNECTION";
            }
        }

        [UIValue("triggers-subtab-text")]
        public string TriggersSubTabText
        {
            get
            {
                return _currentSubTab == TikTokSubTab.Triggers
                    ? "<color=#00D8FF><b>TRIGGERS</b></color>"
                    : "TRIGGERS";
            }
        }

        [UIValue("giftsettings-subtab-text")]
        public string GiftSettingsSubTabText
        {
            get
            {
                if (CurrentTriggerMode != TriggerMode.Gift)
                    return "<color=#8A95A3>GIFT SETTINGS</color>";

                return _currentSubTab == TikTokSubTab.GiftSettings
                    ? "<color=#00D8FF><b>GIFT SETTINGS</b></color>"
                    : "GIFT SETTINGS";
            }
        }

        [UIValue("gift-settings-enabled")]
        public bool GiftSettingsEnabled
        {
            get { return CurrentTriggerMode == TriggerMode.Gift; }
        }

        [UIValue("debug-subtab-text")]
        public string DebugSubTabText
        {
            get
            {
                return _currentSubTab == TikTokSubTab.Debug
                    ? "<color=#00D8FF><b>DEBUG</b></color>"
                    : "DEBUG";
            }
        }

        [UIValue("sim-follow-button-text")]
        public string SimFollowButtonText => "<color=#00D8FF><b>SIM FOLLOW</b></color>";

        [UIValue("sim-gift-button-text")]
        public string SimGiftButtonText => "<color=#00D8FF><b>SIM GIFT</b></color>";

        [UIValue("sim-chat-button-text")]
        public string SimChatButtonText => "<color=#00D8FF><b>SIM CHAT</b></color>";

        [UIAction("simulate-follow")]
        private void SimulateFollow()
        {
            var service = Plugin.GetTikTokService();
            if (service == null)
                return;

            service.SimulateFollow("SIMFOLLOW");
        }

        [UIAction("simulate-gift")]
        private void SimulateGift()
        {
            var service = Plugin.GetTikTokService();
            if (service == null)
                return;

            service.SimulateGift("SIMGIFT", 100);
        }

        [UIAction("simulate-chat")]
        private void SimulateChat()
        {
            var service = Plugin.GetTikTokService();
            if (service == null)
                return;

            var command = string.IsNullOrWhiteSpace(PluginConfig.Instance.TikTokChatCommand)
                ? "!orb"
                : PluginConfig.Instance.TikTokChatCommand;

            service.SimulateChatCommand("SIMCHAT", command);
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
            FlowCoordinator?.ShowTikTokConnection();
        }

        [UIAction("show-about")]
        private void ShowAbout()
        {
            FlowCoordinator?.ShowAbout();
        }

        [UIAction("show-connection-subtab")]
        private void ShowConnectionSubTab()
        {
            FlowCoordinator?.ShowTikTokConnection();
        }

        [UIAction("show-triggers-subtab")]
        private void ShowTriggersSubTab()
        {
            FlowCoordinator?.ShowTikTokTriggers();
        }

        [UIAction("show-giftsettings-subtab")]
        private void ShowGiftSettingsSubTab()
        {
            if (GiftSettingsEnabled)
                FlowCoordinator?.ShowTikTokGiftSettings();
        }

        [UIAction("show-debug-subtab")]
        private void ShowDebugSubTab()
        {
            FlowCoordinator?.ShowTikTokDebug();
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            _currentSubTab = TikTokSubTab.Debug;
            RefreshAll();
        }

        private void RefreshTabs()
        {
            NotifyPropertyChanged(nameof(ConnectionSubTabText));
            NotifyPropertyChanged(nameof(TriggersSubTabText));
            NotifyPropertyChanged(nameof(GiftSettingsSubTabText));
            NotifyPropertyChanged(nameof(GiftSettingsEnabled));
            NotifyPropertyChanged(nameof(DebugSubTabText));
        }

        private void RefreshAll()
        {
            NotifyPropertyChanged(nameof(MainTabText));
            NotifyPropertyChanged(nameof(OrbControlsTabText));
            NotifyPropertyChanged(nameof(TikTokTabText));
            NotifyPropertyChanged(nameof(AboutTabText));

            RefreshTabs();

            NotifyPropertyChanged(nameof(SimFollowButtonText));
            NotifyPropertyChanged(nameof(SimGiftButtonText));
            NotifyPropertyChanged(nameof(SimChatButtonText));
        }
    }
}