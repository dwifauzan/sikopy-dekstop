using SIKOPY.ViewModels;

namespace SIKOPY.ViewModels;

public class MainViewModel : ViewModelBase
{
    public RawMaterialsViewModel RawMaterials { get; } = new();
    public RoastingViewModel Roasting { get; } = new();

    private ViewModelBase _currentPage;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public MainViewModel()
    {
        _currentPage = RawMaterials;
    }

    public void ShowRawMaterials() => CurrentPage = RawMaterials;
    public void ShowRoasting() => CurrentPage = Roasting;
}