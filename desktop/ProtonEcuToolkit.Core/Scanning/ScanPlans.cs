namespace ProtonEcuToolkit.Core.Scanning;

/// <summary>
/// Candidate identifier ranges to scan under Service 0x22
/// (ReadDataByCommonIdentifier) - the same read-only service the 5 known
/// PIDs already use. Deliberately scoped to this one service: no write
/// services, no security access, no routine control, no IO control -
/// reading an unsupported identifier just gets a negative response, it
/// doesn't change ECU state, which is what keeps this safe to brute-force.
/// </summary>
public static class ScanPlans
{
    public const byte ReadByCommonIdentifierSid = 0x22;

    /// <summary>
    /// The 7 identifiers from leftover (likely Pro-only) CSVs in the OEM
    /// app's decompile that were never wired into any code path - high
    /// value, cheap to check first (README.md "Likely-real extra identifiers").
    /// </summary>
    public static IReadOnlyList<int> KnownCandidateIds { get; } =
        [0x1147, 0x1148, 0x1149, 0x11CC, 0x11CD, 0x11CE, 0x11CF];

    public static IReadOnlyList<int> NearbyRange { get; } = BuildRange(0x1000, 0x12FF);

    public static IReadOnlyList<int> WideRange { get; } = BuildRange(0x0000, 0x1FFF);

    private static IReadOnlyList<int> BuildRange(int startInclusive, int endInclusive)
    {
        var count = endInclusive - startInclusive + 1;
        var ids = new int[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = startInclusive + i;
        }
        return ids;
    }
}
