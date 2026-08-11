namespace NotifyGen.MauiSample;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainViewModel();
    }

    private void OnBulkReload(object? sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm)
            vm.BulkReload();
    }
}
