using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MitosScope.Core.Atlas;

namespace MitosScope.App.Services;

/// <summary>
/// Item icons from the game's "items" texture atlas (items.png + items.xml, embedded from the
/// repo root). Each sprite is rendered once into its own untrimmed frame and cached.
/// Use on the UI thread.
/// </summary>
public static class ItemSprites
{
    private const string AtlasBase = "avares://MitosScope.App/Assets/Atlas/";

    private static readonly Dictionary<string, IImage?> Cache = new(StringComparer.Ordinal);
    private static Bitmap? _image;
    private static TextureAtlas? _atlas;
    private static bool _loadFailed;

    public static IImage? Get(string? spriteName)
    {
        if (string.IsNullOrEmpty(spriteName) || !EnsureLoaded())
            return null;

        if (!Cache.TryGetValue(spriteName, out var sprite))
        {
            sprite = _atlas!.TryGet(spriteName, out var region) ? Render(region) : null;
            if (sprite is null)
                Trace.WriteLine($"[MitosScope] Sprite not found in atlas: {spriteName}");
            Cache[spriteName] = sprite;
        }
        return sprite;
    }

    private static IImage? Render(AtlasRegion r)
    {
        if (r.Width <= 0 || r.Height <= 0 || r.FrameWidth <= 0 || r.FrameHeight <= 0)
            return null;

        var target = new RenderTargetBitmap(new PixelSize(r.FrameWidth, r.FrameHeight));
        using (var ctx = target.CreateDrawingContext())
        {
            ctx.DrawImage(_image!,
                new Rect(r.X, r.Y, r.Width, r.Height),
                new Rect(-r.FrameX, -r.FrameY, r.Width, r.Height));
        }
        return target;
    }

    private static bool EnsureLoaded()
    {
        if (_atlas is not null)
            return true;
        if (_loadFailed)
            return false;

        try
        {
            using (var xml = AssetLoader.Open(new Uri(AtlasBase + "items.xml")))
                _atlas = TextureAtlas.Load(xml);
            using (var png = AssetLoader.Open(new Uri(AtlasBase + "items.png")))
                _image = new Bitmap(png);
            return true;
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            _atlas = null;
            Trace.WriteLine($"[MitosScope] Failed to load the items atlas: {ex.Message}");
            return false;
        }
    }
}
