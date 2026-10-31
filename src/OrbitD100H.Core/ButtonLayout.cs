namespace OrbitD100H.Core;

public static class ButtonLayout
{
    // Stored bindings use protocol IDs. Visible numbers follow the user's annotated device image.
    private static readonly (int Number, string Position)[] ByProtocol =
    [
        (3, "TopLeft"), (4, "TopMiddle"), (5, "TopRight"), (2, "UpperLeft"),
        (6, "UpperRight"), (1, "LowerLeft"), (7, "LowerRight"), (8, "DialPress")
    ];

    public static int Number(int protocolId) => Entry(protocolId).Number;
    public static string PositionName(int protocolId) => Text.Get(Entry(protocolId).Position);
    public static int ProtocolId(int number)
    {
        var index = Array.FindIndex(ByProtocol, entry => entry.Number == number);
        return index >= 0 ? index + 1 : throw new ArgumentOutOfRangeException(nameof(number));
    }
    private static (int Number, string Position) Entry(int protocolId) => protocolId is >= 1 and <= 8
        ? ByProtocol[protocolId - 1] : throw new ArgumentOutOfRangeException(nameof(protocolId));
}
