using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlipKit.Desktop.Views
{
    public enum CloseDialogChoice { Minimize, CloseAll }

    public partial class CloseOrMinimizeDialog : Window
    {
        public CloseDialogChoice Choice { get; private set; } = CloseDialogChoice.Minimize;

        public CloseOrMinimizeDialog()
        {
            InitializeComponent();

            this.FindControl<Button>("CloseAllButton")!.Click += (_, _) =>
            {
                Choice = CloseDialogChoice.CloseAll;
                Close();
            };
            this.FindControl<Button>("MinimizeButton")!.Click += (_, _) =>
            {
                Choice = CloseDialogChoice.Minimize;
                Close();
            };
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
