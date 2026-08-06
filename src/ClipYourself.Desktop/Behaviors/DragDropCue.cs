using System.Windows;

namespace ClipYourself.Desktop.Behaviors;

/// <summary>
/// Attached flag toggled while something is dragged over a drawer row that would accept
/// the drop, so the row's template can light up a "drop here" cue.
/// </summary>
public static class DragDropCue
{
    public static readonly DependencyProperty IsDropTargetProperty =
        DependencyProperty.RegisterAttached(
            "IsDropTarget", typeof(bool), typeof(DragDropCue), new PropertyMetadata(false));

    public static bool GetIsDropTarget(DependencyObject o) => (bool)o.GetValue(IsDropTargetProperty);
    public static void SetIsDropTarget(DependencyObject o, bool value) => o.SetValue(IsDropTargetProperty, value);
}
