using TypeD.Models.Interfaces;
using TypeD.ViewModel;

namespace TypeDitor.Commands
{
    internal class ExitProjectCommand : ProjectCommands
    {
        public ISaveModel SaveModel { get; set; }

        public ExitProjectCommand() : base()
        {
            SaveModel = ResourceModel.Get<ISaveModel>();
        }

        public override void Execute(object param)
        {
            ViewModelBase.MainWindow.Close();
        }
    }
}
