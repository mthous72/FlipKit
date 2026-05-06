namespace FlipKit.Core.Services
{
    /// <summary>
    /// Encrypts and decrypts secret strings (API keys, OAuth tokens) for storage.
    /// Encrypted values are prefixed with "protected:" so unencrypted legacy values
    /// are detected on load and returned as-is (zero-downtime migration to encryption).
    /// </summary>
    public interface ISecretEncryption
    {
        /// <summary>
        /// Returns the encrypted form of <paramref name="plaintext"/>, prefixed with
        /// "protected:". Returns null/empty unchanged.
        /// </summary>
        string? Protect(string? plaintext);

        /// <summary>
        /// Decrypts a value previously returned by <see cref="Protect"/>. If the value
        /// does not carry the "protected:" prefix it is returned as-is (plaintext migration
        /// path). Returns null if decryption fails (expired key, tampered data).
        /// </summary>
        string? Unprotect(string? ciphertext);
    }
}
