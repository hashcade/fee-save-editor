namespace FeeEditor.Core;

public enum ProtagonistGender { Male = 1, Female = 2 }

public sealed partial class EngageSave
{
    public ProtagonistGender? ReadProtagonistGender()
    {
        var entry = FindProtagonist(RosterLayout.Read(this, _bytes));
        if (entry?.Progress.NameEnd is null) return null;
        int gender = entry.Character.Progress.Gender;
        if (gender is not (1 or 2)) throw new InvalidDataException("Unsupported protagonist gender.");
        return (ProtagonistGender)gender;
    }

    public EngageSave WithProtagonistGender(ProtagonistGender gender)
    {
        if (!Enum.IsDefined(gender)) throw new ArgumentOutOfRangeException(nameof(gender));
        var layout = RosterLayout.Read(this, _bytes);
        var entry = FindProtagonist(layout)
            ?? throw new InvalidDataException("The save has no protagonist record.");
        if (entry.Progress.NameEnd is not int offset)
            throw new InvalidDataException("The protagonist has no saved customization record.");
        if (entry.Character.Progress.Gender == (int)gender) return this;
        var edited = ReplaceCharacterRange(layout, entry, offset, offset + 1, [(byte)gender]);
        if (edited.ReadProtagonistGender() != gender)
            throw new InvalidDataException("The edited gender did not survive serialization.");
        return edited;
    }
}
