using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SIKOPY.Behaviors;

/// <summary>
/// Attached behaviors for <see cref="ComboBox"/>.
/// <c>OpenDropDownOnClick="True"</c> opens an editable ComboBox's list when the
/// user clicks its text area, not only its arrow, so the options are easy to find.
/// </summary>
public static class ComboBoxBehaviors
{
    public static readonly AttachedProperty<bool> OpenDropDownOnClickProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, bool>(
            "OpenDropDownOnClick", typeof(ComboBoxBehaviors));

    static ComboBoxBehaviors()
    {
        OpenDropDownOnClickProperty.Changed.AddClassHandler<ComboBox>(OnOpenDropDownOnClickChanged);
    }

    public static bool GetOpenDropDownOnClick(ComboBox comboBox) =>
        comboBox.GetValue(OpenDropDownOnClickProperty);

    public static void SetOpenDropDownOnClick(ComboBox comboBox, bool value) =>
        comboBox.SetValue(OpenDropDownOnClickProperty, value);

    private static void OnOpenDropDownOnClickChanged(ComboBox comboBox, AvaloniaPropertyChangedEventArgs e)
    {
        // The inner TextBox marks pointer events as handled, so listen to handled events too.
        comboBox.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        if (e.NewValue is true)
        {
            comboBox.AddHandler(
                InputElement.PointerReleasedEvent,
                OnPointerReleased,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not ComboBox { IsEnabled: true, IsDropDownOpen: false } comboBox ||
            e.InitialPressMouseButton != MouseButton.Left)
            return;

        // Only react to clicks in the text area; the arrow already toggles the list itself.
        if (e.Source is Visual source && source.FindAncestorOfType<TextBox>(includeSelf: true) is not null)
            comboBox.IsDropDownOpen = true;
    }
}
