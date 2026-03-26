using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace GeminiOrbFX.UI
{
    [ViewDefinition("GeminiOrbFX.UI.Tiktok.GeminiOrbTikTokTriggersView.bsml")]
    internal class GeminiOrbTikTokTriggersViewController : BSMLAutomaticViewController
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

        private enum TriggerMode
        {
            NewFollower = 0,
            Gift = 1,
            ChatCommand = 2
        }

        private TikTokSubTab _currentSubTab = TikTokSubTab.Triggers;

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
            set
            {
                PluginConfig.Instance.TikTokTriggerMode = (int)value;
            }
        }

        private TikTokPermissionLevel ChatPermission
        {
            get
            {
                return PluginConfig.Instance.TikTokChatPermission == 1
                    ? TikTokPermissionLevel.FollowersOnly
                    : TikTokPermissionLevel.Everyone;
            }
            set
            {
                PluginConfig.Instance.TikTokChatPermission = value == TikTokPermissionLevel.FollowersOnly ? 1 : 0;
            }
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

        [UIValue("new-follower-label-text")]
        public string NewFollowerLabelText
        {
            get
            {
                return CurrentTriggerMode == TriggerMode.NewFollower
                    ? "New Follower"
                    : "<color=#8A95A3>New Follower</color>";
            }
        }

        [UIValue("gift-label-text")]
        public string GiftLabelText
        {
            get
            {
                return CurrentTriggerMode == TriggerMode.Gift
                    ? "Gift"
                    : "<color=#8A95A3>Gift</color>";
            }
        }

        [UIValue("chat-command-label-text")]
        public string ChatCommandLabelText
        {
            get
            {
                return CurrentTriggerMode == TriggerMode.ChatCommand
                    ? "Chat Command"
                    : "<color=#8A95A3>Chat Command</color>";
            }
        }

        [UIValue("chat-permission-label-text")]
        public string ChatPermissionLabelText
        {
            get
            {
                return CurrentTriggerMode == TriggerMode.ChatCommand
                    ? "Chat Permission"
                    : "<color=#8A95A3>Chat Permission</color>";
            }
        }

        [UIValue("command-text-label-text")]
        public string CommandTextLabelText
        {
            get
            {
                return CurrentTriggerMode == TriggerMode.ChatCommand
                    ? "Command Text"
                    : "<color=#8A95A3>Command Text</color>";
            }
        }

        [UIValue("new-follower-button-text")]
        public string NewFollowerButtonText
        {
            get
            {
                return CurrentTriggerMode == TriggerMode.NewFollower
                    ? "<color=#00D8FF><b>ACTIVE</b></color>"
                    : "<color=#8A95A3><b>SELECT</b></color>";
            }
        }

        [UIValue("gift-button-text")]
        public string GiftButtonText
        {
            get
            {
                return CurrentTriggerMode == TriggerMode.Gift
                    ? "<color=#00D8FF><b>ACTIVE</b></color>"
                    : "<color=#8A95A3><b>SELECT</b></color>";
            }
        }

        [UIValue("chat-command-button-text")]
        public string ChatCommandButtonText
        {
            get
            {
                return CurrentTriggerMode == TriggerMode.ChatCommand
                    ? "<color=#00D8FF><b>ACTIVE</b></color>"
                    : "<color=#8A95A3><b>SELECT</b></color>";
            }
        }

        [UIValue("chat-command-text")]
        public string ChatCommandText
        {
            get { return NormalizeCommand(PluginConfig.Instance.TikTokChatCommand); }
            set { PluginConfig.Instance.TikTokChatCommand = NormalizeCommand(value); }
        }

        [UIValue("chat-permission-text")]
        public string ChatPermissionText
        {
            get
            {
                return ChatPermission == TikTokPermissionLevel.Everyone
                    ? "<color=#00D8FF><b>EVERYONE</b></color>"
                    : "<color=#00D8FF><b>FOLLOWERS ONLY</b></color>";
            }
        }

        [UIValue("chat-permission-interactable")]
        public bool ChatPermissionInteractable
        {
            get { return CurrentTriggerMode == TriggerMode.ChatCommand; }
        }

        [UIValue("chat-command-text-interactable")]
        public bool ChatCommandTextInteractable
        {
            get { return CurrentTriggerMode == TriggerMode.ChatCommand; }
        }

        [UIAction("set-trigger-new-follower")]
        private void SetTriggerNewFollower()
        {
            CurrentTriggerMode = TriggerMode.NewFollower;
            RefreshTriggerState();
        }

        [UIAction("set-trigger-gift")]
        private void SetTriggerGift()
        {
            CurrentTriggerMode = TriggerMode.Gift;
            RefreshTriggerState();
        }

        [UIAction("set-trigger-chat-command")]
        private void SetTriggerChatCommand()
        {
            CurrentTriggerMode = TriggerMode.ChatCommand;
            RefreshTriggerState();
        }

        [UIAction("cycle-chat-permission")]
        private void CycleChatPermission()
        {
            ChatPermission = ChatPermission == TikTokPermissionLevel.Everyone
                ? TikTokPermissionLevel.FollowersOnly
                : TikTokPermissionLevel.Everyone;

            NotifyPropertyChanged(nameof(ChatPermissionText));
        }

        [UIAction("chat-command-text-changed")]
        private void OnChatCommandTextChanged(string value)
        {
            PluginConfig.Instance.TikTokChatCommand = NormalizeCommand(value);
            NotifyPropertyChanged(nameof(ChatCommandText));
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
            _currentSubTab = TikTokSubTab.Triggers;
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

        private void RefreshTriggerState()
        {
            NotifyPropertyChanged(nameof(NewFollowerLabelText));
            NotifyPropertyChanged(nameof(GiftLabelText));
            NotifyPropertyChanged(nameof(ChatCommandLabelText));
            NotifyPropertyChanged(nameof(ChatPermissionLabelText));
            NotifyPropertyChanged(nameof(CommandTextLabelText));

            NotifyPropertyChanged(nameof(NewFollowerButtonText));
            NotifyPropertyChanged(nameof(GiftButtonText));
            NotifyPropertyChanged(nameof(ChatCommandButtonText));

            NotifyPropertyChanged(nameof(ChatPermissionInteractable));
            NotifyPropertyChanged(nameof(ChatCommandTextInteractable));

            NotifyPropertyChanged(nameof(ChatPermissionText));
            NotifyPropertyChanged(nameof(ChatCommandText));

            NotifyPropertyChanged(nameof(GiftSettingsSubTabText));
            NotifyPropertyChanged(nameof(GiftSettingsEnabled));
        }

        private void RefreshAll()
        {
            NotifyPropertyChanged(nameof(MainTabText));
            NotifyPropertyChanged(nameof(OrbControlsTabText));
            NotifyPropertyChanged(nameof(TikTokTabText));
            NotifyPropertyChanged(nameof(AboutTabText));

            RefreshTabs();
            RefreshTriggerState();
        }
    }
}