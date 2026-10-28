using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TypeDitor.Models;
using TypeD;
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

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Splash();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}