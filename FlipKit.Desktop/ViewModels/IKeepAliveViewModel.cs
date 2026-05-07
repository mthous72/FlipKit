namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Marker for ViewModels that must survive tab navigation.
    /// <see cref="MainWindowViewModel.OnCurrentPageChanging"/> skips Dispose for these;
    /// the DI container (singleton lifetime) handles disposal on app shutdown instead.
    /// </summary>
    internal interface IKeepAliveViewModel { }
}
