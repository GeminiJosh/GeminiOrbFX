using System;
using UnityEngine;
using static GeminiOrbFX.UI.Plugin;

namespace GeminiOrbFX.UI.Services
{
    internal class OrbSpawnService
    {
        private readonly Plugin _plugin;

        public OrbSpawnService(Plugin plugin)
        {
            _plugin = plugin;
        }

        private const float MinLaneSpacingZ = 7.5f;
        private const float MinLaneCooldown = 0.25f;
        private const int LaneCount = 4;

        private static readonly bool ProLaneBalancing = true;
        private static readonly bool ProLaneMicroOffset = true;
        private const float LaneMicroOutwardX = 0.06f;
        private const float LaneMicroJitterX = 0.03f;

        private readonly float[] _laneNextAllowedRealtime = new float[LaneCount];
        private readonly float[] _laneLastSpawnRealtime = new float[LaneCount];
        private readonly int[] _laneSpawnCounter = new int[LaneCount];

        private float _nextAllowedSpawnRealtime = 0f;
        private int _laneCursor = 0;

        public int ChooseLaneIfAvailable(OrbRequest req, float speed, float now, out float waitUntil)
        {
            waitUntil = now;

            if (req.Lane.HasValue)
            {
                int forced = Mathf.Clamp(req.Lane.Value, 0, 3);
                float ready = _laneNextAllowedRealtime[forced];
                if (now >= ready)
                    return forced;

                waitUntil = ready;
                return -1;
            }

            int bestLane = -1;
            float bestScore = float.MaxValue;
            float earliest = float.MaxValue;

            for (int attempt = 0; attempt < LaneCount; attempt++)
            {
                int i = (_laneCursor + attempt) % LaneCount;
                float t = _laneNextAllowedRealtime[i];

                if (t > now)
                {
                    if (t < earliest)
                        earliest = t;
                    continue;
                }

                if (!ProLaneBalancing)
                {
                    bestLane = i;
                    break;
                }

                float aliveWeight = Plugin._laneAlive[i] * 1000f;
                float age = (_laneLastSpawnRealtime[i] <= 0f) ? 999999f : (now - _laneLastSpawnRealtime[i]);
                float recencyPenalty = -age;

                float score = aliveWeight + recencyPenalty;
                score += ((_laneSpawnCounter[i] & 1) == 0) ? 0.0002f : 0.0001f;

                if (bestLane < 0 || score < bestScore)
                {
                    bestLane = i;
                    bestScore = score;
                }
            }

            if (bestLane >= 0)
            {
                _laneCursor = (bestLane + 1) % LaneCount;
                return bestLane;
            }

            waitUntil = earliest;
            return -1;
        }

        public void MarkLaneUsed(int lane, float speed, float now)
        {
            float laneCooldown = Mathf.Max(MinLaneCooldown, MinLaneSpacingZ / Mathf.Max(0.01f, speed));
            _laneNextAllowedRealtime[lane] = now + laneCooldown;
            _laneLastSpawnRealtime[lane] = now;
            _laneSpawnCounter[lane] = _laneSpawnCounter[lane] + 1;
        }

        public float ComputeLaneMicroOffsetX(int lane)
        {
            if (!ProLaneMicroOffset)
                return 0f;

            float outward =
                (lane == 0) ? -LaneMicroOutwardX :
                (lane == 3) ? +LaneMicroOutwardX :
                (lane == 1) ? -(LaneMicroOutwardX * 0.55f) :
                (lane == 2) ? +(LaneMicroOutwardX * 0.55f) : 0f;

            int n = _laneSpawnCounter[lane];
            float jitter = ((n & 1) == 0) ? -LaneMicroJitterX : +LaneMicroJitterX;

            if (lane == 0 || lane == 3)
                jitter *= 0.75f;

            return outward + jitter;
        }

        public void ResetState()
        {
            _nextAllowedSpawnRealtime = 0f;

            for (int i = 0; i < LaneCount; i++)
            {
                _laneNextAllowedRealtime[i] = 0f;
                _laneLastSpawnRealtime[i] = 0f;
                _laneSpawnCounter[i] = 0;
                Plugin._laneAlive[i] = 0;
            }

            _laneCursor = 0;
        }

        public void PumpSpawnQueue()
        {
            if (!_plugin.IsInGameplay())
                return;

            float now = Time.realtimeSinceStartup;

            if (now < _nextAllowedSpawnRealtime)
                return;

            OrbRequest req;
            lock (Services.OrbQueueService.QueueLock)
            {
                if (Services.OrbQueueService.Queue.Count == 0)
                    return;

                req = Services.OrbQueueService.Queue.Peek();
            }

            float speed = req.Speed.HasValue
                ? Mathf.Clamp(req.Speed.Value, MinSpeed, MaxSpeed)
                : Mathf.Clamp(PluginConfig.Instance.OrbSpeed, MinSpeed, MaxSpeed);

            float waitUntil;
            int lane = ChooseLaneIfAvailable(req, speed, now, out waitUntil);

            if (lane < 0)
            {
                _nextAllowedSpawnRealtime = Mathf.Max(_nextAllowedSpawnRealtime, waitUntil);
                return;
            }

            lock (Services.OrbQueueService.QueueLock)
            {
                if (Services.OrbQueueService.Queue.Count > 0)
                    Services.OrbQueueService.Queue.Dequeue();
            }

            RaiseQueueChanged();

            float xMicro = ComputeLaneMicroOffsetX(lane);
            SpawnOrbLane(req, lane, speed, xMicro);

            MarkLaneUsed(lane, speed, now);

            float spawnRate = Mathf.Max(0.01f, PluginConfig.Instance.SpawnRate);
            float interval = 1f / spawnRate;
            _nextAllowedSpawnRealtime = now + interval;
        }

        private void SpawnOrbLane(OrbRequest req, int lane, float speed, float laneXMicroOffset)
        {
            try
            {
                if (!_plugin.IsInGameplay())
                    return;

                if (_orbsAlive >= PluginConfig.Instance.MaxLiveOrbs)
                    return;

                GameObject template = _plugin.GetOrbTemplate();
                if (template == null)
                {
                    Plugin.Log.Warn("[GeminiOrbFX UI] Orb template not found.");
                    return;
                }

                Transform cam = _plugin.GetBestCameraTransform(out bool stereo);
                if (cam == null)
                {
                    Plugin.Log.Warn("[GeminiOrbFX UI] No suitable camera found.");
                    return;
                }

                lane = Mathf.Clamp(lane, 0, 3);
                speed = Mathf.Clamp(speed, MinSpeed, MaxSpeed);

                float distance = (PluginConfig.Instance.SpawnDistance - DespawnZ);
                float travelTime = distance / Mathf.Max(0.01f, speed);
                float maxLife = Mathf.Max(DefaultMaxLife, travelTime + 1.0f);

                Color baseC = Color.white;
                bool hasForcedHSV = (req.H.HasValue || req.S.HasValue || req.V.HasValue);

                if (!RandomizeNameColorEachSpawn && req.Color == null && !hasForcedHSV)
                {
                    baseC = Color.white;
                }
                else if (req.Color.HasValue)
                {
                    baseC = req.Color.Value;
                }
                else if (hasForcedHSV)
                {
                    float h = req.H ?? UnityEngine.Random.value;
                    float s = req.S ?? NameHSV_S;
                    float v = req.V ?? NameHSV_V;
                    baseC = Color.HSVToRGB(Plugin.Clamp01(h), Plugin.Clamp01(s), Plugin.Clamp01(v));
                }
                else
                {
                    float hue = Plugin.PickHueNoRepeat();
                    baseC = Color.HSVToRGB(hue, NameHSV_S, NameHSV_V);
                }

                float orbBrightness = PluginConfig.Instance.OrbBrightness;

                baseC = new Color(
                    Mathf.Clamp01(baseC.r * orbBrightness),
                    Mathf.Clamp01(baseC.g * orbBrightness),
                    Mathf.Clamp01(baseC.b * orbBrightness),
                    1f
                );

                GameObject go = UnityEngine.Object.Instantiate(template);
                go.name = "GeminiOrbFX_UI_Runtime";
                go.SetActive(true);

                Plugin.SetLayerRecursively(go, 0);
                Plugin.ForceAllRenderersOn(go);

                var ctrl = go.AddComponent<OrbLaneController>();
                ctrl.Init(cam, req.Name, lane, speed, maxLife, baseC, laneXMicroOffset);

                Plugin._orbsAlive++;
                Plugin._laneAlive[lane] = Plugin._laneAlive[lane] + 1;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn("[GeminiOrbFX UI] SpawnOrbLane error: " + ex.Message);
            }
        }
    }
}