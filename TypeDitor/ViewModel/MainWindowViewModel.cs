using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;
using TypeD;
using TypeD.Helpers;
using TypeD.Models.Data;
using TypeD.Models.Data.Hooks;
using TypeD.Models.Data.SettingContexts;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDitor.Commands;
using TypeDitor.View;
using TypeDitor.View.Dialogs.Tools;

namespace TypeDitor.ViewModel
{
    internal class MainWindowViewModel : ViewModelBase
    {
        // Models
        IHookModel HookModel { get; set; }
        ISaveModel SaveModel { get; set; }
        ISettingModel SettingModel { get; set; }
        IPanelModel PanelModel { get; set; }

        // Commands
        public BuildProjectCommand BuildProjectCommand { get; set; }
        public ExitProjectCommand ExitProjectCommand { get; set; }
        public NewProjectCommand NewProjectCommand { get; set; }
        public ImportProjectCommand ImportProjectCommand { get; set; }
        public OpenProjectCommand OpenProjectCommand { get; set; }
        public RunProjectCommand RunProjectCommand { get; set; }
        public SaveProjectCommand SaveProjectCommand { get; set; }
        public OpenPanelCommand OpenPanelCommand { get; set; }
        public ClosePanelCommand ClosePanelCommand { get; set; }

        // Data
        public Project LoadedProject { get; private set; }
        private List<TypeD.View.Panel> DelayedPanels { get; set; } = new List<TypeD.View.Panel>();

        // Properties
        public int SizeX
        {
            get
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                return setting?.SizeX?.Value ?? 1024;
            }
            set
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                setting.SizeX.Value = value;
                SettingModel.SetContext(setting);
            }
        }

        public int SizeY
        {
            get
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                return setting?.SizeY?.Value ?? 768;
            }
            set
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                setting.SizeY.Value = value;
                SettingModel.SetContext(setting);
            }
        }

        public WindowState Maximized
        {
            get
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                return setting?.Fullscreen.Value == true ? WindowState.Maximized : WindowState.Normal;
            }
            set
            {
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                setting.Fullscreen.Value = value == WindowState.Maximized;
                SettingModel.SetContext(setting);
            }
        }

        // View
        public MainWindow MainWindow { get; set; }

        // Constructors
        public MainWindowViewModel(MainWindow mainWindow, Project loadedProject) : base(mainWindow)
        {
            LoadedProject = loadedProject;
            MainWindow = mainWindow;

            HookModel = ResourceModel.Get<IHookModel>();
            SaveModel = ResourceModel.Get<ISaveModel>();
            SettingModel = ResourceModel.Get<ISettingModel>();
            PanelModel = ResourceModel.Get<IPanelModel>();
            
            BuildProjectCommand = new BuildProjectCommand();
            ExitProjectCommand = new ExitProjectCommand();
            NewProjectCommand = new NewProjectCommand();
            ImportProjectCommand = new ImportProjectCommand();
            OpenProjectCommand = new OpenProjectCommand();
            RunProjectCommand = new RunProjectCommand();
            SaveProjectCommand = new SaveProjectCommand();
            OpenPanelCommand = new OpenPanelCommand();
            ClosePanelCommand = new ClosePanelCommand();
        }

        // Functions
        public void InitUI(MainWindow mainWindow)
        {
            var initUIHook = HookModel.Shoot(new InitUIHook(LoadedProject));

            var menu = initUIHook.Menu;
            var viewMenuItem = new TypeD.View.MenuItem()
            {
                Name = "_View",
                Items = new List<TypeD.View.MenuItem>(RebuildMainMenuViewItems())
            };
            menu.Items.Add(viewMenuItem);
            
            foreach (var menuItem in menu.Items)
            {
                ViewHelper.InitMenu(mainWindow.TopMenu, menuItem, this);
            }
            
            foreach(var p in DelayedPanels)
            {
                OnAddElement(p);
            }
            DelayedPanels.Clear();
        }

        private List<TypeD.View.MenuItem> RebuildMainMenuViewItems()
        {
            var items = new List<TypeD.View.MenuItem>();
            foreach (var panel in PanelModel.GetPanels())
            {
                items.Add(new TypeD.View.MenuItem()
                {
                    Name = panel.Title,
                    Click = (param) =>
                    {
                        if (!panel.Open)
                            OpenPanelCommand.Execute(panel.ID);
                        else
                            ClosePanelCommand.Execute(panel.ID);
                    }
                });
            }
            return items;
        }

        public void OpenModulesWindow()
        {
            var dialog = new ModulesDialog(LoadedProject);
            dialog.Show();
        }

        public void OpenOptionsWindow()
        {
            var dialog = new OptionsDialog(LoadedProject);
            dialog.Show();
        }

        public async Task<bool> OnClose()
        {
            LoadedProject.IsClosing = true;
            if (SaveModel.AnythingToSave)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Closing...", "Save before closing?", ButtonEnum.YesNoCancel, Icon.Question);
                var result = await box.ShowWindowDialogAsync(MainWindow);

                if (result == ButtonResult.Yes)
                {
                    await SaveModel.Save(LoadedProject);
                }
                else if (result == ButtonResult.Cancel)
                {
                    LoadedProject.IsClosing = false;
                    return true;
                }
            }

            TypeDInit.ProjectUnload(LoadedProject, ResourceModel);
            HookModel.Shoot(new ExitHook() { Project = LoadedProject });
            return false;
        }

        public override void OnAddElement(object element)
        {
            if(element is TypeD.View.Panel)
            {
                var panel = element as TypeD.View.Panel;
                if (MainWindow == null)
                {
                    DelayedPanels.Add(panel);
                    return;
                }
            
                var setting = SettingModel.GetContext<MainWindowSettingContext>(SettingLevel.Local);
                var panelSetting = setting.Panels.Value.Find(p => p.ID == panel.ID);
                if (panelSetting == null)
                    return;

                MainWindow.DockRoot.AddPanel(panel, panelSetting);
            }
        }

        public override void OnRemoveElement(object element)
        {
            //TODO: Find a way to remove panels through Avalonia Dock
            //if (element is TypeD.View.Panel)
            //{
            //    //TODO: This is not the most efficent way to do this...
            //    MainWindow.DockPanelRoot.Children.Remove(MainWindow.DockRoot);
            //    MainWindow.DockRoot = new View.TypeDock.TypeDockRoot();
            //    MainWindow.DockRoot.Margin = new Thickness(5);
            //    MainWindow.DockPanelRoot.Children.Add(MainWindow.DockRoot);
            //
            //    foreach (var panel in PanelModel.GetPanels())
            //    {
            //        var parent = VisualTreeHelper.GetParent(panel.PanelView);
            //        if(parent!= null)
            //            (parent as Avalonia.Controls.Panel).Children.Remove(panel.PanelView);
            //    }
            //    foreach (var panel in PanelModel.GetPanels())
            //    {
            //        if (panel.Open)
            //            OnAddElement(panel);
            //    }
            //}
        }
    }
}
