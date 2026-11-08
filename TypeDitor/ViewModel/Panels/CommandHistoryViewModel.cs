using System.Collections.ObjectModel;
using System.Collections.Specialized;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDitor.View.Panels;

namespace TypeDitor.ViewModel.Panels
{
    internal sealed class CommandHistoryViewModel : ViewModelBase
    {
        public ObservableCollection<CommandHistoryEntry> Entries { get; }

        public bool HasEntries => Entries.Count > 0;
        public bool HasNoEntries => !HasEntries;
        public bool CanUndo => _historyModel.CanUndo;
        public bool CanRedo => _historyModel.CanRedo;
        public string CurrentPosition => _historyModel.CurrentPosition;

        private readonly ICommandHistoryModel _historyModel;

        public CommandHistoryViewModel(CommandHistoryPanel element) : base(element)
        {
            _historyModel = ResourceModel.Get<ICommandHistoryModel>();
            Entries = _historyModel.Entries;
            Entries.CollectionChanged += OnEntriesChanged;
            _historyModel.HistoryChanged += OnHistoryChanged;
        }

        public void Undo()
        {
            _historyModel.Undo();
        }

        public void Redo()
        {
            _historyModel.Redo();
        }

        private void OnEntriesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasEntries));
            OnPropertyChanged(nameof(HasNoEntries));
        }

        private void OnHistoryChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(CurrentPosition));
        }
    }
}
