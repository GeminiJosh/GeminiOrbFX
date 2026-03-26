using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections;
using UnityEngine;

namespace GeminiOrbFX.UI
{
    [ViewDefinition("GeminiOrbFX.UI.Tiktok.GeminiOrbTikTokConnectionView.bsml")]
    internal class GeminiOrbTikTokConnectionViewController : BSMLAutomaticViewController
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

        private enum RelayMode
        {
            TikTokRelay = 0,
            HttpOnly = 1
        }

        private TikTokSubTab _currentSubTab = TikTokSubTab.Connection;
        private Coroutine _statusRefreshCoroutine;

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

        private RelayMode CurrentRelayMode
        {
            get
            {
                return PluginConfig.Instance.RelayMode == 1
                    ? RelayMode.HttpOnly
                    : RelayMode.TikTokRelay;
            }
            set
            {
                PluginConfig.Instance.RelayMode = value == RelayMode.HttpOnly ? 1 : 0;
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

        [UIValue("relay-mode-label")]
        public string RelayModeLabel => "SERVICE MODE";

        [UIValue("relay-mode-text")]
        public string RelayModeText
        {
            get
            {
                return CurrentRelayMode == RelayMode.TikTokRelay
                    ? "<color=#00D8FF><b>TIKTOK RELAY</b></color>"
                    : "<color=#FFD966><b>STREAMER-BOT</b></color>";
            }
        }

        [UIValue("relay-mode-description")]
        public string RelayModeDescription
        {
            get
            {
                return CurrentRelayMode == RelayMode.TikTokRelay
                    ? "Built-in TikTok live relay"
                    : "Use Streamer.bot / HTTP integrations";
            }
        }

        [UIValue("relay-connect-enabled")]
        public bool RelayConnectEnabled => CurrentRelayMode == RelayMode.TikTokRelay;

        [UIValue("relay-username-enabled")]
        public bool RelayUsernameEnabled => true;

        [UIValue("connect-button-text")]
        public string ConnectButtonText => "<color=#66FF66><b>SAVE + CONNECT</b></color>";

        [UIValue("disconnect-button-text")]
        public string DisconnectButtonText => "<color=#FF5555><b>CLEAR USERNAME</b></color>";

        [UIValue("tiktok-status")]
        public string TikTokStatus
        {
            get
            {
                if (CurrentRelayMode == RelayMode.HttpOnly)
                    return "<color=#FFD966><b>STREAMER-BOT</b></color>";

                switch (Services.RelayStatusService.State)
                {
                    case "connected":
                        return "<color=#66FF66><b>TIKTOK CONNECTED</b></color>";

                    case "connecting":
                    case "starting":
                        return "<color=#FFD966><b>TIKTOK CONNECTING</b></color>";

                    case "error":
                        if (!string.IsNullOrWhiteSpace(Services.RelayStatusService.Message) && PluginConfig.Instance.VerboseRelayLogging)
                            return $"<color=#FF5555><b>TIKTOK ERROR:</b></color> {Services.RelayStatusService.Message}";

                        return "<color=#FF5555><b>TIKTOK ERROR</b></color>";

                    default:
                        return "<color=#FF5555><b>TIKTOK DISCONNECTED</b></color>";
                }
            }
        }

        [UIValue("tiktok-username")]
        public string TikTokUsername
        {
            get
            {
                return PluginConfig.Instance.TikTokUsername ?? string.Empty;
            }
            set
            {
                PluginConfig.Instance.TikTokUsername = value ?? string.Empty;
            }
        }

        [UIAction("tiktok-username-changed")]
        private void OnTikTokUsernameChanged(string value)
        {
            PluginConfig.Instance.TikTokUsername = value == null ? string.Empty : value.Trim();
            NotifyPropertyChanged(nameof(TikTokUsername));
        }

        [UIAction("cycle-relay-mode")]
        private void CycleRelayMode()
        {
            if (CurrentRelayMode == RelayMode.TikTokRelay)
            {
                CurrentRelayMode = RelayMode.HttpOnly;
                Plugin.StopTikTokRelay();
            }
            else
            {
                CurrentRelayMode = RelayMode.TikTokRelay;
            }

            Plugin.NotifyRelayModeChanged();
            RefreshAll();
        }

        [UIAction("connect-tiktok")]
        private void ConnectTikTok()
        {
            string username = (PluginConfig.Instance.TikTokUsername ?? "").Trim();

            if (string.IsNullOrWhiteSpace(username))
                return;

            PluginConfig.Instance.TikTokUsername = username;

            if (CurrentRelayMode == RelayMode.TikTokRelay)
            {
                Plugin.StopTikTokRelay();
                Plugin.StartTikTokRelay(username);
            }

            RefreshAll();
        }

        [UIAction("disconnect-tiktok")]
        private void DisconnectTikTok()
        {
            if (CurrentRelayMode == RelayMode.TikTokRelay)
                Plugin.StopTikTokRelay();

            PluginConfig.Instance.TikTokUsername = string.Empty;

            RefreshAll();
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

            _currentSubTab = TikTokSubTab.Connection;

            Plugin.RelayModeChanged -= OnRelayModeChanged;
            Plugin.RelayModeChanged += OnRelayModeChanged;

            Plugin.RelayStatusChanged -= OnRelayStatusChanged;
            Plugin.RelayStatusChanged += OnRelayStatusChanged;

            if (firstActivation && _statusRefreshCoroutine == null)
                _statusRefreshCoroutine = StartCoroutine(StatusRefreshLoop());

            RefreshAll();
        }

        private IEnumerator StatusRefreshLoop()
        {
            while (true)
            {
                NotifyPropertyChanged(nameof(TikTokUsername));
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void OnRelayModeChanged()
        {
            RefreshAll();
        }

        private void OnRelayStatusChanged()
        {
            NotifyPropertyChanged(nameof(TikTokStatus));
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

            NotifyPropertyChanged(nameof(RelayModeLabel));
            NotifyPropertyChanged(nameof(RelayModeText));
            NotifyPropertyChanged(nameof(RelayModeDescription));
            NotifyPropertyChanged(nameof(RelayConnectEnabled));
            NotifyPropertyChanged(nameof(RelayUsernameEnabled));

            NotifyPropertyChanged(nameof(ConnectButtonText));
            NotifyPropertyChanged(nameof(DisconnectButtonText));
            NotifyPropertyChanged(nameof(TikTokStatus));
            NotifyPropertyChanged(nameof(TikTokUsername));
        }

        protected override void OnDestroy()
        {
            Plugin.RelayModeChanged -= OnRelayModeChanged;
            Plugin.RelayStatusChanged -= OnRelayStatusChanged;

            if (_statusRefreshCoroutine != null)
            {
                StopCoroutine(_statusRefreshCoroutine);
                _statusRefreshCoroutine = null;
            }

            base.OnDestroy();
        }
    }
}