namespace NotifyGen.WpfSample;

/// <summary>
/// Child INPC object used with NotifyOnSubPropertyChanged.
/// </summary>
[Notify]
public partial class Address
{
    private string _city = "London";
    private string _postalCode = "EC1A 1BB";
}
