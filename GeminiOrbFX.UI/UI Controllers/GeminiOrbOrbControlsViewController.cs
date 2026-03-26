using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace GeminiOrbFX.UI
{
    [ViewDefinition("GeminiOrbFX.UI.GeminiOrbOrbControlsView.bsml")]
    internal class GeminiOrbOrbControlsViewController : BSMLAutomaticViewController
    {
        internal GeminiOrbFlowCoordinator FlowCoordinator { get; set; }

        private string _saveStatus = "";
        private bool _hasPendingChanges = false;

        private const float DefaultOrbHeight = 0.80f;
        private const float DefaultSpawnDistance = 34.0f;
        private const float DefaultOrbSpeed = 10.5f;
        private const float DefaultTextScale = 1.55f;

        private float _tempSpawnDistance;
        private float _tempOrbHeight;
        private float _tempOrbSpeed;
        private float _tempTextScale;

        [UIValue("spawn-distance-display")]
        public string SpawnDistanceDisplay =>
            _tempSpawnDistance.ToString("0.00", CultureInfo.InvariantCulture);

        [UIValue("orb-height-display")]
        public string OrbHeightDisplay =>
            _tempOrbHeight.ToString("0.00", CultureInfo.InvariantCulture);

        [UIValue("orb-speed-display")]
        public string OrbSpeedDisplay =>
            _tempOrbSpeed.ToString("0.00", CultureInfo.InvariantCulture);

        [UIValue("text-size-display")]
        public string TextSizeDisplay =>
            _tempTextScale.ToString("0.00", CultureInfo.InvariantCulture);

        [UIValue("save-status")]
        public string SaveStatus => _saveStatus;

        [UIValue("main-tab-text")]
        public string MainTabText => "MAIN";

        [UIValue("orbcontrols-tab-text")]
        public string OrbControlsTabText => "<color=#00D8FF><b>ORB CONTROLS</b></color>";

        [UIValue("tiktok-tab-text")]
        public string TikTokTabText => "TIKTOK";

        [UIValue("about-tab-text")]
        public string AboutTabText => "ABOUT";

        [UIValue("minus-button-text")]
        public string MinusButtonText => "<color=#00D8FF><b>-</b></color>";

        [UIValue("plus-button-text")]
        public string PlusButtonText => "<color=#00D8FF><b>+</b></color>";

        [UIValue("save-button-text")]
        public string SaveButtonText =>
            _hasPendingChanges
                ? "<color=#00D8FF><b>SAVE</b></color>"
                : "<color=#8A95A3><b>SAVE</b></color>";

        [UIValue("reset-button-text")]
        public string ResetButtonText => "<color=#00D8FF><b>RESET</b></color>";

        [UIValue("can-save")]
        public bool CanSave => _hasPendingChanges;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            Plugin.OrbSettingsChanged -= OnOrbSettingsChanged;
            Plugin.OrbSettingsChanged += OnOrbSettingsChanged;

            LoadTempValuesFromConfig();

            Plugin.SetMenuPreviewOverrides(_tempSpawnDistance, _tempOrbHeight, _tempOrbSpeed, _tempTextScale);

            _hasPendingChanges = false;
            _saveStatus = "";

            NotifySaveResetState();
            RefreshValues();

            NotifyPropertyChanged(nameof(SaveStatus));
            NotifyPropertyChanged(nameof(MainTabText));
            NotifyPropertyChanged(nameof(OrbControlsTabText));
            NotifyPropertyChanged(nameof(TikTokTabText));
            NotifyPropertyChanged(nameof(AboutTabText));
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

            Plugin.ClearMenuPreviewOverrides();

            _saveStatus = "";
            NotifyPropertyChanged(nameof(SaveStatus));
        }

        protected override void OnDestroy()
        {
            Plugin.OrbSettingsChanged -= OnOrbSettingsChanged;
            base.OnDestroy();
        }

        private void OnOrbSettingsChanged()
        {
            if (_hasPendingChanges)
                return;

            LoadTempValuesFromConfig();
            RefreshValues();
        }

        private void LoadTempValuesFromConfig()
        {
            _tempSpawnDistance = PluginConfig.Instance.SpawnDistance;
            _tempOrbHeight = PluginConfig.Instance.OrbHeight;
            _tempOrbSpeed = PluginConfig.Instance.OrbSpeed;
            _tempTextScale = PluginConfig.Instance.NameTextScale;
        }

        [UIAction("inc-spawn-distance")]
        private void IncSpawnDistance()
        {
            _tempSpawnDistance = Mathf.Min(50f, _tempSpawnDistance + 0.5f);
            MarkDirtyAndRefresh();
        }

        [UIAction("dec-spawn-distance")]
        private void DecSpawnDistance()
        {
            _tempSpawnDistance = Mathf.Max(10f, _tempSpawnDistance - 0.5f);
            MarkDirtyAndRefresh();
        }

        [UIAction("inc-orb-height")]
        private void IncOrbHeight()
        {
            _tempOrbHeight = Mathf.Min(1.40f, _tempOrbHeight + 0.05f);
            MarkDirtyAndRefresh();
        }

        [UIAction("dec-orb-height")]
        private void DecOrbHeight()
        {
            _tempOrbHeight = Mathf.Max(0.40f, _tempOrbHeight - 0.05f);
            MarkDirtyAndRefresh();
        }

        [UIAction("inc-orb-speed")]
        private void IncOrbSpeed()
        {
            _tempOrbSpeed = Mathf.Min(25f, _tempOrbSpeed + 0.1f);
            MarkDirtyAndRefresh();
        }

        [UIAction("dec-orb-speed")]
        private void DecOrbSpeed()
        {
            _tempOrbSpeed = Mathf.Max(2f, _tempOrbSpeed - 0.1f);
            MarkDirtyAndRefresh();
        }

        [UIAction("inc-text-size")]
        private void IncTextSize()
        {
            _tempTextScale = Mathf.Min(2.50f, _tempTextScale + 0.05f);
            MarkDirtyAndRefresh();
        }

        [UIAction("dec-text-size")]
        private void DecTextSize()
        {
            _tempTextScale = Mathf.Max(0.80f, _tempTextScale - 0.05f);
            MarkDirtyAndRefresh();
        }

        [UIAction("reset-orb-defaults")]
        private void ResetOrbDefaults()
        {
            _tempOrbHeight = DefaultOrbHeight;
            _tempSpawnDistance = DefaultSpawnDistance;
            _tempOrbSpeed = DefaultOrbSpeed;
            _tempTextScale = DefaultTextScale;

            Plugin.SetMenuPreviewOverrides(_tempSpawnDistance, _tempOrbHeight, _tempOrbSpeed, _tempTextScale);

            ApplyTempValuesToConfig();

            _hasPendingChanges = false;
            _saveStatus = "<color=#66FF66>RESET ✓</color>";

            NotifySaveResetState();
            RefreshValues();
            NotifyPropertyChanged(nameof(SaveStatus));

            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();

            StartCoroutine(ClearStatusRoutine());
        }

        [UIAction("save-config")]
        private void SaveConfig()
        {
            ApplyTempValuesToConfig();

            Plugin.SetMenuPreviewOverrides(_tempSpawnDistance, _tempOrbHeight, _tempOrbSpeed, _tempTextScale);

            _saveStatus = "<color=#66FF66>SAVED ✓</color>";
            NotifyPropertyChanged(nameof(SaveStatus));

            _hasPendingChanges = false;
            NotifySaveResetState();

            Plugin.NotifyOrbSettingsChanged();
            Plugin.RefreshMenuPreview();

            StartCoroutine(ClearStatusRoutine());
        }

        private void ApplyTempValuesToConfig()
        {
            PluginConfig.Instance.SpawnDistance = _tempSpawnDistance;
            PluginConfig.Instance.OrbHeight = _tempOrbHeight;
            PluginConfig.Instance.OrbSpeed = _tempOrbSpeed;
            PluginConfig.Instance.NameTextScale = _tempTextScale;
        }

        private IEnumerator ClearStatusRoutine()
        {
            yield return new WaitForSeconds(1.2f);

            _saveStatus = "";
            NotifyPropertyChanged(nameof(SaveStatus));
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
        private void ShowTiktok()
        {
            FlowCoordinator?.ShowTikTok();
        }

        [UIAction("show-about")]
        private void ShowAbout()
        {
            FlowCoordinator?.ShowAbout();
        }

        private void MarkDirtyAndRefresh()
        {
            _hasPendingChanges = true;

            Plugin.SetMenuPreviewOverrides(_tempSpawnDistance, _tempOrbHeight, _tempOrbSpeed, _tempTextScale);

            RefreshValues();
            NotifySaveResetState();
        }

        private void NotifySaveResetState()
        {
            NotifyPropertyChanged(nameof(CanSave));
            NotifyPropertyChanged(nameof(SaveButtonText));
            NotifyPropertyChanged(nameof(ResetButtonText));
        }

        private void RefreshValues()
        {
            NotifyPropertyChanged(nameof(SpawnDistanceDisplay));
            NotifyPropertyChanged(nameof(OrbHeightDisplay));
            NotifyPropertyChanged(nameof(OrbSpeedDisplay));
            NotifyPropertyChanged(nameof(TextSizeDisplay));
        }
    }
}