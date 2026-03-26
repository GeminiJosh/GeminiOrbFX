using HMUI;

namespace GeminiOrbFX.UI
{
    internal class GeminiOrbFlowCoordinator : FlowCoordinator
    {
        private GeminiOrbMainViewController _mainView;
        private GeminiOrbOrbControlsViewController _orbControlsView;

        private GeminiOrbTikTokConnectionViewController _tikTokConnectionView;
        private GeminiOrbTikTokTriggersViewController _tikTokTriggersView;
        private GeminiOrbTikTokGiftSettingsViewController _tikTokGiftSettingsView;
        private GeminiOrbTikTokDebugViewController _tikTokDebugView;
        private GeminiOrbAboutViewController _aboutView;

        private MainFlowCoordinator _mainFlowCoordinator;

        internal void Setup(
            GeminiOrbMainViewController mainView,
            GeminiOrbOrbControlsViewController orbControlsView,
            GeminiOrbTikTokConnectionViewController tikTokConnectionView,
            GeminiOrbTikTokTriggersViewController tikTokTriggersView,
            GeminiOrbTikTokGiftSettingsViewController tikTokGiftSettingsView,
            GeminiOrbTikTokDebugViewController tikTokDebugView,
            GeminiOrbAboutViewController aboutView,
            MainFlowCoordinator mainFlowCoordinator)
        {
            _mainView = mainView;
            _orbControlsView = orbControlsView;
            _tikTokConnectionView = tikTokConnectionView;
            _tikTokTriggersView = tikTokTriggersView;
            _tikTokGiftSettingsView = tikTokGiftSettingsView;
            _tikTokDebugView = tikTokDebugView;
            _aboutView = aboutView;
            _mainFlowCoordinator = mainFlowCoordinator;

            if (_mainView != null)
                _mainView.FlowCoordinator = this;

            if (_orbControlsView != null)
                _orbControlsView.FlowCoordinator = this;

            if (_tikTokConnectionView != null)
                _tikTokConnectionView.FlowCoordinator = this;

            if (_tikTokTriggersView != null)
                _tikTokTriggersView.FlowCoordinator = this;

            if (_tikTokGiftSettingsView != null)
                _tikTokGiftSettingsView.FlowCoordinator = this;

            if (_tikTokDebugView != null)
                _tikTokDebugView.FlowCoordinator = this;

            if (_aboutView != null)
                _aboutView.FlowCoordinator = this;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("GeminiOrbFX");
                showBackButton = true;
                ProvideInitialViewControllers(_mainView, null, null);
            }
        }

        internal void ShowMain()
        {
            Plugin.SetMenuPreviewEnabled(false);
            SetTitle("GeminiOrbFX");

            if (_mainView != null && topViewController != _mainView)
                ReplaceTopViewController(_mainView, null, ViewController.AnimationType.In);
        }

        internal void ShowOrbControls()
        {
            Plugin.SetMenuPreviewEnabled(true);
            SetTitle("GeminiOrbFX");

            if (_orbControlsView != null && topViewController != _orbControlsView)
                ReplaceTopViewController(_orbControlsView, null, ViewController.AnimationType.In);
        }

        internal void ShowTikTok()
        {
            ShowTikTokConnection();
        }

        internal void ShowTikTokConnection()
        {
            Plugin.SetMenuPreviewEnabled(false);
            SetTitle("GeminiOrbFX");

            if (_tikTokConnectionView != null && topViewController != _tikTokConnectionView)
                ReplaceTopViewController(_tikTokConnectionView, null, ViewController.AnimationType.In);
        }

        internal void ShowTikTokTriggers()
        {
            Plugin.SetMenuPreviewEnabled(false);
            SetTitle("GeminiOrbFX");

            if (_tikTokTriggersView != null && topViewController != _tikTokTriggersView)
                ReplaceTopViewController(_tikTokTriggersView, null, ViewController.AnimationType.In);
        }

        internal void ShowTikTokGiftSettings()
        {
            Plugin.SetMenuPreviewEnabled(false);
            SetTitle("GeminiOrbFX");

            if (_tikTokGiftSettingsView != null)
                ReplaceTopViewController(_tikTokGiftSettingsView, null, ViewController.AnimationType.In);
        }

        internal void ShowTikTokDebug()
        {
            Plugin.SetMenuPreviewEnabled(false);
            SetTitle("GeminiOrbFX");

            if (_tikTokDebugView != null && topViewController != _tikTokDebugView)
                ReplaceTopViewController(_tikTokDebugView, null, ViewController.AnimationType.In);
        }

        internal void ShowAbout()
        {
            Plugin.SetMenuPreviewEnabled(false);
            SetTitle("GeminiOrbFX");

            if (_aboutView != null && topViewController != _aboutView)
                ReplaceTopViewController(_aboutView, null, ViewController.AnimationType.In);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            Plugin.SetMenuPreviewEnabled(false);

            if (_mainFlowCoordinator != null)
            {
                _mainFlowCoordinator.DismissFlowCoordinator(
                    this,
                    HMUI.ViewController.AnimationDirection.Horizontal,
                    null,
                    false);
            }
        }
    }
}
