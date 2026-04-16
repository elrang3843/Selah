using System.Windows;

namespace Selah.App.Views;

public partial class ProgressWindow : Window
{
    /// <summary>true로 설정한 뒤에만 Close()가 허용됩니다.</summary>
    internal bool AllowClose { get; set; }

    public ProgressWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 작업 진행 중에는 X 버튼으로 닫기를 막습니다
        if (!AllowClose)
            e.Cancel = true;
        else
            base.OnClosing(e);
    }
}
