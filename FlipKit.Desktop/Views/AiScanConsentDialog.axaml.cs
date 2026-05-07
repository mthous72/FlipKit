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

            this.FindControl<Button>("AcceptButton")!.Click += (_, _) =>
            {
                _proceed = true;
                Close();
            };
            this.FindControl<Button>("DenyButton")!.Click += (_, _) =>
            {
                _proceed = false;
                Close();
            };
        }

        // Accepting always saves the consent — there's no reason to accept and be asked again.
        public bool Proceed => _proceed;
        public bool Remember => _proceed;

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
