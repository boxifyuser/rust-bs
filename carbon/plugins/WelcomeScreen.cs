using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Welcome Screen", "Mevent", "1.1.0")]
    [Description("Showing welcoming image on player joining")]
    public class WelcomeScreen : RustPlugin
    {
        #region Config

        private static ConfigData _config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Image URL")]
            public string url = "https://i.imgur.com/RhMXzvF.jpg";

            [JsonProperty(PropertyName = "Fade-in duration")]
            public float fadeIn = 5f;

            [JsonProperty(PropertyName = "Fade-out duration")]
            public float fadeOut = 5f;

            [JsonProperty(PropertyName = "Delay after joining to create image")]
            public float delay = 10f;

            [JsonProperty(PropertyName = "Delay after creating image to start fade out")]
            public float duration = 20f;

            [JsonProperty(PropertyName = "Anchor min (left bottom coordinate)")]
            public string anchorMin = "0 0";

            [JsonProperty(PropertyName = "Anchor min (right top coordinate)")]
            public string anchorMax = "1 1";

            [JsonProperty(PropertyName = "Image transparency")]
            public float transparency = 1f;

            [JsonProperty(PropertyName = "Show multiple times?")]
            public bool MultipleTimes = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) LoadDefaultConfig();
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");

                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private static void ValidateConfig()
        {
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region Data

        private PluginData _data;

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> Players = new List<ulong>();
        }

        #endregion

        #region Hooks

        private void Init()
        {
            if (!_config.MultipleTimes)
                LoadData();

            LoadUi();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;
            
            if (!_config.MultipleTimes)
            {
                if (_data.Players.Contains(player.userID))
                    return;
                
                _data.Players.Add(player.userID);
            }

            if (_config.delay > 0)
                timer.Once(_config.delay, () => CreateGUI(player));
            else
                CreateGUI(player);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

            if (!_config.MultipleTimes)
                SaveData();

            _config = null;
        }

        #endregion

        #region Commands

        [ChatCommand("welcomescreen")]
        private void Cmd(BasePlayer player)
        {
            OnPlayerConnected(player);
        }

        #endregion

        #region Interface

        private const string Layer = "welcomescreen.main";

        private void CreateGUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, _mainUi);

            if (_config.duration > 0) timer.Once(_config.duration, () => CuiHelper.DestroyUi(player, Layer));
        }

        #endregion

        #region Utils

        private string _mainUi;

        private void LoadUi()
        {
            var container = new CuiElementContainer
            {
                new CuiElement
                {
                    Name = Layer,
                    FadeOut = _config.fadeOut,
                    Components =
                    {
                        new CuiRawImageComponent
                            {Color = $"1 1 1 {_config.transparency}", FadeIn = _config.fadeIn, Url = _config.url},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = _config.anchorMin,
                            AnchorMax = _config.anchorMax
                        }
                    }
                },
                new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = "0 0 0 0",
                            Close = Layer
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                }
            };

            _mainUi = CuiHelper.ToJson(container);
        }

        #endregion
    }
}