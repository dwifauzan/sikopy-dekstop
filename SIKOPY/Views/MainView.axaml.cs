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
    }

    private void NavRoasting_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.ShowRoasting();
    }

    private void NavShop_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.ShowShop();
    }

    private void NavHistory_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.ShowHistory();
    }
}
