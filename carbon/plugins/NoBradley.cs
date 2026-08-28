using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoBradley", "BICHO", "1.0.0")]
    [Description("Remove o Bradley APC do spawn/respawn e limpa os que ja estao no mapa")]
    public class NoBradley : RustPlugin
    {
        private void OnServerInitialized()
        {
            DisableBradley();
            timer.Once(1f, KillAllBradleys);
            timer.Every(600f, DisableBradley);
            Puts("NoBradley: spawn/respawn do Bradley APC desativado");
        }

        private void OnEntitySpawned(BradleyAPC apc)
        {
            if (apc == null || apc.IsDestroyed) return;

            NextTick(() =>
            {
                if (apc == null || apc.IsDestroyed) return;
                apc.Kill(BaseNetworkable.DestroyMode.None);
            });
        }

        private void DisableBradley()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "bradley.enabled 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "events.set_event_enabled bradley_road false");

            var spawner = BradleySpawner.singleton;
            if (spawner != null)
            {
                spawner.enabled = false;
                spawner.CancelInvoke("DelayedStart");
                spawner.CancelInvoke("CheckIfRespawnNeeded");
            }
        }

        private void KillAllBradleys()
        {
            int killed = 0;
            foreach (var apc in UnityEngine.Object.FindObjectsOfType<BradleyAPC>())
            {
                if (apc == null || apc.IsDestroyed) continue;
                apc.Kill(BaseNetworkable.DestroyMode.None);
                killed++;
            }

            if (killed > 0)
                Puts($"NoBradley: removidos {killed} Bradley APC do mapa");
        }
    }
}
