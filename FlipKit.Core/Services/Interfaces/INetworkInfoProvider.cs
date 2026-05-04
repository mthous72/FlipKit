using FlipKit.Core.Helpers;

namespace FlipKit.Core.Services
{
    /// <summary>
    /// Thin testable wrapper over <see cref="NetworkHelper.GetNetworkInfo"/> static. Phase 5c
    /// extracted this so consumers (notably <see cref="NetworkAddressProvider"/> in the Desktop
    /// project) can be unit-tested with a stub instead of touching real network adapters.
    /// </summary>
    public interface INetworkInfoProvider
    {
        NetworkInfo GetNetworkInfo();
    }

    /// <summary>
    /// Production implementation — delegates to <see cref="NetworkHelper.GetNetworkInfo"/>.
    /// </summary>
    public sealed class NetworkInfoProvider : INetworkInfoProvider
    {
        public NetworkInfo GetNetworkInfo() => NetworkHelper.GetNetworkInfo();
    }
}
