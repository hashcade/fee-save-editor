using Avalonia.Controls;
using Avalonia.Interactivity;
using FeeEditor.Core;

namespace FeeEditor.Gui;

public partial class MainWindow
{
    public bool CanEditTickets { get; private set; }
    private RelayTickets? _ticketBaseline;
    private bool _refreshingTickets;

    private void LoadTickets()
    {
        CanEditTickets = RelayTicketsCard.IsVisible = TicketForm.IsEnabled = false;
        _ticketBaseline = null;
        TicketAmountInput.Value = null;
        TicketCampaignInput.ItemsSource = Array.Empty<string>();
        if (Save?.Kind != SaveKind.Global) return;
        try
        {
            var tickets = Save.ReadRelayTickets();
            CanEditTickets = RelayTicketsCard.IsVisible = true;
            TicketCampaignInput.ItemsSource = tickets.Select(row => row.IdentifierHex).ToArray();
            TicketCampaignInput.SelectedIndex = tickets.Count > 0 ? 0 : -1;
            SelectTicketCampaign();
        }
        catch (Exception error) when (IsFileError(error)) { ShowMessage("MainUnavailable", error.Message); }
    }

    private void SelectTicketCampaign()
    {
        _ticketBaseline = CanEditTickets ? Save?.ReadRelayTickets().FirstOrDefault(row => row.IdentifierHex == TicketCampaignInput.SelectedItem as string) : null;
        TicketForm.IsEnabled = _ticketBaseline is not null;
        TicketAmountInput.Text = _ticketBaseline?.Amount.ToString() ?? "";
    }

    private bool HasPendingTickets() => CanEditTickets && _ticketBaseline is not null
        && TicketAmountInput.Text != _ticketBaseline.Amount.ToString();

    public bool ApplyTicketValues()
    {
        if (Save is null || !CanEditTickets || _ticketBaseline is null) return false;
        try
        {
            Save = Save.WithRelayTickets(_ticketBaseline.Identifier, Amount(TicketAmountInput));
            _ticketBaseline = Save.ReadRelayTickets().Single(row => row.Identifier == _ticketBaseline.Identifier);
            RefreshOverview(); RefreshSections();
            Message.IsVisible = false;
            return true;
        }
        catch (Exception error) when (IsFileError(error)) { ShowMessage("EditFailed", error.Message); return false; }
    }

    private void TicketCampaign_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_refreshingTickets) return;
        if (HasPendingTickets() && !ApplyTicketValues())
        {
            _refreshingTickets = true;
            TicketCampaignInput.SelectedItem = _ticketBaseline!.IdentifierHex;
            _refreshingTickets = false;
            return;
        }
        SelectTicketCampaign();
    }
    private void ApplyTickets_Click(object? sender, RoutedEventArgs e) => ApplyTicketValues();
    private void MaxTickets_Click(object? sender, RoutedEventArgs e)
    {
        if (_ticketBaseline is null) return;
        TicketAmountInput.Value = RelayTickets.Maximum;
        ApplyTicketValues();
    }
}
