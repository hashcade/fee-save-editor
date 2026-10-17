using System.Collections.ObjectModel;
using System.Text.Json;

namespace FeeEditor.Core;

public sealed record AnimalDefinition(string Id, IReadOnlyDictionary<string, string> Names)
{
    public string Key => "G_動物_" + Id;
    public string Name(string language) => Names.GetValueOrDefault(language) ?? Names["en"];
}

public sealed record AdoptedAnimal(AnimalDefinition Definition, int Amount);

public static class AnimalCatalog
{
    public const int Maximum = 99;
    private sealed record CatalogData(AnimalDefinition[] Animals);
    public static IReadOnlyList<AnimalDefinition> Animals { get; } = Load();
    public static AnimalDefinition Get(string id) => Animals.FirstOrDefault(animal => animal.Id == id)
        ?? throw new ArgumentException($"Unknown animal: {id}", nameof(id));

    private static IReadOnlyList<AnimalDefinition> Load()
    {
        using var stream = typeof(AnimalCatalog).Assembly.GetManifestResourceStream("FeeEditor.Core.Data.animals.json")
            ?? throw new InvalidOperationException("The animal catalog is missing.");
        var data = JsonSerializer.Deserialize<CatalogData>(stream) ?? throw new InvalidDataException("Invalid animal catalog.");
        if (data.Animals.Length != 24 || data.Animals.Select(row => row.Id).Distinct().Count() != 24
            || data.Animals.Any(row => string.IsNullOrWhiteSpace(row.Id) || LanguageCatalog.Codes.Any(language =>
                !row.Names.TryGetValue(language, out string? name) || string.IsNullOrWhiteSpace(name))))
            throw new InvalidDataException("Invalid animal identities or localized names.");
        return Array.AsReadOnly(data.Animals.Select(row => row with
        {
            Names = new ReadOnlyDictionary<string, string>(row.Names.ToDictionary(pair => pair.Key, pair => pair.Value))
        }).ToArray());
    }
}

public sealed partial class EngageSave
{
    public IReadOnlyList<AdoptedAnimal> ReadAnimals()
    {
        var layout = GameVariableLayout.Read(this, _bytes);
        return AnimalCatalog.Animals.Select(definition =>
        {
            int amount = layout.IntegerOffset(definition.Key) is int offset ? unchecked((int)ReadUInt32(_bytes, offset)) : 0;
            if (amount < 0) throw new InvalidDataException($"Negative animal count for {definition.Id}.");
            return new AdoptedAnimal(definition, amount);
        }).ToArray();
    }

    public EngageSave WithAnimal(string animalId, int amount)
    {
        if (amount is < 0 or > AnimalCatalog.Maximum)
            throw new ArgumentOutOfRangeException(nameof(amount), "Animal count must be 0–99.");
        return WithGameIntegerValues(new Dictionary<string, int> { [AnimalCatalog.Get(animalId).Key] = amount });
    }

    public EngageSave MaximizeAnimals() => WithGameIntegerValues(AnimalCatalog.Animals.ToDictionary(row => row.Key, _ => AnimalCatalog.Maximum));
}
