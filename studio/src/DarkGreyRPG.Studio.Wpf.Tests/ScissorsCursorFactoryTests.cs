using System.Reflection;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ScissorsCursorFactoryTests
{
    private const int Size = 16;
    private const int DibHeaderSize = 40;
    private const int PixelOffset = 22 + DibHeaderSize;

    [STATestMethod]
    public void CursorPayloadHas16PixelDimensionsAndCuttingHotspot()
    {
        var bytes = BuildPayload();
        using var reader = new BinaryReader(new MemoryStream(bytes));

        Assert.AreEqual((ushort)0, reader.ReadUInt16());
        Assert.AreEqual((ushort)2, reader.ReadUInt16());
        Assert.AreEqual((ushort)1, reader.ReadUInt16());
        Assert.AreEqual((byte)Size, reader.ReadByte());
        Assert.AreEqual((byte)Size, reader.ReadByte());
        reader.ReadBytes(2);
        Assert.AreEqual((ushort)6, reader.ReadUInt16());
        Assert.AreEqual((ushort)8, reader.ReadUInt16());
        Assert.AreEqual(bytes.Length - 22, reader.ReadInt32());
        Assert.AreEqual(22, reader.ReadInt32());

        Assert.AreEqual(DibHeaderSize, reader.ReadInt32());
        Assert.AreEqual(Size, reader.ReadInt32());
        Assert.AreEqual(Size * 2, reader.ReadInt32());
        Assert.AreEqual((ushort)1, reader.ReadUInt16());
        Assert.AreEqual((ushort)32, reader.ReadUInt16());
    }

    [STATestMethod]
    public void CursorPayloadContainsCompactMonochromeScissorsSilhouette()
    {
        var pixels = BuildPayload().AsSpan(PixelOffset, Size * Size * 4);

        Assert.IsTrue(IsOpaque(pixels, 6, 8), "The cursor hotspot must land on the pivot.");
        Assert.IsTrue(IsOpaque(pixels, 13, 1), "The upper blade must reach its tip.");
        Assert.IsTrue(IsOpaque(pixels, 5, 11), "The lower handle must remain visible.");
        Assert.IsFalse(IsOpaque(pixels, 0, 0), "The cursor background must remain transparent.");
        Assert.IsTrue(IsGrayscale(pixels), "The cursor must use monochrome pixels only.");

        var opaquePixels = 0;
        for (var index = 0; index < Size * Size; index++)
            if (pixels[index * 4 + 3] != 0)
                opaquePixels++;
        Assert.IsTrue(opaquePixels is >= 20 and <= 120,
            $"Expected a compact icon silhouette, got {opaquePixels} opaque pixels.");
    }

    private static bool IsGrayscale(ReadOnlySpan<byte> pixels)
    {
        for (var index = 0; index < Size * Size; index++)
        {
            var offset = index * 4;
            if (pixels[offset] != pixels[offset + 1] || pixels[offset + 1] != pixels[offset + 2])
                return false;
        }
        return true;
    }

    private static byte[] BuildPayload()
    {
        var factory = typeof(DarkGreyRPG.Studio.Views.Graph.CanonicalGraphEditorView).Assembly
            .GetType("DarkGreyRPG.Studio.Views.Graph.ScissorsCursorFactory", throwOnError: true)!;
        var method = factory.GetMethod("BuildCursorBytes", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method);
        return (byte[])method.Invoke(null, null)!;
    }

    private static bool IsOpaque(ReadOnlySpan<byte> pixels, int x, int y)
    {
        var offset = ((Size - 1 - y) * Size + x) * 4;
        return pixels[offset + 3] != 0;
    }

    private static byte[] Pixel(ReadOnlySpan<byte> pixels, int x, int y)
    {
        var offset = ((Size - 1 - y) * Size + x) * 4;
        return pixels.Slice(offset, 4).ToArray();
    }
}
