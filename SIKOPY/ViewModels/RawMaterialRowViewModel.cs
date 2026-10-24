using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SIKOPY.Models;

namespace SIKOPY.ViewModels;

/// <summary>One row of the raw material table, with the actions available on it.</summary>
public partial class RawMaterialRowViewModel : ViewModelBase
{
    private readonly Func<RawMaterial, Task> _edit;
    private readonly Action<RawMaterial> _addStock;
    private readonly Action<RawMaterial> _deactivate;
    private readonly Action<RawMaterial> _delete;

    public RawMaterialRowViewModel(
        RawMaterial material,
        Func<RawMaterial, Task> edit,
        Action<RawMaterial> addStock,
        Action<RawMaterial> deactivate,
        Action<RawMaterial> delete)
    {
        Material = material;
        _edit = edit;
        _addStock = addStock;
        _deactivate = deactivate;
        _delete = delete;
    }

    public RawMaterial Material { get; }

    [RelayCommand]
    private Task EditAsync() => _edit(Material);

    [RelayCommand]
    private void AddStock() => _addStock(Material);

    [RelayCommand]
    private void Deactivate() => _deactivate(Material);

    [RelayCommand]
    private void Delete() => _delete(Material);
}
