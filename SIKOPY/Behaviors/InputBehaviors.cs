using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SIKOPY.Behaviors;

/// <summary>
/// Attached behaviors for text input.
/// <c>AllowedCharacters="0123456789,."</c> lets the user type or paste only
/// those characters. It works on a <see cref="TextBox"/> and on controls that
/// contain one, such as <see cref="CalendarDatePicker"/>.
/// </summary>
public static class InputBehaviors
{
    public static readonly AttachedProperty<string?> AllowedCharactersProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>(
            "AllowedCharacters", typeof(InputBehaviors));

    static InputBehaviors()
    {
        AllowedCharactersProperty.Changed.AddClassHandler<Control>(OnAllowedCharactersChanged);
    }

    public static string? GetAllowedCharacters(Control control) =>
        control.GetValue(AllowedCharactersProperty);

    public static void SetAllowedCharacters(Control control, string? value) =>
        control.SetValue(AllowedCharactersProperty, value);

    private static void OnAllowedCharactersChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
        control.RemoveHandler(TextBox.TextChangedEvent, OnTextChanged);
        if (e.NewValue is string { Length: > 0 })
        {
            // Tunnel, so typing is blocked before the inner TextBox inserts it.
            control.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
            // Catches pasted or dropped text, which does not raise TextInput.
            control.AddHandler(TextBox.TextChangedEvent, OnTextChanged, RoutingStrategies.Bubble);
        }
    }

    private static void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is Control control &&
            GetAllowedCharacters(control) is { } allowed &&
            e.Text is { Length: > 0 } text &&
            text.Any(c => !allowed.Contains(c)))
        {
            e.Handled = true;
        }
    }

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Control control ||
            GetAllowedCharacters(control) is not { } allowed ||
            e.Source is not TextBox { Text: { Length: > 0 } text } textBox ||
            text.All(allowed.Contains))
            return;

        var caret = textBox.CaretIndex;
        var removedBeforeCaret = text.Take(caret).Count(c => !allowed.Contains(c));
        textBox.Text = new string(text.Where(allowed.Contains).ToArray());
        textBox.CaretIndex = caret - removedBeforeCaret;
    }
}
