namespace SIKOPY.Models;

/// <summary>A master-data entry (bean type, processing method) shown in pickers and filters.</summary>
public sealed record LookupItem(int Id, string Name)
{
    // Editable ComboBoxes show an item's ToString() as their text.
    public override string ToString() => Name;
}
