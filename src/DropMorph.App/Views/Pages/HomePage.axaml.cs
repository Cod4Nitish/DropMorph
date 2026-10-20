// Drop zone: files dropped on the Home page open the donut (click mode) at the drop position.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DropMorph.App.Services;
using Microsoft.Extensions.Logging;

namespace DropMorph.App.Views.Pages;

public partial class HomePage : UserControl
{
    /// <summary>
    /// The first drag over the drop zone of this run is logged: a log without it after "drops don't work" means the
    /// drag never reached DropMorph (Windows refuses drops from Explorer into a DropMorph running as administrator).
    /// </summary>
    private static int _dragEnterLogged;

    public HomePage()
    {
        InitializeComponent();
        DragDrop.AddDragEnterHandler(DropZone, (_, e) =>
        {
            DropZone.Classes.Add("over");
            var files = DraggedFiles.Offered(e.DataTransfer);
            e.DragEffects = files ? DragDropEffects.Copy : DragDropEffects.None;
            if (Interlocked.Exchange(ref _dragEnterLogged, 1) == 0)
                AppHost.Get<ILogger<HomePage>>().LogInformation("First drag over the drop zone (files: {Files})", files);
        });
        DragDrop.AddDragOverHandler(DropZone, (_, e) =>
            e.DragEffects = DraggedFiles.Offered(e.DataTransfer) ? DragDropEffects.Copy : DragDropEffects.None);
        DragDrop.AddDragLeaveHandler(DropZone, (_, _) => DropZone.Classes.Remove("over"));
        DragDrop.AddDropHandler(DropZone, OnDrop);
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Remove("over");
        var files = DraggedFiles.Local(e.DataTransfer);
        if (files.Count == 0)
            return;

        e.DragEffects = DragDropEffects.Copy;
        var screenPoint = this.PointToScreen(e.GetPosition(this));
        AppHost.Get<RadialController>().ShowForFiles(screenPoint.X, screenPoint.Y, files);
    }
}
