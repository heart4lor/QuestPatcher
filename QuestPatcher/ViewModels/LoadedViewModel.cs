using System;
using System.Diagnostics;
using ReactiveUI;
using QuestPatcher.ViewModels.Modding;
using QuestPatcher.Core.Models;
using Avalonia.Input;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Net;
using Serilog;
using QuestPatcher.Core;
using QuestPatcher.Utils;
using QuestPatcher.Views;

#pragma warning disable CA1822
namespace QuestPatcher.ViewModels
{
    public class LoadedViewModel : ViewModelBase
    {
        public void ShowTutorial()
        {
            Util.OpenWebpage("https://bs.wgzeyu.com/oq-guide-qp/");
        }
        public void OpenSourceAddr()
        {
            Util.OpenWebpage("https://github.com/MicroCBer/QuestPatcher");
        }
        public void OpenSourceFKAddr()
        {
            Util.OpenWebpage("https://github.com/Lauriethefish/QuestPatcher");
        }
        public void WGZEYUAddr()
        {
            Util.OpenWebpage("https://space.bilibili.com/557131");
        }
        
        public void MBAddr()
        {
            Util.OpenWebpage("https://space.bilibili.com/413164365");
        }

        public void SkyQeAddr()
        {
            Util.OpenWebpage("https://space.bilibili.com/3744764");
        }
        
        public string SelectedAppText => $"Modified by MicroBlock";
        
        public PatchingViewModel PatchingView { get; }

        public ManageModsViewModel ManageModsView { get; }

        public LoggingViewModel LoggingView { get; }

        public ToolsViewModel ToolsView { get; }

        public OtherItemsViewModel OtherItemsView { get; }

        private string AppName
        {
            get
            {
                DateTime now = DateTime.Now;
                bool isAprilFools = now.Month == 4 && now.Day == 1;
                return isAprilFools ? "QuestCorrupter" : "QuestPatcher";
            }
        }

        public string WelcomeText => $"{AppName} 2";

        public Config Config { get; }
        public ApkInfo AppInfo
        {
            get
            {
                Debug.Assert(_installManager.InstalledApp != null);
                return _installManager.InstalledApp;
            }
        }
        
        public string QPVersion => VersionUtil.QuestPatcherVersion.ToString();

        public bool NeedsPatchingView => PatchingView.IsPatchingInProgress || !AppInfo.IsModded;

        private readonly InstallManager _installManager;
        private readonly BrowseImportManager _browseManager;

        public LoadedViewModel(PatchingViewModel patchingView, ManageModsViewModel manageModsView, LoggingViewModel loggingView, ToolsViewModel toolsView, OtherItemsViewModel otherItemsView, Config config, InstallManager installManager, BrowseImportManager browseManager)
        {
            PatchingView = patchingView;
            LoggingView = loggingView;
            ToolsView = toolsView;
            ManageModsView = manageModsView;
            OtherItemsView = otherItemsView;

            Config = config;
            _installManager = installManager;
            _browseManager = browseManager;

            _installManager.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(_installManager.InstalledApp) && _installManager.InstalledApp != null)
                {
                    this.RaisePropertyChanged(nameof(AppInfo));
                    this.RaisePropertyChanged(nameof(NeedsPatchingView));
                    this.RaisePropertyChanged(nameof(SelectedAppText));
                }
            };

            patchingView.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(PatchingView.IsPatchingInProgress))
                {
                    this.RaisePropertyChanged(nameof(NeedsPatchingView));
                }
            };
        }

        public async void OnDragAndDrop(object? sender, DragEventArgs args)
        {
            Log.Debug("Handling drag and drop on LoadedViewModel");

            // Sometimes a COMException gets thrown if the items can't be parsed for whatever reason.
            // We need to handle this to avoid crashing QuestPatcher.
            try
            {
                IEnumerable<string>? fileNames = args.Data.GetFileNames();
                if (fileNames == null) // Non-file items dragged
                {
                    Log.Debug("Drag and drop contained no file names");
                    return;
                }

                Log.Debug("Files found in drag and drop. Processing . . .");
                await _browseManager.AttemptImportFiles(fileNames.ToList(), OtherItemsView.SelectedFileCopy);
            }
            catch (COMException)
            {
                Log.Error("Failed to parse dragged items");
            }
        }
    }
}
