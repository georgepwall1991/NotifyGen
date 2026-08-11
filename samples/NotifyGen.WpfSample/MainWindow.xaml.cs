using System.Windows;

namespace NotifyGen.WpfSample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BulkLoad_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.BulkLoadDemoData();
    }
}
