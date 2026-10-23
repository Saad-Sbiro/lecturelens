namespace LectureLens.Core;

public record MediaPacket(int Sequence, double AtMs, byte[] Bytes, string Reason);
public record SessionInfo(string Id, string CreatedAt, string State, int Chunks, long Bytes, string[] Problems, string? Reason = null, string Title = "")
{
    public string DisplayDate => DateTimeOffset.TryParse(CreatedAt, out var date) ? date.ToLocalTime().ToString("g") : "";
}
public interface IRecordingStorage
{
    string Create(string mime);
    void Append(string id, MediaPacket packet);
    void Event(string id, string type, double atMs, string reason);
    void Finish(string id, string reason);
    IReadOnlyList<SessionInfo> List();
    string Recover(string id);
    long FreeBytes();
}
