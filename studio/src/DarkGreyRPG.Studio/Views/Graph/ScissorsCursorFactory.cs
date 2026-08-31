using System.IO;
using System.Windows.Input;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Creates the Studio's compact high-contrast scissors cursor without an external asset.</summary>
internal static class ScissorsCursorFactory
{
    private const int Size = 32;
    private const int HotspotX = 11;
    private const int HotspotY = 17;
    private const byte OutlineRed = 24;
    private const byte OutlineGreen = 76;
    private const byte OutlineBlue = 99;
    private static readonly MemoryStream CursorStream = new(BuildCursorBytes(), writable: false);

    public static Cursor Cursor { get; } = new(CursorStream);

    private static byte[] BuildCursorBytes()
    {
        var pixels = new byte[Size * Size * 4];

        // Compact loop handles: dark outer edge, cyan metal, and transparent finger openings.
        DrawLoop(pixels,
            [(2, 20), (4, 17), (7, 17), (10, 19), (11, 22), (9, 25), (6, 26), (3, 24)],
            [(4, 20), (5, 19), (7, 19), (8, 20), (9, 22), (8, 23), (6, 24), (4, 23)],
            [(6, 20), (7, 20), (8, 22), (7, 23), (6, 22)]);
        DrawLoop(pixels,
            [(10, 23), (12, 20), (15, 21), (18, 24), (19, 27), (17, 30), (14, 30), (11, 27)],
            [(12, 24), (13, 22), (15, 23), (17, 25), (17, 27), (15, 28), (13, 27)],
            [(14, 24), (15, 24), (16, 25), (16, 27), (14, 26)]);

        // Two separate tapered blades open toward the upper-right and lower-right.
        DrawBlade(pixels,
            [(10, 16), (12, 14), (27, 2), (30, 2), (29, 5), (13, 18)],
            [(12, 16), (13, 15), (27, 4), (28, 4), (28, 5), (13, 17)],
            235, 242, 248);
        DrawBlade(pixels,
            [(10, 17), (13, 17), (29, 9), (30, 11), (29, 14), (13, 20)],
            [(12, 18), (14, 18), (28, 11), (29, 11), (28, 12), (14, 19)],
            112, 215, 255);

        // Small pivot reinforces the cutting point without hiding the blade split.
        FillPolygon(pixels, [(8, 16), (11, 14), (14, 16), (14, 19), (11, 21), (8, 19)],
            OutlineRed, OutlineGreen, OutlineBlue);
        FillPolygon(pixels, [(10, 16), (11, 15), (13, 17), (12, 19), (10, 18)], 235, 242, 248);

        var andMask = BuildAndMask(pixels);
        const int bitmapHeaderSize = 40;
        var imageSize = bitmapHeaderSize + pixels.Length + andMask.Length;
        using var stream = new MemoryStream(22 + imageSize);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)2);
        writer.Write((ushort)1);
        writer.Write((byte)Size);
        writer.Write((byte)Size);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)HotspotX);
        writer.Write((ushort)HotspotY);
        writer.Write(imageSize);
        writer.Write(22);

        writer.Write(bitmapHeaderSize);
        writer.Write(Size);
        writer.Write(Size * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(pixels.Length);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(pixels);
        writer.Write(andMask);
        return stream.ToArray();
    }

    private static void DrawLoop(byte[] pixels, (int X, int Y)[] outer, (int X, int Y)[] inner,
        (int X, int Y)[] hole)
    {
        FillPolygon(pixels, outer, OutlineRed, OutlineGreen, OutlineBlue);
        FillPolygon(pixels, inner, 112, 215, 255);
        ClearPolygon(pixels, hole);
    }

    private static void DrawBlade(byte[] pixels, (int X, int Y)[] outer, (int X, int Y)[] inner,
        byte red, byte green, byte blue)
    {
        FillPolygon(pixels, outer, OutlineRed, OutlineGreen, OutlineBlue);
        FillPolygon(pixels, inner, red, green, blue);
    }

    private static byte[] BuildAndMask(byte[] pixels)
    {
        var stride = ((Size + 31) / 32) * 4;
        var mask = new byte[stride * Size];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var pixel = PixelOffset(x, y);
            if (pixels[pixel + 3] != 0) continue;
            var maskRow = Size - 1 - y;
            mask[maskRow * stride + x / 8] |= (byte)(0x80 >> (x % 8));
        }
        return mask;
    }

    private static void FillPolygon(byte[] pixels, (int X, int Y)[] points,
        byte red, byte green, byte blue)
    {
        var minX = Math.Max(0, points.Min(point => point.X));
        var maxX = Math.Min(Size - 1, points.Max(point => point.X));
        var minY = Math.Max(0, points.Min(point => point.Y));
        var maxY = Math.Min(Size - 1, points.Max(point => point.Y));
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
            if (IsInsidePolygon(x + 0.5, y + 0.5, points))
                SetPixel(pixels, x, y, red, green, blue);
    }

    private static void ClearPolygon(byte[] pixels, (int X, int Y)[] points)
    {
        var minX = Math.Max(0, points.Min(point => point.X));
        var maxX = Math.Min(Size - 1, points.Max(point => point.X));
        var minY = Math.Max(0, points.Min(point => point.Y));
        var maxY = Math.Min(Size - 1, points.Max(point => point.Y));
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
            if (IsInsidePolygon(x + 0.5, y + 0.5, points))
                ClearPixel(pixels, x, y);
    }

    private static bool IsInsidePolygon(double x, double y, (int X, int Y)[] points)
    {
        var inside = false;
        for (var i = 0; i < points.Length; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Length];
            if ((current.Y > y) == (next.Y > y)) continue;
            var intersectionX = (next.X - current.X) * (y - current.Y) /
                                (next.Y - current.Y) + current.X;
            if (x < intersectionX) inside = !inside;
        }
        return inside;
    }

    private static void SetPixel(byte[] pixels, int x, int y, byte red, byte green, byte blue)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size) return;
        var offset = PixelOffset(x, y);
        pixels[offset] = blue;
        pixels[offset + 1] = green;
        pixels[offset + 2] = red;
        pixels[offset + 3] = 255;
    }

    private static void ClearPixel(byte[] pixels, int x, int y)
    {
        var offset = PixelOffset(x, y);
        pixels[offset] = 0;
        pixels[offset + 1] = 0;
        pixels[offset + 2] = 0;
        pixels[offset + 3] = 0;
    }

    private static int PixelOffset(int x, int y) => ((Size - 1 - y) * Size + x) * 4;
}
