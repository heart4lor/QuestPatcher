using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using QuestPatcher.Core;
using QuestPatcher.Core.Downgrading;
using QuestPatcher.Core.Models;
using ReactiveUI;
using Serilog;

namespace QuestPatcher.ViewModels
{
    public class DowngradeViewModel: ViewModelBase
    {
        private readonly Window _window;

        private readonly DowngradeManger _downgradeManger;
        
        private readonly InstallManager _installManager;

        private bool _isLoading = true;
        
        private readonly Config _config;

        public ObservableCollection<string> AvailableToVersions { get; set; } = new ObservableCollection<string>();
        
        public string SelectedToVersion { get; set; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                this.RaisePropertyChanged();
            }
        }

        public DowngradeViewModel(Window window, Config config, InstallManager installManager, DowngradeManger downgradeManger)
        {
            _window = window;
            _config = config;
            _installManager = installManager;
            _downgradeManger = downgradeManger;
            
            window.Opened += async (sender, args) => await LoadVersions();
        }

        public async Task LoadVersions()
        {
            IsLoading = true;
            Log.Debug("Loading available versions...");
            await _downgradeManger.LoadAvailableDowngrades();
            var paths = _downgradeManger.GetAvailablePathFor(_installManager.InstalledApp?.Version ?? "");
            Log.Debug("Available paths: {Paths}", paths);
            AvailableToVersions.Clear();
            AvailableToVersions.AddRange(paths);
            
            if (!AvailableToVersions.Contains(SelectedToVersion))
            {
                SelectedToVersion = "";
                this.RaisePropertyChanged(nameof(SelectedToVersion));
            }
            
            IsLoading = false;
        }
        
        public void Downgrade()
        {
            Log.Debug("Selected version: {SelectedVersion}", SelectedToVersion);
            if (string.IsNullOrWhiteSpace(SelectedToVersion)) return;
            // _downgradeManger.Downgrade();
        }
    }
}
