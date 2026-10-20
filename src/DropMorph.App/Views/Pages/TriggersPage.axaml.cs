using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using DropMorph.App.ViewModels.Pages;

namespace DropMorph.App.Views.Pages;

public partial class TriggersPage : UserControl
{
    public TriggersPage() => InitializeComponent();

    /// <summary>While "Record" is on, the next key combination pressed on the button becomes the shortcut.</summary>
    private void OnRecorderKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is ToggleButton { DataContext: TriggerGestureViewModel vm } && vm.IsRecording)
            e.Handled = vm.Record(e.Key, e.KeyModifiers);
    }
}
