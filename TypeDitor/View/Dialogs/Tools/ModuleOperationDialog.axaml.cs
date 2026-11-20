using System.ComponentModel;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TypeD.Models.Data;

namespace TypeDitor.View.Dialogs.Tools
{
    /// <summary>
    /// Reports the status, output and progress of a running module operation, for example a typer install or update.
    /// </summary>
    public partial class ModuleOperationDialog : Window, INotifyPropertyChanged
    {
        private readonly StringBuilder output = new StringBuilder();
        private string lastLine;

        public ModuleOperationDialog() : this("Module operation") { }

        public ModuleOperationDialog(string title)
        {
            InitializeComponent();
            Title = title;
            _message = title;
            DataContext = this;
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private string _message;
        public string Message
        {
            get => _message;
            private set
            {
                _message = value;
                NotifyPropertyChanged(nameof(Message));
            }
        }

        private int _progress;
        public int Progress
        {
            get => _progress;
            private set
            {
                _progress = value;
                NotifyPropertyChanged(nameof(Progress));
                NotifyPropertyChanged(nameof(ProgressText));
            }
        }

        public string ProgressText => $"{Progress}%";

        public string Output => output.ToString();

        private bool _finished;
        public bool Finished
        {
            get => _finished;
            private set
            {
                _finished = value;
                NotifyPropertyChanged(nameof(Finished));
            }
        }

        /// <summary>Reports the current status and progress, and appends the reported output line to the output.</summary>
        public void Report(ProgressUpdate update)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Message = update.Message;
                Progress = update.Progress;
                if (!string.IsNullOrEmpty(update.SubTask)) AppendLine(update.SubTask);
            });
        }

        /// <summary>Appends a line to the output.</summary>
        public void ReportOutput(string line)
        {
            Dispatcher.UIThread.Post(() => AppendLine(line));
        }

        /// <summary>Closes the dialog on success; on failure it stays open so the output can be read.</summary>
        public void Complete(bool success)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Finished = true;
                if (success)
                {
                    Close();
                }
                else
                {
                    Message = "The operation could not be completed. See the output below and the Output log.";
                }
            });
        }

        private void AppendLine(string line)
        {
            // Status lines often repeat, so only the first of consecutive equal lines is kept
            if (line == lastLine) return;
            lastLine = line;
            output.AppendLine(line);
            NotifyPropertyChanged(nameof(Output));
            // Moving the caret to the end keeps the latest output visible
            OutputBox.CaretIndex = output.Length;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (!Finished)
                e.Cancel = true;
        }
    }
}
