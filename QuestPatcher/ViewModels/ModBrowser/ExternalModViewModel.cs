using QuestPatcher.ModBrowser.Models;
using QuestPatcher.Models;
using ReactiveUI;

namespace QuestPatcher.ViewModels.ModBrowser
{
    public class ExternalModViewModel: ViewModelBase
    {
        private readonly BrowseModViewModel _parent;
        
        public ExternalMod Mod { get; }
        
        public OperationLocker Locker { get; }
        
        public string Id => Mod.Id;
        public string Name => Mod.Name;
        public string Description => Mod.Description;
        public string Author => Mod.Author;
        public string Version => Mod.VersionString;
        
        private bool _isChecked;
        public bool IsChecked { 
            get => _isChecked;
            set
            {
                _isChecked = value;
                this.RaisePropertyChanged();
                _parent.SetModSelection(Id, value);
            } 
        }
        
        public ExternalModViewModel(ExternalMod mod, OperationLocker locker, BrowseModViewModel parent)
        {
            Mod = mod;
            Locker = locker;
            _parent = parent;
        }
    }
}
