using Avalonia.Controls;
using TypeD.Models.Data;
using TypeD.ViewModel;

namespace TypeDitor.ViewModel.Dialogs.Project
{
    internal class RenameComponentTypeViewModel : ViewModelBase
    {
        // Data
        public string OldName { get; set; }
        public string Name { get; set; }

        // Constructors
        public RenameComponentTypeViewModel(Control element, Component component) : base(element)
        {
            OldName = Name = component.ClassName;
        }
    }
}
