using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeeEditor.Core;

[JsonConverter(typeof(JsonStringEnumConverter<AmiiboRewardGroup>))]
public enum AmiiboRewardGroup { Outfits, Music }

public sealed record AmiiboRewardDefinition(string Key, AmiiboRewardGroup Group, IReadOnlyDictionary<string, string> Names)
{
    public string Name(string language) => Names.GetValueOrDefault(language) ?? Names["en"];
}

public sealed record AmiiboReward(AmiiboRewardDefinition Definition, int Value)
{
    public bool Unlocked => Value == 1;
}

public static class AmiiboRewardCatalog
{
    private static readonly string[] OutfitKeys =
    [
        "G_所持_AID_英雄王の衣装", "G_所持_AID_慈愛の王女の衣装", "G_所持_AID_聖騎士の衣装",
        "G_所持_AID_賢王の衣装", "G_所持_AID_若き獅子の衣装", "G_所持_AID_草原の公女の衣装",
        "G_所持_AID_碧き風空の衣装", "G_所持_AID_蒼炎の勇者の衣装", "G_所持_AID_暁の巫女の衣装",
        "G_所持_AID_聖王女の衣装", "G_所持_AID_未来を選びし者の衣装", "G_所持_AID_導き手の衣装"
    ];
    private sealed record CatalogData(AmiiboRewardDefinition[] Rewards);
    public static IReadOnlyList<AmiiboRewardDefinition> Rewards { get; } = Load();

    public static AmiiboRewardDefinition Get(string key) => Rewards.FirstOrDefault(row => row.Key == key)
        ?? throw new ArgumentException($"Unknown amiibo reward: {key}", nameof(key));

    private static IReadOnlyList<AmiiboRewardDefinition> Load()
    {
        using var stream = typeof(AmiiboRewardCatalog).Assembly.GetManifestResourceStream("FeeEditor.Core.Data.amiibo.json")
            ?? throw new InvalidOperationException("The amiibo catalog is missing.");
        var data = JsonSerializer.Deserialize<CatalogData>(stream) ?? throw new InvalidDataException("Invalid amiibo catalog.");
        string[] expected = [.. OutfitKeys, .. Enumerable.Range(3, 12).Select(index => $"G_BGM_Amiibo_{index:00}_ST_Play")];
        if (!data.Rewards.Select(row => row.Key).SequenceEqual(expected)
            || data.Rewards.Where((row, index) => row.Group != (index < 12 ? AmiiboRewardGroup.Outfits : AmiiboRewardGroup.Music)).Any()
            || data.Rewards.Any(row => LanguageCatalog.Codes.Any(language =>
                !row.Names.TryGetValue(language, out string? name) || string.IsNullOrWhiteSpace(name))))
            throw new InvalidDataException("Invalid amiibo whitelist or localized names.");
        return Array.AsReadOnly(data.Rewards.Select(row => row with
        {
            Names = new ReadOnlyDictionary<string, string>(row.Names.ToDictionary(pair => pair.Key, pair => pair.Value))
        }).ToArray());
    }
}

public sealed partial class EngageSave
{
    public IReadOnlyList<AmiiboReward> ReadAmiiboRewards()
    {
        var layout = GameVariableLayout.Read(this, _bytes);
        return AmiiboRewardCatalog.Rewards.Select(definition => new AmiiboReward(definition,
            layout.IntegerOffset(definition.Key) is int offset ? unchecked((int)ReadUInt32(_bytes, offset)) : 0)).ToArray();
    }

    public EngageSave UnlockAmiiboRewards(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        string[] selected = keys.Distinct(StringComparer.Ordinal).ToArray();
        foreach (string key in selected) AmiiboRewardCatalog.Get(key);
        ReadAmiiboRewards();
        var edited = WithGameIntegerValues(selected.ToDictionary(key => key, _ => 1, StringComparer.Ordinal));
        if (edited.ReadAmiiboRewards().Any(row => selected.Contains(row.Definition.Key) && !row.Unlocked))
            throw new InvalidDataException("The amiibo unlock did not survive serialization.");
        return edited;
    }

    public EngageSave UnlockAllAmiiboRewards(AmiiboRewardGroup? group = null)
    {
        if (group.HasValue && !Enum.IsDefined(group.Value)) throw new ArgumentOutOfRangeException(nameof(group));
        return UnlockAmiiboRewards(AmiiboRewardCatalog.Rewards.Where(row => group is null || row.Group == group)
            .Select(row => row.Key));
    }
}
