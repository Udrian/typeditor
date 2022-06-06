using System.Windows;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;

namespace TypeDitor.Commands
{
    internal class SaveProjectCommand : ProjectCommands
    {
        // Models
        public ISaveModel SaveModel { get; set; }

        // Constructors
        public SaveProjectCommand(FrameworkElement element) : base(element)
        {
            SaveModel = ResourceModel.Get<ISaveModel>();
        }

        public override async void Execute(object param)
        {
            await SaveModel.Save(param as Project);
        }

        public override bool CanExecute(object parameter)
        {
            return SaveModel.AnythingToSave;
        }
    }
}
