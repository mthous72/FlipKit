using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlipKit.Desktop.Views
{
    public partial class AiScanConsentDialog : Window
    {
        private bool _proceed;

        public AiScanConsentDialog()
        {
            InitializeComponent();

            this.FindControl<Button>("ContinueButton")!.Click += (_, _) =>
            {
                _proceed = true;
                Close();
            };
            this.FindControl<Button>("CancelButton")!.Click += (_, _) =>
            {
                _proceed = false;
                Close();
            };
        }

        public bool Proceed => _proceed;
        public bool Remember => this.FindControl<CheckBox>("RememberCheckBox")!.IsChecked == true;

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
