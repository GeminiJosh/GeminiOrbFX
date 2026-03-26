using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace GeminiOrbFX.UI.GameplaySetup
{
    [ViewDefinition("GeminiOrbFX.UI.GameplaySetup.GeminiOrbGameplaySetupView.bsml")]
    internal class GeminiOrbGameplaySetupViewController : BSMLAutomaticViewController
    {
        private bool _showStatus = true;
        private Coroutine _statusRefreshCoroutine;

        [UIComponent("http-status-value")]
        private TMP_Text _httpStatusValue;

        [UIComponent("tiktok-status-value")]
        private TMP_Text _tiktokStatusValue;

        [UIComponent("tiktok-username-value")]
        private TMP_Text _tiktokUsernameValue;

        [UIComponent("queue-count-value")]
        private TMP_Text _queueCountValue;

        [UIComponent("spawn-distance-value")]
        private TMP_Text _spawnDistanceValue;

        [UIComponent("orb-height-value")]
        private TMP_Text _orbHeightValue;

        [UIComponent("orb-speed-value")]
        private TMP_Text _orbSpeedValue;

        [UIComponent("text-size-value")]
        private TMP_Text _textSizeValue;

        [UIValue("status-tab-text")]
        public string StatusTabText => _showStatus
            ? "<color=#00D8FF>STATUS</color>"
            : "STATUS";

        [UIValue("orbcontrols-tab-text")]
        public string OrbControlsTabText => !_showStatus
            ? "<color=#00D8FF>ORB CONTROLS</color>"
            : "ORB CONTROLS";

        [UIValue("show-status-panel")]
        public bool ShowStatusPanel => _showStatus;

        [UIValue("show-orbcontrols-panel")]
        public bool ShowOrbControlsPanel => !_showStatus;

        [UIValue("can-clear-queue")]
        public bool CanClearQueue => Plugin.GetQueueCount() > 0;

        [UIValue("clear-queue-button-text")]
        public string ClearQueueButtonText =>
            CanClearQueue
                ? "<color=#00D8FF><b>CLEAR QUEUE</b></color>"
                : "<color=#8A95A3><b>CLEAR QUEUE</b></color>";

        [UIValue("minus-button-text")]
        public string MinusButtonText => "<color=#00D8FF><b>-</b></color>";

        [UIValue("plus-button-text")]
        public string PlusButtonText => "<color=#00D8FF><b>+</b></color>";

        [UIAction("#post-parse")]
        private void PostParse()
        {
            Plugin.QueueChanged -= OnQueueChanged;
            Plugin.QueueChanged += OnQueueChanged;

            Plugin.OrbSettingsChanged -= OnOrbSettingsChanged;
            Plugin.OrbSettingsChanged += OnOrbSettingsChanged;

            Plugin.RelayModeChanged -= OnRelayModeChanged;
            Plugin.RelayModeChanged += OnRelayModeChanged;

            Plugin.RelayStatusChanged -= OnRelayStatusChanged;
            Plugin.RelayStatusChanged += OnRelayStatusChanged;

            RefreshAll();

            if (_statusRefreshCoroutine == null)
                _statusRefreshCoroutine = StartCoroutine(StatusRefreshLoop());
        }

        protected override void OnDestroy()
        {
            Plugin.QueueChanged -= OnQueueChanged;
            Plugin.OrbSettingsChanged -= OnOrbSettingsChanged;
            Plugin.RelayModeChanged -= OnRelayModeChanged;
            Plugin.RelayStatusChanged -= OnRelayStatusChanged;

            if (_statusRefreshCoroutine != null)
            {
                StopCoroutine(_statusRefreshCoroutine);
                _statusRefreshCoroutine = null;
            }

            base.OnDestroy();
        }

        private void OnQueueChanged()
        {
            RefreshStatus();
        }

        private void OnOrbSettingsChanged()
        {
            RefreshControls();
        }

        private void OnRelayModeChanged()
        {
            RefreshStatus();
        }

        private void OnRelayStatusChanged()
        {
            RefreshStatus();
        }

        [UIAction("show-status")]
        private void ShowStatus()
        {
            _showStatus = true;
            RefreshTabState();
            RefreshStatus();
        }

        [UIAction("show-orbcontrols")]
        private void ShowOrbControls()
        {
            _showStatus = false;
            RefreshTabState();
            RefreshControls();
        }

        [UIAction("clear-queue")]
        private void ClearQueue()
        {
            Plugin.ClearOrbQueue();
            RefreshStatus();
        }

        [UIAction("inc-spawn-distance")]
        private void IncSpawnDistance()
        {
            PluginConfig.Instance.SpawnDistance = System.Math.Min(50f, PluginConfig.Instance.SpawnDistance + 0.5f);
            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();
            RefreshControls();
        }

        [UIAction("dec-spawn-distance")]
        private void DecSpawnDistance()
        {
            PluginConfig.Instance.SpawnDistance = System.Math.Max(10f, PluginConfig.Instance.SpawnDistance - 0.5f);
            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();
            RefreshControls();
        }

        [UIAction("inc-orb-height")]
        private void IncOrbHeight()
        {
            PluginConfig.Instance.OrbHeight = System.Math.Min(1.40f, PluginConfig.Instance.OrbHeight + 0.05f);
            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();
            RefreshControls();
        }

        [UIAction("dec-orb-height")]
        private void DecOrbHeight()
        {
            PluginConfig.Instance.OrbHeight = System.Math.Max(0.40f, PluginConfig.Instance.OrbHeight - 0.05f);
            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();
            RefreshControls();
        }

        [UIAction("inc-orb-speed")]
        private void IncOrbSpeed()
        {
            PluginConfig.Instance.OrbSpeed = System.Math.Min(25f, PluginConfig.Instance.OrbSpeed + 0.1f);
            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();
            RefreshControls();
        }

        [UIAction("dec-orb-speed")]
        private void DecOrbSpeed()
        {
            PluginConfig.Instance.OrbSpeed = System.Math.Max(2f, PluginConfig.Instance.OrbSpeed - 0.1f);
            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();
            RefreshControls();
        }

        [UIAction("inc-text-size")]
        private void IncTextSize()
        {
            PluginConfig.Instance.NameTextScale = System.Math.Min(2.50f, PluginConfig.Instance.NameTextScale + 0.05f);
            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();
            RefreshControls();
        }

        [UIAction("dec-text-size")]
        private void DecTextSize()
        {
            PluginConfig.Instance.NameTextScale = System.Math.Max(0.80f, PluginConfig.Instance.NameTextScale - 0.05f);
            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();
            RefreshControls();
        }

        private IEnumerator StatusRefreshLoop()
        {
            while (true)
            {
                if (_showStatus)
                    RefreshStatus();

                yield return new WaitForSeconds(0.5f);
            }
        }

        private void RefreshAll()
        {
            RefreshTabState();
            RefreshStatus();
            RefreshControls();
        }

        private void RefreshTabState()
        {
            NotifyPropertyChanged(nameof(StatusTabText));
            NotifyPropertyChanged(nameof(OrbControlsTabText));
            NotifyPropertyChanged(nameof(ShowStatusPanel));
            NotifyPropertyChanged(nameof(ShowOrbControlsPanel));
            NotifyPropertyChanged(nameof(CanClearQueue));
            NotifyPropertyChanged(nameof(ClearQueueButtonText));
        }

        private void RefreshStatus()
        {
            if (_httpStatusValue != null)
            {
                _httpStatusValue.text = Plugin.IsHttpServerRunning()
                    ? "<color=#66FF66><b>ACTIVE</b></color>"
                    : "<color=#FF5555><b>INACTIVE</b></color>";
            }

            if (_tiktokStatusValue != null)
            {
                if (Plugin.IsHttpOnlyMode())
                {
                    _tiktokStatusValue.text = "<color=#FFD966><b>STREAMER-BOT</b></color>";
                }
                else
                {
                    switch (Services.RelayStatusService.State)
                    {
                        case "connected":
                            _tiktokStatusValue.text = "<color=#66FF66><b>TIKTOK CONNECTED</b></color>";
                            break;

                        case "connecting":
                        case "starting":
                            _tiktokStatusValue.text = "<color=#FFD966><b>TIKTOK CONNECTING</b></color>";
                            break;

                        case "error":
                            if (!string.IsNullOrWhiteSpace(Services.RelayStatusService.Message) && PluginConfig.Instance.VerboseRelayLogging)
                                _tiktokStatusValue.text = $"<color=#FF5555><b>TIKTOK ERROR:</b></color> {Services.RelayStatusService.Message}";
                            else
                                _tiktokStatusValue.text = "<color=#FF5555><b>TIKTOK ERROR</b></color>";
                            break;

                        default:
                            _tiktokStatusValue.text = "<color=#FF5555><b>TIKTOK DISCONNECTED</b></color>";
                            break;
                    }
                }
            }

            if (_tiktokUsernameValue != null)
            {
                if (!string.IsNullOrWhiteSpace(PluginConfig.Instance.TikTokUsername))
                {
                    _tiktokUsernameValue.text = "@" + PluginConfig.Instance.TikTokUsername;
                }
                else if (!string.IsNullOrWhiteSpace(Services.RelayStatusService.Username))
                {
                    _tiktokUsernameValue.text = "@" + Services.RelayStatusService.Username;
                }
                else
                {
                    _tiktokUsernameValue.text = "<color=#8A95A3>-</color>";
                }
            }

            if (_queueCountValue != null)
            {
                int current = Plugin.GetQueueCount();
                int max = PluginConfig.Instance.MaxQueue;
                float ratio = max > 0 ? (float)current / max : 0f;

                if (ratio >= 0.90f)
                    _queueCountValue.text = $"<color=#FF5555>{current} / {max}</color>";
                else if (ratio >= 0.75f)
                    _queueCountValue.text = $"<color=#FFA500>{current} / {max}</color>";
                else
                    _queueCountValue.text = $"{current} / {max}";
            }

            NotifyPropertyChanged(nameof(CanClearQueue));
            NotifyPropertyChanged(nameof(ClearQueueButtonText));
        }

        private void RefreshControls()
        {
            if (_spawnDistanceValue != null)
                _spawnDistanceValue.text = PluginConfig.Instance.SpawnDistance.ToString("0.00", CultureInfo.InvariantCulture);

            if (_orbHeightValue != null)
                _orbHeightValue.text = PluginConfig.Instance.OrbHeight.ToString("0.00", CultureInfo.InvariantCulture);

            if (_orbSpeedValue != null)
                _orbSpeedValue.text = PluginConfig.Instance.OrbSpeed.ToString("0.00", CultureInfo.InvariantCulture);

            if (_textSizeValue != null)
                _textSizeValue.text = PluginConfig.Instance.NameTextScale.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}