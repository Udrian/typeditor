using System.Windows;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;

namespace TypeDitor.Commands
{
    internal class BuildProjectCommand : ProjectCommands
    {
        // Models
        private IProjectModel ProjectModel { get; set; }
        public ISaveModel SaveModel { get; set; }

        public BuildProjectCommand(FrameworkElement element) : base(element)
        {
            ProjectModel = ResourceModel.Get<IProjectModel>();
            SaveModel = ResourceModel.Get<ISaveModel>();
        }

        public async override void Execute(object param)
        {
            var project = param as Project;

            if (SaveModel.AnythingToSave)
            {
                await SaveModel.Save(project);
            }
            await ProjectModel.Build(project);
        }
    }
}
