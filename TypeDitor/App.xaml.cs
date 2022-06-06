using System.Windows;
using TypeDitor.Models;
using TypeD;

namespace TypeDitor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Models
            var resourceModel = new ResourceModel(Resources);
            Resources.Add("ResourceModel", resourceModel);
            TypeDInit.Init(resourceModel);
        }
    }
}
