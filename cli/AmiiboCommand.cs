using System.Text.Json;
using FeeEditor.Core;

namespace FeeEditor.Cli;

internal static class AmiiboCommand
{
    public static int Run(string[] args)
    {
        if (args is ["list", var source, .. var options])
        {
            string language = "en";
            var remaining = options.Where(option => option != "--json").ToArray();
            if (remaining is ["--language", var code] && LanguageCatalog.Codes.Contains(code)) language = code;
            else if (remaining.Length != 0) throw new ArgumentException("Choose a supported --language code.");
            var rewards = EngageSave.Load(source).ReadAmiiboRewards();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Groups = Enum.GetValues<AmiiboRewardGroup>().Select(group => new
                {
                    Group = group.ToString(), Unlocked = rewards.Count(row => row.Definition.Group == group && row.Unlocked), Total = 12
                }),
                Rewards = rewards.Select(row => new
                {
                    row.Definition.Key, Group = row.Definition.Group.ToString(), Name = row.Definition.Name(language), row.Unlocked, row.Value
                })
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (args is ["unlock", var input, var output, .. var selection])
        {
            var save = EngageSave.Load(input);
            var edited = selection switch
            {
                ["--all"] => save.UnlockAllAmiiboRewards(),
                ["--group", "outfits"] => save.UnlockAllAmiiboRewards(AmiiboRewardGroup.Outfits),
                ["--group", "music"] => save.UnlockAllAmiiboRewards(AmiiboRewardGroup.Music),
                _ => save.UnlockAmiiboRewards(SelectedKeys(selection))
            };
            edited.WriteCopy(output);
            return 0;
        }
        throw new ArgumentException("Use amiibo list or unlock <save> <new-file> --all|--group outfits|music|--key <key> [--key <key> ...].");
    }

    private static string[] SelectedKeys(string[] options)
    {
        if (options.Length == 0 || options.Length % 2 != 0)
            throw new ArgumentException("Select --all, --group outfits|music, or one or more --key values.");
        for (int index = 0; index < options.Length; index += 2)
            if (options[index] != "--key") throw new ArgumentException("Only --key can be repeated for selected rewards.");
        return options.Where((_, index) => index % 2 == 1).ToArray();
    }
}
