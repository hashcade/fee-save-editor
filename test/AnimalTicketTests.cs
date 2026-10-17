using System.Buffers.Binary;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FeeEditor.Core;
using FeeEditor.Gui;
using FeeEditor.Gui.Localization;

internal static class AnimalTicketTests
{
    public static void Run(MainWindow window, string[] options)
    {
        var game = EngageSave.Parse(MainTests.Fixture());
        Check(AnimalCatalog.Animals.Count == 24 && game.ReadAnimals().All(row => row.Amount == 0), "Incomplete animal catalog.");
        Check(game.WithAnimal("EleosGoat", 0).Serialize().AsSpan().SequenceEqual(game.Serialize()), "Missing zero animal inserted a variable.");
        var full = game.MaximizeAnimals();
        Check(full.ReadAnimals().All(row => row.Amount == 99), "Animal maximum did not round-trip.");
        Check(full.MaximizeAnimals().Serialize().AsSpan().SequenceEqual(full.Serialize()), "Animal maximum is not idempotent.");
        PreserveSections(game, full, "USER");
        Check(game.ReadGameVariables().All(value => full.ReadGameVariables().Contains(value)), "Unrelated variables changed.");
        foreach (var animal in AnimalCatalog.Animals)
        {
            Check(full.WithAnimal(animal.Id, 0).ReadAnimals().Single(row => row.Definition.Id == animal.Id).Amount == 0, "Animal zero failed.");
            Reject(() => game.WithAnimal(animal.Id, -1));
            Reject(() => game.WithAnimal(animal.Id, 100));
            Check(LanguageCatalog.Codes.All(code => !string.IsNullOrWhiteSpace(animal.Name(code))), "Missing localized animal name.");
        }
        Reject(() => game.WithAnimal("unknown", 1));
        Reject(() => EngageSave.Parse(MainTests.Fixture(userVersion: 21)).MaximizeAnimals());
        var global = EngageSave.Parse(GlobalFixture());
        Reject(() => global.ReadAnimals());
        Reject(() => global.MaximizeAnimals());
        Reject(() => game.ReadRelayTickets());
        Check(global.ReadRelayTickets().Select(row => row.Amount).SequenceEqual([32, 7]), "Ticket layout is incorrect.");
        foreach (int amount in new[] { 0, 999 })
        {
            var edited = global.WithRelayTickets(1, amount);
            Check(edited.ReadRelayTickets()[0].Amount == amount && edited.ReadRelayTickets()[1] == global.ReadRelayTickets()[1], "Ticket edit crossed campaigns.");
            Check(edited.ReadRelayTickets()[0].LastTime == 1727932228, "Ticket timestamp changed.");
            PreserveSections(global, edited, "RLGL");
            ExactTicketChanges(global, edited, 0);
            Check(edited.WithRelayTickets(1, amount).Serialize().AsSpan().SequenceEqual(edited.Serialize()), "Ticket update is not idempotent.");
        }
        Reject(() => global.WithRelayTickets(1, -1));
        Reject(() => global.WithRelayTickets(1, 1000));
        Reject(() => global.WithRelayTickets(3, 1));
        foreach (var (offset, value) in new[] { (0, 2u), (36, 3u), (48, uint.MaxValue), (60, 1u) })
        {
            byte[] malformed = GlobalFixture();
            int start = global.Sections.Single(section => section.Name == "RLGL").PayloadOffset;
            Write32(malformed, start + offset, value);
            Seal(malformed);
            Reject(() => EngageSave.Parse(malformed).ReadRelayTickets());
        }
        string temporary = Path.Combine(Path.GetTempPath(), $"fee-animals-tickets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string gamePath = Path.Combine(temporary, "Manual0"), globalPath = Path.Combine(temporary, "Global");
            full.WriteCopy(gamePath); global.WriteCopy(globalPath);
            Check(window.LoadSave(gamePath) && window.CanEditAnimals, "Animal GUI did not load.");
            window.ShowAnimals();
            var list = window.FindControl<ListBox>("AnimalList")!;
            var input = window.FindControl<NumericUpDown>("AnimalAmountInput")!;
            Check(list.ItemCount == 24 && input.Maximum == 99 && window.FindControl<Grid>("AnimalsPanel")!.IsVisible, "Animal page is incomplete.");
            input.Text = "43";
            list.SelectedIndex = 1;
            Check(window.Save!.ReadAnimals()[0].Amount == 43, "Changing animal selection discarded a pending edit.");
            input.Text = "100";
            int previous = list.SelectedIndex;
            list.SelectedIndex = 2;
            Check(list.SelectedIndex == previous && window.Save.ReadAnimals()[1].Amount == 99, "Invalid animal input escaped validation.");
            input.Text = "12";
            string gameCopy = Path.Combine(temporary, "game-copy");
            Check(window.SaveCopy(gameCopy) && EngageSave.Load(gameCopy).ReadAnimals()[1].Amount == 12, "Save Copy discarded animal edits.");
            window.FindControl<TextBox>("AnimalSearch")!.Text = "Goat";
            Check(window.MaximizeAllAnimals() && window.Save.ReadAnimals().All(row => row.Amount == 99), "Search restricted the all-animal operation.");
            foreach (string code in LanguageCatalog.Codes)
            {
                window.SetLanguage(code);
                LanguageTests.CheckNavigation(window);
                Check(UiLanguage.Read(code).Keys.Order().SequenceEqual(UiLanguage.Read("en").Keys.Order()), "Language keys differ.");
            }
            window.SetLanguage("en");
            window.ShowItems();
            Check(!window.FindControl<Grid>("AnimalsPanel")!.IsVisible, "Animal page remained visible after navigation.");
            Check(window.LoadSave(globalPath) && window.CanEditTickets && !window.CanEditAnimals && !window.CanEditMain, "Global capabilities are incorrect.");
            Check(window.FindControl<Grid>("MainPanel")!.IsVisible && window.FindControl<Control>("RelayTicketsCard")!.IsVisible
                && !window.FindControl<Control>("GameMainLeft")!.IsVisible, "Global did not open the ticket editor.");
            var ticketInput = window.FindControl<NumericUpDown>("TicketAmountInput")!;
            var campaigns = window.FindControl<ComboBox>("TicketCampaignInput")!;
            Check(ticketInput.Maximum == 999 && campaigns.ItemCount == 2, "Ticket controls have wrong limits.");
            ticketInput.Text = "1000";
            campaigns.SelectedIndex = 1;
            Check(campaigns.SelectedIndex == 0, "Invalid ticket input was discarded on campaign switch.");
            ticketInput.Text = "999";
            campaigns.SelectedIndex = 1;
            Check(window.Save!.ReadRelayTickets()[0].Amount == 999 && ticketInput.Value == 7, "Ticket campaign switch discarded or crossed edits.");
            ticketInput.Text = "8";
            string globalCopy = Path.Combine(temporary, "global-copy");
            Check(window.SaveCopy(globalCopy) && EngageSave.Load(globalCopy).ReadRelayTickets()[1].Amount == 8, "Save Copy discarded ticket edits.");
            Check(window.LoadSave(gamePath) && !window.CanEditTickets && !window.FindControl<Control>("RelayTicketsCard")!.IsVisible, "Global controls leaked into game saves.");
            if (options is [var realGame, var realGlobal, var screenshots])
            {
                var suppliedGame = EngageSave.Load(realGame);
                var suppliedGlobal = EngageSave.Load(realGlobal);
                var editedGame = suppliedGame.MaximizeAnimals();
                PreserveSections(suppliedGame, editedGame, "USER");
                var beforeVariables = suppliedGame.ReadGameVariables().Where(row => !AnimalCatalog.Animals.Any(animal => animal.Key == row.Key));
                Check(beforeVariables.All(value => editedGame.ReadGameVariables().Contains(value)), "A real-save unrelated variable changed.");
                var ticket = suppliedGlobal.ReadRelayTickets().Single();
                Check(BinaryPrimitives.ReadUInt64LittleEndian(suppliedGame.Serialize().AsSpan(32, 8)) == ticket.Identifier, "Global campaign does not match the supplied game summary.");
                ExactTicketChanges(suppliedGlobal, suppliedGlobal.WithRelayTickets(ticket.Identifier, 999), 0);
                Directory.CreateDirectory(screenshots);
                Check(window.LoadSave(realGame), "Cannot open supplied save.");
                window.SetLanguage("zh-Hans"); window.ShowAnimals();
                Capture(window, Path.Combine(screenshots, "animals.png"));
                Check(window.LoadSave(realGlobal), "Cannot open supplied Global.");
                Capture(window, Path.Combine(screenshots, "tickets.png"));
            }
        }
        finally
        {
            foreach (string path in Directory.EnumerateFiles(temporary)) File.Delete(path);
            Directory.Delete(temporary);
            window.SetLanguage("en");
        }
        Console.WriteLine("Animals / Relay Tickets: limits, catalog languages, malformed records, round trips, checksums, byte preservation and GUI draft/navigation tests passed.");
    }

    private static void ExactTicketChanges(EngageSave before, EngageSave after, int index)
    {
        byte[] original = before.Serialize(), edited = after.Serialize();
        int target = before.Sections.Single(section => section.Name == "RLGL").PayloadOffset + 48 + index * 20;
        Check(original.Length == edited.Length, "Ticket edit resized the save.");
        for (int offset = 0; offset < original.Length - 4; offset++)
            Check(original[offset] == edited[offset] || offset >= target && offset < target + 4, $"Ticket edit changed unrelated byte 0x{offset:X}.");
        EngageSave.Parse(edited);
    }

    private static void PreserveSections(EngageSave before, EngageSave after, string changed)
    {
        byte[] original = before.Serialize(), edited = after.Serialize();
        foreach (var section in before.Sections.Where(section => section.Name != changed))
        {
            var other = after.Sections.Single(row => row.Name == section.Name);
            Check(original.AsSpan(section.PayloadOffset, section.Length).SequenceEqual(edited.AsSpan(other.PayloadOffset, other.Length)), $"Unrelated {section.Name} section changed.");
        }
        EngageSave.Parse(edited);
    }

    private static byte[] GlobalFixture()
    {
        using var payload = new MemoryStream();
        using var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true);
        writer.Write(1u); writer.Write(0xcdcdcdcdu); writer.Write(Enumerable.Range(0, 24).Select(value => (byte)value).ToArray());
        writer.Write(1u); writer.Write(2u);
        writer.Write(1ul); writer.Write(32u); writer.Write(1727932228L);
        writer.Write(2ul); writer.Write(7u); writer.Write(1727932230L);
        byte[] rlgl = payload.ToArray();
        byte[] result = new byte[132 + 8 + rlgl.Length + 8 + 4 + 8];
        "EDNI"u8.CopyTo(result);
        Write32(result, 4, 132); Write32(result, 8, (uint)(140 + rlgl.Length)); Write32(result, 12, (uint)(result.Length - 8));
        "LGLR"u8.CopyTo(result.AsSpan(132)); Write32(result, 136, (uint)rlgl.Length + 4); rlgl.CopyTo(result, 140);
        int opaque = 140 + rlgl.Length;
        "LGSU"u8.CopyTo(result.AsSpan(opaque)); Write32(result, opaque + 4, 8); Write32(result, opaque + 8, 0x12345678);
        "LVRC"u8.CopyTo(result.AsSpan(result.Length - 8)); Seal(result);
        return result;
    }

    private static void Capture(MainWindow window, string path)
    {
        window.Width = 1232; window.Height = 880;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 650)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
        frame.Save(path, PngBitmapEncoderOptions.Default);
    }
    private static void Write32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void Seal(byte[] bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes.AsSpan(0, bytes.Length - 4))
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320;
        }
        Write32(bytes, bytes.Length - 4, ~crc);
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid animal/ticket input was accepted.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
