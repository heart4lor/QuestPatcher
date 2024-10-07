using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using QuestPatcher.Core;
using QuestPatcher.Models;
using QuestPatcher.Services;
using ReactiveUI;
using Serilog;

namespace QuestPatcher.ViewModels
{
    public class GameInstallerViewModel : ViewModelBase
    {
        private readonly Window _window;
        private readonly QuestPatcherUiService _uiService;
        private readonly OperationLocker _locker;
        private readonly InstallManager _installManager;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                this.RaisePropertyChanged();
            }
        }

        private string _apkPath = "";
        public string ApkPath
        {
            get => _apkPath;
            private set
            {
                _apkPath = value;
                this.RaisePropertyChanged();
            }
        }

        private IReadOnlyList<string> _obbPaths = new List<string>();
        public IReadOnlyList<string> ObbPaths {
            get => _obbPaths;
            private set
            {
                _obbPaths = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(ObbSelected));
            }
        }

        public bool ObbSelected => ObbPaths.Count > 0;

        public GameInstallerViewModel(Window window, QuestPatcherUiService uiService, OperationLocker locker, InstallManager installManager)
        {
            _window = window;
            _uiService = uiService;
            _locker = locker;
            _installManager = installManager;
            
            window.Closing += (sender, args) =>
            {
                if (IsLoading)
                {
                    args.Cancel = true;
                }
            };
        }


        public async Task SelectApk()
        {
            var options = new FilePickerOpenOptions
            {
                AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("Beat Saber APK") { Patterns = new[] { "*.apk" } } }
            };
            var files = await _window.StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
            {
                ApkPath = "";
                return;
            }
            string file = files[0].Path.LocalPath;
            if (!file.EndsWith(".apk"))
            {
                DialogBuilder builder1 = new()
                {
                    Title = "你选择的文件有误",
                    Text = "你选择的文件有误，请重新选择。",
                    HideCancelButton = true
                };
                await builder1.OpenDialogue(_window);
                return;
            }
            ApkPath = file;
        }

        public async Task SelectObb()
        {
            var options = new FilePickerOpenOptions
            {
                AllowMultiple = true, FileTypeFilter = new[] { new FilePickerFileType("Obb 文件") { Patterns = new[] { "*.obb" } } }
            };
            var files = await _window.StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
            {
                ObbPaths = new List<string>();
                return;
            }

            ObbPaths = files.Select(file => file.Path.LocalPath).ToList();
        }

        public async Task InstallGame()
        {
            Log.Debug("Installing game with {ApkPath} and {ObbCount} obb files", ApkPath, ObbPaths.Count);
        }
    }
}
