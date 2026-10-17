using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FeeEditor.Core;
using FeeEditor.Gui;
using FeeEditor.Gui.Localization;

internal static class GenderTests
{
    public static void Run(MainWindow window, string source, string screenshot)
    {
        byte[] original = File.ReadAllBytes(source);
        var save = EngageSave.Load(source);
        var initial = save.ReadProtagonistGender() ?? throw new Exception("No protagonist.");
        foreach (var gender in Enum.GetValues<ProtagonistGender>())
        {
            var edited = save.WithProtagonistGender(gender);
            Check(edited.ReadProtagonistGender() == gender, "Wrong gender.");
            byte[] bytes = edited.Serialize();
            Check(bytes.Length == original.Length, "Save size changed.");
            int changes = original[..^4].Zip(bytes[..^4]).Count(pair => pair.First != pair.Second);
            Check(changes == (gender == initial ? 0 : 1), "Changed data outside protagonist gender.");
            Check(EngageSave.Parse(bytes).ReadProtagonistGender() == gender, "Invalid checksum or round trip.");
            Check(edited.WithProtagonistGender(initial).Serialize().SequenceEqual(original), "Restoration was not byte exact.");
        }
        foreach (int invalid in new[] { -1, 0, 3, 255 })
        {
            try { save.WithProtagonistGender((ProtagonistGender)invalid); throw new Exception("Invalid gender accepted."); }
            catch (ArgumentOutOfRangeException) { }
        }
        foreach (string language in LanguageCatalog.Codes)
        {
            var strings = UiLanguage.Read(language);
            Check(new[] { "Gender", "Male", "Female" }.All(key => strings.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)),
                $"Missing gender translation: {language}");
        }
        var input = window.FindControl<ComboBox>("PlayerGenderInput")!;
        Check(!input.IsEnabled, "Gender is editable without a save.");
        Check(window.LoadSave(source), "GUI could not load save.");
        Check(input.IsEnabled && input.SelectedIndex == (int)initial - 1, "Main gender did not load.");
        foreach (var gender in new[] { initial == ProtagonistGender.Male ? ProtagonistGender.Female : ProtagonistGender.Male, initial })
        {
            input.SelectedIndex = (int)gender - 1;
            Check(window.ApplyMainValues(), "GUI rejected gender edit.");
            Check(window.Save!.ReadProtagonistGender() == gender, "GUI changed wrong gender.");
            int changes = original[..^4].Zip(window.Save.Serialize()[..^4]).Count(pair => pair.First != pair.Second);
            Check(changes == (gender == initial ? 0 : 1), "GUI changed unrelated data.");
        }
        window.SetLanguage("zh-Hans");
        Check(input.SelectedIndex == (int)initial - 1, "Language switch changed gender.");
        window.Width = 1232;
        window.Height = 880;
        for (int i = 0; i < 12; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(screenshot))!);
        using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No Main render.");
        frame.Save(screenshot, PngBitmapEncoderOptions.Default);
        Check(File.ReadAllBytes(source).SequenceEqual(original), "Private source save changed.");
        Console.WriteLine($"Gender core/GUI, exact byte preservation, input limits and localization passed. Screenshot: {screenshot}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
