using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Panels
{
    internal class OutputViewModel : ViewModelBase
    {
        internal class GameLogLine
        {
            public string Text { get; init; }
            public IBrush Brush { get; init; }
        }

        internal class LevelFilter : ViewModelBase
        {
            private readonly OutputViewModel Owner;
            private bool _isEnabled = true;

            public GameLogLevel Level { get; }
            public string Name => Level.ToString();
            public bool IsEnabled
            {
                get => _isEnabled;
                set
                {
                    if (SetProperty(ref _isEnabled, value))
                        Owner.RefreshGameLog();
                }
            }

            public LevelFilter(OutputViewModel owner, GameLogLevel level)
            {
                Owner = owner;
                Level = level;
            }
        }

        // Models
        ILogModel LogModel { get; set; }
        IGameLogModel GameLogModel { get; set; }

        private readonly Action<GameLogEntry> OnGameLogEntryAdded;
        private readonly Action OnGameLogCleared;
        private readonly Action OnLevelColorsChanged;

        // Constructors
        public OutputViewModel(Control element) : base(element)
        {
            LogModel = ResourceModel.Get<ILogModel>();
            GameLogModel = ResourceModel.Get<IGameLogModel>();
            LogModel.AttachLogOutput("OutputView", (message) =>
            {
                OnCMDOutput(message);
            });
            CMD.Output += OnCMDOutput;

            foreach (var level in new[] { GameLogLevel.Ludacris, GameLogLevel.Debug, GameLogLevel.Info, GameLogLevel.Warning, GameLogLevel.Error, GameLogLevel.Fatal })
                LevelFilters.Add(new LevelFilter(this, level));

            // Game log events can come from the game's thread, so they are marshalled to the UI thread
            OnGameLogEntryAdded = entry => Dispatcher.UIThread.Post(() =>
            {
                if (Passes(entry))
                    GameLogLines.Add(CreateLine(entry, null));
            });
            OnGameLogCleared = () => Dispatcher.UIThread.Post(() => GameLogLines.Clear());
            OnLevelColorsChanged = () => Dispatcher.UIThread.Post(RefreshGameLog);
            GameLogModel.EntryAdded += OnGameLogEntryAdded;
            GameLogModel.Cleared += OnGameLogCleared;
            GameLogModel.LevelColorsChanged += OnLevelColorsChanged;
            RefreshGameLog();
        }

        // Functions
        private void OnCMDOutput(string output)
        {
            OutputText += output + Environment.NewLine;
        }

        public void ClearGameLog()
        {
            GameLogModel.Clear();
        }

        public void Unload()
        {
            LogModel.DetachLogOutput("OutputView");
            CMD.Output -= OnCMDOutput;
            GameLogModel.EntryAdded -= OnGameLogEntryAdded;
            GameLogModel.Cleared -= OnGameLogCleared;
            GameLogModel.LevelColorsChanged -= OnLevelColorsChanged;
        }

        private bool Passes(GameLogEntry entry)
        {
            foreach (var filter in LevelFilters)
            {
                if (filter.Level == entry.Level && !filter.IsEnabled)
                    return false;
            }
            return string.IsNullOrEmpty(SearchText) ||
                entry.Format().Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private GameLogLine CreateLine(GameLogEntry entry, Dictionary<GameLogLevel, IBrush> brushes)
        {
            IBrush brush = null;
            if (brushes == null || !brushes.TryGetValue(entry.Level, out brush))
            {
                brush = Color.TryParse(GameLogModel.GetLevelColor(entry.Level), out var color)
                    ? new SolidColorBrush(color)
                    : Brushes.Gray;
                brushes?.Add(entry.Level, brush);
            }
            return new GameLogLine() { Text = entry.Format(), Brush = brush };
        }

        private void RefreshGameLog()
        {
            GameLogLines.Clear();
            var brushes = new Dictionary<GameLogLevel, IBrush>();
            foreach (var entry in GameLogModel.GetEntries())
            {
                if (Passes(entry))
                    GameLogLines.Add(CreateLine(entry, brushes));
            }
        }

        // Properties
        public ObservableCollection<GameLogLine> GameLogLines { get; } = new();
        public ObservableCollection<LevelFilter> LevelFilters { get; } = new();

        private string _outputText;
        public string OutputText
        {
            get => _outputText;
            set
            {
                _outputText = value;
                OnPropertyChanged();
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    RefreshGameLog();
            }
        }

        private int _selectedViewIndex;
        public int SelectedViewIndex
        {
            get => _selectedViewIndex;
            set
            {
                if (SetProperty(ref _selectedViewIndex, value))
                    OnPropertyChanged(nameof(IsGameLogView));
            }
        }

        public bool IsGameLogView => SelectedViewIndex == 1;
    }
}
