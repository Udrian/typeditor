using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TypeD.Models.Data;
using TypeDCore.Models;
using TypeDitor.ViewModel.Panels;

namespace TypeDitor.View.Panels;

/// <summary>Displays and manages a project's content files.</summary>
public partial class ContentPanel : UserControl
{
    // ViewModel
    ContentPanelViewModel ViewModel { get; set; }

    /// <summary>Creates a content panel for the specified project.</summary>
    public ContentPanel(Project project)
    {
        DataContext = ViewModel = new ContentPanelViewModel(this, project);
        InitializeComponent();

        ContentList.AddHandler(PointerPressedEvent, ContentList_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        ContentList.AddHandler(PointerMovedEvent, ContentList_PointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        ContentList.AddHandler(PointerReleasedEvent, (_, _) => _pressed = null, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        DragDrop.AddDragOverHandler(FolderTree, FolderTree_DragOver);
        DragDrop.AddDropHandler(FolderTree, FolderTree_Drop);

    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadContentAsync();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ViewModel.Unload();
        base.OnDetachedFromVisualTree(e);
    }

    private PointerPressedEventArgs _pressed;
    private ContentPanelViewModel.ImageItem _dragItem;

    private void ContentList_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        var item = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.DataContext as ContentPanelViewModel.ImageItem;
        var isTextBox = e.Source is Visual visual && visual.GetSelfAndVisualAncestors().OfType<TextBox>().Any();
        _pressed = item != null && !isTextBox && e.GetCurrentPoint(ContentList).Properties.IsLeftButtonPressed ? e : null;
        _dragItem = item;
        _pressedPosition = e.GetPosition(ContentList);
    }

    private Point _pressedPosition;

    private async void ContentList_PointerMoved(object sender, PointerEventArgs e)
    {
        if (_pressed == null || _dragItem == null)
            return;
        if (!e.GetCurrentPoint(ContentList).Properties.IsLeftButtonPressed)
        {
            _pressed = null;
            return;
        }

        var delta = e.GetPosition(ContentList) - _pressedPosition;
        if (System.Math.Abs(delta.X) < 6 && System.Math.Abs(delta.Y) < 6)
            return;

        var pressed = _pressed;
        _pressed = null;

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(_dragItem.Path));
        ViewerDragPayload.SetContent(_dragItem.Path);
        try
        {
            await DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            ViewerDragPayload.Clear();
        }
        _dragItem = null;
    }

    private static ContentPanelViewModel.FolderNode FolderAt(RoutedEventArgs e)
    {
        return (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<TreeViewItem>().FirstOrDefault()?.DataContext as ContentPanelViewModel.FolderNode;
    }

    private void FolderTree_DragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = _dragItem != null && FolderAt(e) != null ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FolderTree_Drop(object sender, DragEventArgs e)
    {
        var item = _dragItem;
        var folder = FolderAt(e);
        e.Handled = true;
        if (item != null && folder != null)
            await ViewModel.MoveItem(item, folder);
    }

    private void FolderTree_GotFocus(object sender, RoutedEventArgs e)
    {
        ViewModel.SetNoTreeActive();
    }

    private void FolderTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel.SelectedFolder is not { IsEditing: false } folder || folder.IsRoot)
            return;

        if (e.Key == Key.F2)
        {
            BeginFolderRename(folder);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            _ = ViewModel.DeleteFolder(folder);
            e.Handled = true;
        }
    }

    private void FolderMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var canModify = ViewModel.SelectedFolder is { IsRoot: false };
        FolderRenameItem.IsEnabled = canModify;
        FolderDeleteItem.IsEnabled = canModify;
    }

    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = ViewModel.CreateFolder(ViewModel.SelectedFolder);
        if (folder != null)
            BeginFolderRename(folder);
    }

    private void FolderRename_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder is { IsRoot: false } folder)
            BeginFolderRename(folder);
    }

    private void FolderDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder is { IsRoot: false } folder)
            _ = ViewModel.DeleteFolder(folder);
    }

    private void FolderShowInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder != null && System.IO.Directory.Exists(ViewModel.SelectedFolder.FullPath))
            System.Diagnostics.Process.Start("explorer.exe", $"\"{ViewModel.SelectedFolder.FullPath}\"");
    }

    private void BeginFolderRename(ContentPanelViewModel.FolderNode folder)
    {
        folder.BeginRename();
        Dispatcher.UIThread.Post(() =>
        {
            var treeItem = FolderTree.GetSelfAndVisualDescendants()
                .OfType<TreeViewItem>()
                .FirstOrDefault(i => ReferenceEquals(i.DataContext, folder));
            var textBox = treeItem?.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault();
            textBox?.Focus();
            textBox?.SelectAll();
        }, DispatcherPriority.Background);
    }

    private async void FolderNameEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ContentPanelViewModel.FolderNode folder })
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ViewModel.CommitFolderRename(folder);
            FolderTree.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            folder.CancelRename();
            e.Handled = true;
            FolderTree.Focus();
        }
    }

    private void FolderNameEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ContentPanelViewModel.FolderNode { IsEditing: true } folder })
            folder.CancelRename();
    }

    private async void LoadContentAsync()
    {
        await ViewModel.LoadAllContent();
    }

    private void ContentList_GotFocus(object sender, RoutedEventArgs e)
    {
        // Keeps global shortcuts like Delete from acting on the component tree while content is focused.
        ViewModel.SetNoTreeActive();
    }

    private void ContentList_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel.SelectedItem is not { IsEditing: false } item)
            return;

        if (e.Key == Key.F2)
        {
            BeginRename(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            _ = ViewModel.Delete(item);
            e.Handled = true;
        }
    }

    private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = ViewModel.SelectedItem == null;
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem != null)
            BeginRename(ViewModel.SelectedItem);
    }

    private void ShowInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem != null)
            ViewModel.ShowInExplorer(ViewModel.SelectedItem);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem != null)
            _ = ViewModel.Delete(ViewModel.SelectedItem);
    }

    private void BeginRename(ContentPanelViewModel.ImageItem item)
    {
        item.BeginRename();
        // The editor only exists in the visual tree once IsVisible has been applied.
        Dispatcher.UIThread.Post(() =>
        {
            var listItem = ContentList.GetSelfAndVisualDescendants()
                .OfType<ListBoxItem>()
                .FirstOrDefault(i => ReferenceEquals(i.DataContext, item));
            var textBox = listItem?.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault();
            textBox?.Focus();
            textBox?.SelectAll();
        }, DispatcherPriority.Background);
    }

    private async void NameEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ContentPanelViewModel.ImageItem item })
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ViewModel.CommitRename(item);
            ContentList.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            item.CancelRename();
            e.Handled = true;
            ContentList.Focus();
        }
    }

    private void NameEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ContentPanelViewModel.ImageItem { IsEditing: true } item })
            item.CancelRename();
    }
}
