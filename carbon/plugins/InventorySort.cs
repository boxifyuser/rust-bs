using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Inventory Sort", "Server", "2.2.0")]
    [Description("Botoes ORGANIZAR / PEGAR TUDO desativados")]
    public class InventorySort : RustPlugin
    {
        private const string UiRoot = "InventorySort.UI";

        private void Init()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, UiRoot);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, UiRoot);
        }
    }
}
