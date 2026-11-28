using Avalonia.Controls;
using Avalonia.Input;
using SIKOPY.ViewModels;

namespace SIKOPY.Views;

public partial class ShopWindowView : UserControl
{
    public ShopWindowView()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShopWindowViewModel vm)
        {
            vm.CloseCart();
        }
    }
}
