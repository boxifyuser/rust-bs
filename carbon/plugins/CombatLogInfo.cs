using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CombatLog Info", "Pho3niX90", "1.0.20")]
    [Description("Collects combat log entries, and submits them to SA for analysis.")]
    class CombatLogInfo : RustPlugin
    {
        const bool debug = false;
        [PluginReference] Plugin ServerArmour;

        HashSet<string> usedHashes = new HashSet<string>();

        // Simple stats
        DateTime logsSince = DateTime.Now;
        int totalLogs = 0;
        int totalLogsUploaded = 0;
        int pendingGeneration = 0;
        int failedUploads = 0;
        //

        void OnServerInitialized(bool first)
        {
            Server.Command($"server.combatlogsize {Math.Max(config.logSize, 30)}");
            if (config.advancedLogs)
                cmd.AddConsoleCommand("combatlog", this, printCombatLog);
        }

        void OnServerSave()
        {
            this.cleanupHashes();
        }

        #region log generation

        private void GenCombatLog(BasePlayer forPlayer)
        {
            if (forPlayer.IsNpc || !forPlayer.userID.IsSteamId())
                return;

            var cLog = CombatLog.Get(forPlayer.userID);
            var snapshot = new List<CombatLog.Event>();
            if (cLog != null)
            {
                foreach (CombatLog.Event evt in cLog)
                    snapshot.Add(evt);
            }

            string steamId = forPlayer.UserIDString;
            string displayName = forPlayer.displayName;
            float delay = ConVar.Server.combatlogdelay + 1f;
            pendingGeneration++;

            // Puts($"CombatLogInfo: scheduled {snapshot.Count} events for {displayName} ({steamId}) in {delay:0.#}s");

            timer.Once(delay, () =>
            {
                var player = BasePlayer.FindByID(Convert.ToUInt64(steamId))
                    ?? BasePlayer.FindSleeping(Convert.ToUInt64(steamId));
                if (player == null)
                {
                    // Puts($"CombatLogInfo: could not resolve player {steamId} ({displayName}) at upload time — skipped {snapshot.Count} events");
                    pendingGeneration--;
                    return;
                }

                AddEntries(player, snapshot);
            });
        }
        #endregion

        #region commands
        [ConsoleCommand("clog.stats")]
        void printStats(ConsoleSystem.Arg arg)
        {
            arg.ReplyWith($"Stats since {logsSince}\n\nTotal Logs: {totalLogs}\nTotal Logs Uploaded: {totalLogsUploaded}\nFailed Logs Upload: {failedUploads}\nPending Log Generation: {pendingGeneration}");
        }

        //[ConsoleCommand("combatlog")]
        bool printCombatLog(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (!player)
                return false;

            TextTable textTable = new TextTable();

            textTable.AddColumns("time", "attacker", "id", "target", "id", "weapon", "ammo", "area", "distance", "old_hp", "new_hp", "info", "hits", "integrity", "travel", "mismatch", "desync");


            foreach (CombatLog.Event evt in CombatLog.Get(player.userID))
            {
                var entry = CLogEntry.from(player, evt);
                if (entry != null)
                {
                    textTable.AddRow((Time.realtimeSinceStartup - entry.EventTime).ToString("0.0s"),
                    GetUsername(player, entry.AttackerSteamId), entry.AttackerSteamId,
                    GetUsername(player, entry.TargetSteamId), entry.TargetSteamId,
                    entry.Weapon, entry.Ammo,
                    entry.Area, entry.Distance.ToString("0.0m"),
                    entry.HealthOld.ToString("0.0"), entry.HealthNew.ToString("0.0"), entry.EventInfo, 
                    entry.ProjectileTravelTime.ToString("0.0s"),
                    entry.ProjectileTrajectoryMismatch.ToString("0.0m"),
                    entry.Desync.ToString());
                }
            }
            player.ConsoleMessage("---------- COMBATLOG  ----------\n");
            player.ConsoleMessage(textTable.ToString());
            return true;
        }

        string GetUsername(BasePlayer player, string id)
        {
            if (id == player.UserIDString || id == player.net.ID.Value.ToString())
                return "you";

            BasePlayer fp = null;

            try
            {
                fp = BasePlayer.allPlayerList.FirstOrDefault(x => x.UserIDString == id || x.net.ID.Value.ToString() == id);
            }
            catch (Exception e) { }

            return fp?.displayName ?? "N/A";

        }
        #endregion

        #region hooks
        void processingTask()
        {
            var task = Task.Run(() => { });
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null)
                return null;

            var initiator = info?.InitiatorPlayer;

            // Upload both perspectives on PvP deaths so killer and victim rows are captured.
            if (!player.IsNpc && player.userID.IsSteamId())
                GenCombatLog(player);

            if (initiator != null && initiator.userID.IsSteamId() && initiator != player)
                GenCombatLog(initiator);

            return null;
        }

        private void UploadLogs(Dictionary<int, CLogEntry> logs, BasePlayer forPlayer = null)
        {
            pendingGeneration--;
            totalLogs += logs.Count;

            if (logs.Count > 0)
            {
                string payload = JsonConvert.SerializeObject(logs.Values);
                Puts($"CombatLogInfo: uploading {logs.Count} entries for {forPlayer?.displayName} ({forPlayer?.UserIDString})");

                if (!DeliverCombatPayload(payload))
                {
                    Puts($"CombatLogInfo: ServerArmour did not accept payload for {forPlayer?.UserIDString}");
                    failedUploads += logs.Count;
                    return;
                }

                totalLogsUploaded += logs.Count;
            }
            else if (forPlayer != null)
                Puts($"CombatLogInfo: No new entries to upload for {forPlayer.displayName} ({forPlayer.UserIDString})");
            else
                LogDebug($"Nothing to upload");
        }

        #endregion

        #region helpers
        private bool DeliverCombatPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return false;

                try
                {
                    object result = (ServerArmour != null && ServerArmour.IsLoaded)
                        ? ServerArmour?.Call("API_UploadCombatEntries", payload)
                        : Interface.Call("API_UploadCombatEntries", payload);

                    if (TryParseQueuedResult(result, out int queued))
                    {
                        // Puts($"CombatLogInfo: delivered via API_UploadCombatEntries (queue depth {queued})");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Puts($"CombatLogInfo: API_UploadCombatEntries via Call failed: {ex.Message}");
                }

            // Puts("CombatLogInfo: ServerArmour.Call did not acknowledge payload (no queue depth returned)");
            return false;
        }

        private bool TryParseQueuedResult(object result, out int queued)
        {
            queued = 0;
            if (result is int i)
            {
                queued = i;
                return i > 0;
            }
            if (result is long l)
            {
                queued = (int)l;
                return l > 0;
            }
            return false;
        }

        private void AddEntries(BasePlayer forPlayer, List<CombatLog.Event> cLog)
        {
            Dictionary<int, CLogEntry> logEntries = new Dictionary<int, CLogEntry>();
            int eventCount = cLog?.Count ?? 0;
            string dedupScope = forPlayer.UserIDString;
            foreach (CombatLog.Event evt in cLog)
            {
                var hash = evt.GetHashCode();
                string dedupKey = $"{dedupScope}:{hash}";
                if (!logEntries.ContainsKey(hash) && !usedHashes.Contains(dedupKey))
                {
                    var entry = CreateEntry(forPlayer, evt);
                    if (entry != null)
                    {
                        usedHashes.Add(dedupKey);
                        logEntries.Add(hash, entry);
                    }
                }
            }
            if (logEntries.Count == 0 && eventCount > 0)
                Puts($"CombatLogInfo: All {eventCount} events skipped for {forPlayer.displayName} ({forPlayer.UserIDString}) — dedup or filter.");
            UploadLogs(logEntries, forPlayer);
            logEntries.Clear();
        }

        void cleanupHashes()
        {
            if (usedHashes.Count > 100000)
                usedHashes = new HashSet<string>(usedHashes.Skip(usedHashes.Count - 1000));
        }

        private CLogEntry CreateEntry(BasePlayer forPlayer, CombatLog.Event evt)
        {
            if ((forPlayer is NPCPlayer || forPlayer.IsNpc || !forPlayer.userID.IsSteamId()) && !evt.target_id.IsSteamId())
            {
                // LogDebug($"Event = {evt.GetHashCode()} bot attacker and bot death. Skipped");
                return null;
            }
            totalLogs++;
            // LogDebug($"Event = {evt.GetHashCode()} saved.");
            return CLogEntry.from(forPlayer, evt);
        }

        private void LogDebug(string txt)
        {
            if (debug) Puts($"DEBUG: {txt}");
        }

        static private PInfo UintFind(ulong id)
        {
            BasePlayer player = null;
            try
            {
                if (id.IsSteamId())
                    player = BasePlayer.allPlayerList.FirstOrDefault(x => x.userID == id);
                if (player == null)
                    player = BasePlayer.allPlayerList.FirstOrDefault(x => x.net.ID.Value == id);
            }
            catch (Exception e)
            {
            }
            return player != null ? new PInfo { Name = player.displayName, SteamId = player.UserIDString } : new PInfo { Name = id.ToString(), SteamId = id.ToString() };
        }

        /**
         * This avoids scientific notations
         */
        static public void RoundOrLimitFloat(ref float value) => value = (value > 1000000) ? value % 1000000 : value;

        public class PInfo
        {
            public string Name;
            public string SteamId;
        }
        #endregion

        public class CLogEntry
        {
            public int EventHash;
            public float EventTime;
            public string AttackerSteamId;
            public string TargetSteamId;
            public string Weapon;
            public string Ammo;
            public string Area;
            public float Distance;
            public float HealthOld;
            public float HealthNew;
            public string EventInfo;
            public int ProjectileHits;
            public float ProjectileIntegrity;
            public float ProjectileTravelTime;
            public float ProjectileTrajectoryMismatch;
            public int Desync;
            public static CLogEntry from(BasePlayer forPlayer, CombatLog.Event evt)
            {
                var pInfo = new PInfo { Name = forPlayer.displayName, SteamId = forPlayer.UserIDString };
                var attacker = evt.attacker == "you" ? pInfo : UintFind(evt.attacker_id);
                var target = evt.target == "you" ? pInfo : UintFind(evt.target_id);
                RoundOrLimitFloat(ref evt.health_new);
                RoundOrLimitFloat(ref evt.health_old);
                return new CLogEntry
                {
                    EventHash = evt.GetHashCode(),
                    AttackerSteamId = attacker.SteamId,
                    TargetSteamId = target.SteamId,
                    Ammo = evt.ammo,
                    Area = HitAreaUtil.Format(evt.area).ToLower(),
                    Distance = evt.distance,
                    EventInfo = evt.info,
                    EventTime = evt.time,
                    HealthNew = evt.health_new,
                    HealthOld = evt.health_old,
                    Desync = evt.desync,
                    ProjectileHits = evt.proj_hits,
                    ProjectileIntegrity = evt.proj_integrity,
                    ProjectileTrajectoryMismatch = evt.proj_mismatch,
                    ProjectileTravelTime = evt.proj_travel,
                    Weapon = evt.weapon
                };
            }
        }


        #region Configuration
        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Advanced Logs")]
            public bool advancedLogs = true;

            [JsonProperty(PropertyName = "Logs Size (default: 30, recommended: 45)")]
            public int logSize = 45;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError("Your config seems to be corrupted. Will load defaults.");
                LoadDefaultConfig();
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}