using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using QuestPatcher.Core;
using QuestPatcher.Core.Models;
using QuestPatcher.Core.Patching;
using QuestPatcher.Models;
using QuestPatcher.Views;
using ReactiveUI;
using Serilog;

namespace QuestPatcher.ViewModels
{
    public class PatchingViewModel : ViewModelBase
    {
        public bool IsPatchingInProgress { get => _isPatchingInProgress; set { if (_isPatchingInProgress != value) { this.RaiseAndSetIfChanged(ref _isPatchingInProgress, value); } } }
        private bool _isPatchingInProgress;

        public string PatchingStageText { get; private set; } = "";

        public string? CustomSplashPath => Config.PatchingOptions.CustomSplashPath;

        public Config Config { get; }

        public OperationLocker Locker { get; }

        public ProgressViewModel ProgressBarView { get; }

        public ExternalFilesDownloader FilesDownloader { get; }

        private readonly PatchingManager _patchingManager;
        private readonly InstallManager _installManager;
        private readonly Window _mainWindow;

        public PatchingViewModel(Config config, OperationLocker locker, PatchingManager patchingManager, InstallManager installManager, Window mainWindow, ProgressViewModel progressBarView, ExternalFilesDownloader filesDownloader)
        {
            Config = config;
            Locker = locker;
            ProgressBarView = progressBarView;
            FilesDownloader = filesDownloader;

            _patchingManager = patchingManager;
            _installManager = installManager;
            _mainWindow = mainWindow;

            _patchingManager.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(_patchingManager.PatchingStage))
                {
                    OnPatchingStageChange(_patchingManager.PatchingStage);
                }
            };
        }

        public async void StartPatching()
        {
            if (Config.PatchingOptions.FlatScreenSupport)
            {
                // Disable VR requirement apparently causes infinite load
                var builder = new DialogBuilder
                {
                    Title = "禁用VR要求已启用",
                    Text = "您在补丁选项中禁用了VR要求，这可能会导致出现错误，例如启动游戏时无限加载"
                };
                
                builder.OkButton.Text = "仍然继续";
                if (!await builder.OpenDialogue(_mainWindow))
                {
                    return;
                }
            }

            IsPatchingInProgress = true;
            Locker.StartOperation();
            try
            {
                await _patchingManager.PatchApp();
            }
            catch (FileDownloadFailedException ex)
            {
                Log.Error("Patching failed as essential files could not be downloaded: {Message}", ex.Message);

                DialogBuilder builder = new()
                {
                    Title = "Could not download files",
                    Text = "QuestPatcher could not download files that it needs to patch the APK. Please check your internet connection, then try again.",
                    HideCancelButton = true
                };

                await builder.OpenDialogue(_mainWindow);
            }
            catch (Exception ex)
            {
                // Print troubleshooting information for debugging
                Log.Error(ex, $"Patching failed!");
                DialogBuilder builder = new()
                {
                    Title = "完蛋!出错了",
                    Text = "在给游戏打补丁的过程中出现了一个意料外的错误。",
                    HideCancelButton = true
                };
                builder.WithException(ex);

                await builder.OpenDialogue(_mainWindow);
            }
            finally
            {
                IsPatchingInProgress = false;
                Locker.FinishOperation();
            }

            if (_installManager.InstalledApp?.IsModded ?? false)
            {
                // Display a dialogue to give the user some info about what to expect next, and to avoid them pressing restore app by mistake
                Log.Debug("Patching completed successfully, displaying info dialogue");
                DialogBuilder builder = new()
                {
                    Title = "完工!",
                    Text = "你的游戏现在已经打完补丁啦\n现在你可以安装Mod了！" +
                    "\n\n提示：如果你在头显里面看到了一个“恢复的应用”窗口，不必惊慌，只用点击取消即可。Oculus不会因为打Mod封号，所以没啥好担心的。",
                    HideCancelButton = true
                };
                await builder.OpenDialogue(_mainWindow);
            }
        }

        public async void SelectSplashPath()
        {
            try
            {
                var files = await _mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    FileTypeFilter = new[]
                    {
                        FilePickerFileTypes.ImagePng
                    }
                });
                Config.PatchingOptions.CustomSplashPath = files.FirstOrDefault()?.Path.LocalPath;
                this.RaisePropertyChanged(nameof(CustomSplashPath));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to select splash screen path");
            }
        }

        /// <summary>
        /// Updates the patching stage text in the view
        /// </summary>
        /// <param name="stage">The new patching stage</param>
        private void OnPatchingStageChange(PatchingStage stage)
        {
            PatchingStageText = stage switch
            {
                PatchingStage.NotStarted => "未开始",
                PatchingStage.FetchingFiles => "下载打补丁所需的文件 (1/6)",
                PatchingStage.MovingToTemp => "将APK移动至指定位置 (2/6)",
                PatchingStage.Patching => "更改APK文件来使其支持安装mod (3/6)",
                PatchingStage.Signing => "给APK签名 (4/6)",
                PatchingStage.UninstallingOriginal => "卸载原有的APK (5/6)",
                PatchingStage.InstallingModded => "安装改过的APK (6/6)",
                _ => throw new NotImplementedException()
            };
            this.RaisePropertyChanged(nameof(PatchingStageText));
        }
    }
}
