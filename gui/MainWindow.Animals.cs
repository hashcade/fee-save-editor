using Avalonia.Controls;
using Avalonia.Interactivity;
using FeeEditor.Core;
using FeeEditor.Gui.Localization;

namespace FeeEditor.Gui;

public partial class MainWindow
{
    public sealed record AnimalRow(AdoptedAnimal Animal, string Label);
    public bool CanEditAnimals { get; private set; }
    private AdoptedAnimal? _animalBaseline;
    private bool _refreshingAnimals;

    public void ShowAnimals()
    {
        ShowPage(inspector: false);
        MainPanel.IsVisible = false;
        AnimalsPanel.IsVisible = true;
        MainNavigation.SelectedIndex = 7;
        RefreshPageLayout();
    }

    private void LoadAnimals()
    {
        CanEditAnimals = MaxAllAnimalsButton.IsEnabled = AnimalForm.IsEnabled = false;
        _animalBaseline = null;
        AnimalList.ItemsSource = Array.Empty<AnimalRow>();
        AnimalAmountInput.Value = null;
        AnimalSearch.Clear();
        if (Save?.Kind != SaveKind.Game) return;
        try
        {
            Save.ReadAnimals();
            CanEditAnimals = MaxAllAnimalsButton.IsEnabled = true;
            RefreshAnimals(selectEditor: true);
        }
        catch (Exception error) when (IsFileError(error)) { }
    }

    private void RefreshAnimals(bool selectEditor)
    {
        if (!CanEditAnimals || Save is null) return;
        string? selected = _animalBaseline?.Definition.Id;
        string query = AnimalSearch.Text?.Trim() ?? "";
        var rows = Save.ReadAnimals().Select(animal => new AnimalRow(animal,
            $"{animal.Definition.Name(UiLanguage.Current)} · ×{animal.Amount}"))
            .Where(row => row.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Animal.Definition.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _refreshingAnimals = true;
        AnimalList.ItemsSource = rows;
        AnimalList.SelectedItem = rows.FirstOrDefault(row => row.Animal.Definition.Id == selected) ?? rows.FirstOrDefault();
        _refreshingAnimals = false;
        if (selectEditor || (AnimalList.SelectedItem as AnimalRow)?.Animal.Definition.Id != selected) SelectAnimal();
    }

    private void SelectAnimal()
    {
        _animalBaseline = (AnimalList.SelectedItem as AnimalRow)?.Animal;
        AnimalForm.IsEnabled = _animalBaseline is not null;
        AnimalAmountInput.Text = _animalBaseline?.Amount.ToString() ?? "";
    }

    private bool HasPendingAnimal() => CanEditAnimals && _animalBaseline is not null
        && AnimalAmountInput.Text != _animalBaseline.Amount.ToString();

    public bool ApplyAnimalValues()
    {
        if (Save is null || !CanEditAnimals || _animalBaseline is null) return false;
        try
        {
            Save = Save.WithAnimal(_animalBaseline.Definition.Id, Amount(AnimalAmountInput));
            RefreshAnimals(selectEditor: true);
            RefreshOverview(); RefreshSections();
            Message.IsVisible = false;
            return true;
        }
        catch (Exception error) when (IsFileError(error)) { ShowMessage("EditFailed", error.Message); return false; }
    }

    public bool MaximizeAllAnimals()
    {
        if (Save is null || !CanEditAnimals) return false;
        try
        {
            Save = Save.MaximizeAnimals();
            RefreshAnimals(selectEditor: true);
            RefreshOverview(); RefreshSections();
            Message.IsVisible = false;
            return true;
        }
        catch (Exception error) when (IsFileError(error)) { ShowMessage("EditFailed", error.Message); return false; }
    }

    private void AnimalList_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingAnimals) return;
        string? next = (AnimalList.SelectedItem as AnimalRow)?.Animal.Definition.Id;
        if (HasPendingAnimal() && !ApplyAnimalValues())
        {
            _refreshingAnimals = true;
            AnimalList.SelectedItem = AnimalList.ItemsSource?.Cast<AnimalRow>().FirstOrDefault(row => row.Animal.Definition.Id == _animalBaseline!.Definition.Id);
            _refreshingAnimals = false;
            return;
        }
        _refreshingAnimals = true;
        AnimalList.SelectedItem = AnimalList.ItemsSource?.Cast<AnimalRow>().FirstOrDefault(row => row.Animal.Definition.Id == next);
        _refreshingAnimals = false;
        SelectAnimal();
    }
    private void AnimalSearch_Changed(object? sender, TextChangedEventArgs e)
    {
        if (HasPendingAnimal() && !ApplyAnimalValues()) return;
        RefreshAnimals(selectEditor: false);
    }
    private void ApplyAnimal_Click(object? sender, RoutedEventArgs e) => ApplyAnimalValues();
    private void MaxAnimal_Click(object? sender, RoutedEventArgs e)
    {
        if (_animalBaseline is null) return;
        AnimalAmountInput.Value = AnimalCatalog.Maximum;
        ApplyAnimalValues();
    }
    private void MaxAllAnimals_Click(object? sender, RoutedEventArgs e) => MaximizeAllAnimals();
}
