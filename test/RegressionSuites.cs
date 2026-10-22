using System.Diagnostics;
using FeeEditor.Gui;

internal static class RegressionSuites
{
    internal static readonly string[] GuiGroups = ["main", "somniel", "items", "roster", "emblems", "languages"];

    private static readonly (string Name, Action Run)[] CoreCases =
    [
        (nameof(MinigameTests), MinigameTests.Run),
        (nameof(DonationTests), DonationTests.Run),
        (nameof(AchievementTests), AchievementTests.Run),
        (nameof(QuantityItemTests), QuantityItemTests.Run),
        (nameof(RosterStatTests), RosterStatTests.Run),
        (nameof(EmblemTests), EmblemTests.Run),
        (nameof(BondRingTests), BondRingTests.Run),
        (nameof(SupportTests), SupportTests.Run),
        (nameof(EngravingTests), EngravingTests.Run)
    ];

    private static (string Name, Action<MainWindow, string> Run)[] GuiCases(string group) => group switch
    {
        "main" =>
        [
            (nameof(MainTests), MainTests.Run),
            (nameof(PlayTimeTests), PlayTimeTests.Run),
            (nameof(ProtagonistNameTests), ProtagonistNameTests.Run),
            (nameof(AmiiboTests), (window, _) => AmiiboTests.Run(window)),
            (nameof(ActivityConditionTests), ActivityConditionTests.Run)
        ],
        "somniel" =>
        [
            (nameof(MinigameGuiTests), MinigameGuiTests.Run),
            (nameof(AchievementGuiTests), AchievementGuiTests.Run),
            (nameof(DonationGuiTests), DonationGuiTests.Run),
            (nameof(AnimalTicketTests), (window, _) => AnimalTicketTests.Run(window, []))
        ],
        "items" =>
        [
            (nameof(InventoryTests), InventoryTests.Run),
            (nameof(QuantityItemGuiTests), QuantityItemGuiTests.Run),
            (nameof(EngravingGuiTests), EngravingGuiTests.Run)
        ],
        "roster" =>
        [
            (nameof(RosterTests), RosterTests.Run),
            (nameof(RosterClassTests), RosterClassTests.Run),
            (nameof(RosterSkillTests), RosterSkillTests.Run),
            (nameof(RosterTransferTests), RosterTransferTests.Run),
            (nameof(RosterEquipmentTests), RosterEquipmentTests.Run),
            (nameof(RosterRecoveryTests), RosterRecoveryTests.Run),
            (nameof(AdvancedEditingTests), AdvancedEditingTests.Run),
            (nameof(RosterStatGuiTests), RosterStatGuiTests.Run)
        ],
        "emblems" =>
        [
            (nameof(EmblemGuiTests), EmblemGuiTests.Run),
            (nameof(EmblemCreationTests), EmblemCreationTests.Run),
            (nameof(BondRingGuiTests), BondRingGuiTests.Run),
            (nameof(BondRingEffectsTests), BondRingEffectsTests.Run),
            (nameof(EmblemNavigationTests), EmblemNavigationTests.Run),
            (nameof(SupportGuiTests), SupportGuiTests.Run)
        ],
        "languages" => [(nameof(LanguageTests), LanguageTests.Run)],
        _ => throw new ArgumentException($"Unknown GUI suite: {group}")
    };

    public static void RunCore()
    {
        foreach (var test in CoreCases) Timed(test.Name, test.Run);
    }

    public static void RunGui(string group, MainWindow window, string temporary)
    {
        foreach (var test in GuiCases(group)) Timed(test.Name, () => test.Run(window, temporary));
    }

    public static void RunAll(MainWindow window, string temporary)
    {
        RunCore();
        foreach (string group in GuiGroups) RunGui(group, window, temporary);
    }

    public static void Timed(string name, Action run)
    {
        var timer = Stopwatch.StartNew();
        Console.WriteLine($"START {name}");
        try { run(); }
        finally { Console.WriteLine($"TIMING {name}: {timer.Elapsed.TotalSeconds:F3}s"); }
    }

    public static void CheckCoverage()
    {
        string[] expected =
        [
            "MainTests", "PlayTimeTests", "ProtagonistNameTests", "AmiiboTests", "ActivityConditionTests",
            "MinigameTests", "MinigameGuiTests", "DonationTests", "AchievementTests", "AchievementGuiTests",
            "DonationGuiTests", "InventoryTests", "QuantityItemTests", "AnimalTicketTests", "QuantityItemGuiTests",
            "RosterTests", "RosterClassTests", "RosterSkillTests", "RosterTransferTests", "RosterEquipmentTests",
            "RosterRecoveryTests", "AdvancedEditingTests", "RosterStatTests", "RosterStatGuiTests", "EmblemTests",
            "BondRingTests", "EmblemGuiTests", "EmblemCreationTests", "BondRingGuiTests", "BondRingEffectsTests",
            "EmblemNavigationTests", "SupportTests", "SupportGuiTests", "EngravingTests", "EngravingGuiTests", "LanguageTests"
        ];
        string[] actual = [.. CoreCases.Select(test => test.Name), .. GuiGroups.SelectMany(group => GuiCases(group).Select(test => test.Name))];
        if (!actual.Order().SequenceEqual(expected.Order()))
            throw new InvalidOperationException("Regression groups must cover every existing case exactly once.");
        Console.WriteLine($"Regression coverage: {actual.Length} cases assigned exactly once; shell checks run separately.");
    }
}
