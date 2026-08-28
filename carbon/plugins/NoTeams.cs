using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("No Teams", "OfficerJAKE", "1.0.4")]
	[Description("Players cannot create teams at all")]

	//Credits to Nivex for Language/Localization help

	public class NoTeams : RustPlugin
	{
		private const string BypassPerm = "noteams.bypass";
		
		#region Configuration

		private Configuration config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Settings")]
			public Settings settings = new Settings();

			public class Settings
			{
				[JsonProperty(PropertyName = "Enable Chat Reply")]
				public bool EnableChatReply = true;

				[JsonProperty(PropertyName = "Enable Console Logs")]
				public bool EnableConsoleLogs = true;
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				config = Config.ReadObject<Configuration>();				
			}
			catch
			{
			}

			if (config == null)
			{
				LoadDefaultConfig();
				Puts("Loaded Default Configuration!");
			}

			SaveConfig();
		}

		protected override void LoadDefaultConfig() => config = new Configuration();
		protected override void SaveConfig() => Config.WriteObject(config);

		#endregion

		#region Localization

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["ChatPrefix"] = "<color=#359bf2>SOLO ONLY: </color>",
				["NoTeamsAllowed"] = "You are not able to create a team on this server."

			}, this);
		}

		#endregion Localization

		#region Hooks

		private void Init()
		{
			permission.RegisterPermission(BypassPerm, this);
		}
		
		private object OnTeamCreate(BasePlayer player)
		{
			if (!permission.UserHasPermission(player.UserIDString, BypassPerm))
			{
				if (config.settings.EnableConsoleLogs)
				{
					Puts("{0} [{1}] tried to make a team", player.displayName, player.userID);
				}
				if (config.settings.EnableChatReply)
				{
					SendChatMessage(player, "NoTeamsAllowed");				
				}
			return true;
			}
			return null;
		}

		private void SendChatMessage(BasePlayer player, string key)
		{
			string prefix = lang.GetMessage("ChatPrefix", this, player.UserIDString);
			string message = lang.GetMessage(key, this, player.UserIDString);

			SendReply(player, prefix + message);
		}

		#endregion
	}
}