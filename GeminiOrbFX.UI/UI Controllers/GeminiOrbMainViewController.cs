using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections;
using UnityEngine;

namespace GeminiOrbFX.UI
{
    [ViewDefinition("GeminiOrbFX.UI.GeminiOrbMainView.bsml")]
    internal class GeminiOrbMainViewController : BSMLAutomaticViewController
    {
        internal GeminiOrbFlowCoordinator FlowCoordinator { get; set; }

        private string _orbTestMessage = "";
        private string _titleText = "GeminiOrbFX";

        private Coroutine _rainbowCoroutine;
        private Coroutine _statusRefreshCoroutine;

        [UIValue("title-text")]
        public string TitleText => _titleText;

        [UIValue("queue-count")]
        public string QueueCount
        {
            get
            {
                int current = Plugin.GetQueueCount();
                int max = PluginConfig.Instance.MaxQueue;

                float ratio = max > 0 ? (float)current / max : 0f;

                if (ratio >= 0.90f)
                    return $"<color=#FF5555>{current} / {max}</color>";

                if (ratio >= 0.75f)
                    return $"<color=#FFA500>{current} / {max}</color>";

                return $"{current} / {max}";
            }
        }

        [UIValue("can-clear-queue")]
        public bool CanClearQueue => Plugin.GetQueueCount() > 0;

        [UIValue("clear-queue-button-text")]
        public string ClearQueueButtonText =>
            CanClearQueue
                ? "<color=#00D8FF><b>CLEAR QUEUE</b></color>"
                : "<color=#8A95A3><b>CLEAR QUEUE</b></color>";

        [UIValue("main-tab-text")]
        public string MainTabText => "<color=#00D8FF><b>MAIN</b></color>";

        [UIValue("orbcontrols-tab-text")]
        public string OrbControlsTabText => "ORB CONTROLS";

        [UIValue("tiktok-tab-text")]
        public string TikTokTabText => "TIKTOK";

        [UIValue("about-tab-text")]
        public string AboutTabText => "ABOUT";

        [UIValue("http-connection-state")]
        public string HttpConnectionState =>
            Plugin.IsHttpServerRunning()
                ? "<color=#66FF66><b>ACTIVE</b></color>"
                : "<color=#FF5555><b>INACTIVE</b></color>";

        [UIValue("tiktok-connection-state")]
        public string TikTokConnectionState
        {
            get
            {
                if (Plugin.IsHttpOnlyMode())
                    return "<color=#FFD966><b>STREAMER-BOT</b></color>";

                switch (Services.RelayStatusService.State)
                {
                    case "connected":
                        return "<color=#66FF66><b>TIKTOK CONNECTED</b></color>";

                    case "connecting":
                    case "starting":
                        return "<color=#FFD966><b>TIKTOK CONNECTING</b></color>";

                    case "error":
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
                if (!string.IsNullOrWhiteSpace(Services.RelayStatusService.Username))
                    return "@" + Services.RelayStatusService.Username;

                if (!string.IsNullOrWhiteSpace(PluginConfig.Instance.TikTokUsername))
                    return "@" + PluginConfig.Instance.TikTokUsername;

                return "<color=#8A95A3>-</color>";
            }
        }

        [UIValue("orb-test-button-text")]
        public string OrbTestButtonText =>
            Plugin.GetOrbTestEnabled()
                ? "<color=#66FF66><b>ORB TEST ON</b></color>"
                : "<color=#FF5555><b>ORB TEST OFF</b></color>";

        [UIValue("orb-test-message")]
        public string OrbTestMessage => _orbTestMessage;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            Plugin.QueueChanged -= OnQueueChanged;
            Plugin.QueueChanged += OnQueueChanged;

            Plugin.RelayModeChanged -= OnRelayModeChanged;
            Plugin.RelayModeChanged += OnRelayModeChanged;

            Plugin.RelayStatusChanged -= OnRelayStatusChanged;
            Plugin.RelayStatusChanged += OnRelayStatusChanged;

            if (firstActivation)
            {
                if (_rainbowCoroutine == null)
                    _rainbowCoroutine = StartCoroutine(RainbowTitleLoop());

                if (_statusRefreshCoroutine == null)
                    _statusRefreshCoroutine = StartCoroutine(StatusRefreshLoop());
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            NotifyPropertyChanged(nameof(TitleText));
            NotifyPropertyChanged(nameof(QueueCount));
            NotifyPropertyChanged(nameof(CanClearQueue));
            NotifyPropertyChanged(nameof(ClearQueueButtonText));
            NotifyPropertyChanged(nameof(HttpConnectionState));
            NotifyPropertyChanged(nameof(TikTokConnectionState));
            NotifyPropertyChanged(nameof(TikTokUsername));
            NotifyPropertyChanged(nameof(OrbTestButtonText));
            NotifyPropertyChanged(nameof(OrbTestMessage));
            NotifyPropertyChanged(nameof(MainTabText));
            NotifyPropertyChanged(nameof(OrbControlsTabText));
            NotifyPropertyChanged(nameof(TikTokTabText));
            NotifyPropertyChanged(nameof(AboutTabText));
        }

        private void RefreshStatusOnly()
        {
            NotifyPropertyChanged(nameof(HttpConnectionState));
            NotifyPropertyChanged(nameof(TikTokConnectionState));
            NotifyPropertyChanged(nameof(TikTokUsername));
        }

        private void OnQueueChanged()
        {
            NotifyPropertyChanged(nameof(QueueCount));
            NotifyPropertyChanged(nameof(CanClearQueue));
            NotifyPropertyChanged(nameof(ClearQueueButtonText));
        }

        private void OnRelayModeChanged()
        {
            RefreshStatusOnly();
        }

        private void OnRelayStatusChanged()
        {
            RefreshStatusOnly();
        }

        private IEnumerator RainbowTitleLoop()
        {
            while (true)
            {
                float hue = (Time.time * 0.08f) % 1f;
                Color c = Color.HSVToRGB(hue, 0.9f, 1f);
                string hex = ColorUtility.ToHtmlStringRGB(c);

                _titleText = $"<color=#{hex}><b>GeminiOrbFX</b></color>";
                NotifyPropertyChanged(nameof(TitleText));

                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator StatusRefreshLoop()
        {
            while (true)
            {
                NotifyPropertyChanged(nameof(HttpConnectionState));
                NotifyPropertyChanged(nameof(TikTokConnectionState));
                NotifyPropertyChanged(nameof(TikTokUsername));
                NotifyPropertyChanged(nameof(OrbTestButtonText));

                yield return new WaitForSeconds(0.5f);
            }
        }

        [UIAction("clear-queue")]
        private void ClearQueue()
        {
            Plugin.ClearOrbQueue();
            NotifyPropertyChanged(nameof(QueueCount));
            NotifyPropertyChanged(nameof(CanClearQueue));
            NotifyPropertyChanged(nameof(ClearQueueButtonText));
        }

        [UIAction("toggle-orb-test")]
        private void ToggleOrbTest()
        {
            bool enabled = !Plugin.GetOrbTestEnabled();

            Plugin.SetOrbTestEnabled(enabled);
            NotifyPropertyChanged(nameof(OrbTestButtonText));

            if (enabled)
                StartCoroutine(ShowSongOnlyMessage());
            else
            {
                _orbTestMessage = "";
                NotifyPropertyChanged(nameof(OrbTestMessage));
            }
        }

        private IEnumerator ShowSongOnlyMessage()
        {
            _orbTestMessage = "<color=#FFD966>Only works during gameplay</color>";
            NotifyPropertyChanged(nameof(OrbTestMessage));

            yield return new WaitForSeconds(2f);

            _orbTestMessage = "";
            NotifyPropertyChanged(nameof(OrbTestMessage));
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

        protected override void OnDestroy()
        {
            Plugin.QueueChanged -= OnQueueChanged;
            Plugin.RelayModeChanged -= OnRelayModeChanged;
            Plugin.RelayStatusChanged -= OnRelayStatusChanged;

            if (_rainbowCoroutine != null)
            {
                StopCoroutine(_rainbowCoroutine);
                _rainbowCoroutine = null;
            }

            if (_statusRefreshCoroutine != null)
            {
                StopCoroutine(_statusRefreshCoroutine);
                _statusRefreshCoroutine = null;
            }

            base.OnDestroy();
        }
    }
}