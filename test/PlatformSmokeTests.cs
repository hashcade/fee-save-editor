using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using FeeEditor.Core;
using FeeEditor.Gui;

internal static class PlatformSmokeTests
{
    public static void Run(MainWindow window)
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"fee-platform-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string path = Path.Combine(temporary, "source");
            byte[] original = AdvancedEditingTests.SafeFixture().Serialize();
            File.WriteAllBytes(path, original);
            if (window.Icon is null || !window.LoadSave(path) || !window.CanEditMain || !window.CanEditRoster)
                throw new InvalidOperationException("The platform could not initialize the editor or load its fixture.");
            var navigation = window.FindControl<TabStrip>("MainNavigation")!;
            string[] pages = ["MainPanel", "ItemsPanel", "RosterPanel", "EmblemPagesPanel", "SupportsPanel",
                "MinigamesPanel", "AchievementsPanel", "AnimalsPanel", "AmiiboPanel", "InspectorPanel"];
            window.Width = 1120; window.Height = 780;
            for (int index = 0; index < pages.Length; index++)
            {
                navigation.SelectedIndex = index;
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var frame = window.CaptureRenderedFrame();
                if (!window.FindControl<Control>(pages[index])!.IsVisible || frame is null || frame.PixelSize.Width == 0)
                    throw new InvalidOperationException($"The platform could not render {pages[index]}.");
            }
            window.SetLanguage("zh-Hans");
            navigation.SelectedIndex = 0;
            window.Width = 860; window.Height = 600;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var localized = window.CaptureRenderedFrame();
            if (localized is null || !window.Save!.Serialize().SequenceEqual(original))
                throw new InvalidOperationException("Localized rendering changed save data or failed.");
            string copy = Path.Combine(temporary, "copy");
            if (!window.SaveCopy(copy) || !File.ReadAllBytes(copy).SequenceEqual(original)
                || !File.ReadAllBytes(path).SequenceEqual(original))
                throw new InvalidOperationException("The platform could not make a lossless save copy.");
        }
        finally { Directory.Delete(temporary, recursive: true); }
        Console.WriteLine("Platform smoke: ten pages, English/Chinese rendering, two window sizes and lossless save copy passed.");
    }
}
