using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Proxima.App.Views.Goals;

public partial class AddGoalDialogView : UserControl
{
    public AddGoalDialogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
