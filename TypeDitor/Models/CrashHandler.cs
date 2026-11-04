using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using TypeD.Models.Data;
using TypeD.Models.Interfaces;
using TypeD.ViewModel;
using TypeDitor.View.Dialogs.Project;

namespace TypeDitor.Models
{
    /// <summary>
    /// Catches unhandled exceptions that originate in the game project or in modules loaded inside the editor,
    /// stops the game, and shows a crash dialog that can exit the editor or reload the game within the editor.
    /// </summary>
    internal static class CrashHandler
    {
        private static ICrashModel CrashModel;
        private static bool Showing;

        public static void Init(IResourceModel resourceModel)
        {
            CrashModel = resourceModel.Get<ICrashModel>();

            // Crashes caught by the code hosting the game, the game is already stopped
            CrashModel.Crashed += report => Dispatcher.UIThread.Post(() => _ = ShowAsync(report, true));

            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                var report = CrashModel.Identify(e.Exception);
                if (report == null) return;
                e.Handled = true;
                CrashModel.StopGame(report.Project);
                Dispatcher.UIThread.Post(() => _ = ShowAsync(report, true));
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                var report = CrashModel.Identify(e.Exception);
                if (report == null) return;
                e.SetObserved();
                Dispatcher.UIThread.Post(() =>
                {
                    CrashModel.StopGame(report.Project);
                    _ = ShowAsync(report, true);
                });
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var report = CrashModel.Identify(e.ExceptionObject as Exception);
                if (report == null) return;
                // The process terminates once this handler returns, so the game can't be reloaded; block the crashing thread
                // while the dialog explains what happened
                if (!Dispatcher.UIThread.CheckAccess())
                {
                    try
                    {
                        Dispatcher.UIThread.InvokeAsync(() => ShowAsync(report, false)).Wait();
                    }
                    catch
                    {
                        // The editor is going down anyway, there is nothing more to report
                    }
                }
            };
        }

        /// <summary>Closes the editor after a crash, asking to save pending changes first.</summary>
        public static void Exit()
        {
            var mainWindow = ViewModelBase.MainWindow;
            if (mainWindow != null && mainWindow.IsVisible)
                mainWindow.Close();
            else
                Environment.Exit(1);
        }

        private static async Task ShowAsync(CrashReport report, bool canReload)
        {
            if (Showing) return;
            Showing = true;
            try
            {
                var dialog = new GameCrashDialog(report, CrashModel, canReload);
                var owner = ViewModelBase.MainWindow;
                if (owner != null && owner.IsVisible)
                {
                    await dialog.ShowDialog(owner);
                }
                else
                {
                    var closed = new TaskCompletionSource();
                    dialog.Closed += (_, _) => closed.TrySetResult();
                    dialog.Show();
                    await closed.Task;
                }

                if (dialog.Ignored)
                    return;

                if (!canReload)
                    Environment.Exit(1);
            }
            finally
            {
                Showing = false;
            }
        }
    }
}
