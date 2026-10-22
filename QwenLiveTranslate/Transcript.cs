using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace QwenLiveTranslate;

public sealed class Transcript : INotifyPropertyChanged
{
    public string Time { get; } = DateTime.Now.ToString("HH:mm:ss");
    string chinese = "", russian = "";
    public string Chinese { get => chinese; set { chinese = value; Changed(); } }
    public string Russian { get => russian; set { russian = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public sealed class TranscriptStore
{
    readonly Dictionary<string, Transcript> entries = new();
    readonly Dictionary<string, string> parents = new();
    public event Action<Transcript>? Added;
    public event Action<Transcript>? Translated;
    static string S(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    Transcript Get(string id)
    {
        if (parents.TryGetValue(id, out var parent)) id = parent;
        if (!entries.TryGetValue(id, out var row)) { row = new(); entries[id] = row; Added?.Invoke(row); }
        return row;
    }
    public void Apply(JsonElement e)
    {
        var type = S(e, "type");
        if (type == "conversation.item.created" && e.TryGetProperty("item", out var item) && S(item, "role") == "assistant")
        {
            // Some service versions label input-audio items as assistant messages as well.
            if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array && content.EnumerateArray().Any(part => S(part, "type") == "input_audio")) return;
            var id = S(item, "id"); var parent = S(e, "previous_item_id");
            if (id.Length > 0 && parent.Length > 0) parents[id] = parent;
            return;
        }
        var key = S(e, "item_id");
        if (key.Length == 0) key = S(e, "response_id");
        if (key.Length == 0) return;
        if (type is "response.text.delta" or "response.audio_transcript.delta")
        { var row = Get(key); row.Chinese += S(e, "delta"); Translated?.Invoke(row); }
        else if (type is "response.text.done" or "response.audio_transcript.done")
        { var row = Get(key); var text = S(e, "text"); if (text.Length == 0) text = S(e, "transcript"); if (text.Length > 0) row.Chinese = text; Translated?.Invoke(row); }
        else if (type == "conversation.item.input_audio_transcription.delta") Get(key).Russian += S(e, "delta");
        else if (type == "conversation.item.input_audio_transcription.completed") Get(key).Russian = S(e, "transcript");
        else if (type == "conversation.item.input_audio_transcription.text") Get(key).Russian = S(e, "text") + S(e, "stash");
        else if (type == "response.text.text") { var row = Get(key); row.Chinese = S(e, "text") + S(e, "stash"); Translated?.Invoke(row); }
    }
    public void Clear() { entries.Clear(); parents.Clear(); }
}
