using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using PDFEditorApp.Models;

namespace PDFEditorApp.Services;

/// <summary>
/// Bộ mã hóa PNG tối giản, thuần .NET (Services không được dùng WPF). Ghi ảnh RGB 8-bit,
/// filter "Sub" cho mỗi dòng, nén zlib, kèm chunk pHYs chứa DPI. Xem docs/export.md.
/// </summary>
public static class PngEncoder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static void Write(Stream output, RenderedPage image, int dpi)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(image);

        output.Write(Signature);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), image.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), image.Height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // color type: truecolor RGB
        WriteChunk(output, "IHDR", ihdr);

        var pixelsPerMeter = (int)Math.Round(dpi / 0.0254);
        var phys = new byte[9];
        BinaryPrimitives.WriteInt32BigEndian(phys.AsSpan(0), pixelsPerMeter);
        BinaryPrimitives.WriteInt32BigEndian(phys.AsSpan(4), pixelsPerMeter);
        phys[8] = 1; // đơn vị: mét
        WriteChunk(output, "pHYs", phys);

        WriteChunk(output, "IDAT", Compress(image));
        WriteChunk(output, "IEND", []);
    }

    private static byte[] Compress(RenderedPage image)
    {
        var rowLength = image.Width * 3;
        var row = new byte[rowLength + 1];
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < image.Height; y++)
            {
                row[0] = 1; // filter Sub
                var src = y * image.Stride;
                byte prevR = 0, prevG = 0, prevB = 0;
                for (var x = 0; x < image.Width; x++)
                {
                    var i = src + (x * 4);
                    byte b = image.Pixels[i], g = image.Pixels[i + 1], r = image.Pixels[i + 2];
                    var o = 1 + (x * 3);
                    row[o] = (byte)(r - prevR);
                    row[o + 1] = (byte)(g - prevG);
                    row[o + 2] = (byte)(b - prevB);
                    prevR = r;
                    prevG = g;
                    prevB = b;
                }

                zlib.Write(row);
            }
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        output.Write(buffer);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crc = UpdateCrc(0xFFFFFFFF, typeBytes);
        crc = UpdateCrc(crc, data) ^ 0xFFFFFFFF;
        BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
        output.Write(buffer);
    }

    internal static uint Crc32(ReadOnlySpan<byte> data) => UpdateCrc(0xFFFFFFFF, data) ^ 0xFFFFFFFF;

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
