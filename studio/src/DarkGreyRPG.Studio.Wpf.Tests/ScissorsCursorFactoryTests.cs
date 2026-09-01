using System.Reflection;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ScissorsCursorFactoryTests
{
    private const int DirectoryOffset = 6;
    private const int DirectoryEntrySize = 16;
    private const int DibHeaderSize = 40;
    private static readonly int[] ExpectedSizes = [32, 48, 64, 96];

    [STATestMethod]
    public void CursorPayloadHasExactlyFourScaledEntries()
    {
        var bytes = BuildPayload();
        using var reader = new BinaryReader(new MemoryStream(bytes));

        Assert.AreEqual((ushort)0, reader.ReadUInt16());
        Assert.AreEqual((ushort)2, reader.ReadUInt16());
        Assert.AreEqual((ushort)ExpectedSizes.Length, reader.ReadUInt16());

        var previousOffset = DirectoryOffset + DirectoryEntrySize * ExpectedSizes.Length;
        for (var index = 0; index < ExpectedSizes.Length; index++)
        {
            var size = ExpectedSizes[index];
            Assert.AreEqual((byte)size, reader.ReadByte());
            Assert.AreEqual((byte)size, reader.ReadByte());
            Assert.AreEqual((byte)0, reader.ReadByte());
            Assert.AreEqual((byte)0, reader.ReadByte());
            Assert.AreEqual((ushort)(2 * size / 32), reader.ReadUInt16());
            Assert.AreEqual((ushort)(6 * size / 32), reader.ReadUInt16());
            var resourceLength = reader.ReadUInt32();
            var imageOffset = reader.ReadUInt32();

            Assert.AreEqual((uint)previousOffset, imageOffset);
            Assert.IsGreaterThanOrEqualTo((uint)DibHeaderSize, resourceLength);
            Assert.IsLessThanOrEqualTo((uint)bytes.Length, imageOffset + resourceLength);
            previousOffset = checked((int)(imageOffset + resourceLength));

            using var image = new BinaryReader(new MemoryStream(bytes, (int)imageOffset, (int)resourceLength, writable: false));
            Assert.AreEqual(DibHeaderSize, image.ReadInt32());
            Assert.AreEqual(size, image.ReadInt32());
            Assert.AreEqual(size * 2, image.ReadInt32());
            Assert.AreEqual((ushort)1, image.ReadUInt16());
            Assert.AreEqual((ushort)32, image.ReadUInt16());
            Assert.AreEqual(0, image.ReadInt32());
            Assert.AreEqual(size * size * 4, image.ReadInt32());
        }

        Assert.AreEqual(bytes.Length, previousOffset);
    }

    [STATestMethod]
    public void EveryEntryRetainsGrayscaleAlphaOutlineAndAntialiasing()
    {
        var bytes = BuildPayload();
        var entries = ReadEntries(bytes);

        foreach (var entry in entries)
        {
            var transparentPixels = 0;
            var partialAlphaPixels = 0;
            var blackPixels = 0;
            var visiblePixels = 0;

            for (var y = 0; y < entry.Size; y++)
            for (var x = 0; x < entry.Size; x++)
            {
                var pixel = ReadPixel(bytes, entry, x, y);
                Assert.AreEqual(pixel.R, pixel.G, $"Entry {entry.Size} contains a non-grayscale pixel.");
                Assert.AreEqual(pixel.G, pixel.B, $"Entry {entry.Size} contains a non-grayscale pixel.");
                if (pixel.A == 0)
                    transparentPixels++;
                else
                {
                    visiblePixels++;
                    if (pixel.A < 255)
                        partialAlphaPixels++;
                    if (pixel.A > 0 && pixel.R <= 32)
                        blackPixels++;
                }
            }

            Assert.IsGreaterThan(0, transparentPixels, $"Entry {entry.Size} lost its transparent background.");
            Assert.IsGreaterThan(entry.Size * entry.Size / 5, visiblePixels, $"Entry {entry.Size} lost the scissors silhouette.");
            Assert.IsGreaterThan(20, partialAlphaPixels, $"Entry {entry.Size} has no antialiased edge pixels.");
            Assert.IsGreaterThan(20, blackPixels, $"Entry {entry.Size} lost its black outline.");
        }
    }

    [STATestMethod]
    public void CursorPayloadContainsReferenceDerivedScissorsAtScaledHotspots()
    {
        var bytes = BuildPayload();
        foreach (var entry in ReadEntries(bytes))
        {
            var hotspotX = 2 * entry.Size / 32;
            var hotspotY = 6 * entry.Size / 32;
            var upperBladeX = 6 * entry.Size / 32;
            var upperBladeY = 2 * entry.Size / 32;
            Assert.IsGreaterThan((byte)0, ReadPixel(bytes, entry, hotspotX, hotspotY).A,
                $"Entry {entry.Size} hotspot must land on the lower blade tip.");
            Assert.IsGreaterThan((byte)0, ReadPixel(bytes, entry, upperBladeX, upperBladeY).A,
                $"Entry {entry.Size} upper blade must reach its tip.");
            Assert.IsGreaterThan((byte)0, ReadPixel(bytes, entry, 13 * entry.Size / 32, 13 * entry.Size / 32).A,
                $"Entry {entry.Size} pivot must remain visible.");
            Assert.IsGreaterThan((byte)0, ReadPixel(bytes, entry, 24 * entry.Size / 32, 19 * entry.Size / 32).A,
                $"Entry {entry.Size} right handle must remain visible.");
        }
    }

    [STATestMethod]
    public void CursorLoadsAsWpfCursor()
    {
        var factory = typeof(DarkGreyRPG.Studio.Views.Graph.CanonicalGraphEditorView).Assembly
            .GetType("DarkGreyRPG.Studio.Views.Graph.ScissorsCursorFactory", throwOnError: true)!;
        var property = factory.GetProperty("Cursor", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(property);
        Assert.IsNotNull(property.GetValue(null));
    }

    private static byte[] BuildPayload()
    {
        var factory = typeof(DarkGreyRPG.Studio.Views.Graph.CanonicalGraphEditorView).Assembly
            .GetType("DarkGreyRPG.Studio.Views.Graph.ScissorsCursorFactory", throwOnError: true)!;
        var method = factory.GetMethod("BuildCursorBytes", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method);
        return (byte[])method.Invoke(null, null)!;
    }

    private static CursorEntry[] ReadEntries(byte[] bytes)
    {
        var entries = new CursorEntry[ExpectedSizes.Length];
        using var reader = new BinaryReader(new MemoryStream(bytes));
        reader.ReadBytes(DirectoryOffset);
        for (var index = 0; index < entries.Length; index++)
        {
            var size = reader.ReadByte();
            reader.ReadByte();
            reader.ReadBytes(2);
            var hotspotX = reader.ReadUInt16();
            var hotspotY = reader.ReadUInt16();
            var resourceLength = reader.ReadUInt32();
            var imageOffset = reader.ReadUInt32();
            entries[index] = new CursorEntry(size, hotspotX, hotspotY, resourceLength, imageOffset);
        }

        return entries;
    }

    private static Pixel ReadPixel(byte[] bytes, CursorEntry entry, int x, int y)
    {
        var pixelOffset = checked((int)entry.ImageOffset + DibHeaderSize + ((entry.Size - 1 - y) * entry.Size + x) * 4);
        return new Pixel(bytes[pixelOffset + 2], bytes[pixelOffset + 1], bytes[pixelOffset], bytes[pixelOffset + 3]);
    }

    private readonly record struct CursorEntry(byte Size, ushort HotspotX, ushort HotspotY, uint ResourceLength, uint ImageOffset);

    private readonly record struct Pixel(byte R, byte G, byte B, byte A);
}
