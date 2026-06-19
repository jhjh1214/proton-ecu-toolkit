namespace ProtonEcuToolkit.Core.Kwp;

/// <param name="ByteLength">Number of data bytes expected after the CID echo (1 = byteA only, 2 = byteA+byteB).</param>
/// <param name="Decode">Applies the PID's formula to the raw data bytes.</param>
public sealed record PidDefinition(string Id, string Name, string Unit, int ByteLength, Func<byte[], double> Decode);
