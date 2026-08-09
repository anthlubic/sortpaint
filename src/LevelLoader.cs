using System.Collections.Generic;
using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>A level's picture turned into the grid the rules work on, plus the palette to draw it with.</summary>
public sealed class LoadedLevel
{
    public LoadedLevel(LevelGrid grid, Color[] palette)
    {
        Grid = grid;
        Palette = palette;
    }

    public LevelGrid Grid { get; }

    /// <summary>Indexed by palette index, the same indices the core logic passes around.</summary>
    public Color[] Palette { get; }
}

/// <summary>
/// Reads a level sprite into a <see cref="LevelGrid"/>. Distinct opaque colours are numbered in
/// the order they are first met, so the palette is stable for a given image.
/// </summary>
public static class LevelLoader
{
    public static LoadedLevel Load(LevelData data)
    {
        if (data?.Sprite is null)
        {
            GD.PushError("LevelData has no sprite assigned.");
            return null;
        }

        Image image = data.Sprite.GetImage();
        if (image is null)
        {
            GD.PushError($"Could not read an image out of {data.Sprite.ResourcePath}.");
            return null;
        }

        if (image.IsCompressed() && image.Decompress() != Error.Ok)
        {
            GD.PushError($"Could not decompress {data.Sprite.ResourcePath}. Import it with a lossless mode.");
            return null;
        }

        int width = image.GetWidth();
        int height = image.GetHeight();
        var targets = new int[width * height];
        var lookup = new Dictionary<uint, int>();
        var palette = new List<Color>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = image.GetPixel(x, y);
                int index = y * width + x;

                if (pixel.A < data.AlphaThreshold)
                {
                    targets[index] = LevelGrid.Empty;
                    continue;
                }

                // Key on RGB only, so partial transparency in the source does not split a colour in two.
                var opaque = new Color(pixel.R, pixel.G, pixel.B);
                uint key = opaque.ToRgba32();

                if (!lookup.TryGetValue(key, out int paletteIndex))
                {
                    paletteIndex = palette.Count;
                    lookup[key] = paletteIndex;
                    palette.Add(opaque);
                }

                targets[index] = paletteIndex;
            }
        }

        return new LoadedLevel(new LevelGrid(width, height, targets), palette.ToArray());
    }
}
