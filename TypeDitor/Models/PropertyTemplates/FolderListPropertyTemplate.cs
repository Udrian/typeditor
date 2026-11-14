using TypeD.Models.Data;
using TypeD.ViewModel;
using TypeDitor.ViewModel.PropertyTemplates;

namespace TypeDitor.Models.PropertyTemplates
{
    public sealed class FolderListPropertyTemplate : PropertyTemplate
    {
        protected override string TemplateUri => "avares://TypeDitor/View/Panels/PropertyTemplates/FolderListPropertyTemplate.axaml";
        protected override string TemplateKey => "FolderListPropertyTemplate";

        public override PropertyNode CreateNode(Component component, Property property, Action<PropertyNode, object> valueChanged)
        {
            return new FolderListPropertyNode(component, property, valueChanged);
        }
    }
}
