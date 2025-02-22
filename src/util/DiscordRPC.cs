using Avalonia.Controls;
using DiscordRPC;

namespace PD3AudioModder
{
    internal class DiscordRPC
    {
        private AppConfig? _appConfig;
        private DiscordRpcClient? client;
        private RichPresence? presence;

        public void Initialize()
        {
            _appConfig = AppConfig.Load();
            if (client == null && _appConfig.RPCEnabled != false)
            {
                client = new DiscordRpcClient("1342384719043756032");
                client.Initialize();

                var (smallImageKey, smallImageText) = GetSmallImageInfo("Single File");

                // Default presence
                presence = new RichPresence()
                {
                    Details = "Modding Audio Files",
                    State = "Tab: Single File",
                    Assets = new Assets()
                    {
                        LargeImageKey = "pam-1024",
                        LargeImageText = "PD3AudioModder",
                        SmallImageKey = smallImageKey,
                        SmallImageText = smallImageText,
                    },
                    Timestamps = Timestamps.Now,
                };
                client.SetPresence(presence);
            }
        }

        private (string key, string text) GetSmallImageInfo(string tabHeader)
        {
            return tabHeader switch
            {
                "Single File" => ("file-bg", "Converting a single audio file"),
                "Batch Conversion" => ("files-bg", "Converting multiple audio files"),
                "Pack Files" => ("tools-bg", "Packing files"),
                _ => ("file-bg", "Converting a single audio file"), // Default
            };
        }

        public void UpdatePresence(TabControl currentTab, string modName)
        {
            _appConfig = AppConfig.Load();

            if (_appConfig!.RPCEnabled)
            {
                if (client == null || client.IsDisposed)
                {
                    client = new DiscordRpcClient("1342384719043756032");
                    client.Initialize();
                }
                presence = new RichPresence()
                {
                    Details = "Modding Audio Files",
                    State = "",
                    Assets = new Assets()
                    {
                        LargeImageKey = "pam-1024",
                        LargeImageText = "PD3AudioModder",
                    },
                    Timestamps = Timestamps.Now,
                };
                if (_appConfig.RPCDisplayTab && currentTab?.SelectedItem is TabItem selectedTab)
                {
                    presence.State = $"Tab: {selectedTab.Header}";
                    var (smallImageKey, smallImageText) = GetSmallImageInfo(
                        selectedTab.Header?.ToString() ?? ""
                    );
                    presence.Assets.SmallImageKey = smallImageKey;
                    presence.Assets.SmallImageText = smallImageText;
                }
                else
                {
                    presence.State = ""; // Clear the tab name if disabled
                }
                if (
                    _appConfig.RPCDisplayModName
                    && !string.IsNullOrEmpty(modName)
                    && modName != "Mod Name"
                )
                {
                    presence.Details = $"Modding: {modName}";
                }
                client.SetPresence(presence);
            }
            else if (client != null && !client.IsDisposed)
            {
                client.ClearPresence();
                client.Dispose();
                client = null;
            }
        }

        public void Dispose()
        {
            if (client != null && !client.IsDisposed)
            {
                client.ClearPresence();
                client.Dispose();
                client = null;
            }
        }
    }
}
