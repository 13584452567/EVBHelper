using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace EVBHelper.Views;

public partial class OpenixImgView : UserControl
{
    public OpenixImgView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
