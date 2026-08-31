using System.Reflection;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ScissorsCursorFactoryTests
{
    private const int Size = 32;
    private const int DibHeaderSize = 40;
    private const int PixelOffset = 22 + DibHeaderSize;

    [STATestMethod]
    public void CursorPayloadHas32PixelDimensionsAndCuttingHotspot()
    {
        var bytes = BuildPayload();
        using var reader = new BinaryReader(new MemoryStream(bytes));

        Assert.AreEqual((ushort)0, reader.ReadUInt16());
        Assert.AreEqual((ushort)2, reader.ReadUInt16());
        Assert.AreEqual((ushort)1, reader.ReadUInt16());
        Assert.AreEqual((byte)Size, reader.ReadByte());
        Assert.AreEqual((byte)Size, reader.ReadByte());
        reader.ReadBytes(2);
        Assert.AreEqual((ushort)11, reader.ReadUInt16());
        Assert.AreEqual((ushort)17, reader.ReadUInt16());
        Assert.AreEqual(bytes.Length - 22, reader.ReadInt32());
        Assert.AreEqual(22, reader.ReadInt32());

        Assert.AreEqual(DibHeaderSize, reader.ReadInt32());
        Assert.AreEqual(Size, reader.ReadInt32());
        Assert.AreEqual(Size * 2, reader.ReadInt32());
        Assert.AreEqual((ushort)1, reader.ReadUInt16());
        Assert.AreEqual((ushort)32, reader.ReadUInt16());
    }

    [STATestMethod]
    public void CursorPayloadContainsCompactSeparatedBladesAndFingerLoops()
    {
        var pixels = BuildPayload().AsSpan(PixelOffset, Size * Size * 4);

        Assert.IsTrue(IsOpaque(pixels, 11, 17), "The cursor hotspot must land on the pivot.");
        Assert.IsTrue(IsOpaque(pixels, 27, 3), "The upper blade must reach its tip.");
        Assert.IsTrue(IsOpaque(pixels, 29, 11), "The lower blade must remain distinct.");
        Assert.IsTrue(IsOpaque(pixels, 4, 21), "The upper finger loop must be visible.");
        Assert.IsTrue(IsOpaque(pixels, 12, 26), "The lower finger loop must be visible.");
        Assert.IsFalse(IsOpaque(pixels, 0, 0), "The cursor background must remain transparent.");
        Assert.IsFalse(IsOpaque(pixels, 7, 21), "The finger openings must remain transparent.");
        CollectionAssert.AreEqual(new byte[] { 99, 76, 24, 255 }, Pixel(pixels, 3, 21),
            "The outline must retain a blue-gray edge that remains visible on the dark graph canvas.");

        var opaquePixels = 0;
        for (var index = 0; index < Size * Size; index++)
            if (pixels[index * 4 + 3] != 0)
                opaquePixels++;
        Assert.IsTrue(opaquePixels is >= 90 and <= 300,
            $"Expected a compact icon silhouette, got {opaquePixels} opaque pixels.");
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
