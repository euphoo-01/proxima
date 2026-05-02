using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Proxima.App.Views.Goals;

public partial class GoalsView : UserControl
{
    public GoalsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
