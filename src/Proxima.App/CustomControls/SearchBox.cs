using Avalonia.Controls;

namespace Proxima.App.CustomControls;

public class SearchBox : TextBox
{
    public SearchBox()
    {
        Watermark = "Поиск";
    }
}
