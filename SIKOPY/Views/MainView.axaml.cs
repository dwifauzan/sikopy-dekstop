using Avalonia.Controls;
using Avalonia.Input;
using SIKOPY.ViewModels;

namespace SIKOPY.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void NavRawMaterials_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.ShowRawMaterials();
        SetActive(NavRawMaterials);
    }

    private void NavRoasting_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.ShowRoasting();
        SetActive(NavRoasting);
    }

    private void NavShop_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.ShowShop();
        SetActive(NavShop);
    }

    // Memindahkan highlight "active" ke menu yang diklik
    private void SetActive(Border active)
    {
        NavRawMaterials.Classes.Set("active", NavRawMaterials == active);
        NavRoasting.Classes.Set("active", NavRoasting == active);
        NavShop.Classes.Set("active", NavShop == active);
    }
}
