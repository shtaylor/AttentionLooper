using System.Windows;
using System.Windows.Controls;
using AttentionLooper.ViewModels;

namespace AttentionLooper.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        InitializeComponent();
    }

    private void SoundComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        _viewModel.RefreshSoundsCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
