using System.Globalization;
using System.Text.Json;
using FeeEditor.Core;

namespace FeeEditor.Cli;

internal static class TicketsCommand
{
    public static int Run(string[] args)
    {
        if (args is ["list", var source, .. var options] && options is [] or ["--json"])
        {
            Console.WriteLine(JsonSerializer.Serialize(EngageSave.Load(source).ReadRelayTickets().Select(row => new
            {
                Identifier = row.IdentifierHex, row.Amount, Maximum = RelayTickets.Maximum, row.LastTime
            }), new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (args is [var verb, var input, var output, "--identifier", var identifier, .. var values]
            && ulong.TryParse(identifier, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong id))
        {
            int amount;
            if (verb == "max" && values.Length == 0) amount = RelayTickets.Maximum;
            else if (verb == "set" && values is ["--amount", var value]
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) amount = number;
            else throw new ArgumentException("Use set --amount <0-999> or max.");
            EngageSave.Load(input).WithRelayTickets(id, amount).WriteCopy(output);
            return 0;
        }
        throw new ArgumentException("Use tickets list <Global> or set|max <Global> <new-file> --identifier <hex> [--amount <0-999>].");
    }
}
