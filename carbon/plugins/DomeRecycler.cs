using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Dome Recycler", "Server", "1.5.1")]
    [Description("Spawns a recycler at the Dome with custom efficiency")]
    public class DomeRecycler : RustPlugin
    {
        private const string Prefab = "assets/bundled/prefabs/static/recycler_static.prefab";

        private Configuration _config;
        private readonly HashSet<ulong> _managedIds = new HashSet<ulong>();

        private class Configuration
        {
            // false = encontra o monumento Dome automaticamente
            public bool UseFixedPosition = false;
            public float PosX = 0f;
            public float PosY = 0f;
            public float PosZ = 0f;
            public float Yaw = 180f;

            // Offset relativo ao monumento Dome (local space do prefab)
            public float OffsetX = 0f;
            public float OffsetY = 0.2f;
            public float OffsetZ = 0f;

            public float CleanupRadius = 80f;
            public float BootDelaySeconds = 5f;
            public float WipeDelaySeconds = 40f;
            public bool LogActions = true;
            public float RecycleEfficiency = 0.4f;
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
                LoadDefaultConfig();
            }

            _config.RecycleEfficiency = Mathf.Clamp(_config.RecycleEfficiency, 0.05f, 1f);
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void OnServerInitialized()
        {
            timer.Once(Mathf.Max(3f, _config.BootDelaySeconds), () => EnsureRecycler(true));
        }

        private void OnNewSave(string filename)
        {
            // Novo mapa: volta a buscar o Dome automaticamente
            _config.UseFixedPosition = false;
            SaveConfig();
            timer.Once(Mathf.Max(10f, _config.WipeDelaySeconds), () => EnsureRecycler(true));
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var rec = entity as Recycler;
            if (rec == null || rec.IsDestroyed || rec.net == null)
                return;

            NextTick(() =>
            {
                if (rec == null || rec.IsDestroyed || rec.net == null)
                    return;
                if (IsNearManagedPosition(rec.transform.position))
                    _managedIds.Add(rec.net.ID.Value);
            });
        }

        private void OnRecyclerThinkSpeed(Recycler recycler, ref float efficiency, ref float duration)
        {
            if (recycler == null || recycler.IsDestroyed)
                return;
            if (!IsManaged(recycler))
                return;

            efficiency = _config.RecycleEfficiency;
        }

        [ConsoleCommand("domerecycler.spawn")]
        private void CmdSpawn(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;

            EnsureRecycler(true);
            var p = GetTargetPos();
            arg.ReplyWith($"DomeRecycler: pos ({p.x:0.##}, {p.y:0.##}, {p.z:0.##}) fixed={_config.UseFixedPosition} eff {_config.RecycleEfficiency:P0}");
        }

        [ConsoleCommand("domerecycler.sethere")]
        private void CmdSetHere(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Connection == null || arg.Connection.authLevel < 2)
            {
                arg.ReplyWith("Fique no lugar certo e rode no F1: domerecycler.sethere");
                return;
            }

            var p = player.transform.position;
            _config.PosX = p.x;
            _config.PosY = p.y;
            _config.PosZ = p.z;
            _config.Yaw = player.eyes.rotation.eulerAngles.y;
            _config.UseFixedPosition = true;
            SaveConfig();

            EnsureRecycler(true);
            arg.ReplyWith($"DomeRecycler fixada em ({p.x:0.##}, {p.y:0.##}, {p.z:0.##})");
        }

        [ConsoleCommand("domerecycler.autofind")]
        private void CmdAutoFind(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;

            _config.UseFixedPosition = false;
            SaveConfig();
            EnsureRecycler(true);

            MonumentInfo dome;
            Vector3 pos;
            if (TryFindDome(out dome, out pos))
                arg.ReplyWith($"Dome encontrado: {dome.name} @ {pos}");
            else
                arg.ReplyWith("Dome NAO encontrado neste mapa. Use domerecycler.sethere no local desejado.");
        }

        [ConsoleCommand("domerecycler.tp")]
        private void CmdTp(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Connection == null || arg.Connection.authLevel < 2)
                return;

            player.Teleport(GetTargetPos() + Vector3.up * 1.2f);
            arg.ReplyWith("Teleportado ate a recycler do Dome.");
        }

        [ConsoleCommand("domerecycler.efficiency")]
        private void CmdEfficiency(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;

            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith($"DomeRecycler efficiency: {_config.RecycleEfficiency}");
                return;
            }

            float value;
            if (!float.TryParse(arg.Args[0].Replace(",", "."), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                arg.ReplyWith("Valor invalido. 0.05 a 1.0");
                return;
            }

            _config.RecycleEfficiency = Mathf.Clamp(value, 0.05f, 1f);
            SaveConfig();
            arg.ReplyWith($"DomeRecycler efficiency = {_config.RecycleEfficiency:P0}");
        }

        [ConsoleCommand("domerecycler.status")]
        private void CmdStatus(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.authLevel < 2)
                return;

            MonumentInfo dome;
            Vector3 domePos;
            if (TryFindDome(out dome, out domePos))
                arg.ReplyWith($"Dome monument: {dome.name} @ {domePos}");
            else
                arg.ReplyWith("Dome monument: NAO encontrado");

            var target = GetTargetPos();
            arg.ReplyWith($"Target spawn: {target} fixed={_config.UseFixedPosition}");
            arg.ReplyWith($"Efficiency: {_config.RecycleEfficiency} | managed={_managedIds.Count}");

            foreach (var r in FindNearbyRecyclers(target, _config.CleanupRadius))
            {
                var id = r.net != null ? r.net.ID.Value.ToString() : "?";
                arg.ReplyWith($"Recycler @ {r.transform.position} id={id} managed={IsManaged(r)}");
            }
        }

        private void EnsureRecycler(bool forceReplace)
        {
            Vector3 target;
            float yaw = _config.Yaw;

            if (_config.UseFixedPosition)
            {
                target = new Vector3(_config.PosX, _config.PosY, _config.PosZ);
            }
            else
            {
                MonumentInfo dome;
                Vector3 domePos;
                if (!TryFindDome(out dome, out domePos))
                {
                    PrintWarning("Dome nao encontrado. Fique no local e use: domerecycler.sethere");
                    return;
                }

                var rot = dome.transform.rotation;
                target = domePos + rot * new Vector3(_config.OffsetX, _config.OffsetY, _config.OffsetZ);
                // Nao raycast longo: o teto da sphere_tank fica em Y alto
                yaw = rot.eulerAngles.y + _config.Yaw;
                if (_config.LogActions)
                    Puts($"Dome auto: {dome.name} center={domePos} spawn={target}");
            }

            // Remove recyclers antigas perto do Dome e perto do alvo
            var toRemove = new HashSet<Recycler>();
            foreach (var r in FindNearbyRecyclers(target, _config.CleanupRadius))
                toRemove.Add(r);

            MonumentInfo domeForCleanup;
            Vector3 domeCenter;
            if (TryFindDome(out domeForCleanup, out domeCenter))
            {
                foreach (var r in FindNearbyRecyclers(domeCenter, _config.CleanupRadius))
                    toRemove.Add(r);
            }

            // Posicao antiga errada do wipe anterior
            foreach (var r in FindNearbyRecyclers(new Vector3(288.28f, 34.34f, -130.5f), 40f))
                toRemove.Add(r);

            if (!forceReplace)
            {
                foreach (var r in toRemove)
                {
                    if ((r.transform.position - target).sqrMagnitude < 4f)
                    {
                        if (r.net != null)
                            _managedIds.Add(r.net.ID.Value);
                        if (_config.LogActions)
                            Puts($"Recycler ja no local: {r.transform.position}");
                        return;
                    }
                }
            }

            foreach (var r in toRemove)
            {
                if (_config.LogActions)
                    Puts($"Removendo recycler antiga: {r.transform.position}");
                if (r.net != null)
                    _managedIds.Remove(r.net.ID.Value);
                r.Kill();
            }

            var entity = GameManager.server.CreateEntity(Prefab, target, Quaternion.Euler(0f, yaw, 0f));
            if (entity == null)
            {
                PrintError($"Falha ao criar {Prefab}");
                return;
            }

            entity.enableSaving = true;
            entity.Spawn();

            var rec = entity as Recycler;
            if (rec != null && rec.net != null)
                _managedIds.Add(rec.net.ID.Value);

            if (_config.LogActions)
                Puts($"Recycler spawnada em {target} eff={_config.RecycleEfficiency}");
        }

        private bool TryFindDome(out MonumentInfo dome, out Vector3 pos)
        {
            dome = null;
            pos = Vector3.zero;

            var monuments = TerrainMeta.Path != null ? TerrainMeta.Path.Monuments : null;
            if (monuments == null || monuments.Count == 0)
            {
                // Fallback
                foreach (var m in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
                {
                    if (IsDomeMonument(m))
                    {
                        dome = m;
                        pos = m.transform.position;
                        return true;
                    }
                }
                return false;
            }

            foreach (var m in monuments)
            {
                if (IsDomeMonument(m))
                {
                    dome = m;
                    pos = m.transform.position;
                    return true;
                }
            }

            return false;
        }

        private static bool IsDomeMonument(MonumentInfo m)
        {
            if (m == null)
                return false;

            var name = (m.name ?? string.Empty).ToLowerInvariant();
            if (name.Contains("dome_monument") || name.Contains("/dome/") || name.Contains("sphere_tank") ||
                (name.Contains("dome") && !name.Contains("oil") && !name.Contains("underwater")))
                return true;

            try
            {
                var phrase = m.displayPhrase;
                if (phrase != null)
                {
                    var eng = (phrase.english ?? string.Empty).ToLowerInvariant();
                    var token = (phrase.token ?? string.Empty).ToLowerInvariant();
                    if (eng.Contains("dome") || token.Contains("dome"))
                        return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private bool IsManaged(Recycler rec)
        {
            if (rec == null || rec.IsDestroyed)
                return false;
            if (rec.net != null && _managedIds.Contains(rec.net.ID.Value))
                return true;
            return IsNearManagedPosition(rec.transform.position);
        }

        private bool IsNearManagedPosition(Vector3 pos)
        {
            return (pos - GetTargetPos()).sqrMagnitude <= (_config.CleanupRadius * _config.CleanupRadius);
        }

        private Vector3 GetTargetPos()
        {
            if (_config.UseFixedPosition)
                return new Vector3(_config.PosX, _config.PosY, _config.PosZ);

            MonumentInfo dome;
            Vector3 domePos;
            if (TryFindDome(out dome, out domePos))
            {
                var rot = dome.transform.rotation;
                return domePos + rot * new Vector3(_config.OffsetX, _config.OffsetY, _config.OffsetZ);
            }

            return new Vector3(_config.PosX, _config.PosY, _config.PosZ);
        }

        private static List<Recycler> FindNearbyRecyclers(Vector3 center, float radius)
        {
            var list = new List<Recycler>();
            var r2 = radius * radius;
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity == null || entity.IsDestroyed)
                    continue;
                var rec = entity as Recycler;
                if (rec == null)
                    continue;
                if ((rec.transform.position - center).sqrMagnitude <= r2)
                    list.Add(rec);
            }

            return list;
        }
    }
}
