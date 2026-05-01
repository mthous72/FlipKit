using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;

namespace FlipKit.Desktop.Views
{
    public partial class PaidModelConsentDialog : Window
    {
        private bool _accepted;

        public PaidModelConsentDialog()
        {
            InitializeComponent();
        }

        public PaidModelConsentDialog(OpenRouterModel proposed, string contextMessage) : this()
        {
            this.FindControl<TextBlock>("ContextMessage")!.Text = contextMessage;
            this.FindControl<TextBlock>("ProposedModelName")!.Text = proposed.DisplayName;
            this.FindControl<TextBlock>("ProposedModelCost")!.Text =
                ModelCostFormatter.FormatConsentSummary(proposed);

            this.FindControl<Button>("ProceedButton")!.Click += (s, e) =>
            {
                _accepted = true;
                Close();
            };
            this.FindControl<Button>("CancelButton")!.Click += (s, e) =>
            {
                _accepted = false;
                Close();
            };
        }

        public bool Accepted => _accepted;

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
