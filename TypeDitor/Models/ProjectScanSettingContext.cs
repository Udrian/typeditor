using TypeD.Models.Data;
using TypeDitor.Models.PropertyTemplates;

namespace TypeDitor.Models
{
    [Name("ProjectScan")]
    public sealed class ProjectScanSettingContext : SettingContext
    {
        public Setting<List<string>> Folders { get; set; } = new(
            new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TypeD")
            })
        {
            PropertyTemplateType = typeof(FolderListPropertyTemplate)
        };

        public ProjectScanSettingContext()
        {
            SaveOnExit = true;
        }
    }
}
