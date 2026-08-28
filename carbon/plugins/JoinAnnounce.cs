using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Join Announce", "Server", "1.0.0")]
    [Description("Broadcasts a chat message to everyone when a player joins")]
    public class JoinAnnounce : RustPlugin
    {
        private Configuration _config;

        private class Configuration
        {
            public string JoinMessage = "<color=#55efc4>{name}</color> entrou no servidor.";
            public bool ExcludeAdmins = false;
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
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new System.Exception();
            }
            catch
            {
                PrintWarning("Config invalido, usando padroes.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;

            if (_config.ExcludeAdmins && player.IsAdmin)
                return;

            var message = _config.JoinMessage
                .Replace("{name}", player.displayName)
                .Replace("{id}", player.UserIDString);

            Server.Broadcast(message);
        }
    }
}
