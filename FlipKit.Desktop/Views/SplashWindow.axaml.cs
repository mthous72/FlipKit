using Avalonia.Controls;

namespace FlipKit.Desktop.Views
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public void SetStatus(string message)
        {
            if (StatusText != null)
                StatusText.Text = message;
        }
    }
}
