using System.Buffers.Binary;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FeeEditor.Core;
using FeeEditor.Gui;
using FeeEditor.Gui.Localization;

internal static class AmiiboTests
{
    public static void Run(MainWindow window, string? source = null, string? screenshots = null)
    {
        var rewards = AmiiboRewardCatalog.Rewards;
        Check(rewards.Count == 24 && rewards.Select(row => row.Key).Distinct().Count() == 24, "Wrong amiibo whitelist.");
        Check(rewards.Take(12).All(row => row.Group == AmiiboRewardGroup.Outfits)
            && rewards.Skip(12).Select(row => row.Key).SequenceEqual(Enumerable.Range(3, 12).Select(i => $"G_BGM_Amiibo_{i:00}_ST_Play")),
            "Wrong amiibo groups or music boundaries.");
        var missing = EngageSave.Parse(MainTests.Fixture());
        Check(missing.ReadAmiiboRewards().All(row => !row.Unlocked), "Missing keys must read as locked.");
        var empty = Append(missing, rewards.Select(row => (row.Key, (object)0)).Concat(new[]
        {
            ("G_衣装既読_AID_英雄王の衣装", (object)0), ("G_Amiibo_scan_count", (object)5),
            ("G_Amiibo_last_scan", (object)"keep this timestamp"), ("G_所持_AID_unrelated", (object)7)
        }));
        var partial = empty.UnlockAmiiboRewards([rewards[0].Key, rewards[12].Key]);
        foreach (var save in new[] { missing, empty, partial })
        {
            var full = save.UnlockAllAmiiboRewards();
            Check(full.ReadAmiiboRewards().All(row => row.Value == 1), "Unlock All left locked rewards.");
            Check(full.ReadGameVariables().Count(row => rewards.Any(reward => reward.Key == row.Key)) == 24, "Missing/duplicate reward keys.");
            Check(ReferenceEquals(full, full.UnlockAllAmiiboRewards()), "Already-unlocked save must be a no-op.");
            Check(full.Serialize().SequenceEqual(full.UnlockAllAmiiboRewards().Serialize()), "Second unlock changed bytes.");
            Preserve(save, full);
            var outfits = save.UnlockAllAmiiboRewards(AmiiboRewardGroup.Outfits);
            Check(outfits.ReadAmiiboRewards().Where(row => row.Definition.Group == AmiiboRewardGroup.Outfits).All(row => row.Unlocked), "Outfit group failed.");
            Check(outfits.ReadAmiiboRewards().Where(row => row.Definition.Group == AmiiboRewardGroup.Music)
                .SequenceEqual(save.ReadAmiiboRewards().Where(row => row.Definition.Group == AmiiboRewardGroup.Music)), "Outfit unlock changed music.");
            Preserve(save, outfits);
        }
        Check(missing.UnlockAmiiboRewards([rewards[3].Key, rewards[3].Key]).ReadAmiiboRewards().Count(row => row.Unlocked) == 1,
            "Repeated selection created duplicate keys.");
        Check(ReferenceEquals(empty, empty.UnlockAmiiboRewards([])), "Empty selection changed data.");
        Reject(() => empty.UnlockAmiiboRewards([rewards[0].Key, "G_Amiibo_scan_count"]));
        Reject(() => empty.UnlockAllAmiiboRewards((AmiiboRewardGroup)42));
        Reject(() => EngageSave.Parse(MainTests.Fixture(userVersion: 21)).UnlockAllAmiiboRewards());
        foreach (object wrong in new object[] { 0, "not an integer" })
        {
            var invalid = Append(empty, [(rewards[0].Key, wrong)]);
            Reject(() => invalid.ReadAmiiboRewards());
            Reject(() => invalid.UnlockAllAmiiboRewards());
        }
        var noninteger = Append(missing, [(rewards[0].Key, (object)"not an integer")]);
        Reject(() => noninteger.ReadAmiiboRewards());
        Reject(() => noninteger.UnlockAllAmiiboRewards());
        byte[] sourceBytes = source is null ? partial.Serialize() : File.ReadAllBytes(source);
        var supplied = EngageSave.Parse(sourceBytes);
        var suppliedFull = supplied.UnlockAllAmiiboRewards();
        Preserve(supplied, suppliedFull);
        Check(suppliedFull.ReadAmiiboRewards().All(row => row.Value == 1), "Real save did not unlock all rewards.");
        Check(suppliedFull.Serialize().SequenceEqual(suppliedFull.UnlockAllAmiiboRewards().Serialize()), "Real save unlock is not idempotent.");
        string temporary = Path.Combine(Path.GetTempPath(), $"fee-amiibo-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);
        try
        {
            string fixture = Path.Combine(temporary, "partial");
            partial.WriteCopy(fixture);
            Check(window.LoadSave(fixture) && window.CanEditAmiibo, "Cannot load amiibo GUI.");
            window.ShowAmiibo();
            var panel = window.FindControl<Grid>("AmiiboPanel")!;
            var outfits = window.FindControl<StackPanel>("AmiiboOutfitRows")!;
            var music = window.FindControl<StackPanel>("AmiiboMusicRows")!;
            Check(panel.IsVisible && outfits.Children.Count == 12 && music.Children.Count == 12, "Amiibo cards are incomplete.");
            Check(outfits.Children.Concat(music.Children).Cast<Grid>().All(row => row.ColumnSpacing == 8),
                "Checkboxes and reward names must be eight pixels apart.");
            Check(window.FindControl<TextBlock>("AmiiboOutfitCount")!.Text == "1/12", "Wrong outfit count.");
            Check(outfits.GetLogicalDescendants().OfType<CheckBox>().First() is { IsEnabled: false, IsChecked: true },
                "Unlocked rewards must be checked and noninteractive.");
            Check(!window.FindControl<Button>("UnlockSelectedAmiiboOutfitsButton")!.IsEnabled, "Empty selection is actionable.");
            outfits.GetLogicalDescendants().OfType<CheckBox>().ElementAt(1).IsChecked = true;
            foreach (string language in LanguageCatalog.Codes)
            {
                window.SetLanguage(language);
                LanguageTests.CheckNavigation(window);
                Check(UiLanguage.Read(language).Keys.Order().SequenceEqual(UiLanguage.Read("en").Keys.Order()), "Language keys differ.");
                Check(outfits.GetLogicalDescendants().OfType<CheckBox>().ElementAt(1).IsChecked == true, "Language switch discarded selection.");
                Check(rewards.All(row => !string.IsNullOrWhiteSpace(row.Name(language))), "Missing reward translation.");
            }
            Check(window.UnlockAmiiboRewards(AmiiboRewardGroup.Outfits, all: false), "Selected unlock failed.");
            Check(window.Save!.ReadAmiiboRewards().Count(row => row.Unlocked) == 3, "Selected unlock crossed selection/groups.");
            Check(!window.FindControl<Button>("UnlockSelectedAmiiboOutfitsButton")!.IsEnabled, "Applied selection was not cleared.");
            Check(window.UnlockAmiiboRewards(AmiiboRewardGroup.Music, all: true), "Group unlock failed.");
            Check(window.Save.ReadAmiiboRewards().Count(row => row.Unlocked) == 14, "Music unlock changed outfits.");
            string saved = Path.Combine(temporary, "saved");
            Check(window.SaveCopy(saved), "File/Save Copy failed after amiibo edits.");
            Check(EngageSave.Load(saved).ReadAmiiboRewards().Count(row => row.Unlocked) == 14, "Saved unlocks differ.");
            Preserve(partial, EngageSave.Load(saved));
            window.ShowItems();
            Check(!panel.IsVisible, "Amiibo page leaked into another page.");
            string malformed = Path.Combine(temporary, "malformed");
            noninteger.WriteCopy(malformed);
            Check(window.LoadSave(malformed) && !window.CanEditAmiibo, "Malformed rewards enabled unlock controls.");
            Check(!window.UnlockAmiiboRewards(AmiiboRewardGroup.Outfits, true), "Malformed GUI was editable.");
            Check(window.LoadSave(fixture), "Cannot restore the valid fixture.");
            window.ShowAmiibo();
            if (screenshots is not null)
            {
                Directory.CreateDirectory(screenshots);
                window.SetLanguage("zh-Hans");
                outfits.GetLogicalDescendants().OfType<CheckBox>().ElementAt(1).IsChecked = true;
                music.GetLogicalDescendants().OfType<CheckBox>().ElementAt(1).IsChecked = true;
                Capture(window, Path.Combine(screenshots, "amiibo-selection-demo.png"));
            }
            var navigation = window.FindControl<TabStrip>("MainNavigation")!;
            navigation.SelectedIndex = 9;
            Check(window.FindControl<Grid>("InspectorPanel")!.IsVisible && !panel.IsVisible, "Save Inspector navigation is broken.");
            if (source is not null && screenshots is not null)
            {
                Directory.CreateDirectory(screenshots);
                Check(window.LoadSave(source), "Cannot load supplied save.");
                window.SetLanguage("zh-Hans"); window.ShowAmiibo();
                Capture(window, Path.Combine(screenshots, "amiibo-before.png"));
                Check(window.UnlockAmiiboRewards(AmiiboRewardGroup.Outfits, true)
                    && window.UnlockAmiiboRewards(AmiiboRewardGroup.Music, true), "GUI real-save unlock failed.");
                Capture(window, Path.Combine(screenshots, "amiibo-unlocked.png"));
                Preserve(supplied, window.Save!);
                Check(File.ReadAllBytes(source).SequenceEqual(sourceBytes), "Private source save was modified.");
            }
        }
        finally { Directory.Delete(temporary, recursive: true); }
        window.SetLanguage("en");
        Console.WriteLine("amiibo: whitelist, absent/zero/partial keys, idempotency, malformed records, CRC/sections, GUI selection/navigation, nine languages and source preservation passed.");
    }

    private static void Capture(MainWindow window, string path)
    {
        window.Width = 1232; window.Height = 880;
        for (int pass = 0; pass < 25; pass++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); Thread.Sleep(20);
        }
        var panel = window.FindControl<Grid>("AmiiboPanel")!;
        var header = window.FindControl<Grid>("Header")!;
        var layout = window.FindControl<Grid>("WindowLayout")!;
        Check(Math.Abs(panel.TranslatePoint(new Point(), layout)!.Value.Y - header.Bounds.Height - 24) < 1,
            "Amiibo page changed the standard header gap.");
        using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No amiibo frame.");
        frame.Save(path, PngBitmapEncoderOptions.Default);
    }

    private static void Preserve(EngageSave before, EngageSave after)
    {
        byte[] original = before.Serialize(), edited = after.Serialize();
        EngageSave.Parse(edited);
        Check(original.AsSpan(0, 128).SequenceEqual(edited.AsSpan(0, 128)), "Save-slot summary changed.");
        foreach (var section in before.Sections.Where(row => row.Name != "USER"))
        {
            var other = after.Sections.Single(row => row.Name == section.Name);
            Check(original.AsSpan(section.PayloadOffset, section.Length).SequenceEqual(edited.AsSpan(other.PayloadOffset, other.Length)),
                $"Unrelated {section.Name} changed.");
        }
        var old = before.Sections.Single(row => row.Name == "USER");
        var updated = after.Sections.Single(row => row.Name == "USER");
        int oldVariables = old.PayloadOffset + 49, newVariables = updated.PayloadOffset + 49;
        int oldEnd = oldVariables + (int)Read32(original, oldVariables), newEnd = newVariables + (int)Read32(edited, newVariables);
        Check(original.AsSpan(old.PayloadOffset, 49).SequenceEqual(edited.AsSpan(updated.PayloadOffset, 49))
            && original.AsSpan(oldEnd, old.PayloadOffset + old.Length - oldEnd)
                .SequenceEqual(edited.AsSpan(newEnd, updated.PayloadOffset + updated.Length - newEnd)), "Unrelated USER data changed.");
        Check(original.AsSpan(oldVariables + 4, 32).SequenceEqual(edited.AsSpan(newVariables + 4, 32)), "Variable block metadata changed.");
        var oldRecords = Records(original, oldVariables);
        var newRecords = Records(edited, newVariables);
        var whitelist = AmiiboRewardCatalog.Rewards.Select(row => row.Key).ToHashSet();
        Check(oldRecords.Select(row => row.Key).SequenceEqual(newRecords.Take(oldRecords.Count).Select(row => row.Key)), "Existing records reordered.");
        foreach (var record in oldRecords.Where(row => !whitelist.Contains(row.Key)))
            Check(record.Bytes.SequenceEqual(newRecords.Single(row => row.Key == record.Key).Bytes), $"Unrelated variable {record.Key} changed.");
        Check(newRecords.Skip(oldRecords.Count).All(row => whitelist.Contains(row.Key)), "Added non-whitelisted keys.");
    }

    private static List<(string Key, byte[] Bytes)> Records(byte[] bytes, int start)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.Unicode);
        reader.BaseStream.Position = start + 40;
        var records = new List<(string, byte[])>();
        for (int index = 0; index < Read32(bytes, start + 36); index++)
        {
            int begin = (int)reader.BaseStream.Position;
            string key = Encoding.Unicode.GetString(reader.ReadBytes(reader.ReadInt32()));
            byte type = reader.ReadByte();
            if (type == 0) reader.ReadInt32();
            else reader.ReadBytes(reader.ReadInt32());
            records.Add((key, bytes[begin..(int)reader.BaseStream.Position]));
        }
        return records;
    }

    private static EngageSave Append(EngageSave save, IEnumerable<(string Key, object Value)> values)
    {
        byte[] bytes = save.Serialize();
        var user = save.Sections.Single(row => row.Name == "USER");
        int start = user.PayloadOffset + 49, end = start + (int)Read32(bytes, start);
        using var records = new MemoryStream();
        using var writer = new BinaryWriter(records, Encoding.UTF8, leaveOpen: true);
        int count = 0;
        foreach (var (key, value) in values)
        {
            byte[] name = Encoding.Unicode.GetBytes(key);
            writer.Write(name.Length); writer.Write(name);
            writer.Write((byte)(value is int ? 0 : 1));
            if (value is int integer) writer.Write(integer);
            else { byte[] text = Encoding.Unicode.GetBytes((string)value); writer.Write(text.Length); writer.Write(text); }
            count++;
        }
        byte[] addition = records.ToArray();
        byte[] result = [.. bytes.AsSpan(0, end), .. addition, .. bytes.AsSpan(end)];
        Write32(result, start, Read32(bytes, start) + (uint)addition.Length);
        Write32(result, start + 36, Read32(bytes, start + 36) + (uint)count);
        Write32(result, user.Offset + 4, Read32(bytes, user.Offset + 4) + (uint)addition.Length);
        for (int index = 0; index < 32; index++)
        {
            int slot = 132 + index * 4;
            uint offset = Read32(bytes, slot);
            if (offset > user.Offset) Write32(result, slot, offset + (uint)addition.Length);
        }
        uint crc = uint.MaxValue;
        foreach (byte value in result.AsSpan(0, result.Length - 4))
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320;
        }
        Write32(result, result.Length - 4, ~crc);
        return EngageSave.Parse(result);
    }

    private static uint Read32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static void Write32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or ArgumentException) { return; }
        throw new InvalidOperationException("Invalid amiibo operation was accepted.");
    }
}
