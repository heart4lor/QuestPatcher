using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using QuestPatcher.Core;
using QuestPatcher.Core.Models;
using QuestPatcher.Resources;
using QuestPatcher.Services;
using QuestPatcher.ViewModels;
using QuestPatcher.Views;
using QuestPatcher.Utils;
using SemVer = SemanticVersioning.Version;

namespace QuestPatcher
{
    public class UIPrompter : IUserPrompter
    {
        private Window? _mainWindow;
        private Config? _config;
        private QuestPatcherUiService? _uiService;
        private SpecialFolders? _specialFolders;

        /// <summary>
        /// This exists instead of a constructor since the prompter must be immediately passed on QuestPatcherService's creation, so we initialise its members after the fact.
        /// Maybe there's a better workaround, but this works fine for now
        /// </summary>
        public void Init(Window mainWindow, Config config, QuestPatcherUiService uiService, SpecialFolders specialFolders)
        {
            _mainWindow = mainWindow;
            _config = config;
            _uiService = uiService;
            _specialFolders = specialFolders;
        }
        
        public async Task<bool> CheckUpdate()
        {
#if DEBUG
            return true;
#endif
            
            try
            {
                JsonNode? res = null;
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4692.71 Safari/537.36");
                client.DefaultRequestHeaders.Add("accept", "application/json");
                // try
                // {
                //     res = JsonNode.Parse(await client.GetStringAsync(@"https://beatmods.wgzeyu.com/githubapi/MicroCBer/QuestPatcher/latest"));
                // }
                // catch (Exception e)
                // {
                //     res = JsonNode.Parse(await client.GetStringAsync(@"https://api.github.com/repos/MicroCBer/QuestPatcher/releases/latest"));
                // }
                
                res = JsonNode.Parse(await client.GetStringAsync(@"https://api.github.com/repos/MicroCBer/QuestPatcher/releases/latest"));
                
                string? newest = res?["tag_name"]?.ToString();
                if (newest == null) throw new Exception("Failed to check update.");

                bool isLatest = SemVer.TryParse(newest, out var latest) && latest == VersionUtil.QuestPatcherVersion;
                
                if (!isLatest)
                {
                    DialogBuilder builder = new()
                    {
                        Title = "有更新！",
                        Text = $"**不更新软件，可能会遇到未知问题，强烈建议更新至最新版**\n" +
                        $"同时，非最新版本将不受支持且不保证没有安全问题\n\n" +
                        $"您的版本 - v{VersionUtil.QuestPatcherVersion}\n" +
                        $"最新版本 - v{latest?.ToString() ?? newest}",
                        HideOkButton = true,
                        HideCancelButton = true
                    };
                    builder.WithButtons(
                        new ButtonInfo
                         {
                             Text = "进入QP教程",
                             CloseDialogue = true,
                             ReturnValue = true,
                             OnClick = () => Util.OpenWebpage("https://bs.wgzeyu.com/oq-guide-qp/#install_qp")
                         }, 
                        new ButtonInfo
                        {
                            Text = "进入网盘下载",
                            CloseDialogue = true,
                            ReturnValue = true,
                            OnClick = () => Util.OpenWebpage("http://share.wgzeyu.vip/?Ly8lRTUlQjclQTUlRTUlODUlQjclRUYlQkMlODglRTUlQTYlODJNb2QlRTUlQUUlODklRTglQTMlODUlRTUlOTklQTglRTMlODAlODElRTglQjAlQjElRTklOUQlQTIlRTclQkMlOTYlRTglQkUlOTElRTUlOTklQTglRTclQUQlODlCUyVFNyU5QiVCOCVFNSU4NSVCMyVFOCVCRCVBRiVFNCVCQiVCNiVFRiVCQyU4OS8=")
                        },
                        new ButtonInfo
                        {
                            Text = "进入GitHub下载",
                            CloseDialogue = true,
                            ReturnValue = true,
                            OnClick = () => Util.OpenWebpage("https://github.com/MicroCBer/QuestPatcher/releases/latest")
                        });

                    await builder.OpenDialogue(_mainWindow);
                }
                return true;
            }
            catch (Exception ex)
            {
                DialogBuilder builder = new()
                {
                    Title = "检查更新失败"+ex,
                    Text = "请手动检查更新",
                    HideOkButton = true
                };
                builder.WithButtons(
                    new ButtonInfo
                    {
                        Text = "进入QP教程",
                        CloseDialogue = true,
                        ReturnValue = true,
                        OnClick = () => Util.OpenWebpage("https://bs.wgzeyu.com/oq-guide-qp/#install_qp")
                    }, 
                    new ButtonInfo
                    {
                        Text = "进入网盘下载",
                        CloseDialogue = true,
                        ReturnValue = true,
                        OnClick = () => Util.OpenWebpage("http://share.wgzeyu.vip/?Ly8lRTUlQjclQTUlRTUlODUlQjclRUYlQkMlODglRTUlQTYlODJNb2QlRTUlQUUlODklRTglQTMlODUlRTUlOTklQTglRTMlODAlODElRTglQjAlQjElRTklOUQlQTIlRTclQkMlOTYlRTglQkUlOTElRTUlOTklQTglRTclQUQlODlCUyVFNyU5QiVCOCVFNSU4NSVCMyVFOCVCRCVBRiVFNCVCQiVCNiVFRiVCQyU4OS8=")
                    },
                    new ButtonInfo
                    {
                        Text = "进入GitHub下载",
                        CloseDialogue = true,
                        ReturnValue = true,
                        OnClick = () => Util.OpenWebpage("https://github.com/MicroCBer/QuestPatcher/releases/latest")
                    });

                await builder.OpenDialogue(_mainWindow);
                return false; 
            }
        }
        
        public Task<bool> PromptAppNotInstalled()
        {
            Debug.Assert(_config != null);

            var builder = new DialogBuilder
            {
                Title = Strings.Prompt_AppNotInstalled_Title,
                Text = string.Format(Strings.Prompt_AppNotInstalled_Text, _config.AppId),
                HideOkButton = true
            };
            builder.CancelButton.Text = Strings.Generic_Close;
            builder.WithButtons(
                new ButtonInfo
                {
                    Text = Strings.Prompt_AppNotInstalled_ChangeApp,
                    CloseDialogue = true,
                    ReturnValue = true,
                    OnClick = async () =>
                    {
                        Debug.Assert(_uiService != null);
                        await _uiService.OpenChangeAppMenu(true);
                    }
                }
            );

            return builder.OpenDialogue(_mainWindow);
        }

        public Task<bool> PromptAdbDisconnect(DisconnectionType type)
        {
            var builder = new DialogBuilder();
            builder.OkButton.Text = Strings.Generic_Retry;

            switch (type)
            {
                case DisconnectionType.NoDevice:
                    builder.Title = Strings.Prompt_AdbDisconnect_NoDevice_Title;
                    builder.Text = Strings.Prompt_AdbDisconnect_NoDevice_Text;
                    builder.WithButtons(
                        new ButtonInfo
                        {
                            Text = "BeatSaber新手教程",
                            OnClick = () => Util.OpenWebpage("https://bs.wgzeyu.com/oq-guide-qp/")
                        }
                    );
                    break;
                case DisconnectionType.DeviceOffline:
                    builder.Title = Strings.Prompt_AdbDisconnect_Offline_Title;
                    builder.Text = Strings.Prompt_AdbDisconnect_Offline_Text;
                    break;
                case DisconnectionType.Unauthorized:
                    builder.Title = Strings.Prompt_AdbDisconnect_Unauthorized_Title;
                    builder.Text = Strings.Prompt_AdbDisconnect_Unauthorized_Text;
                    break;
                default:
                    throw new NotImplementedException($"Variant {type} has no fallback/dialogue box");
            }

            return builder.OpenDialogue(_mainWindow);
        }

        public Task<bool> PromptUnstrippedUnityUnavailable()
        {
            var builder = new DialogBuilder
            {
                Title = Strings.Prompt_NoUnstrippedUnity_Title,
                Text = Strings.Prompt_NoUnstrippedUnity_Text
            };
            builder.OkButton.Text = Strings.Generic_ContinueAnyway;

            return builder.OpenDialogue(_mainWindow);
        }

        public Task<bool> Prompt32Bit()
        {
            var builder = new DialogBuilder
            {
                Title = Strings.Prompt_32Bit_Title,
                Text = Strings.Prompt_32Bit_Text
            };
            builder.OkButton.Text = Strings.Generic_ContinueAnyway;

            return builder.OpenDialogue(_mainWindow);
        }

        public Task<bool> PromptUnknownModLoader()
        {
            var builder = new DialogBuilder
            {
                Title = Strings.Prompt_UnknownModLoader_Title,
                Text = Strings.Prompt_UnknownModLoader_Text
            };
            builder.OkButton.Text = Strings.Generic_ContinueAnyway;

            return builder.OpenDialogue(_mainWindow);
        }

        public Task PromptUpgradeFromOld()
        {
            var builder = new DialogBuilder
            {
                Title = Strings.Prompt_UpgradeFromOld_Title,
                Text = Strings.Prompt_UpgradeFromOld_Text,
                HideCancelButton = true
            };

            return builder.OpenDialogue(_mainWindow);
        }

        public async Task<AdbDevice?> PromptSelectDevice(List<AdbDevice> devices)
        {
            var viewModel = new SelectDeviceWindowViewModel(devices);
            var window = new SelectDeviceWindow
            {
                DataContext = viewModel,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            viewModel.DeviceSelected += (_, device) => window.Close();
            await window.ShowDialog(_mainWindow!);

            return viewModel.SelectedDevice;
        }
    }
}
