// GeminiOrbFX UI - Beat Saber (BSIPA) plugin
// Local HTTP endpoint examples:
//   http://127.0.0.1:6556/orb?name=USERNAME
//   http://127.0.0.1:6556/orb?name=USERNAME&lane=2
//   http://127.0.0.1:6556/orb?name=USERNAME&speed=6.5
//   http://127.0.0.1:6556/orb?name=USERNAME&color=#FF00FF
//   http://127.0.0.1:6556/orb?name=USERNAME&color=cyan
//   http://127.0.0.1:6556/orb?name=USERNAME&h=0.83&s=0.85&v=0.85
//   http://127.0.0.1:6556/orb?name=USERNAME&hsv=0.83,0.85,0.85
//   http://127.0.0.1:6556/status

using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.MenuButtons;
using BeatSaberMarkupLanguage.Util;
using GeminiOrbFX.UI.Services;
using HMUI;
using IPA;
using IPA.Config.Stores;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GeminiOrbFX.UI
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static IPA.Logging.Logger Log;
                
        private const string MOD_VERSION = "2.0.0-ui";

        // ---------------- IMPACT FX TUNING ----------------
        private const int ImpactParticlesMin = 12;
        private const int ImpactParticlesMax = 16;

        // ---------------- ORB PREFAB (BUNDLE + FALLBACK) ----------------
        private const string BUNDLE_FILE_NAME = "geminiorbfx";
        private const string ORB_PREFAB_NAME = "GCFX_OrbPrefab";

        private static AssetBundle _bundle;
        private static GameObject _bundlePrefab;
        private static GameObject _cachedOrbTemplate;

        // ---------------- SHARED MATERIALS ----------------
        private static Material _sharedTrailMaterial;

        // ---------------- SPAWN/MOTION TUNING ----------------
        internal static readonly float[] LaneX = new float[] { -0.9f, -0.3f, 0.3f, 0.9f };
        internal const float DespawnZ = -3.0f;

        internal const float DefaultMaxLife = 7.0f;
        internal const float MinSpeed = 2.0f;
        internal const float MaxSpeed = 25.0f;

        internal static int _orbsAlive = 0;

        private const float TMP_FontSizeMin = 12.5f;
        private const float TMP_FontSizeMax = 18.0f;

        private static readonly Vector3 GameplayForward = Vector3.forward;
        private static readonly Vector3 GameplayRight = Vector3.right;
        private static readonly Vector3 GameplayUp = Vector3.up;

        // ---------------- VISUAL FX TUNING ----------------
        private const float OrbSpinSpeed = 50f;

        private const float HaloPulseScaleMin = 0.95f;
        private const float HaloPulseScaleMax = 1.04f;
        private const float HaloPulseSpeed = 1.6f;

        private const float SpawnFXDuration = 0.15f;

        private const float PopStartScaleMul = 0.00f;
        private const float PopOvershootScaleMul = 1.20f;
        private const float PopEndScaleMul = 1.00f;

        private const float HaloBurstStartMul = 1.00f;
        private const float HaloBurstPeakMul = 1.35f;
        private const float HaloBurstEndMul = 1.00f;

        private const float SpawnForwardKick = 1.5f;
        private const float HaloPulseDamp = 12.0f;
        private const float HaloPulsePhaseRange = 2.5f;

        // ---------------- NAME COLOR ----------------
        internal static readonly bool RandomizeNameColorEachSpawn = true;

        internal const float NameHSV_S = 0.85f;
        internal const float NameHSV_V = 0.85f;

        private const float FaceMul = 0.45f;
        private const float OutlineMul = 0.20f;
        private const float GlowMul = 1.35f;

        // ---------------- COLOR ANTI-REPEAT ----------------
        private static float _lastHue = -999f;
        private const float MinHueSeparation = 0.10f;
        private const int HueRerollTries = 6;

        // ---------------- HALO RGB MATCH ----------------
        private const float HaloColorMul = 1.8f;
        private static readonly bool HaloClearOnSpawn = true;

        // ---------------- QUEUE ----------------
        private static readonly bool DropOldestWhenFull = false;
                
        // -------- REQUEST RATE LIMITER --------
        private const int MaxAcceptedRequestsPerSecond = 40;
        private static float _requestWindowStartRealtime = 0f;
        private static int _requestWindowAcceptedCount = 0;
        private static readonly object _requestRateLock = new object();

        private static void RemoveQueuedTestOrbs()
        {
            lock (Services.OrbQueueService.QueueLock)
            {
                if (Services.OrbQueueService.Queue.Count == 0)
                    return;

                Queue<OrbRequest> rebuilt = new Queue<OrbRequest>();

                while (Services.OrbQueueService.Queue.Count > 0)
                {
                    OrbRequest req = Services.OrbQueueService.Queue.Dequeue();
                    if (!req.IsTest)
                        rebuilt.Enqueue(req);
                }

                while (rebuilt.Count > 0)
                    Services.OrbQueueService.Queue.Enqueue(rebuilt.Dequeue());
            }

            RaiseQueueChanged();
        }

        private static bool TryPassRequestRateLimit()
        {
            lock (_requestRateLock)
            {
                float now = Time.realtimeSinceStartup;

                if (now - _requestWindowStartRealtime >= 1f)
                {
                    _requestWindowStartRealtime = now;
                    _requestWindowAcceptedCount = 0;
                }

                if (_requestWindowAcceptedCount >= MaxAcceptedRequestsPerSecond)
                    return false;

                _requestWindowAcceptedCount++;
                return true;
            }
        }

        
        private float _nextOrbTestRealtime = 0f;
        private static Plugin _instance;

        internal static event System.Action QueueChanged;
        internal static event System.Action OrbSettingsChanged;
        internal static event System.Action RelayModeChanged;
        internal static event System.Action RelayStatusChanged;
        internal static event System.Action UiTick;

        
        private static void RaiseOrbSettingsChanged()
        {
            MainThread.Run(() =>
            {
                try
                {
                    OrbSettingsChanged?.Invoke();
                }
                catch { }
            });
        }

        private static void RaiseRelayModeChanged()
        {
            MainThread.Run(() =>
            {
                try
                {
                    RelayModeChanged?.Invoke();
                }
                catch { }
            });
        }

        private static void RaiseRelayStatusChanged()
        {
            MainThread.Run(() =>
            {
                try
                {
                    RelayStatusChanged?.Invoke();
                }
                catch { }
            });
        }

        private static void RaiseUiTick()
        {
            try
            {
                UiTick?.Invoke();
            }
            catch { }
        }

        // ---------------- PRO SPACING SYSTEM ----------------
        internal static readonly int[] _laneAlive = new int[4];

        // ---------------- UI ----------------
        private bool _modsMenuButtonRegistered = false;
        private MenuButton _modsMenuButton;

        private GeminiOrbMainViewController _mainViewController;
        private GeminiOrbOrbControlsViewController _orbControlsViewController;
        private GeminiOrbTikTokConnectionViewController _tikTokConnectionViewController;
        private GeminiOrbTikTokTriggersViewController _tikTokTriggersViewController;
        private GeminiOrbTikTokDebugViewController _tikTokDebugViewController;
        private GeminiOrbTikTokGiftSettingsViewController _tikTokGiftSettingsViewController;
        private GeminiOrbAboutViewController _aboutViewController;
        private GeminiOrbFlowCoordinator _flowCoordinator;

        private GameplaySetup.GeminiOrbGameplaySetupViewController _gameplaySetupViewController;
        private bool _gameplaySetupTabRegistered = false;

        // ---------------- SERVICES ----------------
        private Services.HttpOrbServer _httpOrbServer;
        private Services.OrbSpawnService _orbSpawnService;
        private Services.TikTokService _tikTokService;
        private Services.TikTokRelayService _tikTokRelayService;

        internal static bool IsTikTokRelayMode()
        {
            return PluginConfig.Instance.RelayMode == 0;
        }

        internal static bool IsHttpOnlyMode()
        {
            return PluginConfig.Instance.RelayMode == 1;
        }

        // ---------------- CONSTANTS ----------------
        internal const int LaneCount = 4;

        // ---------------- MENU PREVIEW ----------------
        private static GameObject _menuPreviewOrb;
        private static bool _menuPreviewEnabled = false;
        private static bool _menuPreviewRefreshPending = false;
        private static float _menuPreviewNextRefreshRealtime = 0f;
        private const float MenuPreviewRefreshDebounceSeconds = 0.15f;

        private static bool _menuPreviewOverridesActive = false;
        private static float _menuPreviewSpawnDistanceOverride;
        private static float _menuPreviewOrbHeightOverride;
        private static float _menuPreviewOrbSpeedOverride;
        private static float _menuPreviewTextScaleOverride;

        [Init]
        public void Init(IPA.Logging.Logger logger, IPA.Config.Config conf)
        {
            Log = logger;
            PluginConfig.Instance = conf.Generated<PluginConfig>();
        }

        [OnStart]
        public void OnStart()
        {
            try
            {
                MainThread.Ensure();
                _instance = this;
                _httpOrbServer = new Services.HttpOrbServer(this);
                _orbSpawnService = new Services.OrbSpawnService(this);
                _tikTokService = new Services.TikTokService(this);

                _cachedOrbTemplate = null;
                _bundlePrefab = null;

                try
                {
                    if (_bundle != null)
                        _bundle.Unload(false);
                }
                catch { }

                _bundle = null;

                try
                {
                    if (_sharedTrailMaterial != null)
                        UnityEngine.Object.Destroy(_sharedTrailMaterial);
                }
                catch { }

                _sharedTrailMaterial = null;

                lock (Services.OrbQueueService.QueueLock)
                {
                    Services.OrbQueueService.Queue.Clear();
                }

                RaiseQueueChanged();

                _nextOrbTestRealtime = 0f;

                _orbSpawnService.ResetState();

                _httpOrbServer.Start();

                _tikTokRelayService = new Services.TikTokRelayService();
                Log.Info("[TikTokRelay] Ready. Waiting for manual connect.");

                MainMenuAwaiter.MainMenuInitializing += OnMainMenuInitializing;

                Log.Info("[GeminiOrbFX UI] Started");
                Log.Info("[GeminiOrbFX UI] Listening on http://127.0.0.1:6556/ (try /orb?name=test)");
            }
            catch (Exception ex)
            {
                Log.Error("[GeminiOrbFX UI] Failed to start: " + ex);
            }
        }

        [OnExit]
        public void OnExit()
        {
            _httpOrbServer?.Stop();
            _tikTokService?.ResetDebugState();

            try
            {
                _tikTokRelayService?.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warn("[TikTokRelay] Stop failed: " + ex.Message);
            }

            _tikTokRelayService = null;

            try
            {
                MainMenuAwaiter.MainMenuInitializing -= OnMainMenuInitializing;
            }
            catch { }

            PluginConfig.Instance.OrbTestEnabled = false;
            _nextOrbTestRealtime = 0f;
            DestroyMenuPreviewOrb();

            try
            {
                if (_modsMenuButtonRegistered && _modsMenuButton != null && MenuButtons.Instance != null)
                    MenuButtons.Instance.UnregisterButton(_modsMenuButton);
            }
            catch { }

            _modsMenuButton = null;
            _modsMenuButtonRegistered = false;

            try
            {
                if (_mainViewController != null)
                    UnityEngine.Object.Destroy(_mainViewController);
            }
            catch { }

            try
            {
                if (_orbControlsViewController != null)
                    UnityEngine.Object.Destroy(_orbControlsViewController);
            }
            catch { }

            try
            {
                if (_tikTokConnectionViewController != null)
                    UnityEngine.Object.Destroy(_tikTokConnectionViewController);
            }
            catch { }

            try
            {
                if (_tikTokTriggersViewController != null)
                    UnityEngine.Object.Destroy(_tikTokTriggersViewController);
            }
            catch { }

            try
            {
                if (_tikTokGiftSettingsViewController != null)
                    UnityEngine.Object.Destroy(_tikTokGiftSettingsViewController);
            }
            catch { }

            try
            {
                if (_tikTokDebugViewController != null)
                    UnityEngine.Object.Destroy(_tikTokDebugViewController);
            }
            catch { }

            try
            {
                if (_aboutViewController != null)
                    UnityEngine.Object.Destroy(_aboutViewController);
            }
            catch { }

            try
            {
                if (_flowCoordinator != null)
                    UnityEngine.Object.Destroy(_flowCoordinator);
            }
            catch { }

            try
            {
                if (_gameplaySetupViewController != null)
                    UnityEngine.Object.Destroy(_gameplaySetupViewController);
            }
            catch { }

            _mainViewController = null;
            _orbControlsViewController = null;
            _tikTokConnectionViewController = null;
            _tikTokTriggersViewController = null;
            _tikTokGiftSettingsViewController = null;
            _tikTokDebugViewController = null;
            _aboutViewController = null;
            _flowCoordinator = null;

            _gameplaySetupViewController = null;
            _gameplaySetupTabRegistered = false;

            try
            {
                if (_bundle != null)
                    _bundle.Unload(false);
            }
            catch { }

            _bundle = null;
            _bundlePrefab = null;
            _cachedOrbTemplate = null;

            try
            {
                if (_sharedTrailMaterial != null)
                    UnityEngine.Object.Destroy(_sharedTrailMaterial);
            }
            catch { }

            _sharedTrailMaterial = null;

            lock (Services.OrbQueueService.QueueLock)
            {
                Services.OrbQueueService.Queue.Clear();
            }

            Log.Info("[GeminiOrbFX UI] Stopped");
        }
           
        private void UpdateMenuPreviewState()
        {
            if (!_menuPreviewEnabled)
            {
                _menuPreviewRefreshPending = false;

                if (_menuPreviewOrb != null)
                    DestroyMenuPreviewOrb();

                return;
            }

            if (IsInGameplay())
            {
                _menuPreviewRefreshPending = false;

                if (_menuPreviewOrb != null)
                    DestroyMenuPreviewOrb();

                return;
            }

            if (_menuPreviewRefreshPending)
            {
                _menuPreviewRefreshPending = false;

                if (_menuPreviewOrb != null)
                    DestroyMenuPreviewOrb();

                EnsureMenuPreviewOrb();
                return;
            }

            if (_menuPreviewOrb == null)
                EnsureMenuPreviewOrb();
        }

        private void OnMainMenuInitializing()
        {
            TryRegisterGameplaySetupTab();
            TryRegisterModsMenuButtonLate();
        }

        private void TryRegisterModsMenuButtonLate()
        {
            if (_modsMenuButtonRegistered)
                return;

            try
            {
                if (_modsMenuButton == null)
                {
                    _modsMenuButton = new MenuButton(
                        "GeminiOrbFX",
                        "Open GeminiOrbFX control panel",
                        OnModsMenuButtonPressed,
                        true);
                }

                MenuButtons.Instance.RegisterButton(_modsMenuButton);
                _modsMenuButtonRegistered = true;

                Log.Info("[GeminiOrbFX UI] Mods menu button registered.");
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception ex)
            {
                Log.Error("[GeminiOrbFX UI] Failed to register Mods menu button: " + ex);
                _modsMenuButtonRegistered = true;
            }
        }

        private void OnModsMenuButtonPressed()
        {
            try
            {
                var mainFlow = BeatSaberUI.MainFlowCoordinator;
                if (mainFlow == null)
                {
                    Log.Warn("[GeminiOrbFX UI] MainFlowCoordinator not found.");
                    return;
                }

                if (_mainViewController == null)
                    _mainViewController = BeatSaberUI.CreateViewController<GeminiOrbMainViewController>();

                if (_orbControlsViewController == null)
                    _orbControlsViewController = BeatSaberUI.CreateViewController<GeminiOrbOrbControlsViewController>();

                if (_tikTokConnectionViewController == null)
                    _tikTokConnectionViewController = BeatSaberUI.CreateViewController<GeminiOrbTikTokConnectionViewController>();

                if (_tikTokTriggersViewController == null)
                    _tikTokTriggersViewController = BeatSaberUI.CreateViewController<GeminiOrbTikTokTriggersViewController>();

                if (_tikTokGiftSettingsViewController == null)
                    _tikTokGiftSettingsViewController = BeatSaberUI.CreateViewController<GeminiOrbTikTokGiftSettingsViewController>();

                if (_tikTokDebugViewController == null)
                    _tikTokDebugViewController = BeatSaberUI.CreateViewController<GeminiOrbTikTokDebugViewController>();

                if (_aboutViewController == null)
                    _aboutViewController = BeatSaberUI.CreateViewController<GeminiOrbAboutViewController>();

                _flowCoordinator = BeatSaberUI.CreateFlowCoordinator<GeminiOrbFlowCoordinator>();

                _flowCoordinator.Setup(
                    _mainViewController,
                    _orbControlsViewController,
                    _tikTokConnectionViewController,
                    _tikTokTriggersViewController,
                    _tikTokGiftSettingsViewController,
                    _tikTokDebugViewController,
                    _aboutViewController,
                    mainFlow);

                mainFlow.PresentFlowCoordinator(
                    _flowCoordinator,
                    null,
                    HMUI.ViewController.AnimationDirection.Horizontal,
                    false);
            }
            catch (Exception ex)
            {
                Log.Error("[GeminiOrbFX UI] Failed to open Mods UI: " + ex);
            }
        }

        private void TryRegisterGameplaySetupTab()
        {
            if (_gameplaySetupTabRegistered)
                return;

            try
            {
                if (_gameplaySetupViewController == null)
                    _gameplaySetupViewController = BeatSaberUI.CreateViewController<GameplaySetup.GeminiOrbGameplaySetupViewController>();

                BeatSaberMarkupLanguage.GameplaySetup.GameplaySetup.Instance.AddTab(
                    "GeminiOrbFX",
                    "GeminiOrbFX.UI.GameplaySetup.GeminiOrbGameplaySetupView.bsml",
                    _gameplaySetupViewController
                );

                _gameplaySetupTabRegistered = true;
                Log.Info("[GeminiOrbFX UI] Gameplay Setup tab registered.");
            }
            catch (Exception ex)
            {
                Log.Error("[GeminiOrbFX UI] Failed to register Gameplay Setup tab: " + ex);
            }
        }

        internal static void EnqueueTestOrb(string username)
        {
            OrbRequest req = new OrbRequest
            {
                Name = string.IsNullOrWhiteSpace(username) ? "TEST" : username.ToUpperInvariant(),
                Lane = null,
                Speed = null,
                Color = null,
                H = null,
                S = null,
                V = null,
                IsTest = true
            };

            EnqueueOrbRequest(req, out _);
        }

        internal static void RaiseQueueChanged()
        {
            MainThread.Run(() =>
            {
                try
                {
                    QueueChanged?.Invoke();
                }
                catch { }
            });
        }

        internal static int GetQueueCount()
        {
            return Services.OrbQueueService.GetQueueCount();
        }

        internal static int GetLiveOrbCount()
        {
            return _orbsAlive;
        }

        internal static void ClearOrbQueue()
        {
            Services.OrbQueueService.ClearQueue();
            RaiseQueueChanged();
        }

        internal static bool IsMenuPreviewEnabled()
        {
            return _menuPreviewEnabled;
        }

        internal static void SetMenuPreviewEnabled(bool enabled)
        {
            _menuPreviewEnabled = enabled;
            _menuPreviewRefreshPending = true;
        }

        internal static void RefreshMenuPreview()
        {
            if (_instance == null)
                return;

            if (!_menuPreviewEnabled)
                return;

            if (_menuPreviewOrb != null)
            {
                var ctrl = _menuPreviewOrb.GetComponent<OrbLaneController>();
                if (ctrl != null)
                {
                    ctrl.RefreshPreviewFromConfig();
                    return;
                }
            }

            _menuPreviewRefreshPending = true;
            _menuPreviewNextRefreshRealtime = 0f;
        }

        internal static void NotifyOrbSettingsChanged()
        {
            RaiseOrbSettingsChanged();
        }

        internal static void SetMenuPreviewOverrides(float spawnDistance, float orbHeight, float orbSpeed, float textScale)
        {
            _menuPreviewOverridesActive = true;
            _menuPreviewSpawnDistanceOverride = spawnDistance;
            _menuPreviewOrbHeightOverride = orbHeight;
            _menuPreviewOrbSpeedOverride = orbSpeed;
            _menuPreviewTextScaleOverride = textScale;

            RefreshMenuPreview();
        }

        internal static void ClearMenuPreviewOverrides()
        {
            _menuPreviewOverridesActive = false;
            RefreshMenuPreview();
        }

        internal static float GetPreviewAwareSpawnDistance()
        {
            return _menuPreviewOverridesActive ? _menuPreviewSpawnDistanceOverride : PluginConfig.Instance.SpawnDistance;
        }

        internal static float GetPreviewAwareOrbHeight()
        {
            return _menuPreviewOverridesActive ? _menuPreviewOrbHeightOverride : PluginConfig.Instance.OrbHeight;
        }

        internal static float GetPreviewAwareOrbSpeed()
        {
            return _menuPreviewOverridesActive ? _menuPreviewOrbSpeedOverride : PluginConfig.Instance.OrbSpeed;
        }

        internal static float GetPreviewAwareTextScale()
        {
            return _menuPreviewOverridesActive ? _menuPreviewTextScaleOverride : PluginConfig.Instance.NameTextScale;
        }

        internal static void NotifyRelayModeChanged()
        {
            RaiseRelayModeChanged();
        }

        internal static void NotifyRelayStatusChanged()
        {
            RaiseRelayStatusChanged();
        }

        private void EnsureMenuPreviewOrb()
        {
            if (IsInGameplay())
            {
                DestroyMenuPreviewOrb();
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Log?.Warn("[GeminiOrbFX UI] Menu preview camera not found.");
                return;
            }

            Color previewColor = GetDefaultGameplayOrbColor();

            if (_menuPreviewOrb != null)
            {
                var existingCtrl = _menuPreviewOrb.GetComponent<OrbLaneController>();
                if (existingCtrl != null)
                    return;

                DestroyMenuPreviewOrb();
            }

            GameObject template = GetOrbTemplate();
            if (template == null)
            {
                Log?.Warn("[GeminiOrbFX UI] Menu preview template not found.");
                return;
            }

            try
            {
                _menuPreviewOrb = UnityEngine.Object.Instantiate(template);
                _menuPreviewOrb.name = "GeminiOrbFX_MenuPreviewOrb";
                _menuPreviewOrb.SetActive(true);

                SetLayerRecursively(_menuPreviewOrb, 0);
                ForceAllRenderersOn(_menuPreviewOrb);

                foreach (var c in _menuPreviewOrb.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.Destroy(c);

                foreach (var rb in _menuPreviewOrb.GetComponentsInChildren<Rigidbody>(true))
                    UnityEngine.Object.Destroy(rb);

                var ctrl = _menuPreviewOrb.GetComponent<OrbLaneController>();
                if (ctrl == null)
                    ctrl = _menuPreviewOrb.AddComponent<OrbLaneController>();

                ctrl.InitPreview(
                    cam.transform,
                    "GEMINIORBFX",
                    1,
                    PluginConfig.Instance.OrbSpeed,
                    previewColor,
                    0f,
                    false
                );
            }
            catch (Exception ex)
            {
                Log?.Warn("[GeminiOrbFX UI] Failed to create menu preview orb: " + ex.Message);
                DestroyMenuPreviewOrb();
            }
        }

        private void DestroyMenuPreviewOrb()
        {
            try
            {
                if (_menuPreviewOrb != null)
                    UnityEngine.Object.Destroy(_menuPreviewOrb);
            }
            catch { }

            _menuPreviewOrb = null;
        }

        internal static string GetSceneStatusText()
        {
            if (_instance == null)
                return "Unknown";

            return _instance.IsInGameplay() ? "Gameplay" : "Menu";
        }

        internal static bool GetOrbTestEnabled()
        {
            return PluginConfig.Instance.OrbTestEnabled;
        }

        internal static bool IsHttpServerRunning()
        {
            return _instance != null &&
                   _instance._httpOrbServer != null &&
                   _instance._httpOrbServer.IsRunning;
        }

        internal static Services.TikTokService GetTikTokService()
        {
            return _instance?._tikTokService;
        }

        internal static void StartTikTokRelay(string username)
        {
            if (_instance == null)
                return;

            if (!IsTikTokRelayMode())
            {
                Log?.Info("[TikTokRelay] Start ignored because HTTP-only mode is enabled.");
                return;
            }

            try
            {
                if (_instance._tikTokRelayService == null)
                    _instance._tikTokRelayService = new Services.TikTokRelayService();

                _instance._tikTokRelayService.Start(username);
            }
            catch (Exception ex)
            {
                Log?.Warn("[TikTokRelay] Start failed: " + ex.Message);
            }
        }

        internal static void StopTikTokRelay()
        {
            if (_instance == null)
                return;

            try
            {
                _ = _instance._tikTokRelayService?.StopAsync();
            }
            catch (Exception ex)
            {
                Log?.Warn("[TikTokRelay] Stop failed: " + ex.Message);
            }
        }

        internal static void SetOrbTestEnabled(bool enabled)
        {
            PluginConfig.Instance.OrbTestEnabled = enabled;

            if (!enabled && _instance != null)
            {
                _instance._nextOrbTestRealtime = 0f;
                RemoveQueuedTestOrbs();
            }
        }
              
        internal bool IsInGameplay()
        {
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                string name = scene.name ?? "";
                return name.IndexOf("GameCore", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { }
            return false;
        }

        internal string GetActiveSceneName()
        {
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                return scene.name ?? "(unknown)";
            }
            catch
            {
                return "(unknown)";
            }
        }
        
             
        internal OrbRequest ParseOrbRequest(Uri url)
        {
            OrbRequest r = new OrbRequest();

            string username = GetQueryParam(url, "name");
            username = CleanNickname(username);
            r.Name = username.ToUpperInvariant();

            string laneStr = GetQueryParam(url, "lane");
            if (TryParseInt(laneStr, out int lane) && lane >= 0 && lane <= 3)
                r.Lane = lane;

            string speedStr = GetQueryParam(url, "speed");
            if (TryParseFloat(speedStr, out float spd))
                r.Speed = Mathf.Clamp(spd, MinSpeed, MaxSpeed);

            string colorStr = GetQueryParam(url, "color");
            if (!string.IsNullOrEmpty(colorStr) && TryParseColor(colorStr, out Color c))
                r.Color = c;

            string hsvStr = GetQueryParam(url, "hsv");
            if (!string.IsNullOrEmpty(hsvStr) && TryParseHSVTriplet(hsvStr, out float h3, out float s3, out float v3))
            {
                r.H = Clamp01(h3);
                r.S = Clamp01(s3);
                r.V = Clamp01(v3);
            }
            else
            {
                string hStr = GetQueryParam(url, "h");
                string sStr = GetQueryParam(url, "s");
                string vStr = GetQueryParam(url, "v");

                if (TryParseFloat(hStr, out float h)) r.H = Clamp01(h);
                if (TryParseFloat(sStr, out float s)) r.S = Clamp01(s);
                if (TryParseFloat(vStr, out float v)) r.V = Clamp01(v);
            }

            return r;
        }
                
        internal static bool EnqueueOrbRequest(OrbRequest r, out int queueCountAfter)
        {
            if (!TryPassRequestRateLimit())
            {
                queueCountAfter = GetQueueCount();
                Log?.Warn("[GeminiOrbFX] Request rate limit hit. Orb rejected.");
                return false;
            }

            lock (Services.OrbQueueService.QueueLock)
            {
                if (Services.OrbQueueService.Queue.Count >= PluginConfig.Instance.MaxQueue)
                {
                    if (DropOldestWhenFull)
                    {
                        Services.OrbQueueService.Queue.Dequeue();
                    }
                    else
                    {
                        queueCountAfter = Services.OrbQueueService.Queue.Count;
                        Log?.Warn($"[GeminiOrbFX UI] Queue full ({Services.OrbQueueService.Queue.Count}). Rejecting request '{r.Name}'.");
                        return false;
                    }
                }

                Services.OrbQueueService.Queue.Enqueue(r);
                queueCountAfter = Services.OrbQueueService.Queue.Count;
            }

            RaiseQueueChanged();
            return true;
        }

        private void PumpOrbTest()
        {
            if (!PluginConfig.Instance.OrbTestEnabled)
                return;

            if (!IsInGameplay())
            {
                _nextOrbTestRealtime = 0f;
                return;
            }

            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextOrbTestRealtime)
                return;

            EnqueueTestOrb("geminiorbfx");

            float intervalSeconds = UnityEngine.Mathf.Max(
                0.02f,
                PluginConfig.Instance.OrbTestIntervalMs / 1000f);

            _nextOrbTestRealtime = now + intervalSeconds;
        }

        private static Shader FindBestTrailShader()
        {
            Shader s = Shader.Find("Particles/Additive");
            if (s != null) return s;

            s = Shader.Find("Legacy Shaders/Particles/Additive");
            if (s != null) return s;

            s = Shader.Find("Sprites/Default");
            if (s != null) return s;

            return Shader.Find("Unlit/Color");
        }

        private Color GetDefaultGameplayOrbColor()
        {
            float hue = PickHueNoRepeat();

            Color baseC = Color.HSVToRGB(hue, NameHSV_S, NameHSV_V);

            float orbBrightness = PluginConfig.Instance.OrbBrightness;

            baseC = new Color(
                Mathf.Clamp01(baseC.r * orbBrightness),
                Mathf.Clamp01(baseC.g * orbBrightness),
                Mathf.Clamp01(baseC.b * orbBrightness),
                1f
            );

            return baseC;
        }

        internal static Material GetSharedTrailMaterial()
        {
            if (_sharedTrailMaterial != null)
                return _sharedTrailMaterial;

            Shader shader = FindBestTrailShader();
            if (shader == null)
            {
                Log?.Warn("[GeminiOrbFX UI] No usable trail shader found.");
                return null;
            }

            _sharedTrailMaterial = new Material(shader);
            _sharedTrailMaterial.name = "GeminiOrbFX_UI_TrailShared";

            try
            {
                if (_sharedTrailMaterial.HasProperty("_Color"))
                    _sharedTrailMaterial.SetColor("_Color", Color.white);

                if (_sharedTrailMaterial.HasProperty("_TintColor"))
                    _sharedTrailMaterial.SetColor("_TintColor", Color.white);
            }
            catch { }

            Log?.Info("[GeminiOrbFX UI] Trail shader: " + shader.name);
            return _sharedTrailMaterial;
        }
          
        
        internal static float Clamp01(float v) => Mathf.Clamp01(v);

        private static bool TryParseInt(string s, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s)) return false;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseFloat(string s, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseHSVTriplet(string s, out float h, out float sat, out float val)
        {
            h = sat = val = 0f;
            if (string.IsNullOrEmpty(s)) return false;

            char[] seps = new char[] { ',', '|', ';', ' ' };
            string[] parts = s.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return false;

            if (!TryParseFloat(parts[0], out h)) return false;
            if (!TryParseFloat(parts[1], out sat)) return false;
            if (!TryParseFloat(parts[2], out val)) return false;
            return true;
        }

        internal static float PickHueNoRepeat()
        {
            float h = UnityEngine.Random.value;

            if (_lastHue < 0f)
            {
                _lastHue = h;
                return h;
            }

            for (int i = 0; i < HueRerollTries; i++)
            {
                float d = HueDistance01(h, _lastHue);
                if (d >= MinHueSeparation)
                {
                    _lastHue = h;
                    return h;
                }

                h = UnityEngine.Random.value;
            }

            h = Wrap01(_lastHue + MinHueSeparation);
            _lastHue = h;
            return h;
        }

        private static float HueDistance01(float a, float b)
        {
            float d = Mathf.Abs(a - b);
            return Mathf.Min(d, 1f - d);
        }

        private static float Wrap01(float v)
        {
            v = v % 1f;
            if (v < 0f) v += 1f;
            return v;
        }

        private static bool TryParseColor(string input, out Color c)
        {
            c = Color.white;
            if (string.IsNullOrEmpty(input)) return false;

            string s = input.Trim();

            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length == 6)
            {
                if (TryParseHexByte(s.Substring(0, 2), out byte r) &&
                    TryParseHexByte(s.Substring(2, 2), out byte g) &&
                    TryParseHexByte(s.Substring(4, 2), out byte b))
                {
                    c = new Color(r / 255f, g / 255f, b / 255f, 1f);
                    return true;
                }
            }

            switch (s.ToLowerInvariant())
            {
                case "red": c = Color.red; return true;
                case "green": c = Color.green; return true;
                case "blue": c = Color.blue; return true;
                case "cyan": c = Color.cyan; return true;
                case "magenta": c = Color.magenta; return true;
                case "yellow": c = Color.yellow; return true;
                case "white": c = Color.white; return true;
                case "black": c = Color.black; return true;
                case "orange": c = new Color(1f, 0.5f, 0f, 1f); return true;
                case "pink": c = new Color(1f, 0.2f, 0.6f, 1f); return true;
                case "purple": c = new Color(0.6f, 0.2f, 1f, 1f); return true;
                case "lime": c = new Color(0.5f, 1f, 0f, 1f); return true;
                case "gold": c = new Color(1f, 0.84f, 0.15f, 1f); return true;
            }

            return false;
        }

        private static bool TryParseHexByte(string hex2, out byte b)
        {
            b = 0;
            if (hex2 == null || hex2.Length != 2) return false;
            return byte.TryParse(hex2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
        }

        private static string CleanNickname(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "USER";

            StringBuilder sb = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '.')
                    sb.Append(c);
            }

            string result = sb.ToString().Trim();

            if (result.Length > 18)
                result = result.Substring(0, 18);

            if (string.IsNullOrEmpty(result))
                result = "USER";

            return result;
        }

        private static string GetQueryParam(Uri uri, string key)
        {
            try
            {
                string q = uri.Query;
                if (string.IsNullOrEmpty(q)) return null;
                if (q.StartsWith("?")) q = q.Substring(1);

                string[] parts = q.Split('&');
                for (int i = 0; i < parts.Length; i++)
                {
                    string[] kv = parts[i].Split(new char[] { '=' }, 2);
                    if (kv.Length != 2) continue;

                    string k = Uri.UnescapeDataString(kv[0] ?? "");
                    if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;

                    return Uri.UnescapeDataString(kv[1] ?? "");
                }
            }
            catch { }
            return null;
        }

        internal GameObject GetOrbTemplate()
        {
            if (_cachedOrbTemplate != null)
                return _cachedOrbTemplate;

            var bundlePrefab = TryLoadOrbPrefabFromEmbeddedBundle();
            if (bundlePrefab != null)
            {
                _cachedOrbTemplate = bundlePrefab;
                return _cachedOrbTemplate;
            }

            bundlePrefab = TryLoadOrbPrefabFromDiskBundle();
            if (bundlePrefab != null)
            {
                _cachedOrbTemplate = bundlePrefab;
                return _cachedOrbTemplate;
            }

            Log?.Warn("[GeminiOrbFX UI] No orb prefab found (embedded or disk).");
            return null;
        }

        private GameObject TryLoadOrbPrefabFromEmbeddedBundle()
        {
            try
            {
                if (_bundlePrefab != null)
                    return _bundlePrefab;

                var asm = Assembly.GetExecutingAssembly();
                string[] names = asm.GetManifestResourceNames();

                if (names == null || names.Length == 0)
                {
                    Log?.Warn("[GeminiOrbFX UI] No embedded resources found in assembly.");
                    return null;
                }

                string match = null;

                for (int i = 0; i < names.Length; i++)
                {
                    string n = names[i];
                    if (string.IsNullOrEmpty(n)) continue;

                    if (n.EndsWith("." + BUNDLE_FILE_NAME, StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith("." + BUNDLE_FILE_NAME + ".bytes", StringComparison.OrdinalIgnoreCase) ||
                        n.EndsWith("." + BUNDLE_FILE_NAME + ".assetbundle", StringComparison.OrdinalIgnoreCase))
                    {
                        match = n;
                        break;
                    }
                }

                if (match == null)
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        string n = names[i];
                        if (string.IsNullOrEmpty(n)) continue;

                        if (n.IndexOf(BUNDLE_FILE_NAME, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            match = n;
                            break;
                        }
                    }
                }

                if (match == null)
                {
                    Log?.Warn("[GeminiOrbFX UI] Could not find embedded resource containing: " + BUNDLE_FILE_NAME);
                    return null;
                }

                using (Stream s = asm.GetManifestResourceStream(match))
                {
                    if (s == null)
                    {
                        Log?.Warn("[GeminiOrbFX UI] Resource stream was null: " + match);
                        return null;
                    }

                    byte[] data = ReadAllBytes(s);
                    if (data == null || data.Length == 0)
                    {
                        Log?.Warn("[GeminiOrbFX UI] Embedded bundle bytes empty: " + match);
                        return null;
                    }

                    _bundle = AssetBundle.LoadFromMemory(data);
                    if (_bundle == null)
                    {
                        Log?.Warn("[GeminiOrbFX UI] AssetBundle.LoadFromMemory failed (embedded).");
                        return null;
                    }

                    _bundlePrefab = _bundle.LoadAsset<GameObject>(ORB_PREFAB_NAME);
                    if (_bundlePrefab == null)
                    {
                        Log?.Warn("[GeminiOrbFX UI] Prefab not found in embedded bundle: " + ORB_PREFAB_NAME);
                        TryLogBundleAssets(_bundle);
                        return null;
                    }

                    Log?.Info("[GeminiOrbFX UI] Orb prefab loaded from EMBEDDED AssetBundle.");
                    return _bundlePrefab;
                }
            }
            catch (Exception ex)
            {
                Log?.Warn("[GeminiOrbFX UI] Embedded bundle load error: " + ex.Message);
                return null;
            }
        }

        private GameObject TryLoadOrbPrefabFromDiskBundle()
        {
            try
            {
                if (_bundlePrefab != null)
                    return _bundlePrefab;

                string root = UnityGame.InstallPath;
                if (string.IsNullOrEmpty(root))
                    root = AppDomain.CurrentDomain.BaseDirectory;

                string bundlePath = Path.Combine(root, "UserData", "GeminiOrbFX", "AssetBundles", BUNDLE_FILE_NAME);

                if (!File.Exists(bundlePath))
                    return null;

                _bundle = AssetBundle.LoadFromFile(bundlePath);
                if (_bundle == null)
                {
                    Log?.Warn("[GeminiOrbFX UI] AssetBundle failed to load from disk.");
                    return null;
                }

                _bundlePrefab = _bundle.LoadAsset<GameObject>(ORB_PREFAB_NAME);
                if (_bundlePrefab == null)
                {
                    Log?.Warn("[GeminiOrbFX UI] Prefab not found in disk bundle: " + ORB_PREFAB_NAME);
                    TryLogBundleAssets(_bundle);
                    return null;
                }

                Log?.Info("[GeminiOrbFX UI] Orb prefab loaded from DISK AssetBundle.");
                return _bundlePrefab;
            }
            catch (Exception ex)
            {
                Log?.Warn("[GeminiOrbFX UI] Disk bundle load error: " + ex.Message);
                return null;
            }
        }

        private static byte[] ReadAllBytes(Stream s)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private void TryLogBundleAssets(AssetBundle b)
        {
            if (b == null) return;
            try
            {
                var assets = b.GetAllAssetNames();
                Log?.Warn("[GeminiOrbFX UI] Assets inside bundle:");
                foreach (var a in assets) Log?.Warn("  - " + a);
            }
            catch { }
        }

        internal Transform GetBestCameraTransform(out bool stereo)
        {
            stereo = false;

            Camera bestStereo = null;
            Camera bestMain = null;

            try
            {
                var cams = UnityEngine.Resources.FindObjectsOfTypeAll<Camera>();
                foreach (var c in cams)
                {
                    if (c == null) continue;
                    if (!c.gameObject.scene.IsValid()) continue;
                    if (!c.enabled) continue;
                    if (!c.gameObject.activeInHierarchy) continue;

                    string n = c.gameObject.name ?? "";
                    if (IsBadCameraName(n)) continue;

                    if (c.stereoEnabled)
                    {
                        if (bestStereo == null) bestStereo = c;

                        if (c.CompareTag("MainCamera") || string.Equals(n, "MainCamera", StringComparison.OrdinalIgnoreCase))
                        {
                            bestStereo = c;
                            break;
                        }
                    }

                    if (bestMain == null && (c.CompareTag("MainCamera") || string.Equals(n, "MainCamera", StringComparison.OrdinalIgnoreCase)))
                        bestMain = c;
                }
            }
            catch { }

            if (bestStereo != null)
            {
                stereo = true;
                return bestStereo.transform;
            }

            if (bestMain != null)
            {
                stereo = bestMain.stereoEnabled;
                return bestMain.transform;
            }

            if (Camera.main != null && Camera.main.enabled && !IsBadCameraName(Camera.main.gameObject.name))
            {
                stereo = Camera.main.stereoEnabled;
                return Camera.main.transform;
            }

            return null;
        }

        private static bool IsBadCameraName(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;

            if (n.IndexOf("EnvironmentMap", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("EnvironmentMapManager", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Reflection", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Mirror", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        internal static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;

            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        internal static void ForceAllRenderersOn(GameObject root)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.enabled = true;
                r.forceRenderingOff = false;
            }
        }


        internal class OrbLaneController : MonoBehaviour
        {
            private Transform _cam;
            private Vector3 _end;
            private float _spawnTime;

            private float _speed;
            private float _maxLife;
            private int _laneIndex;

            private Vector3 _laneCenterXY;

            private object _textComponent;
            private Transform _nameTf;

            private Renderer[] _nameRenderers;
            private MaterialPropertyBlock _mpb;

            private Transform _coreTf;
            private Transform _haloTf;

            private Vector3 _baseScale;
            private Vector3 _haloBaseScale;

            private bool _spawnFxActive;

            private Quaternion _baseRot;
            private const float OrbTiltDegrees = 6.0f;
            private const float NameFaceCamDamp = 18f;

            private float _haloPhase;

            private ParticleSystem[] _haloParticles;
            private Renderer[] _haloRenderers;
            private MaterialPropertyBlock _haloMpb;
            private bool _hasHalo;

            private TrailRenderer _trail;

            private const float TrailBaseTime = 0.36f;
            private const float TrailMinTimeAtImpact = 0.16f;
            private const float TrailPreImpactFadeDist = 1.00f;
            private const float TrailMinWidthAtImpact = 0.08f;

            private const float ImpactTrailBrightnessMul = 1.45f;
            private const float ImpactTrailWidthMul = 1.18f;

            private Color _trailStartColorBase;
            private Color _trailEndColorBase;

            private float _trailBaseWidth;

            private Vector3 _startPos;
            private float _travelDist;

            private const float OrbTravelScaleStart = 0.85f;
            private const float OrbTravelScaleEnd = 1.10f;

            private const float NameTravelScaleStart = 0.95f;
            private const float NameTravelScaleEnd = 1.75f;

            private bool _textBounceActive;
            private float _textBounceStartTime;
            private Vector3 _textBaseScale;
            private Vector3 _nameAuthoredScale;

            private const float TextBounceDuration = 0.24f;
            private const float TextBounceStartMul = 0.60f;
            private const float TextBounceOvershootMul = 1.35f;
            private const float TextBounceEndMul = 1.00f;

            private bool _textGlowBurstActive;
            private float _textGlowBurstStartTime;

            private const float TextGlowBurstDuration = 0.24f;
            private const float TextGlowBurstPeakMul = 2.6f;

            private Color _faceColor;
            private Color _outlineColor;
            private Color _glowColorBase;
            private bool _hasNameColors;

            private bool _impactFxActive;
            private float _impactStartTime;

            private const float ImpactFXDuration = 0.15f;
            private const float ImpactHaloPeakMul = 1.75f;
            private const float ImpactGlowPeakMul = 2.20f;

            private Color _impactParticleColor = Color.white;

            // ---------------- PREVIEW MODE ----------------
            private bool _isPreviewMode;
            private bool _previewFreezeAtSpawn;
            private float _laneXMicroOffset;
            private string _displayName;
            private Color _baseColor;

            private bool _previewWaitingForRespawn;
            private float _previewRespawnTime;
            private const float PreviewRespawnDelay = 0.35f;

            private void StartImpactFX()
            {
                if (_impactFxActive) return;
                _impactFxActive = true;
                _impactStartTime = Time.time;
            }

            private void SpawnImpactParticles(Color baseColor)
            {
                GameObject go = new GameObject("OrbImpactFX_UI");
                go.transform.position = transform.position;
                go.transform.rotation = Quaternion.identity;

                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = false;
                main.duration = 0.25f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(baseColor.r, baseColor.g, baseColor.b, 1f));
                main.maxParticles = 20;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;
                emission.SetBursts(new ParticleSystem.Burst[]
                {
    new ParticleSystem.Burst(0f, (short)ImpactParticlesMin, (short)ImpactParticlesMax)
                });

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;

                var velocityOverLifetime = ps.velocityOverLifetime;
                velocityOverLifetime.enabled = false;

                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[]
                    {
        new GradientColorKey(new Color(baseColor.r * 1.2f, baseColor.g * 1.2f, baseColor.b * 1.2f), 0f),
        new GradientColorKey(baseColor, 0.5f),
        new GradientColorKey(baseColor, 1f)
                    },
                    new GradientAlphaKey[]
                    {
        new GradientAlphaKey(0.95f, 0f),
        new GradientAlphaKey(0.7f, 0.35f),
        new GradientAlphaKey(0f, 1f)
                    }
                );
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

                var sizeOverLifetime = ps.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0f, 1f);
                sizeCurve.AddKey(0.55f, 0.8f);
                sizeCurve.AddKey(1f, 0f);
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    Material mat = Plugin.GetSharedTrailMaterial();
                    if (mat != null)
                        renderer.sharedMaterial = mat;

                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.alignment = ParticleSystemRenderSpace.View;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                ps.Play();
                Destroy(go, 1.0f);
            }

            private void CreateTrail(Color baseColor)
            {
                _trail = gameObject.AddComponent<TrailRenderer>();

                _trailBaseWidth = PluginConfig.Instance.TrailWidth;

                _trail.time = TrailBaseTime;
                _trail.minVertexDistance = 0.010f;
                _trail.widthMultiplier = _trailBaseWidth;

                AnimationCurve width = new AnimationCurve();
                width.AddKey(0f, 1.8f);
                width.AddKey(0.08f, 1.2f);
                width.AddKey(0.25f, 0.65f);
                width.AddKey(0.55f, 0.25f);
                width.AddKey(1f, 0f);
                _trail.widthCurve = width;

                float trailBrightness = PluginConfig.Instance.TrailBrightness;

                Color bright = new Color(
                    Mathf.Clamp01(baseColor.r * trailBrightness),
                    Mathf.Clamp01(baseColor.g * trailBrightness),
                    Mathf.Clamp01(baseColor.b * trailBrightness),
                    1f
                );

                Color mid = new Color(
                    Mathf.Clamp01(baseColor.r * 1.35f),
                    Mathf.Clamp01(baseColor.g * 1.35f),
                    Mathf.Clamp01(baseColor.b * 1.35f),
                    1f
                );

                _trailStartColorBase = new Color(bright.r, bright.g, bright.b, 0.98f);
                _trailEndColorBase = new Color(mid.r, mid.g, mid.b, 0.00f);

                _trail.startColor = _trailStartColorBase;
                _trail.endColor = _trailEndColorBase;

                Material sharedMat = Plugin.GetSharedTrailMaterial();
                if (sharedMat != null)
                    _trail.sharedMaterial = sharedMat;
                else
                    _trail.material = new Material(Shader.Find("Sprites/Default"));

                _trail.alignment = LineAlignment.View;
                _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _trail.receiveShadows = false;
                _trail.textureMode = LineTextureMode.Stretch;
                _trail.autodestruct = false;
                _trail.emitting = true;
            }

            private void UpdateTrailPreImpact(float distToEnd)
            {
                if (_trail == null) return;
                if (_impactFxActive) return;

                if (distToEnd >= TrailPreImpactFadeDist)
                {
                    _trail.time = TrailBaseTime;
                    _trail.widthMultiplier = _trailBaseWidth;
                    _trail.startColor = _trailStartColorBase;
                    _trail.endColor = _trailEndColorBase;
                    return;
                }

                float p = 1f - Mathf.Clamp01(distToEnd / TrailPreImpactFadeDist);
                p = Smooth01(p);

                _trail.time = Mathf.Lerp(TrailBaseTime, TrailMinTimeAtImpact, p);
                _trail.widthMultiplier = Mathf.Lerp(_trailBaseWidth, TrailMinWidthAtImpact, p);

                float alphaMul = Mathf.Lerp(1f, 0.25f, p);
                _trail.startColor = new Color(
                    _trailStartColorBase.r,
                    _trailStartColorBase.g,
                    _trailStartColorBase.b,
                    _trailStartColorBase.a * alphaMul
                );
                _trail.endColor = new Color(
                    _trailEndColorBase.r,
                    _trailEndColorBase.g,
                    _trailEndColorBase.b,
                    _trailEndColorBase.a
                );
            }

            internal void RefreshPreviewFromConfig()
            {
                if (!_isPreviewMode)
                    return;

                ApplyPreviewLiveSettings();

                Color previewColor = Plugin._instance != null
                    ? Plugin._instance.GetDefaultGameplayOrbColor()
                    : _baseColor;

                _baseColor = previewColor;

                ApplyNameColorAndHalo(_baseColor);

                if (_trail != null)
                {
                    float trailBrightness = PluginConfig.Instance.TrailBrightness;

                    Color bright = new Color(
                        Mathf.Clamp01(_baseColor.r * trailBrightness),
                        Mathf.Clamp01(_baseColor.g * trailBrightness),
                        Mathf.Clamp01(_baseColor.b * trailBrightness),
                        1f
                    );

                    Color mid = new Color(
                        Mathf.Clamp01(_baseColor.r * 1.35f),
                        Mathf.Clamp01(_baseColor.g * 1.35f),
                        Mathf.Clamp01(_baseColor.b * 1.35f),
                        1f
                    );

                    _trailBaseWidth = PluginConfig.Instance.TrailWidth;
                    _trailStartColorBase = new Color(bright.r, bright.g, bright.b, 0.98f);
                    _trailEndColorBase = new Color(mid.r, mid.g, mid.b, 0.00f);

                    _trail.widthMultiplier = _trailBaseWidth;
                    _trail.startColor = _trailStartColorBase;
                    _trail.endColor = _trailEndColorBase;
                }

                if (_nameTf != null && !_textBounceActive && !_previewWaitingForRespawn)
                    _nameTf.localScale = _textBaseScale;
            }

            private void UpdateImpactTrailPulse(float impactUp)
            {
                if (_trail == null) return;

                float widthMul = Mathf.Lerp(1f, ImpactTrailWidthMul, impactUp);
                float brightMul = Mathf.Lerp(1f, ImpactTrailBrightnessMul, impactUp);

                _trail.time = TrailMinTimeAtImpact;
                _trail.widthMultiplier = _trailBaseWidth * widthMul;

                Color brightStart = new Color(
                    Mathf.Clamp01(_trailStartColorBase.r * brightMul),
                    Mathf.Clamp01(_trailStartColorBase.g * brightMul),
                    Mathf.Clamp01(_trailStartColorBase.b * brightMul),
                    _trailStartColorBase.a);

                Color brightEnd = new Color(
                    Mathf.Clamp01(_trailEndColorBase.r * brightMul),
                    Mathf.Clamp01(_trailEndColorBase.g * brightMul),
                    Mathf.Clamp01(_trailEndColorBase.b * brightMul),
                    _trailEndColorBase.a);

                _trail.startColor = brightStart;
                _trail.endColor = brightEnd;

                if (impactUp > 0.15f)
                    _trail.emitting = true;
                else
                    _trail.emitting = false;
            }

            internal void Init(Transform cam, string username, int laneIndex, float speed, float maxLife, Color baseColor, float laneXMicroOffset)
            {
                InitInternal(cam, username, laneIndex, speed, maxLife, baseColor, laneXMicroOffset, false, false);
            }

            internal void InitPreview(Transform cam, string username, int laneIndex, float speed, Color baseColor, float laneXMicroOffset, bool freezeAtSpawn)
            {
                InitInternal(cam, username, laneIndex, speed, float.PositiveInfinity, baseColor, laneXMicroOffset, true, freezeAtSpawn);
            }

            private void InitInternal(Transform cam, string username, int laneIndex, float speed, float maxLife, Color baseColor, float laneXMicroOffset, bool isPreviewMode, bool previewFreezeAtSpawn)
            {
                _cam = cam;
                _spawnTime = Time.time;

                _speed = Mathf.Max(0.1f, speed);
                _maxLife = Mathf.Max(0.5f, maxLife);
                _laneIndex = Mathf.Clamp(laneIndex, 0, 3);

                _isPreviewMode = isPreviewMode;
                _previewFreezeAtSpawn = previewFreezeAtSpawn;
                _laneXMicroOffset = laneXMicroOffset;
                _displayName = string.IsNullOrEmpty(username) ? "USER" : username.ToUpperInvariant();
                _baseColor = baseColor;

                _previewWaitingForRespawn = false;
                _previewRespawnTime = 0f;

                Vector3 start;
                Vector3 end;
                ComputeCurrentStartAndEnd(out start, out end);

                _end = end;
                _laneCenterXY = new Vector3(start.x, start.y, 0f);

                transform.position = start;
                _startPos = start;
                _travelDist = Mathf.Max(0.001f, Vector3.Distance(start, _end));
                transform.rotation = Quaternion.LookRotation(-GameplayForward, GameplayUp);
                _baseRot = transform.rotation;

                _baseScale = transform.localScale;

                _coreTf = transform.Find("OrbCore");

                _haloTf = transform.Find("OrbHalo");
                if (_haloTf != null)
                {
                    _haloBaseScale = _haloTf.localScale;

                    _haloParticles = _haloTf.GetComponentsInChildren<ParticleSystem>(true);
                    _haloRenderers = _haloTf.GetComponentsInChildren<Renderer>(true);
                    _haloMpb = new MaterialPropertyBlock();
                    _hasHalo = (_haloParticles != null && _haloParticles.Length > 0) || (_haloRenderers != null && _haloRenderers.Length > 0);

                    _haloPhase = UnityEngine.Random.Range(0f, HaloPulsePhaseRange);
                }

                CreateTrail(baseColor);

                _nameTf = transform.Find("NameText");
                if (_nameTf != null)
                {
                    _nameAuthoredScale = _nameTf.localScale;

                    float textScale = _isPreviewMode ? Plugin.GetPreviewAwareTextScale() : PluginConfig.Instance.NameTextScale;
                    _nameTf.localScale = _nameAuthoredScale * textScale;

                    _textComponent = TryGetTextComponent(_nameTf.gameObject);
                    ConfigureTMPOneLine(_textComponent);
                    SetText(_textComponent, _displayName);

                    _nameRenderers = _nameTf.GetComponentsInChildren<Renderer>(true);
                    if (_mpb == null) _mpb = new MaterialPropertyBlock();

                    ApplyNameColorAndHalo(baseColor);

                    _textBaseScale = _nameTf.localScale;
                    _nameTf.localScale = _textBaseScale * TextBounceStartMul;

                    _textBounceActive = true;
                    _textBounceStartTime = Time.time;

                    _textGlowBurstActive = true;
                    _textGlowBurstStartTime = Time.time;
                }

                _impactParticleColor = baseColor;
                _impactFxActive = false;

                if (_isPreviewMode)
                {
                    StartPreviewCycle(true);
                }
                else
                {
                    _spawnFxActive = true;
                    transform.localScale = _baseScale * PopStartScaleMul;
                    if (_haloTf != null) _haloTf.localScale = _haloBaseScale * HaloBurstStartMul;
                }
            }

            private void ComputeCurrentStartAndEnd(out Vector3 start, out Vector3 end)
            {
                Vector3 origin = _cam.position;

                float x = LaneX[_laneIndex] + _laneXMicroOffset;
                float y = _isPreviewMode ? Plugin.GetPreviewAwareOrbHeight() : PluginConfig.Instance.OrbHeight;
                float spawnDistance = _isPreviewMode ? Plugin.GetPreviewAwareSpawnDistance() : PluginConfig.Instance.SpawnDistance;

                start = origin + GameplayForward * spawnDistance + GameplayRight * x + GameplayUp * y;
                end = origin + GameplayForward * DespawnZ + GameplayRight * x + GameplayUp * y;
            }

            private void ApplyPreviewLiveSettings()
            {
                if (_cam == null)
                    return;

                Vector3 start;
                Vector3 end;
                ComputeCurrentStartAndEnd(out start, out end);

                _end = end;
                _laneCenterXY = new Vector3(start.x, start.y, 0f);
                _startPos = start;
                _travelDist = Mathf.Max(0.001f, Vector3.Distance(start, _end));

                transform.rotation = Quaternion.LookRotation(-GameplayForward, GameplayUp);
                _baseRot = transform.rotation;

                _speed = Mathf.Clamp(Plugin.GetPreviewAwareOrbSpeed(), MinSpeed, MaxSpeed);

                if (_nameTf != null)
                {
                    _textBaseScale = _nameAuthoredScale * Plugin.GetPreviewAwareTextScale();

                    if (!_textBounceActive && !_previewWaitingForRespawn)
                        _nameTf.localScale = _textBaseScale;
                }
            }

            private void ApplyNameColorAndHalo(Color baseC)
            {
                ApplyNameColor(baseC);
                ApplyHaloColor(baseC);
            }

            private void ApplyNameColor(Color baseC)
            {
                TrySetColor(_textComponent, baseC);

                if (_nameRenderers == null) return;

                _faceColor = new Color(baseC.r * FaceMul, baseC.g * FaceMul, baseC.b * FaceMul, 1f);
                _outlineColor = new Color(baseC.r * OutlineMul, baseC.g * OutlineMul, baseC.b * OutlineMul, 1f);
                _glowColorBase = new Color(baseC.r * GlowMul, baseC.g * GlowMul, baseC.b * GlowMul, 1f);
                _hasNameColors = true;

                ApplyNamePropertyBlock(1f);
            }

            private void ApplyHaloColor(Color baseC)
            {
                if (!_hasHalo) return;

                Color haloC = new Color(baseC.r * HaloColorMul, baseC.g * HaloColorMul, baseC.b * HaloColorMul, 1f);

                if (_haloParticles != null)
                {
                    for (int i = 0; i < _haloParticles.Length; i++)
                    {
                        var ps = _haloParticles[i];
                        if (ps == null) continue;

                        try
                        {
                            var main = ps.main;

                            float a = haloC.a;
                            try
                            {
                                var current = main.startColor;
                                if (current.mode == ParticleSystemGradientMode.Color)
                                    a = current.color.a;
                            }
                            catch { }

                            Color final = new Color(haloC.r, haloC.g, haloC.b, a);
                            main.startColor = new ParticleSystem.MinMaxGradient(final);

                            if (HaloClearOnSpawn)
                            {
                                ps.Clear(true);
                                ps.Play(true);
                            }
                        }
                        catch { }
                    }
                }

                if (_haloRenderers != null)
                {
                    for (int i = 0; i < _haloRenderers.Length; i++)
                    {
                        var r = _haloRenderers[i];
                        if (r == null) continue;

                        r.GetPropertyBlock(_haloMpb);

                        var mat = r.sharedMaterial;
                        if (mat != null)
                        {
                            if (mat.HasProperty("_Color")) _haloMpb.SetColor("_Color", haloC);
                            if (mat.HasProperty("_TintColor")) _haloMpb.SetColor("_TintColor", haloC);
                            if (mat.HasProperty("_EmissionColor")) _haloMpb.SetColor("_EmissionColor", haloC);
                        }

                        r.SetPropertyBlock(_haloMpb);
                    }
                }
            }

            private void ApplyNamePropertyBlock(float glowMul)
            {
                if (!_hasNameColors || _nameRenderers == null) return;

                Color glow = new Color(_glowColorBase.r * glowMul, _glowColorBase.g * glowMul, _glowColorBase.b * glowMul, 1f);

                for (int i = 0; i < _nameRenderers.Length; i++)
                {
                    var r = _nameRenderers[i];
                    if (r == null) continue;

                    r.GetPropertyBlock(_mpb);

                    var mat = r.sharedMaterial;
                    if (mat != null)
                    {
                        if (mat.HasProperty("_FaceColor")) _mpb.SetColor("_FaceColor", _faceColor);
                        if (mat.HasProperty("_OutlineColor")) _mpb.SetColor("_OutlineColor", _outlineColor);
                        if (mat.HasProperty("_GlowColor")) _mpb.SetColor("_GlowColor", glow);

                        if (mat.HasProperty("_Color")) _mpb.SetColor("_Color", _faceColor);
                        if (mat.HasProperty("_EmissionColor")) _mpb.SetColor("_EmissionColor", glow);
                    }

                    r.SetPropertyBlock(_mpb);
                }
            }

            private void StartPreviewCycle(bool immediatePositionReset)
            {
                _previewWaitingForRespawn = false;
                _previewRespawnTime = 0f;

                _spawnTime = Time.time;
                _impactFxActive = false;
                _spawnFxActive = true;

                _textBounceActive = (_nameTf != null);
                _textBounceStartTime = Time.time;

                _textGlowBurstActive = (_nameTf != null);
                _textGlowBurstStartTime = Time.time;

                Vector3 start;
                Vector3 end;
                ComputeCurrentStartAndEnd(out start, out end);

                _startPos = start;
                _end = end;
                _laneCenterXY = new Vector3(start.x, start.y, 0f);
                _travelDist = Mathf.Max(0.001f, Vector3.Distance(start, _end));

                if (immediatePositionReset || !_previewFreezeAtSpawn)
                    transform.position = start;

                transform.rotation = Quaternion.LookRotation(-GameplayForward, GameplayUp);
                _baseRot = transform.rotation;

                transform.localScale = _baseScale * PopStartScaleMul;

                if (_haloTf != null)
                    _haloTf.localScale = _haloBaseScale * HaloBurstStartMul;

                if (_nameTf != null)
                {
                    _nameTf.localScale = _textBaseScale * TextBounceStartMul;
                }

                if (_trail != null)
                {
                    _trail.Clear();
                    _trail.time = TrailBaseTime;
                    _trail.widthMultiplier = _trailBaseWidth;
                    _trail.startColor = _trailStartColorBase;
                    _trail.endColor = _trailEndColorBase;
                    _trail.emitting = !_previewFreezeAtSpawn;
                }

                ApplyNameColorAndHalo(_baseColor);
            }

            private void EnterPreviewRespawnWait()
            {
                _previewWaitingForRespawn = true;
                _previewRespawnTime = Time.time + PreviewRespawnDelay;

                _spawnFxActive = false;
                _impactFxActive = false;
                _textBounceActive = false;
                _textGlowBurstActive = false;

                if (_trail != null)
                {
                    _trail.emitting = false;
                    _trail.Clear();
                }

                transform.localScale = Vector3.zero;

                if (_haloTf != null)
                    _haloTf.localScale = Vector3.zero;

                if (_nameTf != null)
                    _nameTf.localScale = Vector3.zero;

                ApplyNamePropertyBlock(1f);
            }

            private void UpdateCommonVisualsFacingAndSpin(float age)
            {
                if (_cam != null)
                {
                    float age01 = Mathf.Clamp01((Time.time - _spawnTime) / Mathf.Max(0.01f, SpawnFXDuration));
                    float tilt = Mathf.Lerp(OrbTiltDegrees, 0f, age01);

                    Quaternion targetRot = _baseRot * Quaternion.Euler(tilt, 0f, 0f);
                    float rLerp = 1f - Mathf.Exp(-10f * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rLerp);

                    if (_nameTf != null)
                    {
                        Vector3 toCam = (_cam.position - _nameTf.position);
                        if (toCam.sqrMagnitude > 0.0001f)
                        {
                            Quaternion face = Quaternion.LookRotation(-toCam.normalized, GameplayUp);
                            float t = 1f - Mathf.Exp(-NameFaceCamDamp * Time.deltaTime);
                            _nameTf.rotation = Quaternion.Slerp(_nameTf.rotation, face, t);
                        }
                    }
                }

                if (_coreTf != null)
                    _coreTf.Rotate(0f, OrbSpinSpeed * Time.deltaTime, 0f, Space.Self);
            }

            private void UpdateHaloPulse(float age)
            {
                if (_haloTf == null || _spawnFxActive || _impactFxActive)
                    return;

                float t = (age + _haloPhase) * HaloPulseSpeed;
                float s01 = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f;
                s01 = Smooth01(s01);

                float targetMul = Mathf.Lerp(HaloPulseScaleMin, HaloPulseScaleMax, s01);

                float lerp = 1f - Mathf.Exp(-HaloPulseDamp * Time.deltaTime);
                _haloTf.localScale = Vector3.Lerp(_haloTf.localScale, _haloBaseScale * targetMul, lerp);
            }

            private void UpdateSpawnFx(float age)
            {
                if (!_spawnFxActive)
                    return;

                float p = Mathf.Clamp01(age / SpawnFXDuration);

                float popMul;
                if (p < 0.6f)
                {
                    float p1 = p / 0.6f;
                    popMul = Mathf.Lerp(PopStartScaleMul, PopOvershootScaleMul, Smooth01(p1));
                }
                else
                {
                    float p2 = (p - 0.6f) / 0.4f;
                    popMul = Mathf.Lerp(PopOvershootScaleMul, PopEndScaleMul, Smooth01(p2));
                }
                transform.localScale = _baseScale * popMul;

                if (_haloTf != null)
                {
                    float haloMul;
                    if (p < 0.5f)
                    {
                        float h1 = p / 0.5f;
                        haloMul = Mathf.Lerp(HaloBurstStartMul, HaloBurstPeakMul, Smooth01(h1));
                    }
                    else
                    {
                        float h2 = (p - 0.5f) / 0.5f;
                        haloMul = Mathf.Lerp(HaloBurstPeakMul, HaloBurstEndMul, Smooth01(h2));
                    }
                    _haloTf.localScale = _haloBaseScale * haloMul;
                }

                if (p >= 1f)
                {
                    _spawnFxActive = false;
                    transform.localScale = _baseScale;
                    if (_haloTf != null) _haloTf.localScale = _haloBaseScale;
                }
            }

            private void UpdateTextBounce()
            {
                if (!_textBounceActive || _nameTf == null)
                    return;

                float t = (Time.time - _textBounceStartTime) / TextBounceDuration;
                float p = Mathf.Clamp01(t);

                float scaleMul;
                if (p < 0.6f)
                {
                    float p1 = p / 0.6f;
                    scaleMul = Mathf.Lerp(TextBounceStartMul, TextBounceOvershootMul, Smooth01(p1));
                }
                else
                {
                    float p2 = (p - 0.6f) / 0.4f;
                    scaleMul = Mathf.Lerp(TextBounceOvershootMul, TextBounceEndMul, Smooth01(p2));
                }

                _nameTf.localScale = _textBaseScale * scaleMul;

                if (p >= 1f)
                {
                    _textBounceActive = false;
                    _nameTf.localScale = _textBaseScale;
                }
            }

            private void UpdateTextGlowBurst()
            {
                if (!_textGlowBurstActive)
                    return;

                float t = (Time.time - _textGlowBurstStartTime) / TextGlowBurstDuration;
                float p = Mathf.Clamp01(t);

                float glowMul;
                if (p < 0.5f)
                {
                    float p1 = p / 0.5f;
                    glowMul = Mathf.Lerp(1.0f, TextGlowBurstPeakMul, Smooth01(p1));
                }
                else
                {
                    float p2 = (p - 0.5f) / 0.5f;
                    glowMul = Mathf.Lerp(TextGlowBurstPeakMul, 1.0f, Smooth01(p2));
                }

                ApplyNamePropertyBlock(glowMul);

                if (p >= 1f)
                {
                    _textGlowBurstActive = false;
                    ApplyNamePropertyBlock(1f);
                }
            }

            private bool UpdateImpactFxAndReturnShouldDestroy()
            {
                if (!_impactFxActive)
                    return false;

                float t = (Time.time - _impactStartTime) / ImpactFXDuration;
                float p = Mathf.Clamp01(t);

                float up = (p < 0.5f) ? (p / 0.5f) : (1f - (p - 0.5f) / 0.5f);
                up = Smooth01(up);

                if (_haloTf != null)
                {
                    float haloMul = Mathf.Lerp(1f, ImpactHaloPeakMul, up);
                    _haloTf.localScale = _haloBaseScale * haloMul;
                }

                UpdateImpactTrailPulse(up);
                ApplyNamePropertyBlock(Mathf.Lerp(1f, ImpactGlowPeakMul, up));

                if (p >= 1f)
                {
                    if (_haloTf != null) _haloTf.localScale = _haloBaseScale;
                    ApplyNamePropertyBlock(1f);

                    if (_isPreviewMode)
                    {
                        EnterPreviewRespawnWait();
                        return false;
                    }

                    Destroy(gameObject);
                    return true;
                }

                return false;
            }

            private void UpdatePreview()
            {
                ApplyPreviewLiveSettings();

                if (_previewWaitingForRespawn)
                {
                    if (Time.time >= _previewRespawnTime)
                        StartPreviewCycle(true);

                    return;
                }

                float age = Time.time - _spawnTime;

                if (!_previewFreezeAtSpawn && !_impactFxActive)
                {
                    float moveSpeed = _speed;

                    if (_spawnFxActive)
                    {
                        float pKick = Mathf.Clamp01(age / SpawnFXDuration);
                        float burst = Mathf.Lerp(SpawnForwardKick, 1f, pKick);
                        moveSpeed *= burst;
                    }

                    float accel01 = Mathf.Clamp01(age / 0.18f);
                    moveSpeed *= Mathf.Lerp(1.25f, 1f, accel01);

                    Vector3 pos = transform.position;

                    float dist = Vector3.Distance(pos, _end);
                    float dist01 = Mathf.Clamp01(dist / 4f);
                    float speedMul = Mathf.Lerp(0.85f, 1f, dist01);

                    pos = Vector3.MoveTowards(pos, _end, moveSpeed * speedMul * Time.deltaTime);

                    float laneSnap = 1f - Mathf.Exp(-14f * Time.deltaTime);
                    pos.x = Mathf.Lerp(pos.x, _laneCenterXY.x, laneSnap);
                    pos.y = Mathf.Lerp(pos.y, _laneCenterXY.y, laneSnap);

                    float bobEase = Mathf.Clamp01((age - 0.18f) / 0.35f);
                    float bobT = (age + _haloPhase) * 2.2f;
                    float bob = Mathf.Sin(bobT * Mathf.PI * 2f) * 0.02f * bobEase;

                    float bobLerp = 1f - Mathf.Exp(-10f * Time.deltaTime);
                    pos.y = Mathf.Lerp(pos.y, _laneCenterXY.y + bob, bobLerp);

                    transform.position = pos;
                }

                float distToEnd = Vector3.Distance(transform.position, _end);
                UpdateTrailPreImpact(distToEnd);

                float progress01 = 1f - Mathf.Clamp01(distToEnd / _travelDist);
                progress01 = Smooth01(progress01);

                float orbTravelMul = Mathf.Lerp(OrbTravelScaleStart, OrbTravelScaleEnd, progress01);
                float nameTravelMul = Mathf.Lerp(NameTravelScaleStart, NameTravelScaleEnd, progress01);

                if (!_spawnFxActive && !_impactFxActive)
                    transform.localScale = _baseScale * orbTravelMul;

                if (_nameTf != null && !_textBounceActive)
                    _nameTf.localScale = _textBaseScale * nameTravelMul;

                UpdateCommonVisualsFacingAndSpin(age);
                UpdateHaloPulse(age);
                UpdateSpawnFx(age);
                UpdateTextBounce();
                UpdateTextGlowBurst();

                if (!_previewFreezeAtSpawn && !_impactFxActive && distToEnd <= 0.02f)
                {
                    SpawnImpactParticles(_impactParticleColor);
                    StartImpactFX();
                }

                UpdateImpactFxAndReturnShouldDestroy();
            }

            private void UpdateGameplay()
            {
                float age = Time.time - _spawnTime;

                if (age >= _maxLife)
                {
                    Destroy(gameObject);
                    return;
                }

                float moveSpeed = _speed;

                if (_spawnFxActive)
                {
                    float pKick = Mathf.Clamp01(age / SpawnFXDuration);
                    float burst = Mathf.Lerp(SpawnForwardKick, 1f, pKick);
                    moveSpeed *= burst;
                }

                float accel01 = Mathf.Clamp01(age / 0.18f);
                moveSpeed *= Mathf.Lerp(1.25f, 1f, accel01);

                Vector3 pos = transform.position;

                float dist = Vector3.Distance(pos, _end);
                float dist01 = Mathf.Clamp01(dist / 4f);
                float speedMul = Mathf.Lerp(0.85f, 1f, dist01);

                pos = Vector3.MoveTowards(pos, _end, moveSpeed * speedMul * Time.deltaTime);

                float laneSnap = 1f - Mathf.Exp(-14f * Time.deltaTime);
                pos.x = Mathf.Lerp(pos.x, _laneCenterXY.x, laneSnap);
                pos.y = Mathf.Lerp(pos.y, _laneCenterXY.y, laneSnap);

                float bobEase = Mathf.Clamp01((age - 0.18f) / 0.35f);
                float bobT = (age + _haloPhase) * 2.2f;
                float bob = Mathf.Sin(bobT * Mathf.PI * 2f) * 0.02f * bobEase;

                float bobLerp = 1f - Mathf.Exp(-10f * Time.deltaTime);
                pos.y = Mathf.Lerp(pos.y, _laneCenterXY.y + bob, bobLerp);

                transform.position = pos;

                float distToEnd = Vector3.Distance(transform.position, _end);
                UpdateTrailPreImpact(distToEnd);

                float progress01 = 1f - Mathf.Clamp01(distToEnd / _travelDist);
                progress01 = Smooth01(progress01);

                float orbTravelMul = Mathf.Lerp(OrbTravelScaleStart, OrbTravelScaleEnd, progress01);
                float nameTravelMul = Mathf.Lerp(NameTravelScaleStart, NameTravelScaleEnd, progress01);

                if (!_spawnFxActive && !_impactFxActive)
                    transform.localScale = _baseScale * orbTravelMul;

                if (_nameTf != null && !_textBounceActive)
                    _nameTf.localScale = _textBaseScale * nameTravelMul;

                UpdateCommonVisualsFacingAndSpin(age);
                UpdateHaloPulse(age);
                UpdateSpawnFx(age);
                UpdateTextBounce();
                UpdateTextGlowBurst();

                if (!_impactFxActive && distToEnd <= 0.02f)
                {
                    SpawnImpactParticles(_impactParticleColor);
                    StartImpactFX();
                }

                if (UpdateImpactFxAndReturnShouldDestroy())
                    return;
            }

            private void Update()
            {
                if (_cam == null)
                {
                    Destroy(gameObject);
                    return;
                }

                if (_isPreviewMode)
                {
                    UpdatePreview();
                    return;
                }

                UpdateGameplay();
            }

            private void OnDestroy()
            {
                if (_isPreviewMode)
                    return;

                Plugin._orbsAlive = Math.Max(0, Plugin._orbsAlive - 1);

                int lane = Mathf.Clamp(_laneIndex, 0, LaneCount - 1);
                Plugin._laneAlive[lane] = Math.Max(0, Plugin._laneAlive[lane] - 1);
            }

            private static float Smooth01(float t)
            {
                t = Mathf.Clamp01(t);
                return t * t * (3f - 2f * t);
            }

            private static object TryGetTextComponent(GameObject go)
            {
                var tmp = go.GetComponent("TMPro.TMP_Text");
                if (tmp != null) return tmp;

                tmp = go.GetComponent("TMPro.TextMeshPro");
                if (tmp != null) return tmp;

                tmp = go.GetComponent("TMPro.TextMeshProUGUI");
                if (tmp != null) return tmp;

                var textMesh = go.GetComponent("UnityEngine.TextMesh");
                if (textMesh != null) return textMesh;

                return null;
            }

            private static void SetText(object textComponent, string value)
            {
                if (textComponent == null) return;
                try
                {
                    var type = textComponent.GetType();
                    var prop = type.GetProperty("text");
                    if (prop != null && prop.CanWrite) { prop.SetValue(textComponent, value, null); return; }
                    var field = type.GetField("text");
                    if (field != null) { field.SetValue(textComponent, value); return; }
                }
                catch { }
            }

            private static void TrySetColor(object textComponent, Color c)
            {
                if (textComponent == null) return;
                try
                {
                    var type = textComponent.GetType();

                    var prop = type.GetProperty("color");
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(Color))
                    {
                        prop.SetValue(textComponent, c, null);
                        return;
                    }

                    var field = type.GetField("color");
                    if (field != null && field.FieldType == typeof(Color))
                    {
                        field.SetValue(textComponent, c);
                        return;
                    }
                }
                catch { }
            }

            private static void ConfigureTMPOneLine(object textComponent)
            {
                if (textComponent == null) return;

                try
                {
                    var type = textComponent.GetType();

                    var wrapProp = type.GetProperty("enableWordWrapping");
                    if (wrapProp != null && wrapProp.CanWrite)
                        wrapProp.SetValue(textComponent, false, null);

                    var autoProp = type.GetProperty("enableAutoSizing");
                    if (autoProp != null && autoProp.CanWrite)
                        autoProp.SetValue(textComponent, true, null);

                    var minProp = type.GetProperty("fontSizeMin");
                    if (minProp != null && minProp.CanWrite)
                        minProp.SetValue(textComponent, TMP_FontSizeMin, null);

                    var maxProp = type.GetProperty("fontSizeMax");
                    if (maxProp != null && maxProp.CanWrite)
                        maxProp.SetValue(textComponent, TMP_FontSizeMax, null);

                    var maxLinesProp = type.GetProperty("maxVisibleLines");
                    if (maxLinesProp != null && maxLinesProp.CanWrite)
                        maxLinesProp.SetValue(textComponent, 1, null);
                }
                catch { }
            }
        }

        public class MainThread : MonoBehaviour
        {
            private static MainThread _inst;
            private static readonly object _lock = new object();
            private static readonly Queue<Action> _queue = new Queue<Action>();

            internal static void Ensure()
            {
                if (_inst != null) return;
                GameObject go = new GameObject("GeminiOrbFX_UI_MainThread");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _inst = go.AddComponent<MainThread>();
            }

            internal static void Run(Action a)
            {
                Ensure();
                lock (_lock) { _queue.Enqueue(a); }
            }

            private void Update()
            {
                lock (_lock)
                {
                    while (_queue.Count > 0)
                    {
                        Action a = _queue.Dequeue();
                        try { a?.Invoke(); } catch { }
                    }
                }

                try { Plugin._instance?._tikTokService?.Tick(); } catch { }
                try { Plugin._instance?._orbSpawnService?.PumpSpawnQueue(); } catch { }
                try { Plugin._instance?.PumpOrbTest(); } catch { }
                try { Plugin._instance?.UpdateMenuPreviewState(); } catch { }
             }
        }
    }
}