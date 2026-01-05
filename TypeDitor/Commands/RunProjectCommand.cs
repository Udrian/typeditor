using TypeD.Models.Data;
using TypeD.Models.Interfaces;

namespace TypeDitor.Commands
{
    internal class RunProjectCommand : ProjectCommands
    {
        // Models
        private IProjectModel ProjectModel { get; set; }

        public RunProjectCommand() : base()
        {
            ProjectModel = ResourceModel.Get<IProjectModel>();
        }

        public override void Execute(object param)
        {
            if (param is Project)
                ProjectModel.Run(param as Project);
        }
    }
}
