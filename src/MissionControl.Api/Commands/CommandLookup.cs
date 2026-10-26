namespace MissionControl.Api.Commands;

public static class CommandLookup
{
    // 🆕 C# 15 : extension indexer — history[commandId] sur IEnumerable<ProbeCommand>, un type qu'on ne possède pas.
    extension(IEnumerable<ProbeCommand> history)
    {
        public ProbeCommand? this[Guid commandId] => history.FirstOrDefault(c => c.Id == commandId);
    }
}
