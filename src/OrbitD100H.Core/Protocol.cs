namespace OrbitD100H.Core;

public enum EventKind { Press, Release, Rotate, KeepAlive }
public readonly record struct DialEvent(EventKind Kind, int Button = 0, int Direction = 0);

public static class Protocol
{
    public const byte ReportId = 0xFB;
    public const int ReportLength = 65;
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromMilliseconds(1020);
    private static readonly byte[] Token = Convert.FromHexString("3890C499A360AAAD");

    // Event codes and framing: https://github.com/ftl/hamdial (MIT; see THIRD-PARTY-NOTICES.md).
    private static readonly Dictionary<string, DialEvent> Events = new()
    {
        ["8E8F5E16EC67281E"] = new(EventKind.Press, 1),
        ["028DB3660224D0C7"] = new(EventKind.Release, 1),
        ["22D73CFA751FFBC7"] = new(EventKind.Press, 2),
        ["96F6B51A60F4FF67"] = new(EventKind.Release, 2),
        ["983AE16D833A42A4"] = new(EventKind.Press, 3),
        ["ABC5377EDA49BE8B"] = new(EventKind.Release, 3),
        ["3D2E482AC73D9006"] = new(EventKind.Press, 4),
        ["446E3A0ED925A0A2"] = new(EventKind.Release, 4),
        ["E6AE57C883933971"] = new(EventKind.Press, 5),
        ["DC24A2910177965F"] = new(EventKind.Release, 5),
        ["2E426BB625BB36DD"] = new(EventKind.Press, 6),
        ["250846C7E37615F9"] = new(EventKind.Release, 6),
        ["FF79B4421864A3F1"] = new(EventKind.Press, 7),
        ["73D198D9C446DFB2"] = new(EventKind.Release, 7),
        ["838CC9CA145C72ED"] = new(EventKind.Press, 8),
        ["85138E54A3497900"] = new(EventKind.Release, 8),
        ["3EDD5F9E876AF42C"] = new(EventKind.Rotate, Direction: 1),
        ["B06BE536DBE92E9D"] = new(EventKind.Rotate, Direction: -1),
        ["21F45EB71C3587AB"] = new(EventKind.KeepAlive)
    };

    public static byte[] CreateKeepAlive(int outputLength)
    {
        if (outputLength < ReportLength) throw new ArgumentOutOfRangeException(nameof(outputLength));
        var report = new byte[outputLength];
        report[0] = ReportId;
        Convert.FromHexString("24569EF228E145A1").CopyTo(report, 1);
        Token.CopyTo(report, 9);
        return report;
    }

    public static byte[] CreateHapticFeedback(bool enabled, int outputLength)
    {
        if (outputLength < ReportLength) throw new ArgumentOutOfRangeException(nameof(outputLength));
        var report = new byte[outputLength];
        report[0] = ReportId;
        Convert.FromHexString(enabled ? "39B056AFD36F9ABF" : "5F4E1D1371B29A76").CopyTo(report, 1);
        Token.CopyTo(report, 9);
        return report;
    }

    public static bool TryDecode(ReadOnlySpan<byte> report, out DialEvent dialEvent)
    {
        dialEvent = default;
        if (report.Length < ReportLength || report[0] != ReportId || !report.Slice(9, 8).SequenceEqual(Token))
            return false;
        return Events.TryGetValue(Convert.ToHexString(report.Slice(1, 8)), out dialEvent);
    }

    public static bool TryDecodeHapticAcknowledgement(ReadOnlySpan<byte> report, out bool enabled)
    {
        enabled = false;
        if (report.Length < ReportLength || report[0] != ReportId || !report.Slice(9, 8).SequenceEqual(Token))
            return false;
        var code = Convert.ToHexString(report.Slice(1, 8));
        if (code == "A1F76ADB93F65A78") { enabled = true; return true; }
        return code == "99BF42CA2354B0AF";
    }
}
