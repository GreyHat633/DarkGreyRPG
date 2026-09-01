using System.IO;
using System.Windows.Input;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Creates a small monochrome scissors cursor without an external asset.</summary>
internal static class ScissorsCursorFactory
{
    private const int Size = 16;
    private const int HotspotX = 6;
    private const int HotspotY = 8;
    private const int BitmapHeaderSize = 40;

    // '#' is the black silhouette and '+' is the white inset. Keeping the
    // source as a tiny bitmap makes the cursor easy to audit and avoids a
    // second, colorful illustration language in the graph editor.
    private static readonly string[] Icon =
    [
        "................",
        ".............#+.",
        "............+#+.",
        "..........+##+..",
        "........+##+....",
        "......+##+......",
        "....+##+........",
        "..##+.##........",
        ".##...+##.......",
        ".##..##..##.....",
        "..####..###.....",
        "...+##...###....",
        "....##....###...",
        "...+##.....###..",
        "..###.......##..",
        "................",
    ];

    private static readonly MemoryStream CursorStream = new(BuildCursorBytes(), writable: false);

    public static Cursor Cursor { get; } = new(CursorStream);

    private static byte[] BuildCursorBytes()
    {
        var pixels = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var marker = Icon[y][x];
            if (marker == '.') continue;
            var color = marker == '+' ? (byte)255 : (byte)0;
            var offset = PixelOffset(x, y);
            pixels[offset] = color;
            pixels[offset + 1] = color;
            pixels[offset + 2] = color;
            pixels[offset + 3] = 255;
        }

        using var stream = new MemoryStream();
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
        var andMask = new byte[((Size + 31) / 32) * 4 * Size];
        var imageSize = BitmapHeaderSize + pixels.Length + andMask.Length;
        writer.Write(imageSize);
        writer.Write(22);
        writer.Write(BitmapHeaderSize);
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

    private static int PixelOffset(int x, int y) => ((Size - 1 - y) * Size + x) * 4;
}
