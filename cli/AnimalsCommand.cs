using System.Globalization;
using System.Text.Json;
using FeeEditor.Core;

namespace FeeEditor.Cli;

internal static class AnimalsCommand
{
    public static int Run(string[] args)
    {
        if (args is ["list", var source, .. var options])
        {
            string language = "en";
            var remaining = options.Where(option => option != "--json").ToArray();
            if (remaining is ["--language", var code] && LanguageCatalog.Codes.Contains(code)) language = code;
            else if (remaining.Length != 0) throw new ArgumentException("Choose a supported --language code.");
            Console.WriteLine(JsonSerializer.Serialize(EngageSave.Load(source).ReadAnimals().Select(row => new
            {
                row.Definition.Id, Name = row.Definition.Name(language), row.Amount, Maximum = AnimalCatalog.Maximum
            }), new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (args is ["set", var input, var output, "--animal", var id, "--amount", var value]
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
        {
            EngageSave.Load(input).WithAnimal(id, amount).WriteCopy(output);
            return 0;
        }
        if (args is ["max", var inputAll, var outputAll, "--all"])
        {
            EngageSave.Load(inputAll).MaximizeAnimals().WriteCopy(outputAll);
            return 0;
        }
        if (args is ["max", var inputOne, var outputOne, "--animal", var animal])
        {
            EngageSave.Load(inputOne).WithAnimal(animal, AnimalCatalog.Maximum).WriteCopy(outputOne);
            return 0;
        }
        throw new ArgumentException("Use animals list, set --animal <id> --amount <0-99>, or max --all|--animal <id>.");
    }
}
