using CommunityToolkit.Mvvm.ComponentModel;

namespace SIKOPY.ViewModels;

/// <summary>
/// What a customer sees after login. Customers may not open stock or roasting
/// screens; this page will become the storefront once Etalase is built.
/// </summary>
public partial class CustomerHomeViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Greeting { get; set; } = "Halo!";
}
