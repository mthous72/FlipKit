using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FlipKit.Core.Services;
using FlipKit.Core.Services.Scanning;

namespace FlipKit.Desktop.Views
{
    public partial class PaidModelConsentDialog : Window
    {
        // Result fields. Chosen is set to the picker selection only when the user
        // hits Confirm; Cancel leaves it null so the consent service interprets
        // that as "abort the scan cleanly".
        private OpenRouterModel? _chosen;

        public PaidModelConsentDialog()
        {
            InitializeComponent();
        }

        public PaidModelConsentDialog(
            IReadOnlyList<OpenRouterModel> available,
            OpenRouterModel suggested,
            string contextMessage) : this()
        {
            this.FindControl<TextBlock>("ContextMessage")!.Text = contextMessage;

            var picker = this.FindControl<ComboBox>("ModelPicker")!;
            picker.ItemsSource = available;
            // Pre-select the suggestion (or the first paid model if for some reason
            // the suggestion isn't in the list). A user who trusts the suggestion
            // just hits Confirm.
            picker.SelectedItem = available.FirstOrDefault(m => m.Id == suggested.Id) ?? available.FirstOrDefault();

            UpdateSelectedSummary(picker.SelectedItem as OpenRouterModel);
            picker.SelectionChanged += (_, _) => UpdateSelectedSummary(picker.SelectedItem as OpenRouterModel);

            this.FindControl<Button>("ProceedButton")!.Click += (_, _) =>
            {
                _chosen = picker.SelectedItem as OpenRouterModel;
                Close();
            };
            this.FindControl<Button>("CancelButton")!.Click += (_, _) =>
            {
                _chosen = null;
                Close();
            };
        }

        private void UpdateSelectedSummary(OpenRouterModel? model)
        {
            this.FindControl<TextBlock>("SelectedModelName")!.Text = model?.DisplayName ?? "(none)";
            this.FindControl<TextBlock>("SelectedModelCost")!.Text =
                model != null ? ModelCostFormatter.FormatConsentSummary(model) : string.Empty;
        }

        public OpenRouterModel? Chosen => _chosen;

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
