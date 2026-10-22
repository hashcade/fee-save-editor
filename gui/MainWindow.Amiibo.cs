using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using FeeEditor.Core;
using FeeEditor.Gui.Localization;

namespace FeeEditor.Gui;

public partial class MainWindow
{
    public bool CanEditAmiibo { get; private set; }

    public void ShowAmiibo()
    {
        ShowPage(inspector: false);
        MainPanel.IsVisible = false;
        AmiiboPanel.IsVisible = true;
        MainNavigation.SelectedIndex = 8;
        RefreshPageLayout();
    }

    private void LoadAmiiboRewards()
    {
        CanEditAmiibo = false;
        AmiiboOutfitRows.Children.Clear();
        AmiiboMusicRows.Children.Clear();
        if (Save?.Kind == SaveKind.Game)
        {
            try { Save.ReadAmiiboRewards(); CanEditAmiibo = true; }
            catch (Exception error) when (IsFileError(error)) { }
        }
        RefreshAmiiboRewards();
    }

    private void RefreshAmiiboRewards()
    {
        var rewards = CanEditAmiibo ? Save!.ReadAmiiboRewards()
            : AmiiboRewardCatalog.Rewards.Select(row => new AmiiboReward(row, 0)).ToArray();
        foreach (var group in Enum.GetValues<AmiiboRewardGroup>())
        {
            var panel = AmiiboRows(group);
            var selected = SelectedAmiiboKeys(group).ToHashSet(StringComparer.Ordinal);
            panel.Children.Clear();
            var rows = rewards.Where(row => row.Definition.Group == group).ToArray();
            var count = group == AmiiboRewardGroup.Outfits ? AmiiboOutfitCount : AmiiboMusicCount;
            count.Text = CanEditAmiibo ? $"{rows.Count(row => row.Unlocked)}/12" : "—/12";
            foreach (var row in rows)
            {
                var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8, MinHeight = 36 };
                var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
                content.Children.Add(new TextBlock { Text = row.Definition.Name(UiLanguage.Current), TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center });
                string statusKey = row.Unlocked ? "Unlocked" : "AmiiboLocked";
                if (!CanEditAmiibo) statusKey = "Unavailable";
                var status = new TextBlock { Text = UiLanguage.Get(statusKey),
                    Opacity = 0.65, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(status, 1);
                content.Children.Add(status);
                var box = new CheckBox { Tag = row.Definition.Key, MinHeight = 36, Width = 22, MinWidth = 22,
                    IsEnabled = CanEditAmiibo && !row.Unlocked,
                    IsChecked = row.Unlocked || selected.Contains(row.Definition.Key),
                    [AutomationProperties.NameProperty] = row.Definition.Name(UiLanguage.Current),
                    Padding = new Thickness(0) };
                box.IsCheckedChanged += (_, _) => RefreshAmiiboButtons();
                Grid.SetColumn(content, 1);
                line.Children.Add(box);
                line.Children.Add(content);
                panel.Children.Add(line);
            }
            var all = group == AmiiboRewardGroup.Outfits ? UnlockAllAmiiboOutfitsButton : UnlockAllAmiiboMusicButton;
            all.IsEnabled = CanEditAmiibo && rows.Any(row => !row.Unlocked);
        }
        RefreshAmiiboButtons();
    }

    private StackPanel AmiiboRows(AmiiboRewardGroup group) => group == AmiiboRewardGroup.Outfits ? AmiiboOutfitRows : AmiiboMusicRows;

    private string[] SelectedAmiiboKeys(AmiiboRewardGroup group) => AmiiboRows(group).GetLogicalDescendants().OfType<CheckBox>()
        .Where(box => box.IsEnabled && box.IsChecked == true).Select(box => (string)box.Tag!).ToArray();

    private void RefreshAmiiboButtons()
    {
        UnlockSelectedAmiiboOutfitsButton.IsEnabled = CanEditAmiibo && SelectedAmiiboKeys(AmiiboRewardGroup.Outfits).Length > 0;
        UnlockSelectedAmiiboMusicButton.IsEnabled = CanEditAmiibo && SelectedAmiiboKeys(AmiiboRewardGroup.Music).Length > 0;
    }

    public bool UnlockAmiiboRewards(AmiiboRewardGroup group, bool all)
    {
        if (Save is null || !CanEditAmiibo) return false;
        try
        {
            if (!Enum.IsDefined(group)) throw new ArgumentOutOfRangeException(nameof(group));
            string[] selected = SelectedAmiiboKeys(group);
            if (!all && selected.Length == 0) return false;
            Save = all ? Save.UnlockAllAmiiboRewards(group) : Save.UnlockAmiiboRewards(selected);
            RefreshAmiiboRewards();
            RefreshOverview(); RefreshSections();
            Message.IsVisible = false;
            return true;
        }
        catch (Exception error) when (IsFileError(error)) { ShowMessage("EditFailed", error.Message); return false; }
    }

    private void UnlockSelectedAmiibo_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string group }) UnlockAmiiboRewards(Enum.Parse<AmiiboRewardGroup>(group), all: false);
    }

    private void UnlockAllAmiibo_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string group }) UnlockAmiiboRewards(Enum.Parse<AmiiboRewardGroup>(group), all: true);
    }
}
