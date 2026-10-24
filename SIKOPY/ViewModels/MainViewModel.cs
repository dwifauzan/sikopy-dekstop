using CommunityToolkit.Mvvm.ComponentModel;

namespace SIKOPY.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public RawMaterialsViewModel RawMaterials { get; } = new();
}