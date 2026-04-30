using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FlipKit.Core.Helpers
{
    /// <summary>
    /// Helper class for network-related operations including local IP and Tailscale detection.
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Gets the primary local network IP address (192.168.x.x, 10.x.x.x, or 172.16-31.x.x).
        /// Excludes loopback, Tailscale, and virtual adapter IPs.
        /// </summary>
        /// <returns>The local IP address, or null if not found.</returns>
        public static string? GetLocalIpAddress()
        {
            var localIPs = GetAllLocalIpAddresses();

            // Prefer 192.168.x.x addresses (most common home networks)
            return localIPs.FirstOrDefault(ip => ip.StartsWith("192.168."))
                ?? localIPs.FirstOrDefault(ip => ip.StartsWith("10."))
                ?? localIPs.FirstOrDefault(ip => ip.StartsWith("172."))
                ?? localIPs.FirstOrDefault();
        }

        /// <summary>
        /// Gets all local network IP addresses from Ethernet and WiFi interfaces.
        /// Excludes Tailscale IPs (100.x.x.x).
        /// </summary>
        /// <returns>List of local IP addresses.</returns>
        public static List<string> GetAllLocalIpAddresses()
        {
            var localIPs = new List<string>();

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    // Only include physical network adapters
                    if (ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                        continue;

                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        var ipStr = ip.Address.ToString();

                        // Skip loopback
                        if (ipStr.StartsWith("127."))
                            continue;

                        // Skip Tailscale IPs (handled separately)
                        if (ipStr.StartsWith("100."))
                            continue;

                        localIPs.Add(ipStr);
                    }
                }
            }
            catch
            {
                // Network enumeration failed - return empty list
            }

            return localIPs;
        }

        /// <summary>
        /// Gets the Tailscale IP address if Tailscale is running and connected.
        /// Tailscale uses the CGNAT range: 100.64.0.0/10 (100.64.x.x - 100.127.x.x).
        /// </summary>
        /// <returns>The Tailscale IP address, or null if Tailscale is not running.</returns>
        public static string? GetTailscaleIpAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        var ipStr = ip.Address.ToString();

                        // Tailscale uses CGNAT range starting with 100.
                        // The full range is 100.64.0.0/10 but checking for 100. prefix is sufficient
                        if (ipStr.StartsWith("100."))
                        {
                            return ipStr;
                        }
                    }
                }
            }
            catch
            {
                // Network enumeration failed
            }

            return null;
        }

        /// <summary>
        /// Checks if Tailscale is running and connected.
        /// </summary>
        /// <returns>True if Tailscale IP is detected, false otherwise.</returns>
        public static bool IsTailscaleRunning()
        {
            return GetTailscaleIpAddress() != null;
        }

        /// <summary>
        /// Gets network information for display, including both local and Tailscale addresses.
        /// </summary>
        /// <returns>A NetworkInfo object with detected addresses.</returns>
        public static NetworkInfo GetNetworkInfo()
        {
            return new NetworkInfo
            {
                LocalIpAddress = GetLocalIpAddress(),
                TailscaleIpAddress = GetTailscaleIpAddress(),
                AllLocalIpAddresses = GetAllLocalIpAddresses()
            };
        }
    }

    /// <summary>
    /// Contains network address information for both local and Tailscale networks.
    /// </summary>
    public class NetworkInfo
    {
        /// <summary>
        /// The primary local network IP address (e.g., 192.168.1.50).
        /// </summary>
        public string? LocalIpAddress { get; set; }

        /// <summary>
        /// The Tailscale IP address if connected (e.g., 100.64.0.1).
        /// </summary>
        public string? TailscaleIpAddress { get; set; }

        /// <summary>
        /// All detected local IP addresses.
        /// </summary>
        public List<string> AllLocalIpAddresses { get; set; } = new();

        /// <summary>
        /// Whether Tailscale is detected and connected.
        /// </summary>
        public bool IsTailscaleAvailable => !string.IsNullOrEmpty(TailscaleIpAddress);

        /// <summary>
        /// Whether any local network is available.
        /// </summary>
        public bool IsLocalNetworkAvailable => !string.IsNullOrEmpty(LocalIpAddress);

        /// <summary>
        /// Whether any network connection is available.
        /// </summary>
        public bool HasAnyConnection => IsLocalNetworkAvailable || IsTailscaleAvailable;
    }
}
