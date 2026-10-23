using System.Text.Json;
using System.Text.Json.Nodes;

namespace UcxStats.Map;

internal static class AtomicJsonFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static JsonObject? ReadObject(string path)
    {
        if (!File.Exists(path))
            return null;

        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new JsonException($"Die Konfiguration muss ein JSON-Objekt sein: {path}");
    }

    public static void WriteIfChanged(string path, JsonObject document) => Write(path, document);

    internal static void Write(string path, JsonObject document, Action? beforeCommit = null)
    {
        // Comparing parsed values retains the original bytes/timestamp on a no-op save.
        if (JsonNode.DeepEquals(ReadObject(path), document))
            return;

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, document, Options);
                stream.Flush(flushToDisk: true);
            }
            beforeCommit?.Invoke();
            // The temporary file is on the destination filesystem. Readers see either
            // the complete previous document or the complete replacement.
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
