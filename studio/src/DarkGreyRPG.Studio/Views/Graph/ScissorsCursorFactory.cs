using System.IO;
using System.Windows.Input;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Creates the Studio's small high-contrast scissors cursor without an external asset.</summary>
internal static class ScissorsCursorFactory
{
    private const int Size = 32;
    private static readonly MemoryStream CursorStream = new(BuildCursorBytes(), writable: false);

    public static Cursor Cursor { get; } = new(CursorStream);

    private static byte[] BuildCursorBytes()
    {
        var pixels = new byte[Size * Size * 4];
        DrawLine(pixels, 12, 17, 28, 3, 3, 20, 24, 30);
        DrawLine(pixels, 12, 17, 28, 3, 1, 235, 242, 248);
        DrawLine(pixels, 13, 18, 27, 13, 3, 20, 24, 30);
        DrawLine(pixels, 13, 18, 27, 13, 1, 235, 242, 248);
        DrawCircle(pixels, 8, 22, 6, 2, 20, 24, 30);
        DrawCircle(pixels, 8, 22, 4, 2, 112, 215, 255);
        DrawCircle(pixels, 16, 25, 6, 2, 20, 24, 30);
        DrawCircle(pixels, 16, 25, 4, 2, 112, 215, 255);
        FillCircle(pixels, 13, 18, 3, 20, 24, 30);
        FillCircle(pixels, 13, 18, 1, 235, 242, 248);

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
        writer.Write((ushort)13);
        writer.Write((ushort)18);
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

    private static void DrawLine(byte[] pixels, int x0, int y0, int x1, int y1, int thickness,
        byte red, byte green, byte blue)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            FillCircle(pixels, x0, y0, thickness / 2, red, green, blue);
            if (x0 == x1 && y0 == y1) break;
            var twice = error * 2;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    private static void DrawCircle(byte[] pixels, int centerX, int centerY, int radius, int thickness,
        byte red, byte green, byte blue)
    {
        var inner = Math.Max(0, radius - thickness);
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            var distance = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
            if (distance <= radius * radius && distance >= inner * inner)
                SetPixel(pixels, x, y, red, green, blue);
        }
    }

    private static void FillCircle(byte[] pixels, int centerX, int centerY, int radius,
        byte red, byte green, byte blue)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
            if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= radius * radius)
                SetPixel(pixels, x, y, red, green, blue);
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

    private static int PixelOffset(int x, int y) => ((Size - 1 - y) * Size + x) * 4;
}
