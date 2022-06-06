using System.Windows;
using TypeD.Models.Interfaces;

namespace TypeDitor.Commands
{
    internal class ExitProjectCommand : ProjectCommands
    {
        public ISaveModel SaveModel { get; set; }

        public ExitProjectCommand(FrameworkElement element) : base(element)
        {
            SaveModel = ResourceModel.Get<ISaveModel>();
        }

        public override void Execute(object param)
        {
            Application.Current.MainWindow.Close();
        }
    }
}
