using Avalonia.Controls;
using QuestPatcher.Core;
using QuestPatcher.Core.Modding;
using QuestPatcher.Models;

namespace QuestPatcher.ViewModels.Modding
{
    public class ManageModsViewModel : ViewModelBase
    {
        public ModListViewModel ModsList { get; }

        public ModListViewModel LibrariesList { get; }

        public ProgressViewModel ProgressView { get; }

        public ManageModsViewModel(ModManager modManager, InstallManager installManager, Window mainWindow, OperationLocker locker, ProgressViewModel progressView, BrowseImportManager browseManager)
        {
            ProgressView = progressView;
            ModsList = new ModListViewModel("模组", true, modManager.Mods, modManager, installManager, mainWindow, locker, browseManager);
            //   LibrariesList = new ModListViewModel("支持库", false, modManager.Libraries, modManager, installManager, mainWindow, locker, browseManager);

            System.Console.Out.WriteLine("Loaded libraries");
            for(int x = 0; x < modManager.Libraries.Count; x++)
            {
                System.Console.Out.WriteLine(
                  $"Lib {modManager.Libraries[x].Name}(By {modManager.Libraries[x].Author}) {modManager.Libraries[x].Version}({modManager.Libraries[x].PackageVersion})");
            }
        }
    }
}
