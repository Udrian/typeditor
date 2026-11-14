using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TypeD;
using TypeD.Models.Interfaces;
using TypeDitor.Models;
using TypeDitor.Models.PropertyTemplates;
using TypeDitor.View;

namespace TypeDitor
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Models
            var resourceModel = new ResourceModel();
            Resources.Add("ResourceModel", resourceModel);
            TypeDInit.Init(resourceModel);
            resourceModel.Get<IPropertyModel>().RegisterTemplate(new FolderListPropertyTemplate());
            var settingModel = resourceModel.Get<ISettingModel>();
            settingModel.InitContext<ProjectScanSettingContext>(null);
            ProjectScanSettings.MigrateLegacy(settingModel, resourceModel.Get<ILogModel>());
            ThemeManager.ApplyGlobal();
            CrashHandler.Init(resourceModel);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new ProjectWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}