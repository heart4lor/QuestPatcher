using System;
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
using QuestPatcher.Models;
using ReactiveUI;
using Serilog;

namespace QuestPatcher.ViewModels
{
    public class DowngradeViewModel: ViewModelBase
    {
        private readonly Window _window;

        private readonly DowngradeManger _downgradeManger;
        
        private readonly InstallManager _installManager;
        
        private readonly OperationLocker _locker;

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

        public DowngradeViewModel(Window window, Config config, InstallManager installManager, DowngradeManger downgradeManger, OperationLocker locker)
        {
            _window = window;
            _config = config;
            _installManager = installManager;
            _downgradeManger = downgradeManger;
            _locker = locker;
            
            window.Opened += async (sender, args) => await LoadVersions();
            window.Closing += (sender, args) =>
            {
                if (IsLoading)
                {
                    args.Cancel = true;
                }
            };
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
        
        public async Task Downgrade()
        {
            Log.Debug("Selected version: {SelectedVersion}", SelectedToVersion);
            if (string.IsNullOrWhiteSpace(SelectedToVersion)) return;
            IsLoading = true;
            try
            {
                _locker.StartOperation();
                await _downgradeManger.DowngradeApp(_installManager.InstalledApp!.Version, SelectedToVersion);
                IsLoading = false;
                var diglog = new DialogBuilder
                {
                    Title = "Downgrade Succeeded",
                    Text = "Now you can patch and mod the app.",
                    HideCancelButton = true
                };
                await diglog.OpenDialogue();
            }
            catch (Exception e)
            {
                var diglog = new DialogBuilder
                {
                    Title = "Downgrade failed",
                    Text = "An error occurred while downgrading the app. Please check the logs for more information.",
                    HideCancelButton = true
                };
                
                diglog.WithException(e);
                await diglog.OpenDialogue();
            }
            finally
            {
                _locker.FinishOperation();
                IsLoading = false;
                _window.Close();
            }
        }
    }
}
