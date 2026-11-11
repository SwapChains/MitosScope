using System;
using System.Text.RegularExpressions;
using Avalonia.Media;
using MitosScope.App.Services;
using MitosScope.Core.Api;

namespace MitosScope.App.ViewModels;

/// <summary>One equipped item as shown in the Equipment panel (immutable; rebuilt on each inventory poll).</summary>
public sealed partial class EquippedItemViewModel(InventoryItem item)
{
    public InventoryItem Item { get; } = item;
    public string Name { get; } = CleanName(item.Name);

    /// <summary>Drops rarity tags like "* SPECIAL *", "* LIMITED *", "* SUPER LIMITED *".</summary>
    private static string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";
        var clean = RarityTag().Replace(name, "").Trim();
        return clean.Length > 0 ? clean : name.Trim();
    }

    [GeneratedRegex(@"\s*\*[^*]*\*")]
    private static partial Regex RarityTag();
    public IImage? Sprite { get; } = ItemSprites.Get(item.Sprite);
    public int Slot => Item.Slot;

    /// <summary>
    /// For consumables the API's "durability" looks like the quantity owned (e.g. 11252/3), so
    /// they get a quantity instead of a durability bar.
    /// </summary>
    public bool IsConsumable { get; } = string.Equals(item.Type, "consumable", StringComparison.OrdinalIgnoreCase);
    public bool HasDurability => !IsConsumable && Item.MaxDurability > 0;

    public double DurabilityFraction => HasDurability ? Math.Clamp((double)Item.Durability / Item.MaxDurability, 0, 1) : 0;

    public string DetailText => IsConsumable
        ? $"x{Item.Durability:N0}"
        : HasDurability ? $"{Item.Durability}/{Item.MaxDurability}" : "";

    public bool IsLow => HasDurability && DurabilityFraction <= 0.25;
    public bool IsMedium => HasDurability && DurabilityFraction is > 0.25 and <= 0.5;
}
