using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Online Players", "MACHIN3", "1.1.8")]
	[Description("Shows list of online players and sleepers in separate UIs")]
	public class OnlinePlayers : RustPlugin
	{
        #region Update Log

        /*****************************************************
			【 𝓜𝓐𝓒𝓗𝓘𝓝𝓔 】
		     ---------------
		Website: https://www.rustlevels.com/
        Discord: http://discord.rustlevels.com/
		
		---------------------
		✯ update 1.1.8
		---------------------
		✯ Fixed player list errors
        
		/*****************************************************/
        #region Previous Releases
        /*****************************************************
		---------------------
		✯ update 1.1.7
		---------------------
		✯ Updated support for latest XPerience version with new UIs
		✯ Added chat command to turn OnlineHUD on/off
		✯ Added option to show player level on HUD & Player List UIs (requires XPerience)
		✯ Adjusted HUD layout for better control of location and display
		✯ Fixed refresh issues for some players
		✯ Fixed HUD showing sleeper list
		---------------------
		✯ update 1.1.5
		---------------------
		✯ Fixed XPerience, NTeleportation, and KillRecords icons not showing when enabled
		✯ Fixed player/sleeper count including admins when hide admin option is true
		---------------------
		✯ update 1.1.4
		---------------------
		✯ Fixed Online HUD showing when disabled in config
		✯ Fixed layout issues in player/sleeper UIs
		---------------------
		✯ update 1.1.3
		---------------------
		✯ Added option to show Online HUD
		✯ Added option to show player avatars (requirer ImageLibrary)

		✯ HUD Options:
			* Location from left, bottom
			* HUD width, height
			* HUD transparency
			* Max number of players to show on HUD
			* HUD refresh rate in seconds
			* Font size for player names

		NOTES: Player avatars require ImageLibrary plugin and in the ImageLibrary config you must set ("Avatars - Store player avatars": true,)
		---------------------
		✯ update 1.1.2
		---------------------
		✯ Added support for DiscordReport plugin
		✯ Option to show report player icon in Online Player list
		---------------------
		✯ update 1.1.1
		---------------------
		✯ Fixed issues where players couldn't close UIs
		---------------------
		✯ update 1.1.0
		---------------------
		✯ Added option to show Ranks from XPerience
		✯ Fixed XPerience profile icon links
		---------------------
		✯ update 1.0.9
		---------------------
		✯ Updated profile links for XPerience
		---------------------
		✯ update 1.0.8
		---------------------
		✯ Fixed errors when sleeper count is disabled
		---------------------
		✯ update 1.0.7
		---------------------
		✯ Added button to switch between Online/Offline UIs
		---------------------
		✯ update 1.0.6
		---------------------
		✯ Added option to set custom chat commands of UIs
		---------------------
		✯ update 1.0.5
		---------------------
		✯ Added UI location adjustments Options
		---------------------
		✯ update 1.0.4
		---------------------
		✯ Added refresh button to UIs
		✯ Added NTeleportation support for TPR to players online
		---------------------
		✯ update 1.0.3
		---------------------
		✯ Fixed UIs not updating lists when new players join
		✯ Added options to show Online/Sleeper count
		---------------------
		✯ update 1.0.2
		---------------------
		✯ Added option to hide admins from lists
		---------------------
		✯ update 1.0.1
		---------------------
		✯ Reduced UI structure
		✯ Organized UI structure
		✯ Cleaned/Removed unused coding
		*****************************************************/
        #endregion
        #endregion

        #region Refrences
        [PluginReference]
		private readonly Plugin KillRecords, XPerience, NTeleportation, DiscordReport, ImageLibrary;		
		#endregion

		#region Fields	
		private Configuration config;
        private Timer _playerdata;
        private const string OnlinePlayersUI = "OnlinePlayersUI";
		private const string OnlinePlayersUIInner = "OnlinePlayersUIInner";
		private const string OnlinePlayersUIPages = "OnlinePlayersUIPages";
		private const string SleeperPlayersUI = "SleeperPlayersUI";
		private const string SleeperPlayersUIInner = "SleeperPlayersUIInner";
		private const string SleeperPlayersUIPages = "SleeperPlayersUIPages";
		private const string OnlinePlayersHUD = "OnlinePlayersHUD";
		private readonly Hash<ulong, int> _onlineUIPage = new Hash<ulong, int>();
		private readonly Hash<ulong, string> onlinePlayers = new Hash<ulong, string>();
		private readonly Hash<ulong, int> _sleeperPlayersUIPage = new Hash<ulong, int>();
		private readonly Hash<ulong, string> sleeperPlayers = new Hash<ulong, string>();
		#endregion

        #region Config
        private class Configuration
		{
			[JsonProperty("Hide Admins")]
			public bool HideAdmins = false;
			[JsonProperty("Show Online Player Count")]
			public bool ShowOnlineCount = true;
			[JsonProperty("Show Sleeper Count")]
			public bool ShowSleeperCount = true;
            [JsonProperty("Show Player Avatars (requires ImageLibrary and Store player avatars = true)")]
            public bool playeravatars = false;
            [JsonProperty("Show KillRecords Icon (Requires Kill Records Plugin)")]
			public bool ShowKRIcon = false;
			[JsonProperty("Show XPerience Icon (Requires XPerience Plugin)")]
			public bool ShowXPIcon = false;
			[JsonProperty("Show XPerience Rank Sig (Requires XPerience Plugin)")]
			public bool ShowRankSig = false;
			[JsonProperty("Show XPerience Level (Requires XPerience Plugin)")]
			public bool ShowLevel = false;
			[JsonProperty("Show TPR Icon (Requires NTeleportation Plugin and tpr permission)")]
			public bool ShowTPIcon = false;
			[JsonProperty("Show Discord Report Icon (Requires DiscordReport Plugin)")]
			public bool ShowDRIcon = false;
			[JsonProperty("UI Location (distance from left 0 - 0.80)")]
			public double UILeft = 0.05;
			[JsonProperty("UI Location (distance from bottom 0.45 - 1.0)")]
			public double UITop = 0.75;
			[JsonProperty("Chat Command (Online Players)")]
			public string chatplayers = "players";
			[JsonProperty("Chat Command (Sleepers)")]
			public string chatsleepers = "sleepers";
            [JsonProperty("Show Online HUD")]
            public bool OnlineHUD = false;
            [JsonProperty("Online HUD Chat Command")]
            public string chathud = "onlinehud";
            [JsonProperty("HUD Location From Left")]
            public double HUDLeft = 0.01;
            [JsonProperty("HUD Location From Top")]
            public double HUDTop = 0.98;
            [JsonProperty("HUD Width")]
            public double HUDWidth = 0.15;
            [JsonProperty("HUD Height")]
            public double HUDHeight = 0.30;
            [JsonProperty("Max Players On HUD")]
            public int HUDplayercount = 10;
            [JsonProperty("HUD Transparency 0.0 - 1.0")]
            public double HUDTransparency = 0.25;
            [JsonProperty("HUD Refresh Rate (seconds)")]
            public float HUDrefreshrate = 60;
            [JsonProperty("HUD Font Size")]
            public int HUDfontsize = 10;

            public string ToJson() => JsonConvert.SerializeObject(this);
			public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
		}
		protected override void LoadDefaultConfig() => config = new Configuration();
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null)
				{
					throw new JsonException();
				}
				if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
				{
					PrintWarning("Configuration appears to be outdated; updating and saving");
					SaveConfig();
				}
			}
			catch
			{
				PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
				LoadDefaultConfig();
			}
		}
		protected override void SaveConfig()
		{
			PrintWarning($"Configuration changes saved to {Name}.json");
			Config.WriteObject(config, true);
		}
		#endregion

		#region Oxide Hooks
        private void OnServerInitialized()
		{
			cmd.AddChatCommand(config.chatplayers, this, Chatplayers);
			cmd.AddChatCommand(config.chatsleepers, this, Chatsleepers);
			cmd.AddChatCommand(config.chathud, this, ChatHUD);
			//foreach (var player in BasePlayer.activePlayerList)
			//{
			//	if (player.userID.IsSteamId())
			//	{
			//		if (config.HideAdmins && player.IsAdmin) continue;
			//		onlinePlayers.Add(player.userID, player.displayName);
			//	}
			//	if (config.OnlineHUD)
			//	{
			//		OnlineHUD(player);
			//		HUDTimer(player, true);
   //             }
   //         }
   //         foreach (var player in BasePlayer.sleepingPlayerList)
			//{
			//	if (player.userID.IsSteamId())
			//	{
			//		if (config.HideAdmins && player.IsAdmin) continue;
   //                 onlinePlayers.Add(player.userID, player.displayName);
   //                 //sleeperPlayers.Add(player.userID, player.displayName);
			//	}
			//}
        }
        private void Loaded()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID.Get().IsSteamId())
                {
                    if (config.HideAdmins && player.IsAdmin) continue;
                    onlinePlayers.Add(player.userID, player.displayName);
                }
                if (config.OnlineHUD)
                {
                    OnlineHUD(player);
                    HUDTimer(player, true);
                }
            }
            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (player.userID.Get().IsSteamId())
                {
                    if (config.HideAdmins && player.IsAdmin) continue;
                    sleeperPlayers.Add(player.userID, player.displayName);
                }
            }
        }
		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
                ClearPlayerUIs(player);
				HUDTimer(player, false);
            }
            onlinePlayers.Clear();
			_onlineUIPage.Clear();
			sleeperPlayers.Clear();
			_sleeperPlayersUIPage.Clear();
        }
        private void OnPlayerConnected(BasePlayer player)
		{
			if (!onlinePlayers.ContainsKey(player.userID))
			{
				if (!config.HideAdmins && player.IsAdmin)
				{
					onlinePlayers.Add(player.userID, player.displayName);
				}
				else if (!player.IsAdmin)
				{
					onlinePlayers.Add(player.userID, player.displayName);
				}
			}
			if (sleeperPlayers.ContainsKey(player.userID))
			{
				if (!config.HideAdmins && player.IsAdmin)
				{
					sleeperPlayers.Remove(player.userID);
				}
				else if (!player.IsAdmin)
				{
					sleeperPlayers.Remove(player.userID);
				}
			}
			if(config.OnlineHUD)
			{
				OnlineHUD(player);
                HUDTimer(player, true);
            }
        }
		private void OnPlayerDisconnected(BasePlayer player)
		{
			ClearPlayerUIs(player);
			if (onlinePlayers.ContainsKey(player.userID))
			{
				if (!config.HideAdmins && player.IsAdmin)
				{
					onlinePlayers.Remove(player.userID);
				}
				else if (!player.IsAdmin)
				{
					onlinePlayers.Remove(player.userID);
				}
			}
			if (!sleeperPlayers.ContainsKey(player.userID))
			{
				if (!config.HideAdmins && player.IsAdmin)
				{
					sleeperPlayers.Add(player.userID, player.displayName);
				}
				else if (!player.IsAdmin)
				{
					sleeperPlayers.Add(player.userID, player.displayName);
				}
			}
		}
		private static BasePlayer FindPlayer(string playerid)
		{
			foreach (var activePlayer in BasePlayer.activePlayerList)
			{
				if (activePlayer.UserIDString == playerid)
					return activePlayer;
			}
			foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
			{
				if (sleepingPlayer.UserIDString == playerid)
					return sleepingPlayer;
			}
			return null;
		}
		#endregion

		#region Commands
		// Chat Commands
		private void Chatplayers(BasePlayer player, string command, string[] args)
		{
            ClearPlayerUIs(player);
            PlayerOnlineUI(player);
            PlayerOnlineUIList(player, _onlineUIPage[player.userID]);
        }	
		private void Chatsleepers(BasePlayer player, string command, string[] args)
		{
            ClearPlayerUIs(player);
            SleeperPlayerUI(player);
            SleeperPlayerUIList(player, _sleeperPlayersUIPage[player.userID]);
        }
		private void ChatHUD(BasePlayer player, string command, string[] args)
		{
			switch (args[0])
			{
				case "on":
					OnlineHUD(player);
                    HUDTimer(player, true);
                    break;
				case "off":
                    DestroyUi(player, OnlinePlayersHUD);
                    HUDTimer(player, false);
                    break;
			}
        }
		// Command Handlers
        [ConsoleCommand("op.playerprofiles")]
		private void Cmdopplayerprofiles(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			string page = arg.GetString(0);
			ClearPlayerUIs(player);
            switch (page)
            {
				case "xperience":
					if (XPerience != null)
					{
                        //XPerience.Call("PlayerProfile", player, FindPlayer(arg.GetString(1)));
                        //XPerience.Call("PlayerProfileMain", player, FindPlayer(arg.GetString(1)));
                        player.SendConsoleCommand($"chat.say \"/xpstats {FindPlayer(arg.GetString(1))}\"");
                    }
                    break;
				case "killrecords":
					if (KillRecords != null)
					{
						KillRecords.Call("KRUIplayers", player, arg.GetString(1).ToLower());
					}
					break;
				case "discordreport":
					if (DiscordReport != null)
					{
						string message = "[Received player report from Online Players]";
						player.SendConsoleCommand($"chat.say \"/report {FindPlayer(arg.GetString(1))} {message}\"");
						//DiscordReport.Call("CommandReport", player, "report", info);
					}
					break;
			}
		}
        [ConsoleCommand("op.sleeperprofiles")]
		private void Cmdopsleeperprofiles(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			string page = arg.GetString(0);
			var info = arg.GetString(1).ToLower();
            var otherplayer = FindPlayer(arg.GetString(1));
            ClearPlayerUIs(player);
            switch (page)
            {
				case "xperience":
                    if (XPerience != null)
                    {
                        //XPerience.Call("PlayerProfile", player, otherplayer);
                        //XPerience.Call("PlayerProfileMain", player, otherplayer);
                        player.SendConsoleCommand($"chat.say \"/xpstats {FindPlayer(arg.GetString(1))}\"");
                    }
                    break;
				case "killrecords":
					if (KillRecords != null)
					{
						KillRecords.Call("KRUIplayers", player, info);
					}
				break;
			}		
		}	
		[ConsoleCommand("op.refreshplayers")]
		private void Cmdrefreshplayers(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
            ClearPlayerUIs(player);
            PlayerOnlineUI(player);
			PlayerOnlineUIList(player, _onlineUIPage[player.userID]);
		}
		[ConsoleCommand("op.refreshsleepers")]
		private void Cmdrefreshsleepers(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
            ClearPlayerUIs(player);
            SleeperPlayerUI(player);
			SleeperPlayerUIList(player, _sleeperPlayersUIPage[player.userID]);
		}
		[ConsoleCommand("op.playerpages")]
		private void Cmdopplayerpages(ConsoleSystem.Arg arg)
		{
            BasePlayer player = arg.Player();
			if (player == null) return;
            DestroyUi(player, OnlinePlayersUIInner);
			DestroyUi(player, OnlinePlayersUIPages);
            PlayerOnlineUIList(player, arg.GetInt(0));
		}
		[ConsoleCommand("op.sleeperpages")]
		private void Cmdopsleeperpages(ConsoleSystem.Arg arg)
		{
            BasePlayer player = arg.Player();
			if (player == null) return;
            DestroyUi(player, SleeperPlayersUIInner);
			DestroyUi(player, SleeperPlayersUIPages);
            SleeperPlayerUIList(player, arg.GetInt(0));
		}
        [ConsoleCommand("op.nteleportation")]
		private void Cmdnteleportation(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (arg.Player() == null) return;
			var target = arg.GetString(0).ToLower();
            ClearPlayerUIs(player);
            if (NTeleportation != null)
			{
				player.SendConsoleCommand($"chat.say \"/tpr {target}\"");
			}	
		}
		#endregion

		#region UIs
		// UI Defaults
		private CuiPanel DefaultUIPanel(string anchorMin, string anchorMax, string color = "0 0 0 0")
		{
			return new CuiPanel
			{
				Image =
				{
					Color = color
				},
				RectTransform =
				{
					AnchorMin = anchorMin,
					AnchorMax = anchorMax
				}
			};
		}
		private CuiLabel DefaultUILabel(string text, double i, float height, TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 13, string xMin = "0", string xMax = "1", string color = "1.0 1.0 1.0 1.0")
		{
			return new CuiLabel
			{
				Text =
				{
					Text = text,
					FontSize = fontSize,
					Align = align,
					Color = color
				},
				RectTransform =
				{
					AnchorMin = $"{xMin} {1 - height*i + i * .002f}",
					AnchorMax = $"{xMax} {1 - height*(i-1) + i * .002f}"
				}
			};
		}
		private CuiButton DefaultUIButton(string command, double i, float rowHeight, int fontSize = 11, string color = "1.0 0.0 0.0 0.7", string content = "+", string xMin = "0", string xMax = "1", TextAnchor align = TextAnchor.MiddleLeft, string fcolor = "1.0 1.0 1.0 1.0")
		{
			return new CuiButton
			{
				Button =
				{
					Command = command,
					Color = $"{color}"
				},
				RectTransform =
				{
					AnchorMin = $"{xMin} {1 - rowHeight*i + i * .002f}",
					AnchorMax = $"{xMax} {1 - rowHeight*(i-1) + i * .002f}"
				},
				Text =
				{
					Text = content,
					FontSize = fontSize,
					Align = align,
					Color = fcolor,
				}
			};
		}
        private CuiElement DefaultUIImage(string parent, string image, double i, float imgheight, string xMin = "0", string xMax = "1")
        {
            return new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", image)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{xMin} {1 - imgheight*i + i * .002f}",
                        AnchorMax = $"{xMax} {1 - imgheight*(i-1) + i * .002f}",
                    }
                }
            };
        }
        private CuiElement DefaultHUDUIImage(string parent, string image, double i, float imgheight, string xMin = "0", string xMax = "1")
        {
            return new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = ImageLibrary?.Call<string>("GetImage", image)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{xMin} {1 - imgheight*i + i * .002f}",
                        AnchorMax = $"{xMax} {1 - imgheight*(i-1) + i * .002f - .03}",
                    }
                }
            };
        }
        private CuiLabel DefaultHUDUILabel(string text, double i, float height, TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 13, string xMin = "0", string xMax = "1", string color = "1.0 1.0 1.0 1.0")
        {
            return new CuiLabel
            {
                Text =
                {
                    Text = text,
                    FontSize = fontSize,
                    Align = align,
                    Color = color
                },
                RectTransform =
                {
                    AnchorMin = $"{xMin} {1 - height*i + i * .002f}",
                    AnchorMax = $"{xMax} {1 - height*(i-1) + i * .002f - .03}"
                }
            };
        }
        // Online Players
        private void OnlineHUD(BasePlayer player)
		{
            if (player == null || !player.userID.Get().IsSteamId()) return;
            DestroyUi(player, OnlinePlayersHUD);
            var HUDelements = new CuiElementContainer();
            // Main UI
            double HUDLeft = config.HUDLeft;
            double HUDTop = config.HUDTop;
            if (HUDLeft > 0.80) { HUDLeft = 0.80; }
            if (HUDLeft < 0) { HUDLeft = 0; }
            if (HUDTop > 1) { HUDTop = 1; }
            if (HUDTop < config.HUDHeight) { HUDTop = config.HUDHeight; }
			double HUDWidth = config.HUDWidth;
			double HUDHeight = config.HUDHeight;
            int n = 1;
            float height = 0.1f;
            var rank = "";
            var level = "";
			string avatarloc = "0.1";
			string nameloc = "0.01";
            HUDelements.Add(DefaultUIPanel($"{HUDLeft} {HUDTop - HUDHeight}", $"{HUDLeft + HUDWidth} {HUDTop}", $"0 0 0 {config.HUDTransparency}"), "Hud", OnlinePlayersHUD);
            foreach (var onlineplayer in onlinePlayers)
            {
                if ((n - config.HUDplayercount) < config.HUDplayercount)
                {
                    if (ImageLibrary != null && config.playeravatars)
                    {
                        HUDelements.Add(DefaultHUDUIImage(OnlinePlayersHUD, onlineplayer.Key.ToString(), n, height, "0", avatarloc));
						nameloc = "0.12";
                    }
                    if (XPerience != null)
                    {
						if(config.ShowRankSig && XPerience?.Call<string>("GetXPCache", FindPlayer(onlineplayer.Key.ToString()), "ranksig") != null)
						{
                            rank = $" {XPerience?.Call<string>("GetXPCache", FindPlayer(onlineplayer.Key.ToString()), "ranksig")}";
                        }
						if(config.ShowLevel && XPerience?.Call<string>("GetXPCache", FindPlayer(onlineplayer.Key.ToString()), "level") != null)
						{
                            level = $" [{XPerience?.Call<string>("GetXPCache", FindPlayer(onlineplayer.Key.ToString()), "level")}]";
                        }
                    }
                    HUDelements.Add(DefaultHUDUILabel($"{onlineplayer.Value}{rank}{level}", n, height, TextAnchor.MiddleLeft, config.HUDfontsize, nameloc, "1", "1 1 1 1"), OnlinePlayersHUD, "HUDList");
                }
                n++;
            }
            // UI End
            CuiHelper.AddUi(player, HUDelements);
			return;
        }        
		private void PlayerOnlineUI(BasePlayer player)
		{
			if (player == null) return;
			DestroyUi(player, OnlinePlayersUI);
			DestroyUi(player, SleeperPlayersUI);
			int count = onlinePlayers.Count;
			var OnlinePlayersUIelements = new CuiElementContainer();
			// Main UI
			double UILeft = config.UILeft;
			double UITop = config.UITop;
			if(UILeft > 0.80){UILeft = 0.80;}			
			if(UILeft < 0){UILeft = 0;}			
			if(UITop > 1) {UITop = 1;}
			if(UITop < 0.45) {UITop = 0.45;}
			OnlinePlayersUIelements.Add(new CuiPanel
			{
				Image =
				{
					Color = "0.1 0.1 0.1 0.95"
				},
				RectTransform =
				{
					AnchorMin = $"{UILeft} {UITop - 0.45}",
					AnchorMax = $"{UILeft + 0.20} {UITop}"
				},
				CursorEnabled = true
			}, "Hud", OnlinePlayersUI);
			// Close Button
			OnlinePlayersUIelements.Add(new CuiButton
			{
				Button =
				{
					Close = OnlinePlayersUI,
					Color = "0.0 0.0 0.0 0.0"
				},
				RectTransform =
				{
					AnchorMin = "0.88 0.94",
					AnchorMax = "1.0 1.0"
				},
				Text =
				{
					Text = "ⓧ",
					FontSize = 15,
					Color = "1.0 0.0 0.0 1.0",
					Align = TextAnchor.MiddleCenter
				}
			}, OnlinePlayersUI);
			// Online Player Box
			if (config.ShowOnlineCount)
			{
				OnlinePlayersUIelements.Add(DefaultUILabel($"{OPLang("OP_01", player.UserIDString, count)}", 1, 0.095f, TextAnchor.MiddleLeft, 15, "0.01", "0.75", "1.0 1.0 1.0 1.0"), OnlinePlayersUI);
			}
			else
			{
				OnlinePlayersUIelements.Add(DefaultUILabel($"{OPLang("OP_03", player.UserIDString)}", 1, 0.095f, TextAnchor.MiddleLeft, 15, "0.01", "0.75", "1.0 1.0 1.0 1.0"), OnlinePlayersUI);
			}
			// UI End
			CuiHelper.AddUi(player, OnlinePlayersUIelements);
			return;
		}
        private void PlayerOnlineUIList(BasePlayer player, int from = 0)
		{
			if (player == null) return;
            _onlineUIPage[player.userID] = from;
            int current = 0;
            float height = 0.05f;
            var OnlinePlayersUIelements = new CuiElementContainer();
			var rank = "";
			var level = "";
			// Online Player Box
			OnlinePlayersUIelements.Add(DefaultUIPanel("0.0 0.10", "0.995 0.90", "0.0 0.0 0.0 0.50"), OnlinePlayersUI, OnlinePlayersUIInner);
            foreach (var p in onlinePlayers)
            {
                if (current >= from && current < from + 20)
                {
                    int pos = (current - from);
                    if (ImageLibrary != null && config.playeravatars)
                    {
                        OnlinePlayersUIelements.Add(DefaultUIImage(OnlinePlayersUIInner, p.Key.ToString(), pos + 1, height, "0", "0.05"));
                    }
					if (XPerience != null)
					{
						if (config.ShowRankSig)
						{
							rank = $" {XPerience.Call<string>("GetXPCache", FindPlayer(p.Key.ToString()), "ranksig")}";
						}
						if (config.ShowLevel)
						{
							level = $" [{XPerience.Call<string>("GetXPCache", FindPlayer(p.Key.ToString()), "level")}]";
						}
					}
					OnlinePlayersUIelements.Add(DefaultUILabel($"{p.Value}{rank}{level}", pos + 1, height, TextAnchor.UpperLeft, 12, "0.06", "0.75", "1 1 1 1"), OnlinePlayersUIInner);
					if (NTeleportation != null && config.ShowTPIcon)
                    {
						OnlinePlayersUIelements.Add(DefaultUIButton($"op.nteleportation {p.Value}", pos + 1, height, 12, "0 0 0 0", "⇆", "0.80", "0.85", TextAnchor.UpperLeft, "0 1 0 1"), OnlinePlayersUIInner);
                    }
					if (XPerience != null && config.ShowXPIcon)
                    {
						OnlinePlayersUIelements.Add(DefaultUIButton($"op.playerprofiles xperience {p.Key}", pos + 1, height, 12, "0 0 0 0", "⇪", "0.85", "0.90", TextAnchor.UpperLeft, "0 0 1 1"), OnlinePlayersUIInner);
                    }
                    if (KillRecords != null && config.ShowKRIcon)
                    {
                        OnlinePlayersUIelements.Add(DefaultUIButton($"op.playerprofiles killrecords {p.Key}", pos + 1, height, 10, "0 0 0 0", "☠", "0.90", "0.95", TextAnchor.UpperLeft, "1 0 0 1"), OnlinePlayersUIInner);
                    }
                    if (DiscordReport != null && config.ShowDRIcon)
                    {
						var iplayer = FindPlayer(p.Key.ToString());
                        OnlinePlayersUIelements.Add(DefaultUIButton($"op.playerprofiles discordreport {iplayer}", pos + 1, height, 10, "0 0 0 0", "☄", "0.95", "1", TextAnchor.UpperLeft, "1 0 0 1"), OnlinePlayersUIInner);
                    }
					current++;
                }
                current++;
			}
            int minfrom = from <= 10 ? 0 : from - 10;
            int maxfrom = from + 10 >= current ? from : from + 10;
            PlayerOnlineUIPages(player, minfrom, maxfrom, from);
            // UI End
			CuiHelper.AddUi(player, OnlinePlayersUIelements);
        }
        private void PlayerOnlineUIPages(BasePlayer player, int next, int back, int from)
		{
            float height = 1f;
            var OnlinePlayersUIelements = new CuiElementContainer();
			OnlinePlayersUIelements.Add(DefaultUIPanel("0.0 0.0", "0.995 0.10", "0.0 0.0 0.0 0.0"), OnlinePlayersUI, OnlinePlayersUIPages);
			// Next Page
            if (from >= 1)
			{
				OnlinePlayersUIelements.Add(DefaultUIButton($"op.playerpages {next}", 1, height, 25, "0 0 0 0", "⇧", "0.0", "0.25", TextAnchor.MiddleCenter, "1.0 1.0 0.0 1.0"), OnlinePlayersUIPages);		
			}
			// Prev Page
			if (from + 10 < onlinePlayers.Count)
			{
				OnlinePlayersUIelements.Add(DefaultUIButton($"op.playerpages {back}", 1, height, 25, "0 0 0 0", "⇩", "0.25", "0.50", TextAnchor.MiddleCenter, "1.0 1.0 0.0 1.0"), OnlinePlayersUIPages);
			}
			// Refresh Page
			OnlinePlayersUIelements.Add(DefaultUIButton($"op.refreshplayers", 1, height, 25, "0 0 0 0", "↺", "0.50", "0.75", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), OnlinePlayersUIPages);
			// Switch To Sleeper Page
			OnlinePlayersUIelements.Add(DefaultUIButton($"op.refreshsleepers", 1, height, 25, "0 0 0 0", "⊷", "0.75", "1.0", TextAnchor.UpperCenter, "0.0 1.0 1.0 1.0"), OnlinePlayersUIPages);
			// UI End
			CuiHelper.AddUi(player, OnlinePlayersUIelements);
			return;
		}
		private void HUDTimer(BasePlayer player, bool active = true) 
		{
			if (active)
			{
                _playerdata = timer.Every(config.HUDrefreshrate, () =>
				{
					OnlineHUD(player);
				});
			} 
            if (!active)
			{
                _playerdata?.Destroy();
            }
        }
		// Sleepers
		private void SleeperPlayerUI(BasePlayer player)
			{
				if (player == null) return;
				DestroyUi(player, OnlinePlayersUI);
				DestroyUi(player, SleeperPlayersUI);
				int count = sleeperPlayers.Count;
				var SleeperPlayersUIelements = new CuiElementContainer();
				// Main UI
				double UILeft = config.UILeft;
				double UITop = config.UITop;
				if (UILeft > 0.80) { UILeft = 0.80; }
				if (UILeft < 0) { UILeft = 0; }
				if (UITop > 1) { UITop = 1; }
				if (UITop < 0.45) { UITop = 0.45; }
				SleeperPlayersUIelements.Add(new CuiPanel
				{
					Image =
					{
						Color = "0.1 0.1 0.1 0.95"
					},
					RectTransform =
					{
						AnchorMin = $"{UILeft} {UITop - 0.45}",
						AnchorMax = $"{UILeft + 0.20} {UITop}"
					},
					CursorEnabled = true
				}, "Hud", SleeperPlayersUI);
				// Close Button
				SleeperPlayersUIelements.Add(new CuiButton
				{
					Button =
					{
						Close = SleeperPlayersUI,
						Color = "0.0 0.0 0.0 0.0"
					},
					RectTransform =
					{
						AnchorMin = "0.88 0.94",
						AnchorMax = "1.0 1.0"
					},
					Text =
					{
						Text = "ⓧ",
						FontSize = 15,
						Color = "1.0 0.0 0.0 1.0",
						Align = TextAnchor.MiddleCenter
					}
				}, SleeperPlayersUI);
				// Online Player Box
				if (config.ShowSleeperCount)
				{
					SleeperPlayersUIelements.Add(DefaultUILabel($"{OPLang("OP_02", player.UserIDString, count)}", 1, 0.095f, TextAnchor.MiddleLeft, 15, "0.01", "0.75", "1.0 1.0 1.0 1.0"), SleeperPlayersUI);
				}
				else
				{
					SleeperPlayersUIelements.Add(DefaultUILabel($"{OPLang("OP_04", player.UserIDString)}", 1, 0.095f, TextAnchor.MiddleLeft, 15, "0.01", "0.75", "1.0 1.0 1.0 1.0"), SleeperPlayersUI);
				}
				// UI End
				CuiHelper.AddUi(player, SleeperPlayersUIelements);
				return;
			}
        private void SleeperPlayerUIList(BasePlayer player, int from = 0)
		{
			if (player == null) return;
            _sleeperPlayersUIPage[player.userID] = from;
            int current = 0;
            float height = 0.05f;
            //float avatarheight = 0.05f;
            var SleeperPlayersUIelements = new CuiElementContainer();
			var rank = "";
			var level = "";
			// Online Player Box
			SleeperPlayersUIelements.Add(DefaultUIPanel("0 .10", ".995 .90", "0 0 0 .50"), SleeperPlayersUI, SleeperPlayersUIInner);
			foreach (var p in sleeperPlayers)
			{
				if (current >= from && current < from + 20)
				{
					int pos = (current - from);
                    if (ImageLibrary != null && config.playeravatars)
                    {
                        SleeperPlayersUIelements.Add(DefaultUIImage(SleeperPlayersUIInner, p.Key.ToString(), pos + 1, height, "0", "0.05"));
                    }
                    if (XPerience != null)
					{
                        if (config.ShowRankSig)
                        {
                            rank = $" {XPerience.Call<string>("GetXPCache", FindPlayer(p.Key.ToString()), "ranksig")}";
                        }
                        if (config.ShowLevel)
                        {
                            level = $" [{XPerience.Call<string>("GetXPCache", FindPlayer(p.Key.ToString()), "level")}]";
                        }
                    }
                    SleeperPlayersUIelements.Add(DefaultUILabel($"{p.Value}{rank}{level}", pos + 1, height, TextAnchor.UpperLeft, 12, "0.06", "0.75", "1 1 1 1"), SleeperPlayersUIInner);
					if (XPerience != null && config.ShowXPIcon)
					{
						SleeperPlayersUIelements.Add(DefaultUIButton($"op.sleeperprofiles xperience {p.Key}", pos + 1, height, 12, "0 1 0 0", "⇪", "0.80", "0.85", TextAnchor.UpperLeft, "0 0 1 1"), SleeperPlayersUIInner);
					}
					if (KillRecords != null && config.ShowKRIcon)
					{
						SleeperPlayersUIelements.Add(DefaultUIButton($"op.sleeperprofiles killrecords {p.Value}", pos + 1, height, 10, "0 1 0 0", "☠", "0.90", "0.95", TextAnchor.UpperLeft, "1 0 0 1"), SleeperPlayersUIInner);
					}
                    current++;
                }
                current++;
			}
            int minfrom = from <= 10 ? 0 : from - 10;
            int maxfrom = from + 10 >= current ? from : from + 10;
			SleeperPlayerUIPages(player, minfrom, maxfrom, from);         
            // UI End
			CuiHelper.AddUi(player, SleeperPlayersUIelements);
        }
        private void SleeperPlayerUIPages(BasePlayer player, int next, int back, int from)
		{
            float height = 1f;
            var SleeperPlayersUIelements = new CuiElementContainer();
			// Next Page
			SleeperPlayersUIelements.Add(DefaultUIPanel("0.0 0.0", "0.995 0.10", "0.0 0.0 0.0 0.0"), SleeperPlayersUI, SleeperPlayersUIPages);
            if (from >= 1)
			{
				SleeperPlayersUIelements.Add(DefaultUIButton($"op.sleeperpages {next}", 1, height, 25, "0 0 0 0", "⇧", "0.0", "0.25", TextAnchor.MiddleCenter, "1.0 1.0 0.0 1.0"), SleeperPlayersUIPages);		
			}
			// Prev Page
			if (from + 10 < sleeperPlayers.Count)
			{
				SleeperPlayersUIelements.Add(DefaultUIButton($"op.sleeperpages {back}", 1, height, 25, "0 0 0 0", "⇩", "0.25", "0.50", TextAnchor.MiddleCenter, "1.0 1.0 0.0 1.0"), SleeperPlayersUIPages);
			}
			// Refresh Page
			SleeperPlayersUIelements.Add(DefaultUIButton($"op.refreshsleepers", 1, height, 25, "0 0 0 0", "↺", "0.50", "0.75", TextAnchor.MiddleCenter, "0.0 1.0 0.0 1.0"), SleeperPlayersUIPages);
			// Switch To Online Page 
			SleeperPlayersUIelements.Add(DefaultUIButton($"op.refreshplayers", 1, height, 25, "0 0 0 0", "⊶", "0.75", "1.0", TextAnchor.UpperCenter, "0.0 1.0 1.0 1.0"), SleeperPlayersUIPages);
			// UI End
			CuiHelper.AddUi(player, SleeperPlayersUIelements);
			return;
		}
		// Destroy UI
		private void DestroyUi(BasePlayer player, string name)
		{
			CuiHelper.DestroyUi(player, name);
		}
        private void ClearPlayerUIs(BasePlayer player, bool hud = false)
        {
            DestroyUi(player, OnlinePlayersUI);
            DestroyUi(player, OnlinePlayersUIInner);
            DestroyUi(player, SleeperPlayersUI);
            DestroyUi(player, SleeperPlayersUIInner);
			if(hud)
			{
				DestroyUi(player, OnlinePlayersHUD);
			}
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["OP_01"] = "Online Players: {0}",
				["OP_02"] = "Sleeping Players: {0}",
				["OP_03"] = "Online Players:",
				["OP_04"] = "Sleeping Players:",

			}, this);
		}
		private string OPLang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}