using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using TypeD.Helpers;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Interaction logic for ProjectCreationProgressDialog.xaml
    /// </summary>
    public partial class ProjectCreationProgressDialog : Window, INotifyPropertyChanged
    {
        public ProjectCreationProgressDialog()
        {
            this.DataContext = this;
            InitializeComponent();

            CMD.Output += OnCMDOutput;
        }

        private void OnCMDOutput(string output)
        {
            OutputText += output + Environment.NewLine;
        }

        private void tbOutputText_TextChanged(object sender, TextChangedEventArgs e)
        {
            tbOutputText.ScrollToEnd();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            CMD.Output -= OnCMDOutput;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private int _progress;
        public int Progress
        {
            get => _progress;
            set {
                _progress = value;
                NotifyPropertyChanged("Progress");
            }
        }

        private string _outputText;
        public string OutputText
        {
            get => _outputText;
            set
            {
                _outputText = value;
                NotifyPropertyChanged("OutputText");
            }
        }
    }
}
