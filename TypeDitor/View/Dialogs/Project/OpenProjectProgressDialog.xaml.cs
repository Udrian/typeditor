using System.ComponentModel;
using System.Windows;

namespace TypeDitor.View.Dialogs.Project
{
    /// <summary>
    /// Interaction logic for OpenProjectProgressDialog.xaml
    /// </summary>
    public partial class OpenProjectProgressDialog : Window, INotifyPropertyChanged
    {
        public OpenProjectProgressDialog()
        {
            InitializeComponent();
            this.DataContext = this;
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
            set
            {
                _progress = value;
                NotifyPropertyChanged("Progress");
            }
        }
    }
}
