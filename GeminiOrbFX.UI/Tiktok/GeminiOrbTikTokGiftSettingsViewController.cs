using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace GeminiOrbFX.UI
{
    [ViewDefinition("GeminiOrbFX.UI.Tiktok.GeminiOrbTikTokGiftSettingsView.bsml")]
    internal class GeminiOrbTikTokGiftSettingsViewController : BSMLAutomaticViewController
    {
        private enum TikTokSubTab
        {
            Connection,
            Triggers,
            GiftSettings,
            Debug
        }

        private enum TikTokPermissionLevel
        {
            Everyone,
            FollowersOnly
        }

        private enum GiftTriggerMode
        {
            AnyGift,
            SpecificGift,
            CoinThreshold
        }

        private enum TriggerMode
        {
            NewFollower = 0,
            Gift = 1,
            ChatCommand = 2
        }

        private TikTokSubTab _currentSubTab = TikTokSubTab.GiftSettings;

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

        private TikTokPermissionLevel GiftPermission
        {
            get
            {
                return PluginConfig.Instance.TikTokGiftPermission == 1
                    ? TikTokPermissionLevel.FollowersOnly
                    : TikTokPermissionLevel.Everyone;
            }
            set
            {
                PluginConfig.Instance.TikTokGiftPermission = value == TikTokPermissionLevel.FollowersOnly ? 1 : 0;
            }
        }

        private GiftTriggerMode CurrentGiftMode
        {
            get
            {
                switch (PluginConfig.Instance.TikTokGiftMode)
                {
                    case 1:
                        return GiftTriggerMode.SpecificGift;
                    case 2:
                        return GiftTriggerMode.CoinThreshold;
                    default:
                        return GiftTriggerMode.AnyGift;
                }
            }
            set
            {
                PluginConfig.Instance.TikTokGiftMode = (int)value;
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

        [UIValue("gift-permission-text")]
        public string GiftPermissionText
        {
            get
            {
                return GiftPermission == TikTokPermissionLevel.Everyone
                    ? "<color=#00D8FF><b>EVERYONE</b></color>"
                    : "<color=#00D8FF><b>FOLLOWERS ONLY</b></color>";
            }
        }

        [UIValue("gift-mode-text")]
        public string GiftModeText
        {
            get
            {
                switch (CurrentGiftMode)
                {
                    case GiftTriggerMode.AnyGift:
                        return "<color=#00D8FF><b>ANY GIFT</b></color>";
                    case GiftTriggerMode.SpecificGift:
                        return "<color=#00D8FF><b>SPECIFIC GIFT</b></color>";
                    case GiftTriggerMode.CoinThreshold:
                        return "<color=#00D8FF><b>COIN THRESHOLD</b></color>";
                    default:
                        return "<color=#00D8FF><b>ANY GIFT</b></color>";
                }
            }
        }

        [UIValue("gift-name-text")]
        public string GiftNameText
        {
            get { return string.IsNullOrWhiteSpace(PluginConfig.Instance.TikTokGiftName) ? "Heart Me" : PluginConfig.Instance.TikTokGiftName; }
            set { PluginConfig.Instance.TikTokGiftName = value ?? string.Empty; }
        }

        [UIValue("gift-min-coins-text")]
        public string GiftMinCoinsText
        {
            get { return PluginConfig.Instance.TikTokGiftMinCoins.ToString(); }
            set
            {
                int parsed;
                if (int.TryParse(value, out parsed))
                    PluginConfig.Instance.TikTokGiftMinCoins = parsed;
            }
        }

        [UIValue("gift-name-label-text")]
        public string GiftNameLabelText
        {
            get
            {
                return CurrentGiftMode == GiftTriggerMode.SpecificGift
                    ? "Gift Name"
                    : "<color=#8A95A3>Gift Name</color>";
            }
        }

        [UIValue("gift-min-coins-label-text")]
        public string GiftMinCoinsLabelText
        {
            get
            {
                return CurrentGiftMode == GiftTriggerMode.CoinThreshold
                    ? "Min Coins"
                    : "<color=#8A95A3>Min Coins</color>";
            }
        }

        [UIValue("gift-name-interactable")]
        public bool GiftNameInteractable
        {
            get { return CurrentGiftMode == GiftTriggerMode.SpecificGift; }
        }

        [UIValue("gift-min-coins-interactable")]
        public bool GiftMinCoinsInteractable
        {
            get { return CurrentGiftMode == GiftTriggerMode.CoinThreshold; }
        }

        [UIAction("cycle-gift-permission")]
        private void CycleGiftPermission()
        {
            GiftPermission = GiftPermission == TikTokPermissionLevel.Everyone
                ? TikTokPermissionLevel.FollowersOnly
                : TikTokPermissionLevel.Everyone;

            NotifyPropertyChanged(nameof(GiftPermissionText));
        }

        [UIAction("cycle-gift-mode")]
        private void CycleGiftMode()
        {
            switch (CurrentGiftMode)
            {
                case GiftTriggerMode.AnyGift:
                    CurrentGiftMode = GiftTriggerMode.SpecificGift;
                    break;

                case GiftTriggerMode.SpecificGift:
                    CurrentGiftMode = GiftTriggerMode.CoinThreshold;
                    break;

                case GiftTriggerMode.CoinThreshold:
                    CurrentGiftMode = GiftTriggerMode.AnyGift;
                    break;

                default:
                    CurrentGiftMode = GiftTriggerMode.AnyGift;
                    break;
            }

            NotifyPropertyChanged(nameof(GiftModeText));
            NotifyPropertyChanged(nameof(GiftNameLabelText));
            NotifyPropertyChanged(nameof(GiftMinCoinsLabelText));
            NotifyPropertyChanged(nameof(GiftNameInteractable));
            NotifyPropertyChanged(nameof(GiftMinCoinsInteractable));
            NotifyPropertyChanged(nameof(GiftNameText));
            NotifyPropertyChanged(nameof(GiftMinCoinsText));
        }

        [UIAction("gift-name-text-changed")]
        private void OnGiftNameTextChanged(string value)
        {
            PluginConfig.Instance.TikTokGiftName = value == null ? string.Empty : value.Trim();
            NotifyPropertyChanged(nameof(GiftNameText));
        }

        [UIAction("gift-min-coins-text-changed")]
        private void OnGiftMinCoinsTextChanged(string value)
        {
            int parsed;
            PluginConfig.Instance.TikTokGiftMinCoins = int.TryParse(value, out parsed) ? parsed : 100;
            NotifyPropertyChanged(nameof(GiftMinCoinsText));
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
            _currentSubTab = TikTokSubTab.GiftSettings;
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

            NotifyPropertyChanged(nameof(GiftPermissionText));
            NotifyPropertyChanged(nameof(GiftModeText));
            NotifyPropertyChanged(nameof(GiftNameText));
            NotifyPropertyChanged(nameof(GiftMinCoinsText));
            NotifyPropertyChanged(nameof(GiftNameLabelText));
            NotifyPropertyChanged(nameof(GiftMinCoinsLabelText));
            NotifyPropertyChanged(nameof(GiftNameInteractable));
            NotifyPropertyChanged(nameof(GiftMinCoinsInteractable));
        }
    }
}