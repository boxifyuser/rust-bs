using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DisableTerrainKick", "BICHO", "1.4.0")]
    [Description("Sem kick InsideTerrain/FlyHack + pouso seguro so quando necessario")]
    public class DisableTerrainKick : RustPlugin
    {
        private void OnServerInitialized()
        {
            ApplyAntihack();
            // Reaplica raramente (algo pode resetar convars) — sem spam a cada 60s
            timer.Every(900f, ApplyAntihack);
            Puts("DisableTerrainKick 1.4.0 OK (terrain + flyhack off)");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            // Uma tentativa apos o cliente estabilizar
            timer.Once(1.0f, () => ForceLand(player));
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null) return;
            timer.Once(0.4f, () => ForceLand(player));
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null) return;
            timer.Once(0.4f, () => ForceLand(player));
        }

        private void ApplyAntihack()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "antihack.terrain_protection 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "antihack.terrain_kill 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "antihack.flyhack_protection 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "antihack.flyhack_penalty 0");
        }

        private void ForceLand(BasePlayer player)
        {
            if (player == null || !player.IsConnected || player.IsDead()) return;
            if (TerrainMeta.HeightMap == null) return;

            Vector3 pos = player.transform.position;

            // Buraco / void do mapa custom
            if (pos.y < -50f)
            {
                var rescue = FindSafeGround(new Vector3(150f, 50f, 150f));
                TeleportSafe(player, rescue);
                Puts($"Void rescue {player.displayName} {pos} -> {rescue}");
                return;
            }

            // Spawn invalido perto de 0,0
            if (Mathf.Abs(pos.x) < 8f && Mathf.Abs(pos.z) < 8f)
            {
                var rescue = FindSafeGround(new Vector3(200f, 50f, 200f));
                TeleportSafe(player, rescue);
                Puts($"Zero rescue {player.displayName} {pos} -> {rescue}");
                return;
            }

            Vector3 safe = FindSafeGround(pos);
            if (safe.y < -20f)
                safe = FindSafeGround(new Vector3(150f, 50f, 150f));

            float dy = Mathf.Abs(pos.y - safe.y);
            float waterAbove = 0f;
            if (TerrainMeta.WaterMap != null)
                waterAbove = TerrainMeta.WaterMap.GetHeight(safe) - safe.y;

            // So teleporta se realmente estiver ruim (evita spam de +1m)
            bool needsFix = pos.y < -20f
                || dy > 3.5f
                || waterAbove > 1.2f;

            if (!needsFix) return;

            TeleportSafe(player, safe);
            Puts($"Safe land {player.displayName} {pos} -> {safe}");
        }

        private static void TeleportSafe(BasePlayer player, Vector3 safe)
        {
            player.PauseFlyHackDetection(12f);
            player.PauseSpeedHackDetection(12f);
            player.Teleport(safe);
            player.SendNetworkUpdateImmediate();
        }

        private Vector3 FindSafeGround(Vector3 from)
        {
            if (Mathf.Abs(from.x) < 8f && Mathf.Abs(from.z) < 8f)
                from = new Vector3(200f, 0f, 200f);
            if (from.y < -50f)
                from = new Vector3(150f, 50f, 150f);

            Vector3 candidate = SnapToGround(from);
            if (IsGoodLand(candidate)) return candidate;

            float size = TerrainMeta.Size.x * 0.45f;
            for (int i = 0; i < 120; i++)
            {
                float ang = i * 0.618f * Mathf.PI * 2f;
                float dist = 60f + i * 25f;
                if (dist > size) dist = size * (0.2f + (i % 10) * 0.05f);
                var p = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                candidate = SnapToGround(p);
                if (IsGoodLand(candidate)) return candidate;
            }

            return SnapToGround(new Vector3(150f, 0f, 150f));
        }

        private Vector3 SnapToGround(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            return new Vector3(pos.x, y + 1.25f, pos.z);
        }

        private bool IsGoodLand(Vector3 pos)
        {
            if (pos.y < -10f) return false;

            if (TerrainMeta.TopologyMap != null)
            {
                var top = TerrainMeta.TopologyMap;
                if (top.GetTopology(pos, TerrainTopology.OCEAN)) return false;
                if (top.GetTopology(pos, TerrainTopology.OFFSHORE)) return false;
                if (top.GetTopology(pos, TerrainTopology.RIVER)) return false;
                if (top.GetTopology(pos, TerrainTopology.LAKE)) return false;
            }

            if (TerrainMeta.WaterMap != null)
            {
                float water = TerrainMeta.WaterMap.GetHeight(pos);
                float ground = TerrainMeta.HeightMap.GetHeight(pos);
                if (ground <= water + 0.6f) return false;
            }

            return true;
        }
    }
}
