using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Views
{
    public partial class ScanView : UserControl
    {
        public ScanView()
        {
            InitializeComponent();

            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
#pragma warning disable CS0618 // Avalonia 11.x deprecation — DataTransfer replacement has different interface
            e.DragEffects = e.Data.Contains(DataFormats.Files)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
#pragma warning restore CS0618
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            if (DataContext is not ScanViewModel vm) return;

#pragma warning disable CS0618
            if (!e.Data.Contains(DataFormats.Files)) return;

            var files = e.Data.GetFiles();
#pragma warning restore CS0618

            var file = files?.FirstOrDefault();
            if (file == null) return;

            var path = file.Path.LocalPath;
            var ext = System.IO.Path.GetExtension(path).ToLower();
            if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp")
            {
                vm.ImagePath = path;
            }
        }
    }
}
