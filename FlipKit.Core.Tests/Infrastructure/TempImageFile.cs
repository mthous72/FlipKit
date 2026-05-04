namespace FlipKit.Core.Tests.Infrastructure;

/// <summary>
/// Creates a temporary file with arbitrary bytes for tests that read images from disk.
/// The actual content doesn't matter — scanner services only base64-encode the bytes
/// and post them to the (mocked) HTTP endpoint.
///
/// Disposing deletes the file. Always use with `using var` to avoid temp-dir pollution.
/// </summary>
public sealed class TempImageFile : IDisposable
{
    public string Path { get; }

    public TempImageFile(string extension = "jpg", byte[]? bytes = null)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"flipkit-test-{Guid.NewGuid():N}.{extension}");
        File.WriteAllBytes(Path, bytes ?? new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }); // minimal JPEG SOI/EOI
    }

    public void Dispose()
    {
        try { File.Delete(Path); }
        catch { /* best-effort cleanup */ }
    }
}
