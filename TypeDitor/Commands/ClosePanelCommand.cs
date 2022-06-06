using System.Windows;
using TypeD.Models.Interfaces;

namespace TypeDitor.Commands
{
    internal class ClosePanelCommand : ProjectCommands
    {
        // Models
        IPanelModel PanelModel { get; set; }

        // Constructors
        public ClosePanelCommand(FrameworkElement element) : base(element)
        {
            PanelModel = ResourceModel.Get<IPanelModel>();
        }

        public override void Execute(object param)
        {
            if (param is not string)
                return;
            var id = (string)param;
            PanelModel.ClosePanel(id);
        }
    }
}
