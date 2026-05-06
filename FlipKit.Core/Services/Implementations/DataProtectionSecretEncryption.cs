using Microsoft.AspNetCore.DataProtection;

namespace FlipKit.Core.Services.Implementations
{
    /// <summary>
    /// ISecretEncryption backed by ASP.NET Core Data Protection.
    /// On Windows this uses DPAPI to protect the key ring; on Linux and macOS the
    /// key ring is stored in a directory protected by OS file permissions.
    /// Both Desktop and Web must share the same key directory and application name
    /// (set in DI registration) so that each can decrypt values written by the other.
    /// </summary>
    public class DataProtectionSecretEncryption : ISecretEncryption
    {
        private const string Prefix = "protected:";
        private readonly IDataProtector _protector;

        public DataProtectionSecretEncryption(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("FlipKit.Secrets.v1");
        }

        public string? Protect(string? plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return plaintext;
            return Prefix + _protector.Protect(plaintext);
        }

        public string? Unprotect(string? ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return ciphertext;

            // Legacy plaintext value — return unchanged so existing installs keep working.
            if (!ciphertext.StartsWith(Prefix))
                return ciphertext;

            try
            {
                return _protector.Unprotect(ciphertext.Substring(Prefix.Length));
            }
            catch
            {
                // Key expired or data tampered — treat as missing rather than crashing.
                return null;
            }
        }
    }
}
