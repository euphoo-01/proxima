using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Proxima.App.Views.Auth;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
