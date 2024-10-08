using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using QuestPatcher.Core;
using QuestPatcher.Core.Models;
using QuestPatcher.ModBrowser;
using QuestPatcher.ModBrowser.Models;
using QuestPatcher.Models;
using ReactiveUI;
using Serilog;

namespace QuestPatcher.ViewModels.ModBrowser
{
    public class BrowseModViewModel : ViewModelBase
    {
        public enum ModListState
        {
            Loading,
            Empty,
            ModsLoaded,
            LoadError
        }
        
        private readonly Window _window;
        private readonly Config _config;
        private readonly InstallManager _installManager;
        private readonly ExternalModManager _externalModManager;

        public ObservableCollection<ExternalModViewModel> Mods { get; } = new ObservableCollection<ExternalModViewModel>();

        public OperationLocker Locker { get; }
        public ProgressViewModel ProgressView { get; }

        private ModListState _state = ModListState.Empty;
        // do this so that the UI can bind to the state without using a long converter as below
        // Converter={x:Static ObjectConverters.Equal}, ConverterParameter={x:Static modBrowserVMs:ModListState.ModsLoaded}
        public bool IsModLoading => State == ModListState.Loading;
        public bool IsModListEmpty => State == ModListState.Empty;
        public bool IsModListLoaded => State == ModListState.ModsLoaded;
        public bool IsModLoadError => State == ModListState.LoadError;

        public ModListState State
        {
            get => _state;
            private set
            {
                _state = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(IsModLoading));
                this.RaisePropertyChanged(nameof(IsModListEmpty));
                this.RaisePropertyChanged(nameof(IsModListLoaded));
                this.RaisePropertyChanged(nameof(IsModLoadError));
            }
        }
        public string EmptyMessage { get; private set; } = "";
        
        public Exception? LoadError { get; private set; } = null;

        public BrowseModViewModel(Window window, Config config, OperationLocker locker, ProgressViewModel progressView, InstallManager installManager, ExternalModManager externalModManager)
        {
            _window = window;
            _config = config;
            Locker = locker;
            ProgressView = progressView;
            _installManager = installManager;
            _externalModManager = externalModManager;

            _installManager.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(InstallManager.InstalledApp))
                {
                    Task.Run(LoadMods);
                }
            };
        }

        public async Task LoadMods()
        {
            string? version = _installManager.InstalledApp?.Version;
            if (version == null) return;
            if (State == ModListState.Loading) return;
            State = ModListState.Loading;
            await Task.Delay(300); // Delay to prevent flickering
            Mods.Clear();
            try
            {
                var mods = await _externalModManager.GetAvailableMods(version);
                if (mods == null)
                {
                    // network error
                    SetListState(ModListState.LoadError, "出错了！\n无法加载Mod列表，请检查网络连接并重试");
                }
                else if (mods.Count == 0)
                {
                    SetListState(ModListState.Empty, $"当前游戏版本 {version} 暂无可用Mod");
                }
                else
                {
                    foreach (var mod in mods)
                    {
                        Mods.Add(new ExternalModViewModel(mod, Locker));
                    }
                    SetListState(ModListState.ModsLoaded, "");
                }
            }
            catch (Exception e)
            {
                // unexpected error
                Log.Error("Unexpected error when loading mods: {Message}", e.Message);
                SetListState(ModListState.LoadError, "加载Mod列表时发生了意外错误", e);
            }

            Debug.Assert(State != ModListState.Loading);
        }

        private void SetListState(ModListState state, string message, Exception? exception = null)
        {
            Log.Debug("Setting state to {State} ", state);
            State = state;
            EmptyMessage = message;
            LoadError = exception;
            this.RaisePropertyChanged(nameof(EmptyMessage));
            this.RaisePropertyChanged(nameof(LoadError));
        }

        public async Task OnInstallClicked()
        {
            try
            {
                Locker.StartOperation();
                foreach (var modVm in Mods)
                {
                    if (modVm.IsChecked)
                    {
                        // Install mod
                        var mod = modVm.Mod;
                        Log.Debug("Installing {Mod}", mod);
                        await _externalModManager.InstallMod(mod);
                        modVm.ClearSelection();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                Locker.FinishOperation();
            }
        }
    }
}
