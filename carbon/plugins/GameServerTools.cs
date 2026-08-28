using ConVar;
using Facepunch.Extend;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Game Server Tools", "Disney + Brad", "2.3.3")]
    [Description("GST integration for gameservertools.com: bans, linking, reports, stats, VPN, WatchDog telemetry (hardcoded GST API & WatchDog URLs), optional stash traps and server policy hooks.")]
    public class GameServerTools : CovalencePlugin
    {
        #region Fields & Constants

        private readonly Dictionary<string, ApprovedCachedPlayer> _approvedCachedJoins = new Dictionary<string, ApprovedCachedPlayer>();
        private readonly Dictionary<string, CachedPlayer> _cachedJoins = new Dictionary<string, CachedPlayer>();
        [PluginReference] private Plugin Clans;

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private string _apiBaseUrl = "https://api.gameservertools.com";

        private readonly float timeout = 30f;

        private string ApiUrl(string path)
        {
            string baseUrl = _apiBaseUrl.TrimEnd('/');
            string p = path.TrimStart('/');
            return $"{baseUrl}/{p}";
        }

        private string _linkedGroupName = "DiscordLinked";
        private string _nitroGroupName = "Nitro";
        private int _port = 28015;
        private bool _showClaimMessage;
        private bool _loggingEnabled = false;
        private bool _disableBanCtrl = false;
        private const string WatchDogBaseUrl = "https://watchdog.gameservertools.com";
        private string _watchDogApiUrl = WatchDogBaseUrl;
        private bool _watchDogEnabled = true;
        private bool _vpnCheckEnabled = true;
        private bool _banCheckEnabled = true;
        private bool _gstServerUnauthorized;
        private float _lastGstAuthWarnTime;
        private readonly Dictionary<ulong, float> _watchDogLastAimSend = new Dictionary<ulong, float>();
        private const float WatchDogAimThrottleSeconds = 0.2f;
        private readonly List<string> _watchDogEventBuffer = new List<string>();
        private Timer _watchDogFlushTimer;
        private Timer _watchDogEnforcementTimer;
        private Timer _highPingCheckTimer;
        private Timer _stashTrapEnsureTimer;
        private Timer _configWarningTimer;
        private Timer _onlinePlayersReportTimer;
        private bool _onlinePlayersReportQueued;
        private float _onlinePlayersReportDebounceSeconds = 10f;
        private float _watchDogBatchIntervalSeconds = 5f;
        private int _watchDogBatchMaxSize = 200;
        private int _watchDogBufferCap = 2000;
        private bool _watchDogFlushInProgress = false;
        private int _watchDogFlushGeneration = 0;
        private Timer _watchDogFlushWatchdogTimer;
        private Timer _watchDogRequestWatchdogTimer;
        private int _watchDogChunkMaxRetries = 12;
        private float _watchDogRetryBaseDelaySeconds = 2f;
        private const float WatchDogEnforcementIntervalSeconds = 15f;
        // Backup only while a request is outstanding; soft timeout should fire first.
        private const float WatchDogFlushStallSeconds = 45f;
        private const float WatchDogRequestGraceSeconds = 5f;
        private bool _enableHighPingKick = false;
        private int _highPingMax = 350;
        private float _highPingCheckIntervalSeconds = 60f;

        private readonly Dictionary<ulong, List<string>> _chatHistory = new Dictionary<ulong, List<string>>();
        private readonly Dictionary<string, Timer> _forceLinkTimers = new Dictionary<string, Timer>();

        private const string PermLinked = "gameservertools.linked";
        private const string PermBypass = "gameservertools.bypass";
        private const string PermHighPingBypass = "gameservertools.whitelist.highping";
        private const string PermStashTrapAlerts = "gameservertools.stashtrapalerts";
        private const string PermRadar = "gameservertools.radar";

        private bool _permissionsRegistered;
        private readonly Dictionary<ulong, AutoStashTrapMeta> _autoStashTrapIds = new Dictionary<ulong, AutoStashTrapMeta>();
        private readonly Dictionary<ulong, Queue<float>> _autoStashViolationWindows = new Dictionary<ulong, Queue<float>>();
        private readonly Dictionary<ulong, List<AutoStashTriggerRecord>> _autoStashRecentTriggers = new Dictionary<ulong, List<AutoStashTriggerRecord>>();
        private readonly Dictionary<ulong, float> _chatCooldownUntil = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _commandCooldownUntil = new Dictionary<ulong, float>();
        private bool _enableAutoStashTraps = false;
        private int _autoStashMaxTraps = 200;
        private float _autoStashEnsureIntervalSeconds = 120f;
        private int _autoStashViolationThreshold = 3;
        private float _autoStashViolationWindowMinutes = 30f;
        private int _autoStashBanDelaySeconds = 10;
        private string _autoStashBanReason = "Cheat Detected (ESP stash trap)";
        private int _autoStashDestroyRevealedAfterMinutes = 5;
        private bool _autoStashReplaceRevealedTrap = true;
        private bool _autoStashSpawnDecoyBags = true;
        private int _autoStashDecoyBagSpawnChance = 55;
        private float _autoStashPlacementBuildingRadius = 20f;
        private float _autoStashPlacementMonumentRadius = 35f;
        private float _autoStashMinWaterClearance = 0.75f;
        private bool _autoStashIgnoreTeamWindowEnabled = true;
        private float _autoStashIgnoreTeamWindowSeconds = 20f;
        private bool _autoStashIgnoreClanWindowEnabled = true;
        private float _autoStashIgnoreClanWindowSeconds = 25f;
        private bool _autoStashLocalAutoBan = true;
        private ulong _autoStashTrapOwnerSteamId = 0;
        private bool _autoStashTrapDecoyLoot = true;
        private bool _antiFloodChatEnabled = false;
        private float _antiFloodChatCooldownSeconds = 1.5f;
        private bool _antiFloodCommandEnabled = false;
        private float _antiFloodCommandCooldownSeconds = 1.0f;

        private readonly HashSet<ulong> _gstRadarUsers = new HashSet<ulong>();
        private readonly List<ulong> _gstRadarTickSnapshot = new List<ulong>();
        private readonly List<ulong> _gstRadarRemoveBuffer = new List<ulong>();
        private readonly List<BasePlayer> _gstRadarNearbyPlayers = new List<BasePlayer>();
        private readonly List<GstRadarProjectileLine> _gstRadarProjectileLines = new List<GstRadarProjectileLine>();
        private readonly Dictionary<ulong, float> _gstRadarToggleLastTime = new Dictionary<ulong, float>();
        private Timer _gstRadarTimer;
        private float _gstRadarMaxDistance = 200f;
        private float _gstRadarMaxDistanceSq;
        private float _gstRadarInterval = 0.5f;
        private float _gstRadarLookLineLength = 15f;
        private float _gstRadarProjectileLineLength = 100f;
        private float _gstRadarProjectileSeconds = 1.25f;
        private int _gstRadarProjectileCap = 400;
        private bool _gstRadarEnabled = false;
        private bool _gstRadarShowWorldEntities = true;
        private int _gstRadarMaxEntityDraws = 140;
        private int _gstRadarMaxDropDraws = 24;
        private int _gstRadarMaxPlayerDraws = 60;
        private int _gstRadarMaxSleeperDraws = 30;
        private int _gstRadarMaxUsers = 8;
        private float _gstRadarToggleCooldownSeconds = 2f;
        private float _gstRadarAutoOffSeconds = 120f;
        private int _gstRadarDrawBudgetPerTick = 800;
        private readonly Dictionary<ulong, float> _gstRadarSessionStart = new Dictionary<ulong, float>();
        private bool _gstRadarHighlightGstTraps = true;
        private readonly Dictionary<ulong, GstRadarFilters> _gstRadarFilters = new Dictionary<ulong, GstRadarFilters>();
        private readonly HashSet<ulong> _gstRadarWorldEntitySeen = new HashSet<ulong>();
        private int _gstRadarPlayerMask;

        #endregion

        #region Inner Types

        private struct GstRadarFilters
        {
            public bool World;
            public bool Players;
            public bool Sleepers;
            public bool Shots;
            public bool Stashes;
            public bool ToolCupboard;
            public bool Bags;
            public bool Defense;
            public bool FieldTraps;
            public bool Loot;
            public bool Npc;
            public bool Vehicles;
            public bool Military;
            public bool Drops;
            public bool Resource;
            public bool WorldEvents;
            public bool Cctv;
            public bool Mlrs;

            public static GstRadarFilters AllOn => new GstRadarFilters
            {
                World = true,
                Players = true,
                Sleepers = true,
                Shots = true,
                Stashes = true,
                ToolCupboard = true,
                Bags = true,
                Defense = true,
                FieldTraps = true,
                Loot = true,
                Npc = true,
                Vehicles = true,
                Military = true,
                Drops = true,
                Resource = true,
                WorldEvents = true,
                Cctv = true,
                Mlrs = true
            };

            public static GstRadarFilters DefaultOn => new GstRadarFilters
            {
                World = false,
                Players = true,
                Sleepers = true,
                Shots = false,
                Stashes = false,
                ToolCupboard = false,
                Bags = false,
                Defense = false,
                FieldTraps = false,
                Loot = false,
                Npc = false,
                Vehicles = false,
                Military = false,
                Drops = false,
                Resource = false,
                WorldEvents = false,
                Cctv = false,
                Mlrs = false
            };
        }

        private struct GstRadarProjectileLine
        {
            public Vector3 A;
            public Vector3 B;
            public float Expire;
        }
        private static readonly JsonSerializerSettings GstApiJsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { new GstSafeFloatingPointJsonConverter() }
        };

        private sealed class GstSafeFloatingPointJsonConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(float) || objectType == typeof(double)
                    || objectType == typeof(float?) || objectType == typeof(double?)
                    || objectType == typeof(float[]) || objectType == typeof(double[]);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }
                if (value is double[] da)
                {
                    writer.WriteStartArray();
                    foreach (var v in da)
                        writer.WriteValue(double.IsNaN(v) || double.IsInfinity(v) ? 0d : v);
                    writer.WriteEndArray();
                    return;
                }
                if (value is float[] fa)
                {
                    writer.WriteStartArray();
                    foreach (var v in fa)
                        writer.WriteValue(float.IsNaN(v) || float.IsInfinity(v) ? 0f : v);
                    writer.WriteEndArray();
                    return;
                }
                switch (value)
                {
                    case float f:
                        writer.WriteValue(float.IsNaN(f) || float.IsInfinity(f) ? 0f : f);
                        break;
                    case double d:
                        writer.WriteValue(double.IsNaN(d) || double.IsInfinity(d) ? 0d : d);
                        break;
                    default:
                        double x = Convert.ToDouble(value);
                        writer.WriteValue(double.IsNaN(x) || double.IsInfinity(x) ? 0d : x);
                        break;
                }
            }
        }

        #endregion

        #region Plugin Lifecycle

        private void Init()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }

            LoadConfigData();
            _gstRadarPlayerMask = LayerMask.GetMask("Player (Server)");
            permission.CreateGroup(_linkedGroupName, "linked", 0);
            permission.CreateGroup(_nitroGroupName, "nitro", 0);
            if (!_permissionsRegistered)
            {
                permission.RegisterPermission(PermLinked, this);
                permission.RegisterPermission(PermBypass, this);
                permission.RegisterPermission(PermHighPingBypass, this);
                permission.RegisterPermission(PermStashTrapAlerts, this);
                permission.RegisterPermission(PermRadar, this);
                _permissionsRegistered = true;
            }
            permission.GrantGroupPermission(_linkedGroupName, PermLinked, this);

            if (_disableBanCtrl)
            {
                Unsubscribe("OnPlayerBanned");
                Unsubscribe("OnUserApprove");
            }
            else
            {
                AddUniversalCommand("ban", "OverrideBanCommand");
            }

            if (string.IsNullOrWhiteSpace(_watchDogApiUrl))
            {
                Unsubscribe("OnPlayerAttack");
                Unsubscribe("OnEntityTakeDamage");
                Unsubscribe("OnEntityDeath");
                Unsubscribe("OnPlayerViolation");
                Unsubscribe("OnPlayerInput");
                _watchDogFlushTimer?.Destroy();
                _watchDogEnforcementTimer?.Destroy();
            }
            else
            {
                _watchDogFlushTimer = timer.Every(_watchDogBatchIntervalSeconds, () => FlushWatchDogBatch());
                _watchDogEnforcementTimer = timer.Every(WatchDogEnforcementIntervalSeconds, () => PollWatchDogEnforcement(0));
            }

            _highPingCheckTimer?.Destroy();
            if (_enableHighPingKick && _highPingMax > 0)
            {
                _highPingCheckTimer = timer.Every(Mathf.Max(10f, _highPingCheckIntervalSeconds), () => CheckHighPingPlayers());
            }

            _stashTrapEnsureTimer?.Destroy();
            if (_enableAutoStashTraps && _autoStashMaxTraps > 0)
            {
                EnsureAutoStashTraps();
                _stashTrapEnsureTimer = timer.Every(Mathf.Max(30f, _autoStashEnsureIntervalSeconds), () => EnsureAutoStashTraps());
            }

            _gstRadarTimer?.Destroy();
            if (_gstRadarEnabled)
                _gstRadarTimer = timer.Every(Mathf.Clamp(_gstRadarInterval, 0.12f, 2f), GstRadarTick);

            if (_loggingEnabled)
                Puts($"GST Debug: Port={_port} ApiKey={(_headers.ContainsKey("ApiKey") ? "[set]" : "[NOT SET - add APIKEY to config]")}");

            // Build a combined warning for any misconfigured values
            var warnings = new System.Text.StringBuilder();
            if (string.IsNullOrWhiteSpace(_headers.ContainsKey("ApiKey") ? _headers["ApiKey"]?.ToString() : null))
                warnings.AppendLine("[GST] WARNING: APIKEY is not set in your config. Open oxide/config/GameServerTools.json and set General.APIKEY to your key from gameservertools.com/dashboard. All API features are disabled until this is set.");

            _configWarningTimer?.Destroy();
            _configWarningTimer = null;
            if (warnings.Length > 0)
            {
                string msg = warnings.ToString().TrimEnd();
                Puts(msg);
                _configWarningTimer = timer.Every(300f, () =>
                {
                    if (!IsLoaded) return;
                    Puts(msg);
                    PurgeStaleApprovedCacheEntries();
                });
            }
            else
            {
                _configWarningTimer = timer.Every(300f, () =>
                {
                    if (!IsLoaded) return;
                    PurgeStaleApprovedCacheEntries();
                });
            }
        }

        private void PurgeStaleApprovedCacheEntries()
        {
            if (_approvedCachedJoins.Count == 0) return;
            var staleKeys = new List<string>();
            DateTime cutoff = DateTime.Now.AddMinutes(-5);
            foreach (var kv in _approvedCachedJoins)
            {
                if (kv.Value.timeOfAdd < cutoff)
                    staleKeys.Add(kv.Key);
            }
            for (int i = 0; i < staleKeys.Count; i++)
                _approvedCachedJoins.Remove(staleKeys[i]);
        }

        private void Unload()
        {
            _gstRadarTimer?.Destroy();
            _gstRadarTimer = null;
            _watchDogFlushTimer?.Destroy();
            _watchDogFlushTimer = null;
            _watchDogFlushGeneration++;
            _watchDogFlushInProgress = false;
            _watchDogFlushWatchdogTimer?.Destroy();
            _watchDogFlushWatchdogTimer = null;
            _watchDogRequestWatchdogTimer?.Destroy();
            _watchDogRequestWatchdogTimer = null;
            _watchDogEnforcementTimer?.Destroy();
            _watchDogEnforcementTimer = null;
            _highPingCheckTimer?.Destroy();
            _highPingCheckTimer = null;
            _stashTrapEnsureTimer?.Destroy();
            _stashTrapEnsureTimer = null;
            _configWarningTimer?.Destroy();
            _configWarningTimer = null;
            _onlinePlayersReportTimer?.Destroy();
            _onlinePlayersReportTimer = null;
            _onlinePlayersReportQueued = false;

            foreach (var kvp in _forceLinkTimers)
                kvp.Value?.Destroy();
            _forceLinkTimers.Clear();

            _gstRadarUsers.Clear();
            _gstRadarFilters.Clear();
            _gstRadarProjectileLines.Clear();
            _gstRadarTickSnapshot.Clear();
            _gstRadarRemoveBuffer.Clear();
            _gstRadarNearbyPlayers.Clear();
            _gstRadarToggleLastTime.Clear();
            _gstRadarSessionStart.Clear();
            _gstRadarWorldEntitySeen.Clear();
            _watchDogLastAimSend.Clear();
            _chatCooldownUntil.Clear();
            _commandCooldownUntil.Clear();
            _autoStashViolationWindows.Clear();
            _autoStashRecentTriggers.Clear();
        }

        #region Config Helpers

        private bool CfgBool(string sec, string key, bool def)
            => bool.TryParse(Config[sec, key]?.ToString(), out bool v) ? v : def;

        private int CfgInt(string sec, string key, int def)
            => int.TryParse(Config[sec, key]?.ToString(), out int v) ? v : def;

        private int CfgIntClamped(string sec, string key, int def, int min, int max)
            => Mathf.Clamp(CfgInt(sec, key, def), min, max);

        private float CfgFloat(string sec, string key, float def)
            => double.TryParse(Config[sec, key]?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? (float)v : def;

        private float CfgFloatClamped(string sec, string key, float def, float min, float max)
            => Mathf.Clamp(CfgFloat(sec, key, def), min, max);

        private string CfgString(string sec, string key, string def)
            => Config[sec, key]?.ToString() ?? def;

        private bool CfgBoolDefaultTrue(string sec, string key) => CfgBool(sec, key, true);

        // Migrates old flat config keys to the current nested-section layout.
        // Safe to call multiple times — only acts when flat keys are still present.
        private void MigrateFlatConfig()
        {
            var map = new (string Old, string Sec, string Key)[]
            {
                ("APIKEY",                                    "General",    "APIKEY"),
                ("Port",                                      null,         null),  // deprecated: auto-detected from ConVar.Server.port
                ("DebugLoggingEnabled",                       "General",    "DebugLoggingEnabled"),
                ("DisableBanCtrl",                            "General",    "DisableBanCtrl"),
                ("DisplayMessageOnClaimRewards",              "General",    "DisplayMessageOnClaimRewards"),
                ("OxideGroupNameForLinked",                   "General",    "OxideGroupNameForLinked"),
                ("OxideGroupNameForNitro",                    "General",    "OxideGroupNameForNitro"),

                ("AntiFloodChatEnabled",                      "AntiFlood",  "ChatEnabled"),
                ("AntiFloodChatCooldownSeconds",              "AntiFlood",  "ChatCooldownSeconds"),
                ("AntiFloodCommandEnabled",                   "AntiFlood",  "CommandEnabled"),
                ("AntiFloodCommandCooldownSeconds",           "AntiFlood",  "CommandCooldownSeconds"),

                ("GstRadarMaxDistance",                       "Radar",      "MaxDistance"),
                ("GstRadarIntervalSeconds",                   "Radar",      "IntervalSeconds"),
                ("GstRadarLookLineLength",                    "Radar",      "LookLineLength"),
                ("GstRadarProjectileLineLength",              "Radar",      "ProjectileLineLength"),
                ("GstRadarProjectileSeconds",                 "Radar",      "ProjectileSeconds"),
                ("GstRadarProjectileCap",                     "Radar",      "ProjectileCap"),
                ("GstRadarShowWorldEntities",                 "Radar",      "ShowWorldEntities"),
                ("GstRadarMaxEntityDraws",                    "Radar",      "MaxEntityDraws"),
                ("GstRadarMaxDropDraws",                      "Radar",      "MaxDropDraws"),
                ("GstRadarMaxPlayerDraws",                    "Radar",      "MaxPlayerDraws"),
                ("GstRadarMaxSleeperDraws",                   "Radar",      "MaxSleeperDraws"),
                ("GstRadarMaxUsers",                          "Radar",      "MaxUsers"),
                ("GstRadarToggleCooldownSeconds",             "Radar",      "ToggleCooldownSeconds"),
                ("GstRadarAutoOffSeconds",                    "Radar",      "AutoOffSeconds"),
                ("GstRadarDrawBudgetPerTick",                 "Radar",      "DrawBudgetPerTick"),
                ("GstRadarHighlightGstTraps",                 "Radar",      "HighlightGstTraps"),

                ("EnableAutomatedStashTraps",                 "StashTraps", "Enabled"),
                ("AutomatedStashTrapsMaxActive",              "StashTraps", "MaxActive"),
                ("AutomatedStashTrapsEnsureIntervalSeconds",  "StashTraps", "EnsureIntervalSeconds"),
                ("AutomatedStashTrapViolationThreshold",      "StashTraps", "ViolationThreshold"),
                ("AutomatedStashTrapViolationWindowMinutes",  "StashTraps", "ViolationWindowMinutes"),
                ("AutomatedStashTrapBanDelaySeconds",         "StashTraps", "BanDelaySeconds"),
                ("AutomatedStashTrapBanReason",               "StashTraps", "BanReason"),
                ("AutomatedStashDestroyRevealedAfterMinutes", "StashTraps", "DestroyRevealedAfterMinutes"),
                ("AutomatedStashReplaceRevealedTrap",         "StashTraps", "ReplaceRevealedTrap"),
                ("AutomatedStashTrapSpawnDecoyBags",          "StashTraps", "SpawnDecoyBags"),
                ("AutomatedStashTrapDecoyBagSpawnChance",     "StashTraps", "DecoyBagSpawnChance"),
                ("AutomatedStashTrapDecoyLoot",               "StashTraps", "DecoyLoot"),
                ("AutomatedStashTrapOwnerSteamId",            "StashTraps", "OwnerSteamId"),
                ("AutomatedStashTrapPlacementBuildingRadius", "StashTraps", "PlacementBuildingRadius"),
                ("AutomatedStashTrapPlacementMonumentRadius", "StashTraps", "PlacementMonumentRadius"),
                ("AutomatedStashTrapMinWaterClearance",       "StashTraps", "MinWaterClearance"),
                ("AutomatedStashTrapIgnoreTeamWindowEnabled", "StashTraps", "IgnoreTeamWindowEnabled"),
                ("AutomatedStashTrapIgnoreTeamWindowSeconds", "StashTraps", "IgnoreTeamWindowSeconds"),
                ("AutomatedStashTrapIgnoreClanWindowEnabled", "StashTraps", "IgnoreClanWindowEnabled"),
                ("AutomatedStashTrapIgnoreClanWindowSeconds", "StashTraps", "IgnoreClanWindowSeconds"),
                ("AutomatedStashTrapLocalAutoBan",            "StashTraps", "LocalAutoBan"),

                ("WatchDogBatchIntervalSeconds",              "WatchDog",   "BatchIntervalSeconds"),
                ("WatchDogBatchMaxSize",                      "WatchDog",   "BatchMaxSize"),
                ("WatchDogBufferCap",                         "WatchDog",   "BufferCap"),
                ("WatchDogChunkMaxRetries",                   "WatchDog",   "ChunkMaxRetries"),
                ("WatchDogRetryBaseDelaySeconds",             "WatchDog",   "RetryBaseDelaySeconds"),

                ("EnableHighPingKick",                        "HighPing",   "Enabled"),
                ("HighPingMax",                               "HighPing",   "MaxPing"),
                ("HighPingCheckIntervalSeconds",              "HighPing",   "CheckIntervalSeconds"),

                ("ApiUrl",       null, null),
                ("WatchDogApiUrl", null, null),
            };

            bool migrated = false;
            foreach (var m in map)
            {
                if (Config[m.Old] == null) continue;
                if (m.Sec != null && Config[m.Sec, m.Key] == null)
                    Config[m.Sec, m.Key] = Config[m.Old];
                Config.Remove(m.Old);
                migrated = true;
            }
            if (migrated)
            {
                Puts("[GST] Config migrated to section layout.");
                SaveConfig();
            }
        }

        #endregion

        private void LoadConfigData()
        {
            _headers.Clear();
            _headers["Content-Type"] = "application/json";

            _apiBaseUrl   = "https://api.gameservertools.com";
            _watchDogApiUrl = WatchDogBaseUrl;

            MigrateFlatConfig();

            // General
            string apiKey = CfgString("General", "APIKEY", "");
            if (string.IsNullOrEmpty(apiKey))
                LogError("NO API KEY PROVIDED! Please ensure you have added your api key in the config file");
            else
                _headers["ApiKey"] = apiKey;

            _linkedGroupName  = CfgString("General", "OxideGroupNameForLinked", "DiscordLinked");
            _nitroGroupName   = CfgString("General", "OxideGroupNameForNitro", "NitroBoosted");
            _showClaimMessage = CfgBool("General", "DisplayMessageOnClaimRewards", false);
            _loggingEnabled   = CfgBool("General", "DebugLoggingEnabled", false);
            _disableBanCtrl   = CfgBool("General", "DisableBanCtrl", false);

            // API checks (disable while server IP/port not registered in GST dashboard)
            _banCheckEnabled = CfgBool("Api", "BanCheckEnabled", true);
            _vpnCheckEnabled = CfgBool("Api", "VpnCheckEnabled", true);

            // WatchDog telemetry — off stops enforcement polling + combat batch spam
            _watchDogEnabled = CfgBool("WatchDog", "Enabled", true);
            if (!_watchDogEnabled)
                _watchDogApiUrl = string.Empty;

            int actualPort = ConVar.Server.port > 0 ? ConVar.Server.port : 28015;
            _port = actualPort;

            // AntiFlood
            _antiFloodChatEnabled            = CfgBool("AntiFlood",  "ChatEnabled",          false);
            _antiFloodChatCooldownSeconds    = CfgFloat("AntiFlood", "ChatCooldownSeconds",   1.5f);
            _antiFloodCommandEnabled         = CfgBool("AntiFlood",  "CommandEnabled",        false);
            _antiFloodCommandCooldownSeconds = CfgFloat("AntiFlood", "CommandCooldownSeconds", 1.0f);

            // Radar
            _gstRadarEnabled              = CfgBool("Radar", "Enabled", false);
            _gstRadarMaxDistance          = Mathf.Max(20f, CfgFloat("Radar", "MaxDistance",    200f));
            _gstRadarInterval             = CfgFloatClamped("Radar", "IntervalSeconds",        0.5f, 0.12f, 2f);
            _gstRadarLookLineLength       = CfgFloatClamped("Radar", "LookLineLength",         15f,   2f,    500f);
            _gstRadarProjectileLineLength = CfgFloatClamped("Radar", "ProjectileLineLength",   100f,  10f,   500f);
            _gstRadarProjectileSeconds    = CfgFloatClamped("Radar", "ProjectileSeconds",      1.25f, 0.2f,  10f);
            _gstRadarProjectileCap        = CfgIntClamped(  "Radar", "ProjectileCap",          400,   50,    5000);
            _gstRadarShowWorldEntities    = CfgBoolDefaultTrue("Radar", "ShowWorldEntities");
            _gstRadarMaxEntityDraws       = CfgIntClamped(  "Radar", "MaxEntityDraws",         220,   20,    500);
            _gstRadarMaxDropDraws         = CfgIntClamped(  "Radar", "MaxDropDraws",           24,    0,     200);
            _gstRadarMaxPlayerDraws       = CfgIntClamped(  "Radar", "MaxPlayerDraws",         60,    5,     200);
            _gstRadarMaxSleeperDraws      = CfgIntClamped(  "Radar", "MaxSleeperDraws",        30,    5,     200);
            _gstRadarMaxUsers             = CfgIntClamped(  "Radar", "MaxUsers",               8,     1,     32);
            _gstRadarToggleCooldownSeconds = CfgFloatClamped("Radar", "ToggleCooldownSeconds", 2f,    0f,    30f);
            _gstRadarAutoOffSeconds       = CfgFloatClamped("Radar", "AutoOffSeconds",         120f,  10f,   3600f);
            _gstRadarDrawBudgetPerTick    = CfgIntClamped(  "Radar", "DrawBudgetPerTick",      800,   100,   5000);
            _gstRadarHighlightGstTraps    = CfgBoolDefaultTrue("Radar", "HighlightGstTraps");
            _gstRadarMaxDistanceSq        = _gstRadarMaxDistance * _gstRadarMaxDistance;

            // StashTraps
            _enableAutoStashTraps               = CfgBool(      "StashTraps", "Enabled",                   false);
            _autoStashMaxTraps                  = CfgIntClamped( "StashTraps", "MaxActive",                 200,  1,   500);
            _autoStashEnsureIntervalSeconds     = CfgFloat(      "StashTraps", "EnsureIntervalSeconds",     120f);
            _autoStashViolationThreshold        = CfgInt(        "StashTraps", "ViolationThreshold",        3);
            _autoStashViolationWindowMinutes    = CfgFloat(      "StashTraps", "ViolationWindowMinutes",    30f);
            _autoStashBanDelaySeconds           = CfgInt(        "StashTraps", "BanDelaySeconds",           10);
            _autoStashBanReason                 = CfgString(     "StashTraps", "BanReason",                 "Cheat Detected (ESP stash trap)");
            _autoStashDestroyRevealedAfterMinutes = CfgInt(      "StashTraps", "DestroyRevealedAfterMinutes", 5);
            _autoStashReplaceRevealedTrap       = CfgBoolDefaultTrue("StashTraps", "ReplaceRevealedTrap");
            _autoStashSpawnDecoyBags            = CfgBoolDefaultTrue("StashTraps", "SpawnDecoyBags");
            _autoStashDecoyBagSpawnChance       = CfgInt(        "StashTraps", "DecoyBagSpawnChance",       55);
            _autoStashPlacementBuildingRadius   = CfgFloat(      "StashTraps", "PlacementBuildingRadius",   20f);
            _autoStashPlacementMonumentRadius   = CfgFloat(      "StashTraps", "PlacementMonumentRadius",   35f);
            _autoStashMinWaterClearance         = CfgFloat(      "StashTraps", "MinWaterClearance",         0.75f);
            _autoStashIgnoreTeamWindowEnabled   = CfgBoolDefaultTrue("StashTraps", "IgnoreTeamWindowEnabled");
            _autoStashIgnoreTeamWindowSeconds   = CfgFloat(      "StashTraps", "IgnoreTeamWindowSeconds",   20f);
            _autoStashIgnoreClanWindowEnabled   = CfgBoolDefaultTrue("StashTraps", "IgnoreClanWindowEnabled");
            _autoStashIgnoreClanWindowSeconds   = CfgFloat(      "StashTraps", "IgnoreClanWindowSeconds",   25f);
            _autoStashTrapDecoyLoot             = CfgBoolDefaultTrue("StashTraps", "DecoyLoot");
            _autoStashLocalAutoBan              = CfgBoolDefaultTrue("StashTraps", "LocalAutoBan");
            {
                string ownerStr = Config["StashTraps", "OwnerSteamId"]?.ToString()?.Trim();
                _autoStashTrapOwnerSteamId = !string.IsNullOrEmpty(ownerStr) && ulong.TryParse(ownerStr, out ulong trapOwner) ? trapOwner : 0UL;
            }

            // WatchDog
            _watchDogBatchIntervalSeconds  = CfgFloat(      "WatchDog", "BatchIntervalSeconds",  5f);
            _watchDogBatchMaxSize          = CfgInt(        "WatchDog", "BatchMaxSize",           200);
            _watchDogBufferCap             = CfgInt(        "WatchDog", "BufferCap",              2000);
            _watchDogChunkMaxRetries       = CfgIntClamped( "WatchDog", "ChunkMaxRetries",        12,   1, 30);
            _watchDogRetryBaseDelaySeconds = CfgFloatClamped("WatchDog", "RetryBaseDelaySeconds", 2f,   0.5f, 30f);

            // HighPing
            _enableHighPingKick           = CfgBool( "HighPing", "Enabled",             false);
            _highPingMax                  = CfgInt(  "HighPing", "MaxPing",              350);
            _highPingCheckIntervalSeconds = CfgFloat("HighPing", "CheckIntervalSeconds", 60f);
        }

        #endregion

        #region Permission Helpers

        private bool HasWatchDogBypass(BasePlayer player)
        {
            if (player == null) return false;
            return HasWatchDogBypassById(player.UserIDString);
        }

        private bool HasWatchDogBypassById(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            return permission.UserHasPermission(playerId, PermBypass);
        }

        private bool HasHighPingBypass(BasePlayer player)
        {
            if (player == null) return false;
            return permission.UserHasPermission(player.UserIDString, PermHighPingBypass);
        }
        private bool CanReceiveStashTrapAdminAlert(BasePlayer viewer)
        {
            if (viewer == null || !viewer.IsConnected) return false;
            if (viewer.IsAdmin) return true;
            return permission.UserHasPermission(viewer.UserIDString, PermStashTrapAlerts);
        }
        private void SendStashTrapAdminChat(BasePlayer offender, string langKey, Dictionary<string, string> extraReplacements = null)
        {
            if (offender == null) return;
            string offenderName = string.IsNullOrEmpty(offender.displayName) ? "Unknown" : offender.displayName;
            ulong offenderId = offender.userID;
            foreach (var admin in BasePlayer.activePlayerList)
            {
                if (!CanReceiveStashTrapAdminAlert(admin)) continue;
                if (admin.userID == offenderId) continue;
                string msg = lang.GetMessage(langKey, this, admin.UserIDString);
                msg = msg.Replace("@offender", offenderName).Replace("@steamid", offender.UserIDString);
                if (extraReplacements != null)
                {
                    foreach (var kv in extraReplacements)
                        msg = msg.Replace(kv.Key, kv.Value);
                }
                admin.ChatMessage(msg);
            }
        }

        #endregion

        #region Oxide Hooks

        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (_antiFloodChatEnabled && player != null && !HasWatchDogBypass(player))
            {
                float now = UnityEngine.Time.realtimeSinceStartup;
                if (_chatCooldownUntil.TryGetValue(player.userID, out float nextAllowedChat) && now < nextAllowedChat)
                {
                    string floodMsg = lang.GetMessage("ChatFloodBlockedMessage", this, player.UserIDString);
                    floodMsg = floodMsg.Replace("@seconds", Math.Max(0.1f, nextAllowedChat - now).ToString("0.0"));
                    player.ChatMessage(floodMsg);
                    return true;
                }
                _chatCooldownUntil[player.userID] = now + Mathf.Max(0.1f, _antiFloodChatCooldownSeconds);
            }

            if (player == null) return null;

            List<string> messageList;
            if (!_chatHistory.TryGetValue(player.userID, out messageList))
            {
                messageList = new List<string>();
                _chatHistory.Add(player.userID, messageList);
            }

            messageList.Insert(0, $"{DateTime.UtcNow} UTC | {channel} | {message}");
            if (messageList.Count > 100)
                messageList.RemoveAt(messageList.Count - 1);
            return null;
        }

        private void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type)
        {
            ReportType typeOfReport = GetTypeIdFromType(type);

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "ReporterId", reporter.userID.ToString() },
                { "ReportedPlayerId", targetId },
                { "Subject", subject },
                { "Reason", message },
                { "ServerPort", _port },
                { "Type", typeOfReport }
            };

            List<KeyValuePair<int, List<string>>> files = new List<KeyValuePair<int, List<string>>>();

            if (typeOfReport == ReportType.Abusive || typeOfReport == ReportType.Spam)
            {
                if (ulong.TryParse(targetId, out ulong playerIdLong) && _chatHistory.TryGetValue(playerIdLong, out List<string> userMessages))
                {
                    if (userMessages.Count > 100)
                        userMessages.RemoveRange(100, userMessages.Count - 100);
                    files.Add(new KeyValuePair<int, List<string>>(0, userMessages));
                }
            }

            if (typeOfReport == ReportType.Cheat)
            {
                BasePlayer target = BasePlayer.Find(targetId);
                if (target != null)
                {
                    int oldDelay = ConVar.Server.combatlogdelay;
                    ConVar.Server.combatlogdelay = 0;
                    string combatLogString = target.stats.combat.Get(ConVar.Server.combatlogsize);
                    ConVar.Server.combatlogdelay = oldDelay;

                    files.Add(new KeyValuePair<int, List<string>>(1, combatLogString.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList()));
                }
            }

            parameters.Add("TextFiles", files);

            SendReport(parameters);
        }

        private void OnServerInitialized(bool initial)
        {
            if (initial)
            {
                ClearAllPlayerConnections();
            }
        }

        private void OnServerShutdown()
        {
            if (!string.IsNullOrWhiteSpace(_watchDogApiUrl))
                FlushWatchDogBatch();
            _highPingCheckTimer?.Destroy();
            _stashTrapEnsureTimer?.Destroy();
            _gstRadarTimer?.Destroy();
            ClearAllPlayerConnections();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            CancelForceLinkTimer(player.UserIDString);
            _watchDogLastAimSend.Remove(player.userID);
            _chatHistory.Remove(player.userID);
            _chatCooldownUntil.Remove(player.userID);
            _commandCooldownUntil.Remove(player.userID);
            _autoStashViolationWindows.Remove(player.userID);
            _cachedJoins.Remove(player.UserIDString);
            _gstRadarUsers.Remove(player.userID);
            _gstRadarFilters.Remove(player.userID);
            _gstRadarToggleLastTime.Remove(player.userID);
            _gstRadarSessionStart.Remove(player.userID);

            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters.Add("SteamId", player.UserIDString);
            parameters.Add("ServerPort", _port);

            UserLeftServer(parameters);
            QueueOnlinePlayersReport();
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (!_antiFloodCommandEnabled || arg == null) return null;
            BasePlayer player = arg.Player();
            if (player == null || HasWatchDogBypass(player)) return null;
            string fullName = arg.cmd?.FullName ?? string.Empty;
            if (fullName == "craft.add") return null;

            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_commandCooldownUntil.TryGetValue(player.userID, out float nextAllowedCommand) && now < nextAllowedCommand)
            {
                string floodMsg = lang.GetMessage("CommandFloodBlockedMessage", this, player.UserIDString);
                floodMsg = floodMsg.Replace("@seconds", Math.Max(0.1f, nextAllowedCommand - now).ToString("0.0"));
                player.ChatMessage(floodMsg);
                return true;
            }
            _commandCooldownUntil[player.userID] = now + Mathf.Max(0.1f, _antiFloodCommandCooldownSeconds);
            return null;
        }

        private void OnStashExposed(StashContainer stash, BasePlayer player)
        {
            if (!_enableAutoStashTraps || stash == null || player == null) return;
            if (!_autoStashTrapIds.TryGetValue(stash.net.ID.Value, out AutoStashTrapMeta trapMeta)) return;
            if (HasWatchDogBypass(player)) return;
            if (ShouldSuppressAutoStashViolation(player, trapMeta)) return;

            RegisterAutoStashViolation(player, stash);
        }

        private void OnEntityKill(StashContainer stash)
        {
            if (!_enableAutoStashTraps || stash == null) return;
            _autoStashTrapIds.Remove(stash.net.ID.Value);
            _autoStashRecentTriggers.Remove(stash.net.ID.Value);
        }

        private void OnEntityKill(SleepingBag bag)
        {
            if (!_enableAutoStashTraps || bag == null) return;
            ulong bid = bag.net.ID.Value;
            foreach (KeyValuePair<ulong, AutoStashTrapMeta> kv in _autoStashTrapIds)
            {
                AutoStashTrapMeta m = kv.Value;
                if (m == null || m.DecoyBagId != bid) continue;
                m.DecoyBagId = 0;
                m.DecoyBagPosition = null;
                break;
            }
        }

        private void OnUserUnbanned(string name, string id, string address)
        {
            if (string.IsNullOrEmpty(id) || !ulong.TryParse(id, out ulong uid)) return;
            _autoStashViolationWindows.Remove(uid);
        }

        private void OnPlayerBanned(string name, ulong id, string address, string reason)
        {
            AddBanClass ban = new AddBanClass();

            ban.SteamId = id.ToString();
            ban.Reason = reason;
            ban.ExpireTime = null;
            ban.BannedBy = null;

            SubmitNewBan(ban, null, (newBan) =>
            {
                ServerUsers.User user = ServerUsers.Get(id);
                if (user == null || user.group != ServerUsers.UserGroup.Banned)
                {
                    Puts("no user found that is banned");
                }
                else
                {
                    ServerUsers.Remove(id);
                    ServerUsers.Save();
                }
            });
        }

        private void OnUserApprove(Network.Connection connection)
        {
            ulong ownerId = connection.userid;

            string id = connection.userid.ToString();
            if (_approvedCachedJoins.ContainsKey(id))
            {
                ApprovedCachedPlayer data = _approvedCachedJoins[id];
                TimeSpan timeSinceAdd = DateTime.Now - data.timeOfAdd;

                if (timeSinceAdd.TotalMinutes < 1)
                {
                    timer.Once(5.0f, () =>
                    {
                        if (connection != null)
                            Network.Net.sv.Kick(connection, data.reason, false);
                    });

                    return;
                }

                _approvedCachedJoins.Remove(id);
            }

            if (_banCheckEnabled && !_gstServerUnauthorized)
                CheckForBans(connection, ownerId);
            if (_vpnCheckEnabled && !_gstServerUnauthorized)
                CheckForVpn(connection);
        }

        private void OnUserConnected(IPlayer player)
        {
            FetchDiscordLinkData(player);

            UserConnectedToServer(player);
        }

        private static Vector3 GetVelocityOrZero(BasePlayer player)
        {
            if (player == null) return Vector3.zero;
            var rb = player.GetComponent<Rigidbody>();
            return rb != null ? rb.velocity : Vector3.zero;
        }

        private void OnPlayerAttack(BasePlayer player, HitInfo hitInfo)
        {
            if (string.IsNullOrWhiteSpace(_watchDogApiUrl)) return;
            if (player == null || !player.userID.IsSteamId()) return;
            if (HasWatchDogBypass(player)) return;
            BasePlayer victimPlayer = hitInfo?.HitEntity as BasePlayer;
            if (victimPlayer != null && !victimPlayer.userID.IsSteamId()) return;
            if (victimPlayer != null && victimPlayer.userID == player.userID) return;
            SendWatchDogCombatEvent("attack", player, hitInfo, victimPlayer);
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (string.IsNullOrWhiteSpace(_watchDogApiUrl)) return null;
            BasePlayer victim = entity as BasePlayer;
            BasePlayer attacker = hitInfo?.Initiator as BasePlayer;
            if (attacker == null || !attacker.userID.IsSteamId()) return null;
            if (victim == null || !victim.userID.IsSteamId()) return null;
            if (HasWatchDogBypass(attacker)) return null;
            if (victim.userID != attacker.userID)
                SendWatchDogCombatEvent("damage", attacker, hitInfo, victim);
            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (string.IsNullOrWhiteSpace(_watchDogApiUrl)) return;
            if (info == null) return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null || !attacker.userID.IsSteamId()) return;
            if (HasWatchDogBypass(attacker)) return;
            BasePlayer victim = entity as BasePlayer;
            if (victim == null || !victim.userID.IsSteamId()) return;
            if (victim.userID == attacker.userID) return;
            SendWatchDogCombatEvent("kill", attacker, info, victim);
        }

        private void OnPlayerViolation(BasePlayer player, AntiHackType type, float amount, UnityEngine.GameObject target)
        {
            if (string.IsNullOrWhiteSpace(_watchDogApiUrl)) return;
            if (HasWatchDogBypass(player)) return;
            var pos = player != null ? player.transform.position : Vector3.zero;
            var vel = GetVelocityOrZero(player);
            var payload = new Dictionary<string, object>
            {
                { "Player", player?.UserIDString ?? "" },
                { "Event", "violation" },
                { "Violation", type.ToString() },
                { "Severity", amount },
                { "PosX", pos.x },
                { "PosY", pos.y },
                { "PosZ", pos.z },
                { "VelocityX", vel.x },
                { "VelocityY", vel.y },
                { "VelocityZ", vel.z },
                { "Speed", (float)vel.magnitude },
                { "IsOnGround", player != null && player.IsOnGround() },
                { "IsFlying", player != null && player.IsFlying },
                { "IsMounted", player != null && player.isMounted },
                { "IsSleeping", player != null && player.IsSleeping() },
                { "IsWounded", player != null && player.IsWounded() },
                { "Health", player != null ? player.health : 0f },
                { "Timestamp", (int)UnityEngine.Time.realtimeSinceStartup },
                { "ServerPort", _port }
            };
            var heldItem = player?.GetActiveItem();
            if (heldItem != null) payload["Weapon"] = heldItem.info.shortname;
            SendWatchDogPayload(payload);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (string.IsNullOrWhiteSpace(_watchDogApiUrl) || player == null) return;
            if (HasWatchDogBypass(player)) return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_watchDogLastAimSend.TryGetValue(player.userID, out float last) && (now - last) < WatchDogAimThrottleSeconds)
                return;

            var angles = player.serverInput.current.aimAngles;
            var prevAngles = player.serverInput.previous.aimAngles;
            double deltaPitch = (double)(angles.x - prevAngles.x);
            double deltaYaw = (double)(angles.y - prevAngles.y);
            if (deltaYaw > 180) deltaYaw -= 360;
            if (deltaYaw < -180) deltaYaw += 360;

            // Skip if aim hasn't moved meaningfully - avoids allocating a dictionary for micro-jitter
            if (Math.Abs(deltaPitch) < 0.5 && Math.Abs(deltaYaw) < 0.5) return;

            _watchDogLastAimSend[player.userID] = now;

            var pos = player.transform.position;
            var vel = GetVelocityOrZero(player);
            var heldItem = player.GetActiveItem();

            var payload = new Dictionary<string, object>
            {
                { "Player", player.UserIDString },
                { "Event", "aim" },
                { "ViewPitch", (double)angles.x },
                { "ViewYaw", (double)angles.y },
                { "DeltaPitch", deltaPitch },
                { "DeltaYaw", deltaYaw },
                { "PosX", pos.x },
                { "PosY", pos.y },
                { "PosZ", pos.z },
                { "VelocityX", vel.x },
                { "VelocityY", vel.y },
                { "VelocityZ", vel.z },
                { "Speed", (float)vel.magnitude },
                { "IsOnGround", player.IsOnGround() },
                { "IsSprinting", input.IsDown(BUTTON.SPRINT) },
                { "IsDucked", input.IsDown(BUTTON.DUCK) },
                { "IsMounted", player.isMounted },
                { "Buttons", (int)input.current.buttons },
                { "Timestamp", (int)now },
                { "ServerPort", _port }
            };
            if (heldItem != null) payload["Weapon"] = heldItem.info.shortname;
            SendWatchDogPayload(payload);
        }

        private object OnWorldProjectileCreate(HitInfo info, Item item)
        {
            if (info == null || _gstRadarUsers.Count == 0) return null;
            if (GstRadarPlayerHeldBaseProjectileSteam(info)) return null;

            Vector3 vel = info.ProjectileVelocity;
            if (!GstRadarVector3Finite(vel) || vel.sqrMagnitude < 1e-6f)
            {
                BasePlayer shooter = info.InitiatorPlayer;
                if (shooter?.eyes != null)
                {
                    Vector3 fwd = shooter.eyes.HeadForward();
                    if (GstRadarVector3Finite(fwd) && fwd.sqrMagnitude > 1e-6f)
                        vel = fwd;
                }
            }
            if (!GstRadarVector3Finite(vel) || vel.sqrMagnitude < 1e-6f) return null;
            Vector3 start = GstRadarResolveProjectileSpawn(info);
            if (!GstRadarVector3Finite(start) || start == Vector3.zero) return null;
            RecordGstRadarProjectileLine(start, vel.normalized);
            return null;
        }

        private static bool GstRadarPlayerHeldBaseProjectileSteam(HitInfo info)
        {
            BasePlayer p = info.InitiatorPlayer;
            if (p == null || !p.userID.IsSteamId()) return false;
            return p.GetHeldEntity() is BaseProjectile;
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (_gstRadarUsers.Count == 0 || entity == null || entity.IsDestroyed) return;
            Vector3 start = entity.transform.position;
            if (!GstRadarVector3Finite(start)) return;
            Vector3 dir;
            Rigidbody rb = entity.GetComponent<Rigidbody>();
            if (rb != null && GstRadarVector3Finite(rb.velocity) && rb.velocity.sqrMagnitude > 0.25f)
                dir = rb.velocity.normalized;
            else
            {
                Vector3 f = entity.transform.forward;
                dir = GstRadarVector3Finite(f) && f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
            RecordGstRadarProjectileLine(start, dir);
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile itemModProjectile)
        {
            if (player == null || !player.IsConnected || !player.userID.IsSteamId() || _gstRadarUsers.Count == 0) return;
            if (projectile == null || player.eyes == null) return;
            Vector3 start = projectile.MuzzlePoint != null && projectile.MuzzlePoint.transform != null
                ? projectile.MuzzlePoint.transform.position
                : player.eyes.position;
            Vector3 dir = player.eyes.HeadForward();
            if (!GstRadarVector3Finite(start) || !GstRadarVector3Finite(dir) || dir.sqrMagnitude < 1e-6f) return;
            RecordGstRadarProjectileLine(start, dir.normalized);
        }

        private static bool GstRadarVector3Finite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        private static bool GstRadarInRange(Vector3 a, Vector3 b, float maxDistSq)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return (dx * dx + dy * dy + dz * dz) <= maxDistSq;
        }

        private static Vector3 GstRadarResolveProjectileSpawn(HitInfo info)
        {
            Vector3 ps = info.PointStart;
            if (GstRadarVector3Finite(ps) && ps != Vector3.zero)
                return ps;
            Vector3 hpw = info.HitPositionWorld;
            if (GstRadarVector3Finite(hpw) && hpw != Vector3.zero)
                return hpw;
            BasePlayer shooter = info.InitiatorPlayer;
            if (shooter != null && shooter.eyes != null)
            {
                Vector3 eyes = shooter.eyes.position;
                if (GstRadarVector3Finite(eyes))
                    return eyes;
            }
            return Vector3.zero;
        }

        #endregion

        #region Radar System

        private void RecordGstRadarProjectileLine(Vector3 start, Vector3 directionNormalized)
        {
            if (!GstRadarVector3Finite(start) || !GstRadarVector3Finite(directionNormalized)) return;
            if (directionNormalized.sqrMagnitude < 1e-8f) return;
            Vector3 end = start + directionNormalized * _gstRadarProjectileLineLength;
            if (!GstRadarVector3Finite(end)) return;
            float exp = UnityEngine.Time.realtimeSinceStartup + _gstRadarProjectileSeconds;
            _gstRadarProjectileLines.Add(new GstRadarProjectileLine { A = start, B = end, Expire = exp });
            if (_gstRadarProjectileLines.Count > _gstRadarProjectileCap)
                _gstRadarProjectileLines.RemoveAt(0);
        }

        private void GstRadarTick()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            for (int i = _gstRadarProjectileLines.Count - 1; i >= 0; i--)
            {
                if (_gstRadarProjectileLines[i].Expire < now)
                    _gstRadarProjectileLines.RemoveAt(i);
            }

            if (_gstRadarUsers.Count == 0) return;

            float drawDur = Mathf.Clamp(_gstRadarInterval + 0.12f, 0.15f, 2.5f);
            _gstRadarTickSnapshot.Clear();
            _gstRadarTickSnapshot.AddRange(_gstRadarUsers);
            _gstRadarRemoveBuffer.Clear();

            int playerMask = _gstRadarPlayerMask;
            int globalDraws = 0;

            foreach (ulong uid in _gstRadarTickSnapshot)
            {
                // Global draw call budget: stop when server-side ddraw limit would be exceeded
                if (globalDraws >= _gstRadarDrawBudgetPerTick) break;

                BasePlayer admin = BasePlayer.FindByID(uid);
                if (admin == null || !admin.IsConnected)
                {
                    _gstRadarRemoveBuffer.Add(uid);
                    continue;
                }

                Vector3 ap = admin.transform.position;
                GstRadarFilters rf = GstRadarGetFiltersForUser(uid);

                // Players & sleepers via spatial query (uses Rust's internal grid)
                if (rf.Players || rf.Sleepers)
                {
                    _gstRadarNearbyPlayers.Clear();
                    Vis.Entities(ap, _gstRadarMaxDistance, _gstRadarNearbyPlayers, playerMask, QueryTriggerInteraction.Ignore);

                    int playerCount = 0;
                    int sleeperCount = 0;
                    for (int i = 0; i < _gstRadarNearbyPlayers.Count; i++)
                    {
                        if (globalDraws >= _gstRadarDrawBudgetPerTick) break;
                        BasePlayer target = _gstRadarNearbyPlayers[i];
                        if (target == null || target.IsDestroyed) continue;
                        if (target.userID == admin.userID) continue;

                        if (target.IsSleeping())
                        {
                            if (rf.Sleepers && sleeperCount < _gstRadarMaxSleeperDraws)
                            {
                                DrawGstRadarSleeperMarkers(admin, target, drawDur, rf);
                                sleeperCount++;
                                globalDraws += 3; // sphere + line + text
                            }
                        }
                        else if (target.IsConnected)
                        {
                            if (rf.Players && playerCount < _gstRadarMaxPlayerDraws)
                            {
                                DrawGstRadarPlayerMarkers(admin, target, drawDur, rf);
                                playerCount++;
                                globalDraws += 4; // sphere + look line + body box + text
                            }
                        }
                    }
                }

                if (_gstRadarShowWorldEntities && rf.World && globalDraws < _gstRadarDrawBudgetPerTick)
                    GstRadarDrawWorldEntities(admin, ap, drawDur, rf);

                if (rf.Shots && globalDraws < _gstRadarDrawBudgetPerTick)
                {
                    for (int i = 0; i < _gstRadarProjectileLines.Count; i++)
                    {
                        if (globalDraws >= _gstRadarDrawBudgetPerTick) break;
                        var seg = _gstRadarProjectileLines[i];
                        if (!GstRadarVector3Finite(seg.A) || !GstRadarVector3Finite(seg.B)) continue;
                        if (!GstRadarInRange(ap, seg.A, (_gstRadarMaxDistance + _gstRadarProjectileLineLength) * (_gstRadarMaxDistance + _gstRadarProjectileLineLength))) continue;
                        admin.SendConsoleCommand("ddraw.line", drawDur, new Color(1f, 0.42f, 0.08f, 1f), seg.A, seg.B);
                        globalDraws++;
                    }
                }
            }

            // Auto-off: expire sessions that have run past _gstRadarAutoOffSeconds
            foreach (ulong uid in _gstRadarTickSnapshot)
            {
                if (!_gstRadarRemoveBuffer.Contains(uid)
                    && _gstRadarSessionStart.TryGetValue(uid, out float sessionStart)
                    && (now - sessionStart) >= _gstRadarAutoOffSeconds)
                {
                    _gstRadarRemoveBuffer.Add(uid);
                    BasePlayer expiredAdmin = BasePlayer.FindByID(uid);
                    expiredAdmin?.ChatMessage(lang.GetMessage("GstRadarAutoOff", this, uid.ToString()));
                }
            }

            // Deferred removal — clean pattern, no collection-during-iteration
            for (int i = 0; i < _gstRadarRemoveBuffer.Count; i++)
            {
                ulong removeUid = _gstRadarRemoveBuffer[i];
                _gstRadarUsers.Remove(removeUid);
                _gstRadarFilters.Remove(removeUid);
                _gstRadarToggleLastTime.Remove(removeUid);
                _gstRadarSessionStart.Remove(removeUid);
            }
        }

        private void DrawGstRadarPlayerMarkers(BasePlayer viewer, BasePlayer target, float drawDur, GstRadarFilters f)
        {
            if (!f.Players || viewer == null || target == null || target.eyes == null) return;
            Vector3 feet = target.transform.position;
            Vector3 eye = target.eyes.position;
            Vector3 fwd = target.eyes.HeadForward();
            if (!GstRadarVector3Finite(feet) || !GstRadarVector3Finite(eye) || !GstRadarVector3Finite(fwd)) return;
            if (fwd.sqrMagnitude < 1e-6f) return;
            Vector3 lookEnd = eye + fwd.normalized * _gstRadarLookLineLength;
            if (!GstRadarVector3Finite(lookEnd)) return;
            Vector3 sphereCenter = feet + new Vector3(0f, 0.05f, 0f);
            viewer.SendConsoleCommand("ddraw.sphere", drawDur, Color.cyan, sphereCenter, 0.4f);
            viewer.SendConsoleCommand("ddraw.line", drawDur, Color.yellow, eye, lookEnd);
            viewer.SendConsoleCommand("ddraw.box", drawDur, new Color(0.25f, 0.85f, 1f), feet + Vector3.up * 0.9f, 0.5f);
            string name = string.IsNullOrEmpty(target.displayName) ? target.UserIDString : target.displayName;
            int hp = Mathf.RoundToInt(target.health);
            int dist = Mathf.RoundToInt(Vector3.Distance(viewer.transform.position, feet));
            string teamStr = target.currentTeam != 0 ? $"[T{target.currentTeam % 10000}] " : "";
            string weaponStr = "";
            Item heldItem = target.GetActiveItem();
            if (heldItem?.info != null) weaponStr = $" | {heldItem.info.shortname}";
            string label = $"{name} {teamStr}| HP:{hp} | {dist}m{weaponStr}";
            Vector3 textPos = eye + Vector3.up * 0.28f;
            if (!GstRadarVector3Finite(textPos)) return;
            viewer.SendConsoleCommand("ddraw.text", drawDur, Color.white, textPos, label);
        }

        private void DrawGstRadarSleeperMarkers(BasePlayer viewer, BasePlayer sleeper, float drawDur, GstRadarFilters f)
        {
            if (!f.Sleepers || viewer == null || sleeper == null || sleeper.eyes == null) return;
            Vector3 feet = sleeper.transform.position;
            Vector3 eye = sleeper.eyes.position;
            Vector3 fwd = sleeper.eyes.HeadForward();
            if (!GstRadarVector3Finite(feet) || !GstRadarVector3Finite(eye)) return;
            Vector3 lookEnd = GstRadarVector3Finite(fwd) && fwd.sqrMagnitude > 1e-6f
                ? eye + fwd.normalized * Mathf.Min(_gstRadarLookLineLength, 8f)
                : eye;
            if (!GstRadarVector3Finite(lookEnd)) return;
            viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(0.65f, 0.35f, 0.95f), feet + new Vector3(0f, 0.05f, 0f), 0.45f);
            if ((lookEnd - eye).sqrMagnitude > 0.01f)
                viewer.SendConsoleCommand("ddraw.line", drawDur, new Color(0.85f, 0.55f, 1f), eye, lookEnd);
            string name = string.IsNullOrEmpty(sleeper.displayName) ? sleeper.UserIDString : sleeper.displayName;
            int hp = Mathf.RoundToInt(sleeper.health);
            int dist = Mathf.RoundToInt(Vector3.Distance(viewer.transform.position, feet));
            Vector3 textPos = eye + Vector3.up * 0.3f;
            if (!GstRadarVector3Finite(textPos)) return;
            viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.9f, 0.75f, 1f), textPos, $"{name} (sleep) | HP:{hp} | {dist}m");
        }

        private void GstRadarDrawWorldEntities(BasePlayer viewer, Vector3 adminPos, float drawDur, GstRadarFilters f)
        {
            if (viewer == null || !f.World) return;
            int mask = LayerMask.GetMask("Deployable", "Deployed", "AI", "Ragdoll", "Resource", "World", "Tree");
            List<BaseEntity> list = Facepunch.Pool.Get<List<BaseEntity>>();
            _gstRadarWorldEntitySeen.Clear();
            try
            {
                int drawn = 0;
                int drops = 0;
                Vis.Entities(adminPos, _gstRadarMaxDistance, list, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < list.Count && drawn < _gstRadarMaxEntityDraws; i++)
                {
                    BaseEntity ent = list[i];
                    if (ent == null || ent.IsDestroyed) continue;
                    ulong id = ent.net?.ID.Value ?? 0ul;
                    if (id != 0ul && !_gstRadarWorldEntitySeen.Add(id)) continue;
                    Vector3 p = ent.transform.position;
                    if (!GstRadarVector3Finite(p) || (p - adminPos).sqrMagnitude > _gstRadarMaxDistanceSq) continue;
                    GstRadarTryDrawWorldEntity(viewer, ent, p, drawDur, ref drawn, ref drops, f);
                }
            }
            finally
            {
                Facepunch.Pool.FreeUnmanaged(ref list);
            }
        }

        private const uint GstRadarCargoPlanePrefabId = 2383782438u;

        private static bool GstRadarIsWorldNpc(BaseEntity ent)
        {
            if (ent is BaseNpc) return true;
            if (ent is TravellingVendor) return true;
            for (Type t = ent.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.Name == "BaseNPC2")
                    return true;
            }
            if (ent is WildlifeHazard) return true;
            if (ent is SimpleShark) return true;
            if (ent is FarmableAnimal) return true;
            return false;
        }

        private void GstRadarTryDrawWorldEntity(BasePlayer viewer, BaseEntity ent, Vector3 pos, float drawDur, ref int drawn, ref int drops, GstRadarFilters f)
        {
            if (viewer == null || drawn >= _gstRadarMaxEntityDraws) return;

            if (ent is BuildingBlock || ent is Door)
                return;

            if (f.Resource && ent is OreResourceEntity)
            {
                Vector3 t = pos + Vector3.up * 0.5f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(0.4f, 0.85f, 0.35f), pos + Vector3.up * 0.1f, 0.35f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.55f, 1f, 0.5f), t, "ore");
                drawn++;
                return;
            }

            if (f.Resource && ent is CollectibleEntity)
            {
                Vector3 t = pos + Vector3.up * 0.35f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(0.35f, 1f, 0.55f), pos + Vector3.up * 0.05f, 0.22f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.5f, 1f, 0.7f), t, "pickup");
                drawn++;
                return;
            }

            if (f.FieldTraps && (ent is Landmine || ent is BearTrap || ent is RFTimedExplosive))
            {
                string tn = ent is Landmine ? "mine" : ent is BearTrap ? "bear trap" : "RF / exp";
                Vector3 t = pos + Vector3.up * 0.35f;
                viewer.SendConsoleCommand("ddraw.box", drawDur, new Color(1f, 0.35f, 0.15f), pos + Vector3.up * 0.08f, 0.28f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.55f, 0.35f), t, tn);
                drawn++;
                return;
            }

            if (f.WorldEvents && ent is CargoShip)
            {
                Vector3 t = pos + Vector3.up * 4f;
                viewer.SendConsoleCommand("ddraw.box", drawDur, new Color(0.2f, 0.55f, 1f), pos + Vector3.up * 1f, 3f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.45f, 0.75f, 1f), t, "cargo ship");
                drawn++;
                return;
            }

            if (f.WorldEvents && ent.prefabID == GstRadarCargoPlanePrefabId)
            {
                Vector3 t = pos + Vector3.up * 12f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(0.35f, 0.65f, 1f), pos + Vector3.up * 6f, 2f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.55f, 0.8f, 1f), t, "cargo plane");
                drawn++;
                return;
            }

            if (f.Cctv && (ent is CCTV_RC || ent is Drone))
            {
                Vector3 t = pos + Vector3.up * 0.55f;
                string cl = ent is Drone ? "drone" : "CCTV";
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(0.75f, 0.35f, 1f), pos + Vector3.up * 0.15f, 0.32f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.9f, 0.55f, 1f), t, cl);
                drawn++;
                return;
            }

            if (f.Mlrs && ent is MLRSRocket)
            {
                Vector3 t = pos + Vector3.up * 1.2f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(1f, 0.25f, 0.25f), pos + Vector3.up * 0.4f, 0.55f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.5f, 0.45f), t, "MLRS");
                drawn++;
                return;
            }

            if (f.Stashes && ent is StashContainer stash)
            {
                bool isGstTrap = _autoStashTrapIds.ContainsKey(stash.net.ID.Value);
                bool hidden = stash.IsHidden();
                Vector3 t = pos + Vector3.up * 0.42f;
                Color sphereColor = isGstTrap ? new Color(1f, 0.88f, 0.15f) : new Color(0.15f, 0.85f, 0.35f);
                Color textColor = isGstTrap ? new Color(1f, 0.92f, 0.35f) : new Color(0.4f, 1f, 0.55f);
                string label = isGstTrap && _gstRadarHighlightGstTraps
                    ? "GST stash trap"
                    : (hidden ? "stash (hidden)" : "stash");
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, sphereColor, pos + Vector3.up * 0.12f, 0.28f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, textColor, t, label);
                drawn++;
                return;
            }

            if (f.ToolCupboard && ent is BuildingPrivlidge)
            {
                Vector3 t = pos + Vector3.up * 1.1f;
                viewer.SendConsoleCommand("ddraw.box", drawDur, new Color(1f, 0.35f, 0.85f), pos + Vector3.up * 0.5f, 0.55f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.55f, 0.95f), t, "TC");
                drawn++;
                return;
            }

            if (f.Bags && ent is SleepingBag)
            {
                Vector3 t = pos + Vector3.up * 0.35f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(0.25f, 0.55f, 1f), pos + Vector3.up * 0.08f, 0.35f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.5f, 0.75f, 1f), t, "bag");
                drawn++;
                return;
            }

            if (f.Defense && ent is GunTrap)
            {
                Vector3 t = pos + Vector3.up * 0.55f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(1f, 0.35f, 0.35f), pos + Vector3.up * 0.2f, 0.32f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.55f, 0.45f), t, "gun trap");
                drawn++;
                return;
            }

            if (f.Defense && ent is AutoTurret)
            {
                Vector3 t = pos + Vector3.up * 1.8f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(1f, 0.2f, 0.2f), pos + Vector3.up * 1.2f, 0.45f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.45f, 0.45f), t, "turret");
                drawn++;
                return;
            }

            if (f.Defense && ent is SamSite)
            {
                Vector3 t = pos + Vector3.up * 2.2f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(1f, 0.5f, 0.1f), pos + Vector3.up * 1.5f, 0.5f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.65f, 0.3f), t, "SAM");
                drawn++;
                return;
            }

            if (f.Loot && ent is LootableCorpse)
            {
                Vector3 t = pos + Vector3.up * 0.45f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(0.55f, 0.35f, 0.2f), pos + Vector3.up * 0.15f, 0.4f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.85f, 0.65f, 0.45f), t, "corpse");
                drawn++;
                return;
            }

            if (f.Loot && ent is DroppedItemContainer)
            {
                Vector3 t = pos + Vector3.up * 0.4f;
                viewer.SendConsoleCommand("ddraw.box", drawDur, new Color(0.9f, 0.75f, 0.2f), pos + Vector3.up * 0.2f, 0.35f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.9f, 0.4f), t, "backpack");
                drawn++;
                return;
            }

            if (f.Loot && ent is SupplyDrop)
            {
                Vector3 t = pos + Vector3.up * 0.6f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(0.2f, 1f, 0.9f), pos + Vector3.up * 0.25f, 1.2f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, Color.cyan, t, "airdrop");
                drawn++;
                return;
            }

            if (f.Npc && GstRadarIsWorldNpc(ent))
            {
                Vector3 t = pos + Vector3.up * 1.2f;
                string shortName = ent.ShortPrefabName ?? "npc";
                if (shortName.Length > 20) shortName = shortName.Substring(0, 20);
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(1f, 0.55f, 0.1f), pos + Vector3.up * 0.5f, 0.35f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.7f, 0.35f), t, shortName);
                drawn++;
                return;
            }

            if (f.Vehicles && (ent is BaseBoat || ent is RHIB || ent is RidableHorse || ent is Bike || ent is ModularCar || ent is BasicCar || ent is Minicopter || ent is AttackHelicopter || ent is HotAirBalloon))
            {
                Vector3 t = pos + Vector3.up * 1.5f;
                string vn = "vehicle";
                if (ent is Minicopter || ent is AttackHelicopter) vn = "heli";
                if (ent is RidableHorse) vn = "horse";
                if (ent is BaseBoat || ent is RHIB) vn = "boat";
                if (ent is HotAirBalloon) vn = "balloon";
                viewer.SendConsoleCommand("ddraw.box", drawDur, new Color(0.3f, 0.85f, 1f), pos + Vector3.up * 0.6f, 1.2f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.5f, 0.95f, 1f), t, vn);
                drawn++;
                return;
            }

            if (f.Military && (ent is PatrolHelicopter || ent is BradleyAPC || ent is CH47Helicopter))
            {
                Vector3 t = pos + Vector3.up * 3f;
                string lbl = ent is BradleyAPC ? "Bradley" : ent is CH47Helicopter ? "CH47" : "patrol heli";
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(1f, 0.15f, 0.15f), pos + Vector3.up * 2f, 2.5f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, Color.red, t, lbl);
                drawn++;
                return;
            }

            if (f.Loot && ent is HackableLockedCrate hlc)
            {
                string hlcLabel = hlc.IsBeingHacked() ? $"hackable [{Mathf.CeilToInt(HackableLockedCrate.requiredHackSeconds - hlc.hackSeconds)}s]" : "hackable crate";
                Vector3 t = pos + Vector3.up * 0.9f;
                viewer.SendConsoleCommand("ddraw.box", drawDur, new Color(1f, 0.1f, 0.1f), pos + Vector3.up * 0.5f, 0.65f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 0.45f, 0.45f), t, hlcLabel);
                drawn++;
                return;
            }

            if (f.Loot && ent is StorageContainer)
            {
                string sn = ent.ShortPrefabName ?? "box";
                if (sn.Length > 22) sn = sn.Substring(0, 22);
                Vector3 t = pos + Vector3.up * 0.55f;
                viewer.SendConsoleCommand("ddraw.box", drawDur, new Color(0.75f, 0.75f, 0.78f), pos + Vector3.up * 0.35f, 0.45f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(0.9f, 0.9f, 0.95f), t, sn);
                drawn++;
                return;
            }

            if (f.Drops && (ent is DroppedItem || ent is WorldItem))
            {
                if (drops >= _gstRadarMaxDropDraws) return;
                string label = "drop";
                if (ent is WorldItem wi && wi.item != null && wi.item.info != null)
                    label = wi.item.info.shortname ?? "drop";
                else if (ent is DroppedItem di && di.item != null && di.item.info != null)
                    label = di.item.info.shortname ?? "drop";
                if (label.Length > 18) label = label.Substring(0, 18);
                Vector3 t = pos + Vector3.up * 0.2f;
                viewer.SendConsoleCommand("ddraw.sphere", drawDur, new Color(1f, 0.95f, 0.3f), pos + Vector3.up * 0.05f, 0.12f);
                if (GstRadarVector3Finite(t))
                    viewer.SendConsoleCommand("ddraw.text", drawDur, new Color(1f, 1f, 0.55f), t, label);
                drops++;
                drawn++;
                return;
            }
        }

        #endregion

        #region WatchDog Telemetry

        private void SendWatchDogCombatEvent(string eventType, BasePlayer attacker, HitInfo hitInfo, BasePlayer victim)
        {
            double distance = 0;
            string hitBone = "";
            bool didHit = false;
            double[] projectileDir = null;
            float hitPosX = 0, hitPosY = 0, hitPosZ = 0;
            bool isHeadshot = false;
            bool isProjectile = hitInfo?.IsProjectile() ?? false;
            float projectileIntegrity = 0;
            float projectileTravelTime = 0;
            float projectileDistance = 0;
            float projectileVelocity = 0;

            if (hitInfo != null)
            {
                if (hitInfo.HitPositionWorld != Vector3.zero && attacker != null)
                {
                    distance = Vector3.Distance(attacker.eyes.position, hitInfo.HitPositionWorld);
                    Vector3 dir = (hitInfo.HitPositionWorld - attacker.eyes.position).normalized;
                    projectileDir = new[] { (double)dir.x, (double)dir.y, (double)dir.z };
                }
                hitPosX = hitInfo.HitPositionWorld.x;
                hitPosY = hitInfo.HitPositionWorld.y;
                hitPosZ = hitInfo.HitPositionWorld.z;

                if (hitInfo.HitBone != 0)
                    hitBone = StringPool.Get(hitInfo.HitBone);

                didHit = hitInfo.HitEntity != null;
                isHeadshot = hitInfo.boneArea == HitArea.Head;

                if (isProjectile)
                {
                    projectileIntegrity = hitInfo.ProjectileIntegrity;
                    if (hitInfo.ProjectilePrefab != null)
                        projectileVelocity = hitInfo.ProjectilePrefab.initialVelocity.magnitude;
                }
                if (hitInfo.ProjectileDistance > 0)
                    projectileDistance = hitInfo.ProjectileDistance;
                if (hitInfo.ProjectileTravelTime > 0)
                    projectileTravelTime = hitInfo.ProjectileTravelTime;
            }

            var attackerPos = attacker != null ? attacker.transform.position : Vector3.zero;
            var attackerVel = GetVelocityOrZero(attacker);
            var euler = attacker != null ? attacker.eyes.rotation.eulerAngles : Vector3.zero;

            var payload = new Dictionary<string, object>
            {
                { "Player", attacker?.UserIDString ?? "" },
                { "Event", eventType },
                { "Weapon", hitInfo?.Weapon?.ShortPrefabName ?? "" },
                { "Distance", distance },
                { "HitBone", hitBone },
                { "Hit", didHit },
                { "IsHeadshot", isHeadshot },
                { "IsProjectile", isProjectile },
                { "HitPosX", hitPosX },
                { "HitPosY", hitPosY },
                { "HitPosZ", hitPosZ },
                { "Damage", hitInfo != null ? hitInfo.damageTypes.Total() : 0f },
                { "DamageType", hitInfo?.damageTypes.GetMajorityDamageType().ToString() ?? "" },
                { "ViewPitch", (double)euler.x },
                { "ViewYaw", (double)euler.y },
                { "PosX", attackerPos.x },
                { "PosY", attackerPos.y },
                { "PosZ", attackerPos.z },
                { "VelocityX", attackerVel.x },
                { "VelocityY", attackerVel.y },
                { "VelocityZ", attackerVel.z },
                { "Speed", (float)attackerVel.magnitude },
                { "IsOnGround", attacker != null && attacker.IsOnGround() },
                { "IsSprinting", attacker != null && attacker.IsRunning() },
                { "IsDucked", attacker != null && attacker.IsDucked() },
                { "IsSwimming", attacker != null && attacker.IsSwimming() },
                { "IsFlying", attacker != null && attacker.IsFlying },
                { "IsMounted", attacker != null && attacker.isMounted },
                { "IsWounded", attacker != null && attacker.IsWounded() },
                { "Health", attacker != null ? attacker.health : 0f },
                { "HasBuildingPrivilege", attacker != null && attacker.IsBuildingAuthed() },
                { "Timestamp", (int)UnityEngine.Time.realtimeSinceStartup },
                { "ServerPort", _port }
            };

            if (projectileDir != null)
                payload["ProjectileDir"] = projectileDir;
            if (isProjectile)
            {
                payload["ProjectileIntegrity"] = projectileIntegrity;
                payload["ProjectileVelocity"] = projectileVelocity;
                if (projectileTravelTime > 0) payload["ProjectileTravelTime"] = projectileTravelTime;
                if (projectileDistance > 0) payload["ProjectileDistance"] = projectileDistance;
            }

            var ammoType = hitInfo?.Weapon?.GetItem()?.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType;
            if (ammoType != null) payload["Ammo"] = ammoType.shortname;
            if (hitInfo?.Weapon?.ShortPrefabName != null)
                payload["WeaponPrefab"] = hitInfo.Weapon.ShortPrefabName;

            if (victim != null && attacker != null)
            {
                Vector3 toTarget = (victim.eyes.position - attacker.eyes.position).normalized;
                Vector3 aimDir = attacker.eyes.HeadForward();
                float angle = Vector3.Angle(aimDir, toTarget);
                payload["AimAngleToTarget"] = (double)angle;
            }

            if (victim != null)
            {
                payload["Target"] = victim.UserIDString;
                payload["TargetPlayer"] = victim.UserIDString;
                payload["TargetPosX"] = victim.transform.position.x;
                payload["TargetPosY"] = victim.transform.position.y;
                payload["TargetPosZ"] = victim.transform.position.z;
                payload["TargetHealth"] = victim.health;
            }

            if (hitInfo?.HitMaterial != 0)
                payload["HitMaterial"] = StringPool.Get(hitInfo.HitMaterial);
            if (hitInfo?.boneArea != (HitArea)0)
                payload["HitPart"] = hitInfo.boneArea.ToString();

            if (eventType == "kill")
            {
                payload["IsKill"] = true;
                payload["TargetHealth"] = 0f;
            }

            if (victim == null && hitInfo?.HitEntity != null)
                payload["Target"] = hitInfo.HitEntity.ShortPrefabName ?? "";

            SendWatchDogPayload(payload);
        }

        private void SendWatchDogPayload(Dictionary<string, object> payload)
        {
            if (payload == null) return;
            if (!payload.ContainsKey("GameType"))
                payload["GameType"] = "rust";
            string json;
            try
            {
                json = JsonConvert.SerializeObject(payload, GstApiJsonSettings);
            }
            catch { return; }
            if (string.IsNullOrEmpty(json)) return;
            lock (_watchDogEventBuffer)
            {
                if (_watchDogBufferCap > 0 && _watchDogEventBuffer.Count >= _watchDogBufferCap) return;
                _watchDogEventBuffer.Add(json);
                if (_watchDogEventBuffer.Count >= _watchDogBatchMaxSize)
                    FlushWatchDogBatchInternal();
            }
        }

        private void FlushWatchDogBatch()
        {
            lock (_watchDogEventBuffer)
            {
                FlushWatchDogBatchInternal();
            }
        }

        private void CompleteWatchDogFlushPipeline()
        {
            _watchDogFlushInProgress = false;
            _watchDogFlushWatchdogTimer?.Destroy();
            _watchDogFlushWatchdogTimer = null;
            _watchDogRequestWatchdogTimer?.Destroy();
            _watchDogRequestWatchdogTimer = null;
        }

        private void ArmWatchDogFlushWatchdog(int generation)
        {
            // Armed only while a request is in flight (not during retry backoff).
            _watchDogFlushWatchdogTimer?.Destroy();
            _watchDogFlushWatchdogTimer = timer.Once(WatchDogFlushStallSeconds, () =>
            {
                if (!IsLoaded || !_watchDogFlushInProgress || generation != _watchDogFlushGeneration) return;
                PrintWarning($"[GST] WatchDog batch flush stalled after {WatchDogFlushStallSeconds:F0}s with no callback; resetting pipeline so new events can be sent.");
                _watchDogFlushGeneration++;
                CompleteWatchDogFlushPipeline();
                FlushWatchDogBatch();
            });
        }

        private void FlushWatchDogBatchInternal()
        {
            if (_watchDogFlushInProgress || _watchDogEventBuffer.Count == 0) return;
            int toTake = _watchDogBufferCap > 0 ? Math.Min(_watchDogEventBuffer.Count, _watchDogBufferCap) : _watchDogEventBuffer.Count;
            List<string> toSend = new List<string>(toTake);
            for (int i = 0; i < toTake; i++)
                toSend.Add(_watchDogEventBuffer[i]);
            _watchDogEventBuffer.RemoveRange(0, toTake);

            List<List<string>> chunks = new List<List<string>>();
            for (int start = 0; start < toSend.Count; start += _watchDogBatchMaxSize)
            {
                int end = Math.Min(start + _watchDogBatchMaxSize, toSend.Count);
                chunks.Add(toSend.GetRange(start, end - start));
            }
            if (chunks.Count == 0) return;
            _watchDogFlushInProgress = true;
            _watchDogFlushGeneration++;
            int generation = _watchDogFlushGeneration;
            SendWatchDogChunkAtIndex(chunks, 0, _watchDogApiUrl.TrimEnd('/') + "/api/Event/batch", 0, generation);
        }

        private void SendWatchDogChunkAtIndex(List<List<string>> chunks, int index, string url, int attempt, int generation)
        {
            if (generation != _watchDogFlushGeneration) return;
            if (index >= chunks.Count)
            {
                CompleteWatchDogFlushPipeline();
                ScheduleWatchDogDrainAfterFlush();
                return;
            }

            var chunk = chunks[index];
            string body = "{\"Events\":[" + string.Join(",", chunk) + "]}";

            if (body.Length <= 14)
            {
                SendWatchDogChunkAtIndex(chunks, index + 1, url, 0, generation);
                return;
            }

            ArmWatchDogFlushWatchdog(generation);

            float startedAt = UnityEngine.Time.realtimeSinceStartup;
            bool settled = false;
            var headers = new Dictionary<string, string>(_headers);

            Action<int, string> settle = (code, response) =>
            {
                if (settled) return;
                settled = true;
                _watchDogRequestWatchdogTimer?.Destroy();
                _watchDogRequestWatchdogTimer = null;
                _watchDogFlushWatchdogTimer?.Destroy();
                _watchDogFlushWatchdogTimer = null;

                if (!IsLoaded)
                {
                    CompleteWatchDogFlushPipeline();
                    return;
                }
                if (generation != _watchDogFlushGeneration)
                    return;

                float elapsed = UnityEngine.Time.realtimeSinceStartup - startedAt;
                response = response ?? string.Empty;
                bool ok = code >= 200 && code < 300;
                if (ok)
                {
                    if (_loggingEnabled && (attempt > 0 || elapsed >= 5f))
                        Puts($"[GST] WatchDog batch OK after {attempt} retry(s) in {elapsed:F1}s (chunk {index + 1}/{chunks.Count}).");
                    SendWatchDogChunkAtIndex(chunks, index + 1, url, 0, generation);
                    return;
                }

                if (attempt < _watchDogChunkMaxRetries)
                {
                    float delay = ComputeWatchDogRetryDelaySeconds(attempt);
                    if (_loggingEnabled)
                        Puts($"[GST] WatchDog batch HTTP {code} after {elapsed:F1}s; retry {attempt + 1}/{_watchDogChunkMaxRetries} in {delay:F1}s (chunk {index + 1}/{chunks.Count}, bytes={body.Length}).");
                    timer.Once(delay, () => SendWatchDogChunkAtIndex(chunks, index, url, attempt + 1, generation));
                    return;
                }

                LogError($"[GST] WatchDog batch failed after {_watchDogChunkMaxRetries} attempts (HTTP {code}, {elapsed:F1}s, bytes={body.Length}); dropping {chunk.Count} events.");
                PrependEventsToWatchDogBuffer(chunk);
                SendWatchDogChunkAtIndex(chunks, index + 1, url, 0, generation);
            };

            float softTimeout = timeout + WatchDogRequestGraceSeconds;
            _watchDogRequestWatchdogTimer?.Destroy();
            _watchDogRequestWatchdogTimer = timer.Once(softTimeout, () =>
            {
                if (settled || generation != _watchDogFlushGeneration) return;
                PrintWarning($"[GST] WatchDog batch request timed out after {softTimeout:F0}s with no webrequest callback (chunk {index + 1}/{chunks.Count}, bytes={body.Length}); treating as failure.");
                settle(0, "client-timeout");
            });

            try
            {
                webrequest.Enqueue(url, body, (code, response) => settle(code, response), this, RequestMethod.POST, headers, timeout);
            }
            catch (Exception ex)
            {
                LogError($"[GST] WatchDog batch Enqueue threw: {ex.Message}");
                settle(0, ex.Message);
            }
        }

        private float ComputeWatchDogRetryDelaySeconds(int attemptZeroBased)
        {
            double exp = Math.Pow(2, Math.Min(attemptZeroBased, 6));
            return Math.Min(45f, _watchDogRetryBaseDelaySeconds * (float)exp);
        }

        private void PrependEventsToWatchDogBuffer(List<string> chunk)
        {
            // Deliberately drop failed events instead of re-queuing to prevent memory spiral.
            // On a busy server with API issues, re-prepending causes unbounded memory growth.
            if (chunk == null || chunk.Count == 0) return;
            if (_loggingEnabled)
                Puts($"[GST] WatchDog dropping {chunk.Count} events after max retries to prevent memory buildup.");
        }
        private void ScheduleWatchDogDrainAfterFlush()
        {
            lock (_watchDogEventBuffer)
            {
                if (_watchDogEventBuffer.Count == 0) return;
            }
            timer.Once(0.35f, () => FlushWatchDogBatch());
        }

        private void PollWatchDogEnforcement(int attempt)
        {
            if (string.IsNullOrWhiteSpace(_watchDogApiUrl)) return;
            string url = _watchDogApiUrl.TrimEnd('/') + "/api/Event/enforcement?serverPort=" + _port;
            var headers = new Dictionary<string, string>(_headers);
            webrequest.Enqueue(url, string.Empty, (code, response) =>
            {
                if (!IsLoaded) return;
                response = response ?? string.Empty;
                if (code < 200 || code >= 300)
                {
                    if (attempt < 1 && ShouldRetryWatchDogEnforcementHttp(code))
                    {
                        if (_loggingEnabled)
                            Puts($"[GST] WatchDog enforcement HTTP {code}; retry in 2s.");
                        timer.Once(2f, () => PollWatchDogEnforcement(attempt + 1));
                    }
                    return;
                }
                if (string.IsNullOrEmpty(response) || response == "[]") return;
                try
                {
                    var actions = JsonConvert.DeserializeObject<WatchDogEnforcementAction[]>(response);
                    if (actions == null) return;
                    foreach (var action in actions)
                    {
                        string playerId = action.PlayerId ?? action.playerId ?? "";
                        if (string.IsNullOrEmpty(playerId) || HasWatchDogBypassById(playerId))
                        {
                            AckWatchDogEnforcement(action.Id);
                            continue;
                        }

                        string act = (action.Action ?? action.action ?? "").Trim().ToLowerInvariant();
                        string reason = action.Reason ?? action.reason ?? "";

                        switch (act)
                        {
                            case "kick":
                                ApplyWatchDogKick(playerId, reason);
                                break;
                            case "ban":
                                ApplyWatchDogBan(playerId, reason);
                                break;
                            case "flag":
                                if (_loggingEnabled)
                                    Puts($"[GST] Flag {playerId}: {reason}");
                                break;
                        }
                        AckWatchDogEnforcement(action.Id);
                    }
                }
                catch (Exception ex)
                {
                    if (_loggingEnabled) Puts($"[GST] Enforcement parse error: {ex.Message}");
                }
            }, this, RequestMethod.GET, headers, timeout);
        }

        private void AckWatchDogEnforcement(long enforcementId)
        {
            if (enforcementId <= 0 || !_headers.ContainsKey("ApiKey")) return;
            string ackUrl = _watchDogApiUrl.TrimEnd('/') + "/api/Event/enforcement/ack";
            string body = JsonConvert.SerializeObject(new { EnforcementId = enforcementId }, GstApiJsonSettings);
            var headers = new Dictionary<string, string>(_headers);
            webrequest.Enqueue(ackUrl, body, (code, ackResponse) =>
            {
                if (!IsLoaded) return;
                if (code < 200 || code >= 300)
                    PrintWarning($"[GST] WatchDog enforcement ack failed: {code} {ackResponse ?? string.Empty}");
            }, this, RequestMethod.POST, headers, timeout);
        }
        private static bool ShouldRetryWatchDogEnforcementHttp(int code)
        {
            if (code == 0) return true;
            return code == 408 || code == 425 || code == 429 || code == 500 || code == 502 || code == 503 || code == 504;
        }

        private void ApplyWatchDogKick(string playerId, string reason)
        {
            if (!ulong.TryParse(playerId, out ulong steamId)) return;
            var player = BasePlayer.FindByID(steamId) ?? BasePlayer.FindSleeping(steamId);
            if (player == null || !player.IsConnected) return;
            string msg = lang.GetMessage("YouAreBannedMessage", this, playerId);
            if (!string.IsNullOrEmpty(reason)) msg = msg.Replace("@reason", reason);
            else msg = msg.Replace("@reason", "Violation");
            player.Kick(msg);
            if (_loggingEnabled) Puts($"[GST] Kicked {playerId}: {reason}");
        }

        private void ApplyWatchDogBan(string playerId, string reason)
        {
            var ban = new AddBanClass { SteamId = playerId, Reason = reason ?? "WatchDog", ExpireTime = null, BannedBy = null };
            SubmitNewBan(ban, null, _ => { });
            if (_loggingEnabled) Puts($"[GST] Banned {playerId}: {reason}");
        }
        #endregion

        #region Stash Trap System

        private void EnsureAutoStashTraps()
        {
            if (!_enableAutoStashTraps || _autoStashMaxTraps <= 0) return;
            int need = _autoStashMaxTraps - _autoStashTrapIds.Count;
            if (need <= 0) return;

            int spawned = 0;
            for (int i = 0; i < need; i++)
            {
                var trap = SpawnOneAutoStashTrap();
                if (trap != null) spawned++;
            }

            if (_loggingEnabled && spawned > 0)
                Puts($"[GST] Spawned {spawned} automated stash traps.");
        }

        private StashContainer SpawnOneAutoStashTrap()
        {
            if (TerrainMeta.HeightMap == null || TerrainMeta.WaterMap == null) return null;
            const string prefabPath = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";
            const string sleepingBagPrefabPath = "assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab";

            for (int attempts = 0; attempts < 55; attempts++)
            {
                float x = UnityEngine.Random.Range(-TerrainMeta.Size.x * 0.45f, TerrainMeta.Size.x * 0.45f);
                float z = UnityEngine.Random.Range(-TerrainMeta.Size.z * 0.45f, TerrainMeta.Size.z * 0.45f);
                float y = TerrainMeta.HeightMap.GetHeight(new Vector3(x, 0f, z));
                Vector3 pos = new Vector3(x, y, z);
                if (!IsSafeAutoStashLocation(pos)) continue;
                Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                BaseEntity entity = GameManager.server.CreateEntity(prefabPath, pos, rot);
                StashContainer stash = entity as StashContainer;
                if (stash == null)
                {
                    entity?.Kill();
                    continue;
                }

                stash.Spawn();
                if (_autoStashTrapOwnerSteamId != 0UL)
                    stash.OwnerID = _autoStashTrapOwnerSteamId;
                stash.CancelInvoke(stash.Decay);
                stash.inventory?.Clear();
                if (_autoStashTrapDecoyLoot)
                    GstAutoStashPopulateDecoyLoot(stash);
                stash.SetHidden(true);
                if (stash.IsDestroyed)
                {
                    entity?.Kill();
                    continue;
                }

                ulong trapNetId = stash.net.ID.Value;
                var trapMeta = new AutoStashTrapMeta
                {
                    TrapId = trapNetId,
                    Position = stash.ServerPosition,
                    DecoyBagId = 0,
                    DecoyBagPosition = null,
                    CreatedRealtime = UnityEngine.Time.realtimeSinceStartup
                };
                _autoStashTrapIds[trapNetId] = trapMeta;

                if (_autoStashSpawnDecoyBags && UnityEngine.Random.Range(0, 100) < Mathf.Clamp(_autoStashDecoyBagSpawnChance, 0, 100))
                {
                    Vector3 offset = UnityEngine.Random.insideUnitSphere * 3.5f;
                    offset.y = 0f;
                    Vector3 bagPos = pos + offset;
                    float bagY = TerrainMeta.HeightMap.GetHeight(bagPos);
                    bagPos.y = bagY;
                    if (GstAutoStashGroundAcceptable(bagPos))
                    {
                        var bagEntity = GameManager.server.CreateEntity(sleepingBagPrefabPath, bagPos, Quaternion.identity) as SleepingBag;
                        if (bagEntity != null)
                        {
                            DestroyOnGroundMissing dmg = bagEntity.GetComponent<DestroyOnGroundMissing>();
                            if (dmg != null) UnityEngine.Object.DestroyImmediate(dmg);
                            GroundWatch gw = bagEntity.GetComponent<GroundWatch>();
                            if (gw != null) UnityEngine.Object.DestroyImmediate(gw);
                            if (_autoStashTrapOwnerSteamId != 0UL)
                                bagEntity.OwnerID = _autoStashTrapOwnerSteamId;
                            bagEntity.Spawn();
                            trapMeta.DecoyBagId = bagEntity.net.ID.Value;
                            trapMeta.DecoyBagPosition = bagEntity.ServerPosition;
                        }
                    }
                }

                if (stash.IsDestroyed)
                {
                    _autoStashTrapIds.Remove(trapNetId);
                    continue;
                }

                ulong finalTrapId = stash.net.ID.Value;
                trapMeta.Position = stash.ServerPosition;
                trapMeta.TrapId = finalTrapId;
                if (finalTrapId != trapNetId)
                {
                    _autoStashTrapIds.Remove(trapNetId);
                    _autoStashTrapIds[finalTrapId] = trapMeta;
                    if (_autoStashRecentTriggers.TryGetValue(trapNetId, out List<AutoStashTriggerRecord> trList))
                    {
                        _autoStashRecentTriggers[finalTrapId] = trList;
                        _autoStashRecentTriggers.Remove(trapNetId);
                    }
                }

                return stash;
            }
            return null;
        }

        private bool IsSafeAutoStashLocation(Vector3 pos)
        {
            if (!GstAutoStashGroundAcceptable(pos))
                return false;

            if (_autoStashPlacementBuildingRadius > 0f)
            {
                List<BuildingBlock> nearbyBuildings = Facepunch.Pool.Get<List<BuildingBlock>>();
                Vis.Entities(pos, _autoStashPlacementBuildingRadius, nearbyBuildings, LayerMask.GetMask("Construction"), QueryTriggerInteraction.Ignore);
                bool hasBuildings = nearbyBuildings.Count > 0;
                Facepunch.Pool.FreeUnmanaged(ref nearbyBuildings);
                if (hasBuildings) return false;
            }

            if (_autoStashPlacementMonumentRadius > 0f && TerrainMeta.Path?.Monuments != null)
            {
                foreach (var monument in TerrainMeta.Path.Monuments)
                {
                    if (monument == null) continue;
                    if (Vector3.Distance(pos, monument.transform.position) <= _autoStashPlacementMonumentRadius)
                        return false;
                }
            }

            return true;
        }
        private bool GstAutoStashGroundAcceptable(Vector3 pos)
        {
            if (TerrainMeta.HeightMap == null || TerrainMeta.WaterMap == null)
                return false;

            if (WaterLevel.Test(pos, false, false))
                return false;

            if (TerrainMeta.TopologyMap != null)
            {
                TerrainTopologyMap top = TerrainMeta.TopologyMap;
                if (top.GetTopology(pos, TerrainTopology.OCEAN) || top.GetTopology(pos, TerrainTopology.OFFSHORE))
                    return false;
            }

            if (GstAutoStashOnIceOrOffshoreDecor(pos))
                return false;

            if (GstAutoStashGroundTooJagged(pos, 2.6f, 2.1f))
                return false;

            float water = TerrainMeta.WaterMap.GetHeight(pos);
            float ground = TerrainMeta.HeightMap.GetHeight(pos);
            float dry = Mathf.Max(0.35f, _autoStashMinWaterClearance) + 1.15f;
            if (ground <= water + dry)
                return false;

            return true;
        }

        private static bool GstAutoStashGroundTooJagged(Vector3 pos, float radius, float maxDelta)
        {
            if (TerrainMeta.HeightMap == null) return false;
            float h0 = TerrainMeta.HeightMap.GetHeight(pos);
            float h1 = TerrainMeta.HeightMap.GetHeight(new Vector3(pos.x + radius, 0f, pos.z));
            float h2 = TerrainMeta.HeightMap.GetHeight(new Vector3(pos.x - radius, 0f, pos.z));
            float h3 = TerrainMeta.HeightMap.GetHeight(new Vector3(pos.x, 0f, pos.z + radius));
            float h4 = TerrainMeta.HeightMap.GetHeight(new Vector3(pos.x, 0f, pos.z - radius));
            float min = h0, max = h0;
            min = Mathf.Min(min, h1); max = Mathf.Max(max, h1);
            min = Mathf.Min(min, h2); max = Mathf.Max(max, h2);
            min = Mathf.Min(min, h3); max = Mathf.Max(max, h3);
            min = Mathf.Min(min, h4); max = Mathf.Max(max, h4);
            return (max - min) > maxDelta;
        }

        private static bool GstAutoStashOnIceOrOffshoreDecor(Vector3 pos)
        {
            List<Collider> col = Facepunch.Pool.Get<List<Collider>>();
            try
            {
                Vis.Colliders(pos, 1.35f, col, LayerMask.GetMask("World"), QueryTriggerInteraction.Ignore);
                for (int i = 0; i < col.Count; i++)
                {
                    Collider c = col[i];
                    if (c == null) continue;
                    string n = c.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    string l = n.ToLowerInvariant();
                    if (l.Contains("ice_lake") || l.Contains("ice_sheet") || l.Contains("iceberg") || l.Contains("ice_floe"))
                        return true;
                }
            }
            finally
            {
                Facepunch.Pool.FreeUnmanaged(ref col);
            }

            return false;
        }

        private void GstAutoStashPopulateDecoyLoot(StashContainer stash)
        {
            if (stash?.inventory == null) return;
            string[] decoys = { "wood", "stones", "cloth", "scrap", "lowgradefuel", "leather" };
            int n = UnityEngine.Random.Range(1, 4);
            for (int i = 0; i < n; i++)
            {
                string sn = decoys[UnityEngine.Random.Range(0, decoys.Length)];
                ItemDefinition def = ItemManager.FindItemDefinition(sn);
                if (def == null) continue;
                int maxStack = def.stackable > 0 ? def.stackable : 100;
                int hi = Mathf.Min(maxStack, UnityEngine.Random.Range(12, 121));
                int amt = UnityEngine.Random.Range(1, hi + 1);
                Item item = ItemManager.CreateByName(sn, Mathf.Clamp(amt, 1, maxStack), 0UL);
                if (item == null) continue;
                if (!item.MoveToContainer(stash.inventory))
                    item.Remove();
                else
                    item.MarkDirty();
            }
        }

        private void RegisterAutoStashViolation(BasePlayer player, StashContainer stash)
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            AutoStashTrapMeta trapMeta = null;
            _autoStashTrapIds.TryGetValue(stash.net.ID.Value, out trapMeta);
            if (!_autoStashViolationWindows.TryGetValue(player.userID, out Queue<float> entries))
            {
                entries = new Queue<float>();
                _autoStashViolationWindows[player.userID] = entries;
            }

            float windowSeconds = Mathf.Max(60f, _autoStashViolationWindowMinutes * 60f);
            while (entries.Count > 0 && (now - entries.Peek()) > windowSeconds)
                entries.Dequeue();

            entries.Enqueue(now);
            int count = entries.Count;
            RecordAutoStashTrigger(player, stash.net.ID.Value, now);

            SendStashTrapAdminChat(player, "AutoStashTrapRevealMessage", new Dictionary<string, string>
            {
                ["@count"] = count.ToString(),
                ["@threshold"] = _autoStashViolationThreshold.ToString()
            });

            if (_loggingEnabled)
                Puts($"[GST] Auto stash trap revealed by {player.UserIDString} at {stash.ServerPosition} ({count}/{_autoStashViolationThreshold})");

            SendWatchDogStashEvent(player, stash, trapMeta, count);

            timer.Once(Mathf.Max(1f, _autoStashDestroyRevealedAfterMinutes * 60f), () =>
            {
                if (stash != null && !stash.IsDestroyed) stash.Kill();
                if (trapMeta != null && trapMeta.DecoyBagId != 0)
                {
                    var bagEntity = BaseNetworkable.serverEntities.Find(new NetworkableId(trapMeta.DecoyBagId)) as BaseEntity;
                    if (bagEntity != null && !bagEntity.IsDestroyed) bagEntity.Kill();
                }
                if (_autoStashReplaceRevealedTrap) EnsureAutoStashTraps();
            });

            if (_autoStashLocalAutoBan && count >= _autoStashViolationThreshold)
            {
                timer.Once(Mathf.Max(1f, _autoStashBanDelaySeconds), () =>
                {
                    if (player == null || !player.userID.IsSteamId()) return;
                    if (HasWatchDogBypass(player)) return;

                    if (_autoStashViolationWindows.TryGetValue(player.userID, out Queue<float> recent))
                    {
                        while (recent.Count > 0 && (UnityEngine.Time.realtimeSinceStartup - recent.Peek()) > windowSeconds)
                            recent.Dequeue();
                        if (recent.Count < _autoStashViolationThreshold) return;
                    }

                    AddBanClass ban = new AddBanClass
                    {
                        SteamId = player.UserIDString,
                        Reason = _autoStashBanReason,
                        ExpireTime = null,
                        BannedBy = null
                    };
                    SubmitNewBan(ban, null, _ => { });
                });
            }
        }

        private void SendWatchDogStashEvent(BasePlayer player, StashContainer stash, AutoStashTrapMeta trapMeta, int violationCount)
        {
            if (string.IsNullOrWhiteSpace(_watchDogApiUrl) || player == null || stash == null) return;
            var pos = stash.ServerPosition;
            var payload = new Dictionary<string, object>
            {
                { "Player", player.UserIDString },
                { "Event", "stash_trap" },
                { "Detection", "esp_stash" },
                { "PosX", pos.x },
                { "PosY", pos.y },
                { "PosZ", pos.z },
                { "ViolationCountWindow", violationCount },
                { "ViolationThreshold", _autoStashViolationThreshold },
                { "WindowMinutes", _autoStashViolationWindowMinutes },
                { "TrapId", stash.net.ID.Value.ToString() },
                { "IsAutomatedTrap", trapMeta != null },
                { "Timestamp", (int)UnityEngine.Time.realtimeSinceStartup },
                { "ServerPort", _port }
            };

            if (trapMeta != null)
            {
                payload["TrapAgeSeconds"] = Mathf.Max(0f, UnityEngine.Time.realtimeSinceStartup - trapMeta.CreatedRealtime);
                payload["HasDecoyBag"] = trapMeta.DecoyBagId != 0;
                if (trapMeta.DecoyBagPosition.HasValue)
                {
                    payload["DecoyBagPosX"] = trapMeta.DecoyBagPosition.Value.x;
                    payload["DecoyBagPosY"] = trapMeta.DecoyBagPosition.Value.y;
                    payload["DecoyBagPosZ"] = trapMeta.DecoyBagPosition.Value.z;
                    payload["DecoyDistance"] = Vector3.Distance(pos, trapMeta.DecoyBagPosition.Value);
                }
            }

            string clanTag = GetClanTag(player);
            if (!string.IsNullOrEmpty(clanTag))
                payload["ClanTag"] = clanTag;
            if (player.currentTeam != 0)
                payload["TeamId"] = player.currentTeam.ToString();

            SendWatchDogPayload(payload);
        }

        private void RecordAutoStashTrigger(BasePlayer player, ulong trapId, float now)
        {
            if (!_autoStashRecentTriggers.TryGetValue(trapId, out List<AutoStashTriggerRecord> records))
            {
                records = new List<AutoStashTriggerRecord>();
                _autoStashRecentTriggers[trapId] = records;
            }
            if (records.Count >= 500)
                records.RemoveAt(0);
            records.Add(new AutoStashTriggerRecord
            {
                PlayerId = player.userID,
                TeamId = player.currentTeam,
                ClanTag = GetClanTag(player),
                Timestamp = now
            });
        }

        private bool ShouldSuppressAutoStashViolation(BasePlayer player, AutoStashTrapMeta trapMeta)
        {
            if (trapMeta == null) return false;
            if (!_autoStashRecentTriggers.TryGetValue(trapMeta.TrapId, out List<AutoStashTriggerRecord> records) || records.Count == 0)
                return false;

            float now = UnityEngine.Time.realtimeSinceStartup;
            records.RemoveAll(r => (now - r.Timestamp) > Mathf.Max(_autoStashIgnoreTeamWindowSeconds, _autoStashIgnoreClanWindowSeconds, 5f));

            if (_autoStashIgnoreTeamWindowEnabled && player.currentTeam != 0)
            {
                bool teamSuppressed = false;
                for (int i = 0; i < records.Count; i++)
                {
                    AutoStashTriggerRecord r = records[i];
                    if (r.TeamId != 0 && r.TeamId == player.currentTeam && (now - r.Timestamp) <= _autoStashIgnoreTeamWindowSeconds)
                    { teamSuppressed = true; break; }
                }
                if (teamSuppressed)
                {
                    SendStashTrapAdminChat(player, "AutoStashTrapSuppressedMessage");
                    if (_loggingEnabled)
                        Puts($"[GST] Suppressed stash violation (team window) for {player.UserIDString}");
                    return true;
                }
            }

            if (_autoStashIgnoreClanWindowEnabled)
            {
                string tag = GetClanTag(player);
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    bool clanSuppressed = false;
                    for (int i = 0; i < records.Count; i++)
                    {
                        AutoStashTriggerRecord r = records[i];
                        if (!string.IsNullOrWhiteSpace(r.ClanTag) && string.Equals(r.ClanTag, tag, StringComparison.OrdinalIgnoreCase) && (now - r.Timestamp) <= _autoStashIgnoreClanWindowSeconds)
                        { clanSuppressed = true; break; }
                    }
                    if (clanSuppressed)
                    {
                        SendStashTrapAdminChat(player, "AutoStashTrapSuppressedMessage");
                        if (_loggingEnabled)
                            Puts($"[GST] Suppressed stash violation (clan window) for {player.UserIDString}");
                        return true;
                    }
                }
            }

            return false;
        }

        private string GetClanTag(BasePlayer player)
        {
            if (player == null || Clans == null || !Clans.IsLoaded) return string.Empty;
            try
            {
                object result = Clans.Call("GetClanOf", player.UserIDString);
                return result?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        private static int GetPlayerPingMs(BasePlayer player)
        {
            var c = player?.net?.connection;
            if (c == null) return 0;
            int raw = Network.Net.sv.GetAveragePing(c);
            if (raw <= 0) return 0;
            return (raw + 1) >> 1;
        }

        #endregion

        #region Server Policy (High Ping, VPN, Bans)

        private void CheckHighPingPlayers()
        {
            if (!_enableHighPingKick || _highPingMax <= 0) return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                if (HasHighPingBypass(player)) continue;

                int ping = GetPlayerPingMs(player);
                if (ping <= _highPingMax) continue;

                string kickMessage = lang.GetMessage("HighPingKickMessage", this, player.UserIDString);
                kickMessage = kickMessage.Replace("@pingms", ping.ToString()).Replace("@maxPingms", _highPingMax.ToString());
                player.Kick(kickMessage);

                if (_loggingEnabled)
                    Puts($"[GST] High ping kick: {player.UserIDString} ({ping}>{_highPingMax})");
            }
        }

        // Plugin helper: check if API key is configured
        private bool HasApiKey()
        {
            return _headers.ContainsKey("ApiKey") && !string.IsNullOrEmpty(_headers["ApiKey"]);
        }

        // Plugin helper: extract IP address without port (works for both IPv4 and IPv6)
        private string ExtractIpWithoutPort(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return string.Empty;

            address = address.Trim();

            if (IPAddress.TryParse(address, out _))
                return address;

            int lastColon = address.LastIndexOf(':');
            if (lastColon > 0)
            {
                string possibleIp = address.Substring(0, lastColon);
                string possiblePort = address.Substring(lastColon + 1);

                if (int.TryParse(possiblePort, out _) && IPAddress.TryParse(possibleIp, out _))
                    return possibleIp;
            }

            return address;
        }

        // Plugin helper: queue online players report with debounce
        private void QueueOnlinePlayersReport()
        {
            if (!HasApiKey())
                return;

            if (_onlinePlayersReportQueued)
                return;

            _onlinePlayersReportQueued = true;

            _onlinePlayersReportTimer?.Destroy();
            _onlinePlayersReportTimer = timer.Once(_onlinePlayersReportDebounceSeconds, () =>
            {
                _onlinePlayersReportQueued = false;
                ReportOnlinePlayersToTracker();
            });
        }

        private void CheckForBans(Network.Connection connection, ulong ownerId)
        {
            if (!HasApiKey()) return;
            if (_gstServerUnauthorized) return;
            if (ownerId == 0) return;

            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            webrequest.Enqueue(ApiUrl($"api/Ban/GetActiveBans?steamId={ownerId}&serverPort={_port}"), string.Empty, (code, response) =>
            {
                if (!IsLoaded) return;
                response = response ?? string.Empty;
                if (code == 200 && !string.IsNullOrEmpty(response))
                {
                    var bans = JsonConvert.DeserializeObject<AddBanClass[]>(response);
                    if (bans != null && bans.Length > 0)
                    {
                        AddBanClass banSuccess = bans.FirstOrDefault();

                        string kickReason = lang.GetMessage("YouAreBannedMessage", this, connection.userid.ToString());
                        kickReason = kickReason.Replace("@reason", banSuccess.Reason);
                        Network.Net.sv.Kick(connection, kickReason, false);

                        _approvedCachedJoins[connection.userid.ToString()] = new ApprovedCachedPlayer() { reason = kickReason, timeOfAdd = DateTime.Now };
                    }
                }
                else if (code == 204)
                {
                }
                else if (code == 0)
                {
                    Puts($"[GST] {requestId} gameservertools.com is unreachable.");
                }
                else if (code == 401)
                {
                    Puts($"[GST] {requestId} GetActiveBans 401: Invalid API key.");
                }
                else if (code == 403)
                {
                    MarkGstUnauthorized($"GetActiveBans {requestId}");
                }
                else
                {
                    if (_loggingEnabled) Puts($"[GST] {requestId} GetActiveBans failed: {code} {response}");
                }
            }, this, Core.Libraries.RequestMethod.GET, headers, timeout);
        }

        private void MarkGstUnauthorized(string source)
        {
            _gstServerUnauthorized = true;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now - _lastGstAuthWarnTime < 600f) return;
            _lastGstAuthWarnTime = now;
            Puts($"[GST] Server IP/port not authorized ({source}). Register in GST dashboard. Ban/VPN checks paused until reload.");
        }

        // Helper: log API error with status code interpretation
        private void LogApiError(string requestId, string endpoint, int code, string response)
        {
            if (code == 403)
            {
                MarkGstUnauthorized($"{endpoint} {requestId}");
                return;
            }

            string message;
            switch (code)
            {
                case 0:
                    message = $"unreachable";
                    break;
                case 400:
                    message = $"400 - bad request: {response ?? ""}";
                    break;
                case 401:
                    message = "401 - invalid API key";
                    break;
                default:
                    message = $"{code} - {response ?? "unknown error"}";
                    break;
            }
            Puts($"[GST] {requestId} {endpoint} failed: {message}");
        }

        private void CheckForVpn(Network.Connection connection)
        {
            if (!HasApiKey()) return;
            if (_gstServerUnauthorized) return;
            if (connection == null) return;
            if (string.IsNullOrEmpty(connection.ipaddress)) return;

            string playerIp = ExtractIpWithoutPort(connection.ipaddress);
            if (string.IsNullOrEmpty(playerIp)) return;

            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            string body = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                { "PlayerId", connection.userid.ToString() },
                { "PlayerName", connection.username ?? "" },
                { "PlayerIp", playerIp },
                { "ServerPort", _port },
                { "GameType", "rust" }
            }, GstApiJsonSettings);

            webrequest.Enqueue(ApiUrl("api/Vpn/Check"), body, (code, response) =>
            {
                if (!IsLoaded) return;
                if (code == 200 && !string.IsNullOrEmpty(response))
                {
                    try
                    {
                        var result = JsonConvert.DeserializeObject<VpnCheckResponse>(response);
                        if (result != null && result.ShouldBlock)
                        {
                            string kickReason = lang.GetMessage("VpnKickMessage", this, connection.userid.ToString());
                            Network.Net.sv.Kick(connection, kickReason, false);

                            if (_loggingEnabled)
                                Puts($"GST: {requestId} connection rejected for {connection.userid}");
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintWarning($"GST: VPN check parse error: {ex.Message}");
                    }
                }
                else if (code != 200 && code != 0)
                {
                    LogApiError(requestId, "Vpn/Check", code, response);
                }
            }, this, RequestMethod.POST, headers, timeout);
        }
        private void SendReport(Dictionary<string, object> parameters)
        {
            if (!HasApiKey()) return;

            string body = JsonConvert.SerializeObject(parameters, GstApiJsonSettings);

            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            if (_loggingEnabled)
                Puts($"Sending report {requestId}...");

            webrequest.Enqueue(ApiUrl("api/Report/AddReport"), body, (code, response) =>
            {
                if (!IsLoaded) return;
                if (code != 200 && code != 0 && _loggingEnabled)
                    LogApiError(requestId, "AddReport", code, response);
            }, this, RequestMethod.POST, headers, timeout);
        }

        private void ClearAllPlayerConnections()
        {
            if (!HasApiKey()) return;

            string url = ApiUrl($"api/Stat/ServerStarted?serverPort={_port}");
            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            webrequest.Enqueue(url, string.Empty, (code, response) =>
            {
                if (!IsLoaded) return;
                if (code != 200 && code != 0 && _loggingEnabled)
                    LogApiError(requestId, "ServerStarted", code, response);
            }, this, Core.Libraries.RequestMethod.POST, headers, timeout);
        }

        private void SubmitNewBan(AddBanClass newBan, IPlayer admin, Action<AddBanClass> successCallBack)
        {
            if (!HasApiKey()) return;

            newBan.ServerPort = _port;
            string body = JsonConvert.SerializeObject(newBan, GstApiJsonSettings);
            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            webrequest.Enqueue(ApiUrl("api/Ban/AddBan"), body, (code, response) =>
            {
                if (!IsLoaded) return;
                if (code == 200)
                {
                    if (string.IsNullOrEmpty(response))
                    {
                        if (_loggingEnabled) Puts($"[GST] {requestId} AddBan 200 with empty body.");
                        return;
                    }
                    AddBanClass banSuccess = JsonConvert.DeserializeObject<AddBanClass>(response);

                    BasePlayer playerToKick = BasePlayer.Find(banSuccess.SteamId.ToString());
                    if (playerToKick != null && playerToKick.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("YouAreBannedMessage", this, playerToKick.UserIDString);
                        messageReplaced = messageReplaced.Replace("@reason", banSuccess.Reason);
                        playerToKick.Kick(messageReplaced);
                    }

                    string bannedDisplayName = (playerToKick != null ? playerToKick.displayName : null) ?? banSuccess.SteamId;
                    string broadCastMessage = lang.GetMessage("PlayerBannedBroadcastMsg", this, banSuccess.SteamId);
                    broadCastMessage = broadCastMessage.Replace("@user", bannedDisplayName);
                    Chat.Broadcast(broadCastMessage);

                    successCallBack.Invoke(banSuccess);
                }
                else if (code == 400)
                {
                    if (admin != null && admin.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("FailedToBan", this, admin.Id);
                        messageReplaced = messageReplaced.Replace("@response", response);
                        admin.Reply(messageReplaced);
                    }
                    Puts($"[GST] {requestId} AddBan 400 - bad request: {response}");
                }
                else if (code == 401)
                {
                    if (admin != null && admin.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("FailedToBanNoPermission", this, admin.Id);
                        admin.Reply(messageReplaced);
                    }
                    Puts($"[GST] {requestId} AddBan 401 - invalid API key");
                }
                else if (code == 403)
                {
                    if (admin != null && admin.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("FailedToBanNoPermission", this, admin.Id);
                        admin.Reply(messageReplaced);
                    }
                    Puts($"[GST] {requestId} AddBan 403 - server IP/port not authorized");
                }
                else
                {
                    if (admin != null && admin.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("FailedToBan", this, admin.Id);
                        messageReplaced = messageReplaced.Replace("@response", response);
                        admin.Reply(messageReplaced);
                    }

                    Puts($"[GST] {requestId} AddBan failed: {code} - {response}");
                }
            }, this, Core.Libraries.RequestMethod.PUT, headers);
        }

        private void UserLeftServer(Dictionary<string, object> parameters) {
            if (!HasApiKey()) return;

            string body = JsonConvert.SerializeObject(parameters, GstApiJsonSettings);

            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            webrequest.Enqueue(ApiUrl("api/Stat/UserLeftServer"), body, (code, response) =>
            {
                if (!IsLoaded) return;
                if (code != 200 && code != 0 && _loggingEnabled)
                    LogApiError(requestId, "UserLeftServer", code, response);
            }, this, Core.Libraries.RequestMethod.PUT, headers, timeout);
        }

        private void UserConnectedToServer(IPlayer player)
        {
            if (!HasApiKey()) return;

            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters.Add("SteamId", player.Id);
            parameters.Add("ServerPort", _port);

            string body = JsonConvert.SerializeObject(parameters, GstApiJsonSettings);

            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            webrequest.Enqueue(ApiUrl("api/Stat/UserJoinnedServer"), body, (code, response) =>
            {
                if (!IsLoaded) return;
                if (code != 200 && code != 0 && _loggingEnabled)
                    LogApiError(requestId, "UserJoinnedServer", code, response);
            }, this, Core.Libraries.RequestMethod.POST, headers, timeout);

            // Use debounced queue instead of immediate call
            QueueOnlinePlayersReport();
        }

        private void ReportOnlinePlayersToTracker()
        {
            if (!HasApiKey()) return;

            var players = new List<Dictionary<string, string>>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                players.Add(new Dictionary<string, string>
                {
                    { "SteamId", player.UserIDString },
                    { "PlayerName", player.displayName ?? player.UserIDString }
                });
            }

            var payload = new Dictionary<string, object>
            {
                { "ServerPort", _port },
                { "Players", players }
            };
            string body = JsonConvert.SerializeObject(payload, GstApiJsonSettings);
            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            webrequest.Enqueue(ApiUrl("api/PlayerTracking/ReportOnlinePlayers"), body, (code, response) =>
            {
                if (!IsLoaded) return;
                if (_loggingEnabled && code != 200 && code != 0)
                    LogApiError(requestId, "ReportOnlinePlayers", code, response);
            }, this, Core.Libraries.RequestMethod.POST, headers, timeout);
        }

        #endregion

        #region Discord Linking

        private void FetchDiscordLinkData(IPlayer player)
        {
            if (player == null)
                return;
            if (string.IsNullOrEmpty(player.Id))
            {
                player.Reply("Link check failed: no player ID.");
                return;
            }
            if (!_headers.ContainsKey("ApiKey") || string.IsNullOrEmpty(_headers["ApiKey"]))
            {
                player.Reply("Link check failed: server API key not configured. Ask the server owner to add APIKEY to the Game Server Tools config.");
                return;
            }

            string requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var headers = new Dictionary<string, string>(_headers);
            headers["X-GST-RequestId"] = requestId;

            string steamIdParam = Uri.EscapeDataString(player.Id);
            string url = ApiUrl($"api/Link/GetLinkData?steamId={steamIdParam}&serverPort={_port}");
            webrequest.Enqueue(url, string.Empty, (code, response) =>
            {
                if (!IsLoaded) return;
                response = response ?? string.Empty;
                if (code == 200)
                {
                    LinkModel linkData = JsonConvert.DeserializeObject<LinkModel>(response);
                    HandleDiscordLinkerConnect(linkData, player);
                }
                else if (code == 401)
                {
                    player.Reply("Link check failed: invalid API key. Ask the server owner to verify the API key in the GST dashboard.");
                    if (_loggingEnabled) Puts($"[GST] {requestId} GetLinkData 401: {response}");
                }
                else if (code == 403)
                {
                    player.Reply("Link check failed: server not authorized. The server owner should register this server in the GST dashboard.");
                    if (_loggingEnabled) Puts($"[GST] {requestId} GetLinkData 403: {response}");
                }
                else if (code == 400)
                {
                    player.Reply("Link check failed: bad request. The server owner should ensure the server is registered in the GST dashboard.");
                    if (_loggingEnabled) Puts($"[GST] {requestId} GetLinkData 400: {response}");
                }
                else
                {
                    player.Reply("Something went wrong with the link check. Please try again or contact the server admin.");
                    if (_loggingEnabled) Debug.LogError($"[GST] {requestId} GetLinkData error: {code} {response}");
                }
            }, this, RequestMethod.GET, headers, timeout);
        }

        private void HandleDiscordLinkerConnect(LinkModel linkData, IPlayer player)
        {
            string linkUrl = !string.IsNullOrEmpty(linkData.OrgUrl)
                ? $"https://discordlinker.com/{linkData.OrgUrl}"
                : "https://discordlinker.com";

            if (linkData.LinkId == 0)
            {
                string joinMessageNotLinked = lang.GetMessage("JoinMessageNotLinked", this, player.Id);
                if (joinMessageNotLinked.Contains("@linkUrl"))
                    joinMessageNotLinked = joinMessageNotLinked.Replace("@linkUrl", linkUrl);
                else
                    joinMessageNotLinked += $" Link your account at {linkUrl}";
                player.Reply($"{joinMessageNotLinked}");

                bool userHasGroup = player.BelongsToGroup(_linkedGroupName);
                if (userHasGroup)
                {
                    player.RemoveFromGroup(_linkedGroupName);
                    player.RemoveFromGroup(_nitroGroupName);
                    Interface.CallHook("OnDiscordUserUnLinked", player);
                }

                StartForceLinkTimer(linkData, player, linkUrl);
            }
            else if (linkData.LinkId != 0 && !linkData.InDiscord)
            {
                string joinMessageLeft = lang.GetMessage("JoinMessageLeft", this, player.Id);
                if (joinMessageLeft.Contains("@linkUrl"))
                    joinMessageLeft = joinMessageLeft.Replace("@linkUrl", linkUrl);
                else
                    joinMessageLeft += $" Rejoin and link your account at {linkUrl}";
                player.Reply($"{joinMessageLeft}");

                bool userHasGroup = player.BelongsToGroup(_linkedGroupName);
                if (userHasGroup)
                {
                    player.RemoveFromGroup(_linkedGroupName);
                    player.RemoveFromGroup(_nitroGroupName);
                    Interface.CallHook("OnDiscordUserUnLinked", player);
                }

                StartForceLinkTimer(linkData, player, linkUrl);
            }
            else
            {
                CancelForceLinkTimer(player.Id);

                string joinMessageLinked = lang.GetMessage("JoinMessageLinked", this, player.Id);
                player.Reply($"{joinMessageLinked}");
                bool userHasGroup = player.BelongsToGroup(_linkedGroupName);
                if (!userHasGroup)
                {
                    player.AddToGroup(_linkedGroupName);
                    Interface.CallHook("OnDiscordUserAddedToGroup", player);
                }

                bool userHasNitro = player.BelongsToGroup(_nitroGroupName);
                if (userHasNitro && !linkData.NitroBoosted)
                {
                    string noLongerBoostingMessage = lang.GetMessage("NitroLostMessage", this, player.Id);

                    player.Reply(noLongerBoostingMessage);

                    player.RemoveFromGroup(_nitroGroupName);
                    Interface.CallHook("OnNitroBoostRemove", player);
                }
                else if (!userHasNitro && linkData.NitroBoosted)
                {
                    string nowBoostingMessage = lang.GetMessage("NitroGainMessage", this, player.Id);
                    player.Reply(nowBoostingMessage);

                    player.AddToGroup(_nitroGroupName);
                    Interface.CallHook("OnNitroBoost", player);
                }
            }
        }

        private void StartForceLinkTimer(LinkModel linkData, IPlayer player, string linkUrl)
        {
            if (!linkData.ForceLinkEnabled || linkData.ForceLinkKickSeconds <= 0)
                return;

            CancelForceLinkTimer(player.Id);

            int kickSeconds = linkData.ForceLinkKickSeconds;
            string playerId = player.Id;

            string warnMsg = lang.GetMessage("ForceLinkWarning", this, playerId);
            if (warnMsg.Contains("@seconds"))
                warnMsg = warnMsg.Replace("@seconds", kickSeconds.ToString());
            if (warnMsg.Contains("@linkUrl"))
                warnMsg = warnMsg.Replace("@linkUrl", linkUrl);
            else
                warnMsg += $" {linkUrl}";
            player.Reply(warnMsg);

            _forceLinkTimers[playerId] = timer.Once(kickSeconds, () =>
            {
                _forceLinkTimers.Remove(playerId);
                IPlayer p = players.FindPlayerById(playerId);
                if (p == null || !p.IsConnected)
                    return;
                if (p.BelongsToGroup(_linkedGroupName))
                    return;

                string kickMsg = lang.GetMessage("ForceLinkKick", this, playerId);
                if (kickMsg.Contains("@linkUrl"))
                    kickMsg = kickMsg.Replace("@linkUrl", linkUrl);
                else
                    kickMsg += $" {linkUrl}";
                p.Kick(kickMsg);
            });
        }

        private void CancelForceLinkTimer(string playerId)
        {
            if (_forceLinkTimers.TryGetValue(playerId, out Timer existing))
            {
                existing.Destroy();
                _forceLinkTimers.Remove(playerId);
            }
        }

        private ReportType GetTypeIdFromType(string type)
        {
            switch (type)
            {
                case "abusive":
                    return ReportType.Abusive;

                case "cheat":
                    return ReportType.Cheat;

                case "spam":
                    return ReportType.Spam;

                case "name":
                    return ReportType.Name;
            }
            return ReportType.Abusive;
        }

        private void MessageAllPlayers(string message)
        {
            foreach (IPlayer player in players.Connected)
            {
                player.Message(message);
            }
        }

        private bool TryGetBanExpiry(
          string arg,
          int n,
          IPlayer iplayer,
          out long expiry,
          out string durationSuffix)
        {
            expiry = GetTimestamp(arg, n, -1L);
            durationSuffix = (string)null;
            int current = Epoch.Current;
            if (expiry > 0L && expiry <= (long)current)
            {
                string messageReplaced = lang.GetMessage("PastExpireDate", this, iplayer.Id);
                iplayer.Reply(messageReplaced);
                return false;
            }
            durationSuffix = expiry > 0L ? " for " + (expiry - (long)current).FormatSecondsLong() : "";
            return true;
        }

        private long GetTimestamp(string arg, int iArg, long def = 0)
        {
            string s = arg == string.Empty ? null : arg;
            if (s == null)
                return def;
            int num = 3600;
            if (s.Length > 1 && char.IsLetter(s[s.Length - 1]))
            {
                switch (s[s.Length - 1])
                {
                    case 'M':
                        num = 2592000;
                        break;

                    case 'Y':
                        num = 31536000;
                        break;

                    case 'd':
                        num = 86400;
                        break;

                    case 'h':
                        num = 3600;
                        break;

                    case 'm':
                        num = 60;
                        break;

                    case 's':
                        num = 1;
                        break;

                    case 'w':
                        num = 604800;
                        break;
                }

                s = s.Substring(0, s.Length - 1);
            }
            long result;
            if (!long.TryParse(s, out result))
                return def;
            if (result > 0L && result <= 315360000L)
                result = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + result * (long)num;
            return result;
        }

        #endregion

        #region Chat & Console Commands

        [Command("Near")]
        private void FindNear(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer.IsServer)
            {
                if (args == null || args.Length < 1) { iplayer.Reply("Usage: Near <player>"); return; }
                BasePlayer target = BasePlayer.Find(args[0]);
                if (target != null)
                {
                    IOrderedEnumerable<BasePlayer> orderList = BasePlayer.activePlayerList.OrderBy(p => Vector3.Distance(p.transform.position, target.transform.position));

                    int i = 0;
                    List<ulong> discordId = new List<ulong>();
                    foreach (BasePlayer player in orderList)
                    {
                        if (player.userID == target.userID)
                        {
                            continue;
                        }

                        if (i >= 15)
                        {
                            break;
                        }
                        discordId.Add(player.userID);
                        i++;
                    }
                    iplayer.Reply(String.Join(",", discordId.ToArray()));
                }
            }
            else
            {
                iplayer.Reply("Not server");
            }
        }

        [Command("checklink")]
        private void CheckLinkCommand(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.IsServer) return;
            if (args == null || args.Length < 1) return;

            BasePlayer player = BasePlayer.Find(args[0]);
            if (player == null || !player.IsConnected)
            {
                return;
            }

            if (args.Length > 1)
            {
                if (_showClaimMessage)
                {
                    string messageReplaced = lang.GetMessage("BroadcastMessage", this, iplayer.Id);
                    string newMessage = messageReplaced.Replace("@userName", player.displayName);
                    MessageAllPlayers(newMessage);
                }
            }

            FetchDiscordLinkData(player.IPlayer);
        }

        [Command("link", "nitro", "linked")]
        private void NitroCheck(IPlayer iplayer, string command, string[] args)
        {
            CachedPlayer data;
            if (_cachedJoins.TryGetValue(iplayer.Id, out data))
            {
                TimeSpan timeSinceAdd = DateTime.UtcNow - data.TimeOfAdd;
                if (timeSinceAdd.TotalMinutes < 1)
                {
                    string message = lang.GetMessage("RecentlyUsedThisCommand", this, iplayer.Id);
                    iplayer.Reply(message);
                    return;
                }

                _cachedJoins.Remove(iplayer.Id);
            }

            _cachedJoins[iplayer.Id] = new CachedPlayer();

            string messageCheckingAccount = lang.GetMessage("CheckingAccount", this, iplayer.Id);
            iplayer.Reply(messageCheckingAccount);

            FetchDiscordLinkData(iplayer);
        }

        private void OverrideBanCommand(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.IsAdmin && !iplayer.IsServer)
            {
                return;
            }
            if (args.Length < 1)
            {
                string messageReplaced = lang.GetMessage("InvalidArguments", this, iplayer.Id);
                iplayer.Reply(messageReplaced);
                return;
            }

            BasePlayer player = args[0] == null ? null : BasePlayer.Find(args[0]);
            if (player == null || player.net == null || player.net.connection == null)
            {
                string messageReplaced = lang.GetMessage("NoPlayerFound", this, iplayer.Id);
                iplayer.Reply(messageReplaced);
            }
            else
            {
                string noReasonString = lang.GetMessage("NoReason", this, iplayer.Id);

                string notes = args.Length < 2 ? noReasonString : args[1];

                long expiry;
                string durationSuffix;
               
                if (!TryGetBanExpiry(args.Length < 3 ? string.Empty : args[2], 2, iplayer, out expiry, out durationSuffix))
                    return;
                
                AddBanClass ban = new AddBanClass();
                
                ban.SteamId = player.UserIDString;
                ban.Reason = notes;
                ban.BannedBy = iplayer.IsServer ? null : iplayer.Id;
                
                if (expiry > 0L)
                {
                    
                    ban.ExpireTime = DateTimeOffset.FromUnixTimeSeconds(expiry).DateTime;
                }
                else
                    ban.ExpireTime = null;
                
                SubmitNewBan(ban, iplayer, (sumbitedBan) =>
                {
                    if (iplayer != null && iplayer.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("BanSentSuccess", this, iplayer.Id);
                        messageReplaced = messageReplaced.Replace("@niceBanId", sumbitedBan.NiceBanId);
                        iplayer.Reply(messageReplaced);
                    }
                    if (player.IsConnected && player.net != null && player.net.connection != null
                        && player.net.connection.ownerid != 0UL
                        && (long)player.net.connection.ownerid != (long)player.net.connection.userid
                        && _loggingEnabled)
                    {
                        Puts($"[GST] Ban: Steam ownerid ({player.net.connection.ownerid}) != userid ({player.net.connection.userid}) for {player.displayName}. Rust has no family share; no secondary ban issued.");
                    }
                });
            }
        }

        [Command("AddAllBans")]
        private void AddAllBans(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer != null && iplayer.IsAdmin)
            {
                List<ServerUsers.User> list = ServerUsers.GetAll(ServerUsers.UserGroup.Banned).ToList<ServerUsers.User>();
                float time = 0.0f;
                int i = 1;
                foreach (ServerUsers.User user in list)
                {
                    timer.Once(time, () =>
                    {
                        if (iplayer != null && iplayer.IsConnected)
                        {
                            string messageReplaced = lang.GetMessage("MassBanMessage", this, iplayer.Id);
                            messageReplaced = messageReplaced.Replace("@user", user.steamid.ToString());
                            messageReplaced = messageReplaced.Replace("@listCount", list.Count.ToString());
                            messageReplaced = messageReplaced.Replace("@index", i.ToString());
                            iplayer.Reply(messageReplaced);
                        }

                        i++;
                        AddBanClass ban = new AddBanClass();
                        ban.SteamId = user.steamid.ToString();
                        ban.Reason = user.notes;
                        ban.BannedBy = null;
                        ban.DontSendRconKick = true;
                        if (user.expiry > 0L)
                        {
                            long minsToAdd = (user.expiry - (long)Facepunch.Math.Epoch.Current);
                            minsToAdd = minsToAdd / 60;
                            ban.ExpireTime = DateTime.UtcNow.AddMinutes(minsToAdd);
                        }
                        else
                            ban.ExpireTime = null;

                        SubmitNewBan(ban, iplayer, (newban) => { });
                    });
                    time = time + 0.5f;
                }
            }
        }

        [Command("gst.traps.status")]
        private void TrapStatusCommand(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer == null || (!iplayer.IsAdmin && !iplayer.IsServer)) return;
            iplayer.Reply($"Auto traps active: {_autoStashTrapIds.Count}/{_autoStashMaxTraps} | enabled={_enableAutoStashTraps}");
        }

        [Command("gst.traps.draw")]
        private void TrapDrawCommand(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer == null || (!iplayer.IsAdmin && !iplayer.IsServer)) return;
            if (!ulong.TryParse(iplayer.Id, out ulong id)) return;
            BasePlayer admin = BasePlayer.FindByID(id);
            if (admin == null || !admin.IsConnected) return;

            int duration = 60;
            if (args != null && args.Length > 0) int.TryParse(args[0], out duration);
            if (duration <= 0) duration = 60;

            int drawn = 0;
            foreach (var kv in _autoStashTrapIds)
            {
                var meta = kv.Value;
                if (meta == null) continue;
                DrawTrapDebug(admin, meta, duration);
                drawn++;
            }
            iplayer.Reply($"Drawn {drawn} trap markers for {duration}s.");
        }

        [Command("gst.traps.respawn")]
        private void TrapRespawnCommand(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer == null || (!iplayer.IsAdmin && !iplayer.IsServer)) return;
            EnsureAutoStashTraps();
            iplayer.Reply("Trap ensure run completed.");
        }

        [Command("radar")]
        private void GstRadarCommand(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer == null || iplayer.IsServer) return;
            if (!_gstRadarEnabled)
            {
                iplayer.Reply("Radar is disabled in the server config.");
                return;
            }
            if (!GstRadarCanUse(iplayer))
            {
                iplayer.Reply(lang.GetMessage("GstRadarNoAccess", this, iplayer.Id));
                return;
            }
            if (!ulong.TryParse(iplayer.Id, out ulong uid)) return;
            if (args == null) args = Array.Empty<string>();

            if (args.Length == 0 || (args.Length == 1 && (args[0].Equals("toggle", StringComparison.OrdinalIgnoreCase))))
            {
                if (_gstRadarUsers.Contains(uid))
                {
                    // Toggle cooldown: prevent spam
                    float nowTick = UnityEngine.Time.realtimeSinceStartup;
                    if (_gstRadarToggleLastTime.TryGetValue(uid, out float lastToggle) && nowTick - lastToggle < _gstRadarToggleCooldownSeconds)
                    {
                        iplayer.Reply(lang.GetMessage("GstRadarToggleCooldown", this, iplayer.Id));
                        return;
                    }
                    _gstRadarToggleLastTime[uid] = nowTick;
                    _gstRadarUsers.Remove(uid);
                    iplayer.Reply(lang.GetMessage("GstRadarOff", this, iplayer.Id));
                }
                else
                {
                    // Radar user cap: prevent too many concurrent admins hammering ddraw
                    if (_gstRadarUsers.Count >= _gstRadarMaxUsers)
                    {
                        iplayer.Reply(lang.GetMessage("GstRadarFull", this, iplayer.Id));
                        return;
                    }
                    float nowTick = UnityEngine.Time.realtimeSinceStartup;
                    if (_gstRadarToggleLastTime.TryGetValue(uid, out float lastToggle) && nowTick - lastToggle < _gstRadarToggleCooldownSeconds)
                    {
                        iplayer.Reply(lang.GetMessage("GstRadarToggleCooldown", this, iplayer.Id));
                        return;
                    }
                    _gstRadarToggleLastTime[uid] = nowTick;
                    _gstRadarSessionStart[uid] = nowTick;
                    _gstRadarUsers.Add(uid);
                    iplayer.Reply(lang.GetMessage("GstRadarOn", this, iplayer.Id));
                }
                return;
            }

            string a0 = args[0].Trim();
            string a0l = a0.ToLowerInvariant();
            if (a0l == "ui" || a0l == "menu" || a0l == "help" || a0l == "?")
            {
                GstRadarReplyUi(iplayer, uid);
                return;
            }

            if (args.Length == 1 && (a0l == "reset" || a0l == "default"))
            {
                _gstRadarFilters.Remove(uid);
                iplayer.Reply(lang.GetMessage("GstRadarFiltersReset", this, iplayer.Id));
                return;
            }

            if (args.Length == 2 && a0l == "all")
            {
                bool? allOn = GstRadarParseBoolArg(args[1]);
                if (allOn == true)
                {
                    _gstRadarFilters.Remove(uid);
                    iplayer.Reply(lang.GetMessage("GstRadarAllOn", this, iplayer.Id));
                    return;
                }
                if (allOn == false)
                {
                    _gstRadarFilters[uid] = new GstRadarFilters();
                    iplayer.Reply(lang.GetMessage("GstRadarAllOff", this, iplayer.Id));
                    return;
                }
            }

            if (args.Length == 3 && args[0].Equals("filter", StringComparison.OrdinalIgnoreCase))
            {
                if (GstRadarTryApplyFilter(iplayer.Id, uid, args[1], args[2], out string err))
                {
                    iplayer.Reply(string.Format(lang.GetMessage("GstRadarFilterOk", this, iplayer.Id), err));
                    return;
                }
                iplayer.Reply(string.IsNullOrEmpty(err) ? lang.GetMessage("GstRadarUsage", this, iplayer.Id) : err);
                return;
            }

            if (args.Length == 2)
            {
                if (GstRadarTryApplyFilter(iplayer.Id, uid, args[0], args[1], out string detail))
                {
                    iplayer.Reply(string.Format(lang.GetMessage("GstRadarFilterOk", this, iplayer.Id), detail));
                    return;
                }
                iplayer.Reply(string.IsNullOrEmpty(detail) ? lang.GetMessage("GstRadarUsage", this, iplayer.Id) : detail);
                return;
            }

            if (args.Length == 1)
            {
                if (a0l == "on" || a0l == "1" || a0l == "true")
                {
                    if (!_gstRadarUsers.Contains(uid))
                    {
                        if (_gstRadarUsers.Count >= _gstRadarMaxUsers)
                        {
                            iplayer.Reply(lang.GetMessage("GstRadarFull", this, iplayer.Id));
                            return;
                        }
                        float nowOn = UnityEngine.Time.realtimeSinceStartup;
                        if (_gstRadarToggleLastTime.TryGetValue(uid, out float lt) && nowOn - lt < _gstRadarToggleCooldownSeconds)
                        {
                            iplayer.Reply(lang.GetMessage("GstRadarToggleCooldown", this, iplayer.Id));
                            return;
                        }
                        _gstRadarToggleLastTime[uid] = nowOn;
                        _gstRadarSessionStart[uid] = nowOn;
                        _gstRadarUsers.Add(uid);
                    }
                    iplayer.Reply(lang.GetMessage("GstRadarOn", this, iplayer.Id));
                    return;
                }
                if (a0l == "off" || a0l == "0" || a0l == "false")
                {
                    _gstRadarUsers.Remove(uid);
                    iplayer.Reply(lang.GetMessage("GstRadarOff", this, iplayer.Id));
                    return;
                }
            }

            iplayer.Reply(lang.GetMessage("GstRadarUsage", this, iplayer.Id));
        }

        private GstRadarFilters GstRadarGetFiltersForUser(ulong uid)
        {
            if (_gstRadarFilters.TryGetValue(uid, out GstRadarFilters f))
                return f;
            return GstRadarFilters.DefaultOn;
        }

        private static bool? GstRadarParseBoolArg(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return null;
            string s = arg.Trim().ToLowerInvariant();
            if (s == "on" || s == "1" || s == "true" || s == "yes") return true;
            if (s == "off" || s == "0" || s == "false" || s == "no") return false;
            return null;
        }

        private static string GstRadarNormalizeCategoryKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string k = raw.Trim().ToLowerInvariant();
            switch (k)
            {
                case "cupboard":
                case "cup":
                case "toolcupboard": return "tc";
                case "shoot":
                case "projectiles":
                case "tracers": return "shots";
                case "fieldtraps": return "traps";
                case "turret":
                case "turrets":
                case "sam":
                case "guntrap": return "defense";
                case "boxes":
                case "storage":
                case "crates": return "loot";
                case "ship":
                case "plane":
                case "cargo":
                case "cargoship":
                case "cargoplane": return "events";
                case "pickups":
                case "collectibles":
                case "collectible":
                case "ore": return "resource";
                default: return k;
            }
        }

        private bool GstRadarTryApplyFilter(string langUserId, ulong uid, string rawKey, string rawVal, out string detailOrError)
        {
            detailOrError = null;
            bool? on = GstRadarParseBoolArg(rawVal);
            if (on == null)
            {
                detailOrError = lang.GetMessage("GstRadarBadBool", this, langUserId);
                return false;
            }
            string key = GstRadarNormalizeCategoryKey(rawKey);
            GstRadarFilters f = GstRadarGetFiltersForUser(uid);
            if (!GstRadarApplyCategory(ref f, key, on.Value))
            {
                detailOrError = lang.GetMessage("GstRadarUnknownCategory", this, langUserId);
                return false;
            }
            _gstRadarFilters[uid] = f;
            detailOrError = $"{key}={(on.Value ? "on" : "off")}";
            return true;
        }

        private static bool GstRadarApplyCategory(ref GstRadarFilters f, string key, bool on)
        {
            switch (key)
            {
                case "world": f.World = on; return true;
                case "players": f.Players = on; return true;
                case "sleepers": f.Sleepers = on; return true;
                case "shots": f.Shots = on; return true;
                case "stashes": f.Stashes = on; return true;
                case "tc": f.ToolCupboard = on; return true;
                case "bags": f.Bags = on; return true;
                case "defense": f.Defense = on; return true;
                case "traps": f.FieldTraps = on; return true;
                case "loot": f.Loot = on; return true;
                case "npc": f.Npc = on; return true;
                case "vehicles": f.Vehicles = on; return true;
                case "military": f.Military = on; return true;
                case "drops": f.Drops = on; return true;
                case "resource": f.Resource = on; return true;
                case "events": f.WorldEvents = on; return true;
                case "cctv":
                case "drone": f.Cctv = on; return true;
                case "mlrs": f.Mlrs = on; return true;
                default: return false;
            }
        }

        private void GstRadarReplyUi(IPlayer iplayer, ulong uid)
        {
            GstRadarFilters f = GstRadarGetFiltersForUser(uid);
            string B(bool x) => x ? "on" : "off";
            iplayer.Reply(lang.GetMessage("GstRadarUiHeader", this, iplayer.Id));
            iplayer.Reply($"world={B(f.World)} players={B(f.Players)} sleepers={B(f.Sleepers)} shots={B(f.Shots)} stashes={B(f.Stashes)} tc={B(f.ToolCupboard)} bags={B(f.Bags)} defense={B(f.Defense)} traps={B(f.FieldTraps)} loot={B(f.Loot)} npc={B(f.Npc)} vehicles={B(f.Vehicles)} military={B(f.Military)} drops={B(f.Drops)} resource={B(f.Resource)} events={B(f.WorldEvents)} cctv={B(f.Cctv)} mlrs={B(f.Mlrs)}");
            iplayer.Reply(lang.GetMessage("GstRadarUiFooter", this, iplayer.Id));
        }

        private bool GstRadarCanUse(IPlayer iplayer)
        {
            if (iplayer == null || iplayer.IsServer) return false;
            return iplayer.IsConnected && (iplayer.IsAdmin || permission.UserHasPermission(iplayer.Id, PermRadar));
        }

        private void DrawTrapDebug(BasePlayer admin, AutoStashTrapMeta meta, int duration)
        {
            if (admin == null || meta == null) return;
            admin.SendConsoleCommand("ddraw.sphere", duration, Color.yellow, meta.Position, 0.55f);
            admin.SendConsoleCommand("ddraw.text", duration, Color.white, meta.Position + new Vector3(0f, 0.8f, 0f), $"GST Trap {meta.TrapId}");
            if (meta.DecoyBagPosition.HasValue)
            {
                admin.SendConsoleCommand("ddraw.sphere", duration, Color.cyan, meta.DecoyBagPosition.Value, 0.6f);
                admin.SendConsoleCommand("ddraw.arrow", duration, Color.gray, meta.Position, meta.DecoyBagPosition.Value, 0.3f);
            }
        }

        #endregion

        #region Models & Enums

        private enum ReportType
        {
            Abusive = 1,
            Cheat = 2,
            Spam = 3,
            Name = 4
        }

        public class LinkModel
        {
            public int LinkId { get; set; }
            public long SteamId { get; set; }
            public long DiscordId { get; set; }
            public int OrgId { get; set; }
            public DateTime LinkDate { get; set; }
            public bool InDiscord { get; set; }
            public bool ClaimedRewards { get; set; }
            public int? NitroBoostId { get; set; }
            public bool NitroBoosted { get; set; }
            public string OrgUrl { get; set; }
            public bool ForceLinkEnabled { get; set; }
            public int ForceLinkKickSeconds { get; set; }
        }

        private class CachedPlayer
        {
            public CachedPlayer()
            {
                TimeOfAdd = DateTime.UtcNow;
            }

            public DateTime TimeOfAdd { get; }
        }

        private class AddBanClass
        {
            public string SteamId { get; set; }
            public string Reason { get; set; }
            public DateTime? ExpireTime { get; set; }
            public int OrgId { get; set; }
            public int? ServerId { get; set; }
            public string BannedBy { get; set; }
            public string NiceBanId { get; set; }
            public int ServerPort { get; set; }
            public bool DontSendRconKick { get; set; }
        }

        private class ApprovedCachedPlayer
        {
            public string reason { get; set; }
            public DateTime timeOfAdd { get; set; }
        }

        private class VpnCheckResponse
        {
            public bool ShouldBlock { get; set; }
            public bool IsVpn { get; set; }
            public bool IsHosting { get; set; }
            public int AbuseScore { get; set; }
            public string CountryCode { get; set; }
            public string Isp { get; set; }
        }

        private class WatchDogEnforcementAction
        {
            public long Id { get; set; }
            public string PlayerId { get; set; }
            public string Action { get; set; }
            public string Reason { get; set; }
            public double Confidence { get; set; }
            public string playerId { get => PlayerId; set => PlayerId = value; }
            public string action { get => Action; set => Action = value; }
            public string reason { get => Reason; set => Reason = value; }
        }

        private class AutoStashTrapMeta
        {
            public ulong TrapId { get; set; }
            public Vector3 Position { get; set; }
            public ulong DecoyBagId { get; set; }
            public Vector3? DecoyBagPosition { get; set; }
            public float CreatedRealtime { get; set; }
        }

        private class AutoStashTriggerRecord
        {
            public ulong PlayerId { get; set; }
            public ulong TeamId { get; set; }
            public string ClanTag { get; set; }
            public float Timestamp { get; set; }
        }

        #endregion

        #region Default Config & Lang

        protected override void LoadDefaultConfig()
        {
            // General
            Config["General", "APIKEY"]                       = "";
            Config["General", "DebugLoggingEnabled"]          = false;
            Config["General", "DisableBanCtrl"]               = false;
            Config["General", "DisplayMessageOnClaimRewards"] = true;
            Config["General", "OxideGroupNameForLinked"]      = "DiscordLinked";
            Config["General", "OxideGroupNameForNitro"]       = "NitroBoosted";

            // AntiFlood
            Config["AntiFlood", "ChatEnabled"]           = false;
            Config["AntiFlood", "ChatCooldownSeconds"]   = 1.5;
            Config["AntiFlood", "CommandEnabled"]        = false;
            Config["AntiFlood", "CommandCooldownSeconds"] = 1.0;

            // Radar
            Config["Radar", "Enabled"]               = false;
            Config["Radar", "MaxDistance"]           = 200;
            Config["Radar", "IntervalSeconds"]       = 0.5;
            Config["Radar", "LookLineLength"]        = 15;
            Config["Radar", "ProjectileLineLength"]  = 100;
            Config["Radar", "ProjectileSeconds"]     = 1.25;
            Config["Radar", "ProjectileCap"]         = 400;
            Config["Radar", "ShowWorldEntities"]     = true;
            Config["Radar", "MaxEntityDraws"]        = 220;
            Config["Radar", "MaxDropDraws"]          = 24;
            Config["Radar", "MaxPlayerDraws"]        = 60;
            Config["Radar", "MaxSleeperDraws"]       = 30;
            Config["Radar", "MaxUsers"]              = 8;
            Config["Radar", "ToggleCooldownSeconds"] = 2.0;
            Config["Radar", "AutoOffSeconds"]        = 120.0;
            Config["Radar", "DrawBudgetPerTick"]     = 800;
            Config["Radar", "HighlightGstTraps"]     = true;

            // StashTraps
            Config["StashTraps", "Enabled"]                    = false;
            Config["StashTraps", "MaxActive"]                  = 200;
            Config["StashTraps", "EnsureIntervalSeconds"]      = 120;
            Config["StashTraps", "ViolationThreshold"]         = 3;
            Config["StashTraps", "ViolationWindowMinutes"]     = 30;
            Config["StashTraps", "BanDelaySeconds"]            = 10;
            Config["StashTraps", "BanReason"]                  = "Cheat Detected (ESP stash trap)";
            Config["StashTraps", "DestroyRevealedAfterMinutes"]= 5;
            Config["StashTraps", "ReplaceRevealedTrap"]        = true;
            Config["StashTraps", "SpawnDecoyBags"]             = true;
            Config["StashTraps", "DecoyBagSpawnChance"]        = 55;
            Config["StashTraps", "DecoyLoot"]                  = true;
            Config["StashTraps", "OwnerSteamId"]               = "";
            Config["StashTraps", "PlacementBuildingRadius"]    = 20;
            Config["StashTraps", "PlacementMonumentRadius"]    = 35;
            Config["StashTraps", "MinWaterClearance"]          = 0.75;
            Config["StashTraps", "IgnoreTeamWindowEnabled"]    = true;
            Config["StashTraps", "IgnoreTeamWindowSeconds"]    = 20;
            Config["StashTraps", "IgnoreClanWindowEnabled"]    = true;
            Config["StashTraps", "IgnoreClanWindowSeconds"]    = 25;
            Config["StashTraps", "LocalAutoBan"]               = true;

            // WatchDog
            Config["WatchDog", "BatchIntervalSeconds"]  = 5;
            Config["WatchDog", "BatchMaxSize"]          = 200;
            Config["WatchDog", "BufferCap"]             = 2000;
            Config["WatchDog", "ChunkMaxRetries"]       = 12;
            Config["WatchDog", "RetryBaseDelaySeconds"] = 2;

            // HighPing
            Config["HighPing", "Enabled"]              = false;
            Config["HighPing", "MaxPing"]              = 350;
            Config["HighPing", "CheckIntervalSeconds"] = 60;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BroadcastMessage"] = "@userName has just claimed some really cool reward for linking his account. Head to discordlinker.com to claim yours",
                ["JoinMessageLinked"] = "Your account is linked!",
                ["JoinMessageNotLinked"] = "Your account is NOT linked! Link your account at @linkUrl",
                ["JoinMessageLeft"] = "You left our discord :( Rejoin and link your account at @linkUrl",
                ["RecentlyUsedThisCommand"] = "You recently used this command",
                ["CheckingAccount"] = "Checking your account...",
                ["AccountLinkSuccess"] = "Account Link successful!",
                ["AlreadyLinked"] = "Your account has not been linked! Link your account at discordlinker.com/",
                ["UnknownError"] = "Unknown error",
                ["NitroLostMessage"] = "You are no longer nitro boosting this amazing server!",
                ["NitroGainMessage"] = "You are now nitro boosting this amazing server. You will now get this awesome thing!",
                ["YouAreBannedMessage"] = "You are banned! Reason: @reason Head to discord.com/yourserver to appeal this ban",
                ["VpnKickMessage"] = "Kicked due to VPN, please disable.",
                ["FailedToBan"] = "Failed to ban user! @response",
                ["FailedToBanNoPermission"] = "Failed to ban user! You do not have permission on gameservertools.com to ban users! Please contact your server owner to get this resolved",
                ["PastExpireDate"] = "Expiry time is in the past",
                ["NoPlayerFound"] = "Player not found",
                ["NoReason"] = "No Reason Given",
                ["BanSentSuccess"] = "Ban successfully sent to Game Server Tools. Ban ID: @niceBanId",
                ["MassBanMessage"] = "@index/@listCount Sending ban for @user",
                ["PlayerBannedBroadcastMsg"] = "Player @user has been banned.",
                ["InvalidArguments"] = "Please provide a user to ban",
                ["ForceLinkWarning"] = "WARNING: You must link your account within @seconds seconds or you will be kicked! Link at @linkUrl",
                ["ForceLinkKick"] = "You were kicked for not linking your account. Link at @linkUrl and rejoin.",
                ["HighPingKickMessage"] = "Kicked for high ping (@pingms). Max allowed is @maxPingms.",
                ["AutoStashTrapRevealMessage"] = "WatchDog notice: @offender (@steamid) — stash trap trigger @count/@threshold.",
                ["ChatFloodBlockedMessage"] = "Slow down: chat cooldown active for @seconds sec.",
                ["CommandFloodBlockedMessage"] = "Slow down: command cooldown active for @seconds sec.",
                ["AutoStashTrapSuppressedMessage"] = "WatchDog notice: stash signal suppressed for @offender (@steamid) (team/clan trigger window).",
                ["GstRadarOn"] = "GST radar on (ddraw). Shoot tracers off by default (/radar shots on). Layers: /radar ui — /radar off to stop.",
                ["GstRadarOff"] = "GST radar off.",
                ["GstRadarNoAccess"] = "You cannot use GST radar.",
                ["GstRadarUsage"] = "Usage: /radar | on | off | toggle | ui | reset | all on|off | <layer> on|off | filter <layer> on|off — see /radar ui for layer names.",
                ["GstRadarUiHeader"] = "GST radar layers (per-player). Toggle example: /radar loot off",
                ["GstRadarUiFooter"] = "Master world scan: /radar world off — reset defaults: /radar reset",
                ["GstRadarFiltersReset"] = "Radar layers reset to defaults (players + sleepers only).",
                ["GstRadarAllOn"] = "All radar layers enabled.",
                ["GstRadarAllOff"] = "All radar layers disabled (radar still runs; turn off with /radar off).",
                ["GstRadarFilterOk"] = "Radar: {0}",
                ["GstRadarBadBool"] = "Use on or off (or 1/0, true/false).",
                ["GstRadarUnknownCategory"] = "Unknown radar layer. Use /radar ui for names.",
                ["GstRadarFull"] = "Radar is already active for the maximum number of admins. Ask one of them to turn it off first.",
                ["GstRadarToggleCooldown"] = "You toggled radar recently. Please wait a moment before toggling again.",
                ["GstRadarAutoOff"] = "GST radar auto-disabled after session timeout."
            }, this);
        }

        #endregion
    }
}
