using System.Security.Cryptography;

namespace K1.VectorDB.Engine.Helpers;

/// <summary>
/// Provides atomic write and checksum-verified read operations for binary data files.
/// File format: [32 bytes SHA-256 of payload] [payload bytes]
/// </summary>
public static class DataFileHelper
{
    private const int ChecksumLength = 32; // SHA-256 produces 32 bytes

    /// <summary>
    /// Writes <paramref name="data"/> to <paramref name="path"/> atomically.
    /// The file is first written to a temporary sibling path, then renamed,
    /// so a crash mid-write cannot leave a partial file at the target path.
    /// A SHA-256 checksum of the payload is prepended to the file so that
    /// subsequent reads can detect corruption.
    /// </summary>
    public static void WriteWithChecksum(string path, byte[] data)
    {
        var checksum = SHA256.HashData(data);

        var fileBytes = new byte[ChecksumLength + data.Length];
        checksum.CopyTo(fileBytes, 0);
        data.CopyTo(fileBytes, ChecksumLength);

        var tempPath = path + ".tmp";
        try
        {
            File.WriteAllBytes(tempPath, fileBytes);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            // Best-effort cleanup of the temp file so we don't leave debris.
            try { File.Delete(tempPath); } catch { /* ignore */ }
            throw;
        }
    }

    /// <summary>
    /// Reads the file at <paramref name="path"/>, verifies its embedded
    /// SHA-256 checksum, and returns the payload bytes.
    /// </summary>
    /// <exception cref="FileNotFoundException">File does not exist.</exception>
    /// <exception cref="InvalidDataException">
    /// File is too short to contain a checksum header, or the checksum does
    /// not match (i.e. the data is corrupted or truncated).
    /// </exception>
    public static byte[] ReadAndVerifyChecksum(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Data file not found: {path}", path);

        byte[] fileBytes;
        try
        {
            fileBytes = File.ReadAllBytes(path);
        }
        catch (IOException ex)
        {
            throw new IOException($"Failed to read data file '{path}': {ex.Message}", ex);
        }

        if (fileBytes.Length < ChecksumLength)
            throw new InvalidDataException(
                $"Data file '{path}' is too short ({fileBytes.Length} bytes) to contain a valid checksum header.");

        var storedChecksum = fileBytes[..ChecksumLength];
        var payload = fileBytes[ChecksumLength..];

        var actualChecksum = SHA256.HashData(payload);

        if (!storedChecksum.AsSpan().SequenceEqual(actualChecksum))
            throw new InvalidDataException(
                $"Checksum mismatch in '{path}': the file is corrupted or was modified outside of the database.");

        return payload;
    }
}
