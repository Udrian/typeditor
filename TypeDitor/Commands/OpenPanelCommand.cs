using Avalonia.Controls;
using TypeD.Models.Interfaces;

namespace TypeDitor.Commands
{
    internal class OpenPanelCommand : ProjectCommands
    {
        // Models
        IPanelModel PanelModel { get; set; }

        // Constructors
        public OpenPanelCommand(Control element) : base(element)
        {
            PanelModel = ResourceModel.Get<IPanelModel>();
        }

        public override void Execute(object param)
        {
            if (param is not string)
                return;
            var id = (string)param;
            PanelModel.OpenPanel(id);
        }
    }
}
