using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TypeD.Models.Data;
using TypeDitor.Models;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Interaction logic for OpenProjectProgressDialog.xaml
    /// </summary>
    public partial class OpenProjectProgressDialog : Window, INotifyPropertyChanged
    {
        public OpenProjectProgressDialog() : this(null) { }

        public OpenProjectProgressDialog(string projectFilePath)
        {
            InitializeComponent();
            var location = string.IsNullOrEmpty(projectFilePath) ? null : Path.GetDirectoryName(projectFilePath);
            Splash = BrandingManager.LoadSplash(location);
            ProjectName = string.IsNullOrEmpty(projectFilePath) ? string.Empty : Path.GetFileNameWithoutExtension(projectFilePath);
            this.DataContext = this;

            // Without a title bar the window is moved by dragging anywhere on it
            PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
            };
            Closed += (_, _) => Splash?.Dispose();
        }

        public Bitmap Splash { get; }

        public string ProjectName { get; }

        public new event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private int _progress;
        public int Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                NotifyPropertyChanged(nameof(Progress));
                NotifyPropertyChanged(nameof(ProgressText));
            }
        }

        public string ProgressText => $"{Progress}%";

        private string _message = "Opening project...";
        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                NotifyPropertyChanged(nameof(Message));
            }
        }

        private string _subTask;
        public string SubTask
        {
            get => _subTask;
            set
            {
                _subTask = value;
                NotifyPropertyChanged(nameof(SubTask));
                NotifyPropertyChanged(nameof(HasSubTask));
            }
        }

        public bool HasSubTask => !string.IsNullOrEmpty(_subTask);

        private int _subProgress;
        public int SubProgress
        {
            get => _subProgress;
            set
            {
                _subProgress = value;
                NotifyPropertyChanged(nameof(SubProgress));
            }
        }

        private bool _subIndeterminate = true;
        public bool SubIndeterminate
        {
            get => _subIndeterminate;
            set
            {
                _subIndeterminate = value;
                NotifyPropertyChanged(nameof(SubIndeterminate));
            }
        }

        /// <summary>
        /// Shows the dialog and completes once its first frame has been presented,
        /// so blocking work started afterwards doesn't leave an unpainted window.
        /// </summary>
        public async Task ShowAndWaitForFirstFrameAsync()
        {
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnOpened(object sender, EventArgs e)
            {
                Opened -= OnOpened;
                // The first callback runs before the first frame is composed, the second after it has been presented
                RequestAnimationFrame(_ => RequestAnimationFrame(_ => rendered.TrySetResult()));
            }
            Opened += OnOpened;
            Show();
            // Never block loading if the platform doesn't deliver frame callbacks
            await Task.WhenAny(rendered.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        }

        public void Report(ProgressUpdate update)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Progress = update.Progress;
                Message = update.Message;
                SubTask = update.SubTask;
                SubIndeterminate = !update.SubProgress.HasValue;
                SubProgress = update.SubProgress ?? 0;
            });
        }
    }
}
