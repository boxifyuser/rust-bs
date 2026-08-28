using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Monument Loot Boot", "Server", "1.1.0")]
    [Description("Keeps default monument loot respawn settings. Does not force-fill after players loot crates.")]
    public class MonumentLootBoot : RustPlugin
    {
        private Configuration _config;

        private class Configuration
        {
            // Only enables normal Rust respawn — never bypasses loot timer while playing
            public bool ApplySpawnSettingsOnBoot = true;

            // Optional one-shot after wipe/new map only (empty world). OFF by default.
            public bool ForceFillOnlyOnWipe = false;
            public float ForceFillDelaySecondsOnWipe = 120f;

            public bool LogActions = true;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>() ?? new Configuration();
            }
            catch
            {
                PrintWarning("Config invalido, usando padroes.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void OnServerInitialized()
        {
            // Apply default spawn settings only — do NOT fill all spawns on every boot,
            // so looted monument crates keep their normal respawn timers.
            if (_config.ApplySpawnSettingsOnBoot)
                ApplyDefaultSpawnSettings("boot");
        }

        private void OnNewSave(string strFilename)
        {
            if (_config.LogActions)
                Puts($"New save/wipe ({strFilename}). Aplicando settings de respawn padrao.");

            if (_config.ApplySpawnSettingsOnBoot)
                ApplyDefaultSpawnSettings("wipe");

            // Only if explicitly enabled: fill empty world once after wipe (not after looting)
            if (_config.ForceFillOnlyOnWipe)
            {
                var delay = Math.Max(30f, _config.ForceFillDelaySecondsOnWipe);
                timer.Once(delay, () =>
                {
                    Run("spawn.fill_groups");
                    Run("spawn.fill_individuals");
                    Run("spawn.fill_populations");
                    if (_config.LogActions)
                        Puts("Force fill apos wipe (somente mundo novo). Timers de caixas lootadas voltam ao padrao depois.");
                });
            }
        }

        /// <summary>
        /// Admin-only emergency fill. Does not run automatically after players loot.
        /// </summary>
        [ConsoleCommand("monumentloot.fill")]
        private void CmdFill(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;

            Run("spawn.fill_groups");
            Run("spawn.fill_individuals");
            Run("spawn.fill_populations");
            arg.ReplyWith("MonumentLootBoot: fill manual (nao afeta o timer normal do que for lootado depois).");
            if (_config.LogActions)
                Puts("Force fill manual pelo console.");
        }

        [ConsoleCommand("monumentloot.settings")]
        private void CmdSettings(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;

            ApplyDefaultSpawnSettings("manual");
            arg.ReplyWith("MonumentLootBoot: spawn settings padrao aplicadas (respawn com timer vanilla).");
        }

        private void ApplyDefaultSpawnSettings(string reason)
        {
            // Enable natural respawn with Facepunch defaults (rate/density 1 / 0.5)
            Run("spawn.respawn_groups true");
            Run("spawn.respawn_populations true");
            Run("spawn.respawn_individuals true");
            Run("spawn.max_density 1");
            Run("spawn.min_density 0.5");
            Run("spawn.max_rate 1");
            Run("spawn.min_rate 0.5");

            if (_config.LogActions)
                Puts($"Spawn settings padrao aplicadas ({reason}). Caixas lootadas respawnam no tempo default do jogo.");
        }

        private static void Run(string command)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), command);
        }
    }
}
