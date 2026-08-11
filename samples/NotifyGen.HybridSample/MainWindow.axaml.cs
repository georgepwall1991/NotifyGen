using Avalonia.Controls;

namespace NotifyGen.HybridSample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new EditorViewModel();
    }
}
