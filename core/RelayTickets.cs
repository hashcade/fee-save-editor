using System.Buffers.Binary;

namespace FeeEditor.Core;

public sealed record RelayTickets(ulong Identifier, int Amount, long LastTime)
{
    public const int Maximum = 999;
    public string IdentifierHex => $"{Identifier:X16}";
}

public sealed partial class EngageSave
{
    public IReadOnlyList<RelayTickets> ReadRelayTickets()
    {
        if (Kind != SaveKind.Global)
            throw new InvalidDataException("Relay Tickets belong to the Global save. Open Global, not Manual or Auto.");
        var sections = Sections.Where(section => section.Name == "RLGL").ToArray();
        if (sections.Length != 1) throw new InvalidDataException("Expected one RLGL section.");
        var section = sections[0];
        var reader = new SaveReader(_bytes, section.PayloadOffset, section.PayloadOffset + section.Length);
        if (reader.UInt32() != 1) throw new InvalidDataException("Unsupported RLGL version.");
        reader.Skip(32); // Preserved singleton metadata and daily ticket counter.
        uint count = reader.UInt32();
        if (count > reader.Remaining / 20 || reader.Remaining != count * 20)
            throw new InvalidDataException("Invalid Relay Ticket record count.");
        var entries = new List<RelayTickets>();
        var identifiers = new HashSet<ulong>();
        for (uint index = 0; index < count; index++)
        {
            int offset = reader.Position;
            reader.Skip(20);
            ulong identifier = BinaryPrimitives.ReadUInt64LittleEndian(_bytes.AsSpan(offset, 8));
            uint amount = ReadUInt32(_bytes, offset + 8);
            long time = BinaryPrimitives.ReadInt64LittleEndian(_bytes.AsSpan(offset + 12, 8));
            if (!identifiers.Add(identifier) || amount > int.MaxValue)
                throw new InvalidDataException("Invalid or duplicated Relay Ticket record.");
            entries.Add(new(identifier, (int)amount, time));
        }
        return entries.AsReadOnly();
    }

    public EngageSave WithRelayTickets(ulong identifier, int amount)
    {
        if (amount is < 0 or > RelayTickets.Maximum)
            throw new ArgumentOutOfRangeException(nameof(amount), "Relay Ticket count must be 0–999.");
        var entries = ReadRelayTickets();
        int index = entries.ToList().FindIndex(entry => entry.Identifier == identifier);
        if (index < 0) throw new ArgumentException("This campaign identifier is not registered in Global.", nameof(identifier));
        if (entries[index].Amount == amount) return this;
        var section = Sections.Single(section => section.Name == "RLGL");
        byte[] payload = _bytes.AsSpan(section.PayloadOffset, section.Length).ToArray();
        Write32(payload, 48 + index * 20, (uint)amount);
        return ReplaceSection(section, payload);
    }
}
