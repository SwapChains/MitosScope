using System.Globalization;
using System.Xml.Linq;

namespace MitosScope.Core.Atlas;

/// <summary>
/// One sprite in a Starling/Sparrow texture atlas. (X, Y, Width, Height) is the trimmed region in
/// the atlas image; the frame describes the original, untrimmed sprite: draw the region at
/// (-FrameX, -FrameY) inside a FrameWidth × FrameHeight canvas to get the sprite as designed.
/// </summary>
public sealed record AtlasRegion(int X, int Y, int Width, int Height, int FrameX, int FrameY, int FrameWidth, int FrameHeight);

/// <summary>Parser for the game's atlas XML (&lt;TextureAtlas&gt;&lt;SubTexture name x y width height [frameX ...]/&gt;).</summary>
public sealed class TextureAtlas
{
    private readonly Dictionary<string, AtlasRegion> _regions;

    private TextureAtlas(Dictionary<string, AtlasRegion> regions) => _regions = regions;

    public int Count => _regions.Count;

    public bool TryGet(string name, out AtlasRegion region) => _regions.TryGetValue(name, out region!);

    public static TextureAtlas Load(Stream xml)
    {
        var regions = new Dictionary<string, AtlasRegion>(StringComparer.Ordinal);
        foreach (var e in XDocument.Load(xml).Root!.Elements("SubTexture"))
        {
            var name = (string?)e.Attribute("name");
            if (string.IsNullOrEmpty(name))
                continue;

            int Int(string attr, int fallback) =>
                int.TryParse((string?)e.Attribute(attr), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

            var w = Int("width", 0);
            var h = Int("height", 0);
            // Sprites without frame attributes weren't trimmed: the frame is the region itself.
            regions[name] = new AtlasRegion(Int("x", 0), Int("y", 0), w, h,
                Int("frameX", 0), Int("frameY", 0), Int("frameWidth", w), Int("frameHeight", h));
        }
        return new TextureAtlas(regions);
    }
}
