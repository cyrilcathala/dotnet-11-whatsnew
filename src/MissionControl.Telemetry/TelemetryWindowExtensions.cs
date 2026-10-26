namespace MissionControl.Telemetry;

public static class TelemetryWindowExtensions
{
    // 🆕 C# 15 : extension indexer à deux bornes, packets[from, to].
    extension(IReadOnlyList<TelemetryPacket> packets)
    {
        /// <summary>Les paquets mesurés dans la fenêtre [<paramref name="from"/>, <paramref name="to"/>[.</summary>
        public IReadOnlyList<TelemetryPacket> this[DateTimeOffset from, DateTimeOffset to]
            => [.. packets.Where(p => p.Timestamp >= from && p.Timestamp < to)];
    }
}
