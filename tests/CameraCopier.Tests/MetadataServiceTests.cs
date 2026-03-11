using Microsoft.Extensions.Logging.Abstractions;
using DanTheMan827.CameraCopier.Services;

namespace DanTheMan827.CameraCopier.Tests;

/// <summary>
/// Tests for <see cref="MetadataService"/>.
/// </summary>
public class MetadataServiceTests : IDisposable
{
    private readonly MetadataService _sut = new(NullLogger<MetadataService>.Instance);
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public MetadataServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void GetCaptureDate_NonImageFile_FallsBackToCreationTime()
    {
        // Create a plain text file with no EXIF
        var filePath = Path.Combine(_tempDir, "test.jpg");
        File.WriteAllText(filePath, "not a real jpeg");

        var before = DateTime.Now.AddSeconds(-1);
        var result = _sut.GetCaptureDate(filePath);
        var after = DateTime.Now.AddSeconds(1);

        // Should fall back to creation time which is within the test window
        Assert.InRange(result, before, after);
    }

    [Fact]
    public void GetCaptureDate_ValidJpegWithExif_ReturnsExifDate()
    {
        // This is a minimal valid JPEG with an EXIF DateTimeOriginal = 2025:08:14 10:30:00
        // Generated from a known EXIF-containing image for testing purposes.
        // We use a small real JPEG with EXIF data embedded.
        var filePath = Path.Combine(_tempDir, "exif.jpg");
        File.WriteAllBytes(filePath, CreateMinimalJpegWithExif());

        var date = _sut.GetCaptureDate(filePath);

        Assert.Equal(2025, date.Year);
        Assert.Equal(8, date.Month);
        Assert.Equal(14, date.Day);
    }

    [Fact]
    public void GetCaptureDate_CorruptFile_FallsBackToCreationTime()
    {
        var filePath = Path.Combine(_tempDir, "corrupt.jpg");
        File.WriteAllBytes(filePath, [0xFF, 0x00, 0x01, 0x02]);

        var before = DateTime.Now.AddSeconds(-1);
        var result = _sut.GetCaptureDate(filePath);
        var after = DateTime.Now.AddSeconds(1);

        Assert.InRange(result, before, after);
    }

    /// <summary>
    /// Builds a minimal JPEG with an EXIF IFD containing DateTimeOriginal = "2025:08:14 10:30:00".
    /// </summary>
    private static byte[] CreateMinimalJpegWithExif()
    {
        // EXIF APP1 marker with DateTimeOriginal tag (0x9003) set to "2025:08:14 10:30:00"
        // We construct a minimal JFIF/EXIF file manually.
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // SOI
        w.Write((byte)0xFF);
        w.Write((byte)0xD8);

        // Build EXIF payload
        var exif = BuildExifApp1();

        // APP1 marker
        w.Write((byte)0xFF);
        w.Write((byte)0xE1);
        // Length = 2 (length bytes) + exif.Length
        int app1Length = 2 + exif.Length;
        w.Write((byte)(app1Length >> 8));
        w.Write((byte)(app1Length & 0xFF));
        w.Write(exif);

        // EOI
        w.Write((byte)0xFF);
        w.Write((byte)0xD9);

        return ms.ToArray();
    }

    private static byte[] BuildExifApp1()
    {
        // "Exif\0\0" header
        var header = "Exif\0\0"u8.ToArray();

        // TIFF header (little-endian)
        // II = little endian, 42 = magic, offset to IFD0 = 8
        var tiff = new byte[]
        {
            0x49, 0x49,       // 'II' = little endian
            0x2A, 0x00,       // magic 42
            0x08, 0x00, 0x00, 0x00  // IFD0 at offset 8
        };

        // IFD0: ExifIFD pointer (tag 0x8769)
        // We'll make IFD0 point to a sub-IFD that contains DateTimeOriginal
        // IFD0 has 1 entry: ExifSubIFD pointer
        // After IFD0 (8 + 2 + 12 + 4 = 26), we put ExifSubIFD

        int ifd0Start = 8;                      // offset 8 in TIFF data
        int ifd0EntryCount = 1;
        int ifd0Size = 2 + ifd0EntryCount * 12 + 4; // 18
        int exifSubIfdOffset = ifd0Start + ifd0Size; // 26

        // ExifSubIFD: DateTimeOriginal (0x9003), 1 entry
        int exifEntryCount = 1;
        int exifSubIfdSize = 2 + exifEntryCount * 12 + 4; // 18
        int dateTimeOriginalDataOffset = exifSubIfdOffset + exifSubIfdSize; // 44

        const string dateStr = "2025:08:14 10:30:00\0"; // 20 bytes
        var dateBytes = System.Text.Encoding.ASCII.GetBytes(dateStr);

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, true);

        // TIFF header
        w.Write(tiff);

        // IFD0 (offset 8)
        w.Write((ushort)ifd0EntryCount);
        // Entry: ExifSubIFD pointer (tag=0x8769, type=LONG(4), count=1, value=offset)
        w.Write((ushort)0x8769);  // tag
        w.Write((ushort)4);       // type = LONG
        w.Write((uint)1);         // count
        w.Write((uint)exifSubIfdOffset); // value = offset to sub-IFD
        w.Write((uint)0);         // next IFD = 0 (none)

        // ExifSubIFD (offset 26)
        w.Write((ushort)exifEntryCount);
        // Entry: DateTimeOriginal (tag=0x9003, type=ASCII(2), count=20, value=offset)
        w.Write((ushort)0x9003);  // tag
        w.Write((ushort)2);       // type = ASCII
        w.Write((uint)20);        // count = 20 chars
        w.Write((uint)dateTimeOriginalDataOffset); // value = offset to data
        w.Write((uint)0);         // next IFD = 0

        // DateTimeOriginal data (offset 44)
        w.Write(dateBytes);

        var tiffData = ms.ToArray();
        var result = new byte[header.Length + tiffData.Length];
        header.CopyTo(result, 0);
        tiffData.CopyTo(result, header.Length);
        return result;
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }
}
