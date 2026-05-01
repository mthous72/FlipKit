using CommunityToolkit.Mvvm.ComponentModel;

namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Wraps a single additional-photo slot so the View can bind to an
    /// ObservableCollection&lt;PhotoSlot&gt; and pass each item to the remove command
    /// by reference (avoiding string-equality ambiguity when paths repeat).
    /// Tracks both the local <see cref="Path"/> (pre-upload) and the hosted
    /// <see cref="Url"/> (post-upload) so the Edit view can show already-uploaded
    /// images even when the local file is gone.
    /// </summary>
    public partial class PhotoSlot : ObservableObject
    {
        [ObservableProperty] private string? _path;
        [ObservableProperty] private string? _url;

        /// <summary>Prefer the hosted URL when available, fall back to the local path.</summary>
        public string? DisplayImage => !string.IsNullOrEmpty(Url) ? Url : Path;

        public PhotoSlot() { }
        public PhotoSlot(string? path, string? url = null) { _path = path; _url = url; }

        partial void OnPathChanged(string? value) => OnPropertyChanged(nameof(DisplayImage));
        partial void OnUrlChanged(string? value) => OnPropertyChanged(nameof(DisplayImage));
    }
}
