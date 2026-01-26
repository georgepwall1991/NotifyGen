using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Mvvm;
using PropertyChanged;

namespace NotifyGen.Benchmarks.Models;

/// <summary>
/// ViewModel using NotifyGen source generator.
/// Generates INPC implementation at compile time with zero runtime overhead.
/// </summary>
[Notify]
public partial class NotifyGenViewModel
{
    private string _name = "";
    private int _age;
    private string? _email;
    private bool _isActive;
}

/// <summary>
/// ViewModel using CommunityToolkit.Mvvm source generator.
/// Microsoft's official MVVM toolkit with source generation.
/// </summary>
public partial class CommunityToolkitViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private int _age;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private bool _isActive;
}

/// <summary>
/// ViewModel using Prism's BindableBase.
/// Uses SetProperty method with virtual calls for property change notification.
/// </summary>
public class PrismViewModel : BindableBase
{
    private string _name = "";
    private int _age;
    private string? _email;
    private bool _isActive;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value);
    }

    public string? Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}

/// <summary>
/// ViewModel using Fody PropertyChanged IL weaving.
/// IL is modified post-build to inject INPC implementation automatically.
/// </summary>
[AddINotifyPropertyChangedInterface]
public class FodyViewModel
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}
