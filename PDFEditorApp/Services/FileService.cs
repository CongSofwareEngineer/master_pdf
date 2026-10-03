namespace PDFEditorApp.Services;

/// <summary>Đọc / ghi file an toàn (xem docs/open-file.md, docs/save.md).</summary>
public static class FileService
{
    public static bool IsPdfPath(string? path) =>
        !string.IsNullOrEmpty(path) && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>Đọc toàn bộ file; lỗi IO được bọc thành <see cref="PdfException"/> loại File.</summary>
    public static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new PdfException(PdfErrorKind.File, ex);
        }
    }

    /// <summary>
    /// Ghi file "nguyên tử": ghi ra file tạm cùng thư mục, flush xuống đĩa rồi mới thay file đích.
    /// Nếu lỗi giữa chừng, file đích cũ không bị hỏng.
    /// </summary>
    public static void WriteAtomic(string path, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new PdfException(PdfErrorKind.File);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            throw new PdfException(PdfErrorKind.File, ex);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
