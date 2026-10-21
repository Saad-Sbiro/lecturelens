using System.Security.Cryptography;
using System.Text.Json;
using System.Net;

namespace LectureLens.Storage;
public sealed partial class LocalStore
{
    public void SaveSlide(string id, int slide, double atMs, JsonElement crop, string reason, byte[] jpeg)
    {
        RequireActive(id);
        ValidateCrop(crop);
        if (slide is < 0 or >= 720 || !double.IsFinite(atMs) || atMs < 0 || reason.Length > 100 || jpeg.Length is < 4 or > 500000 || jpeg[0] != 255 || jpeg[1] != 216 || jpeg[^2] != 255 || jpeg[^1] != 217) throw new InvalidDataException("Invalid slide");
        var folder = Path.Combine(Folder(id), "slides"); Directory.CreateDirectory(folder);
        // The atomic envelope is authoritative; JPEG/gallery exports can be rebuilt after a crash.
        WriteJson(Path.Combine(folder, $"{slide:D5}.slide.json"), new { slide, atMs, crop, reason, sha256 = Convert.ToHexStringLower(SHA256.HashData(jpeg)), data = Convert.ToBase64String(jpeg) });
    }
    public void VisualEvent(string id, double atMs, string reason, int? slide, JsonElement crop)
    {
        RequireActive(id);
        ValidateCrop(crop);
        if (!double.IsFinite(atMs) || atMs < 0 || reason.Length > 100 || crop.GetRawText().Length > 512) throw new InvalidDataException("Invalid visual event");
        using var stream = new FileStream(Path.Combine(Folder(id), "visual-events.ndjson"), FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(JsonSerializer.SerializeToUtf8Bytes(new { atMs, reason, slide, crop }, Json)); stream.WriteByte(10); stream.Flush(true);
    }
    private static void ValidateCrop(JsonElement crop)
    {
        var x = crop.GetProperty("x").GetDouble(); var y = crop.GetProperty("y").GetDouble();
        var w = crop.GetProperty("width").GetDouble(); var h = crop.GetProperty("height").GetDouble();
        if (!double.IsFinite(x + y + w + h) || x < 0 || y < 0 || w < 0.05 || h < 0.05 || x + w > 1.001 || y + h > 1.001) throw new InvalidDataException("Invalid crop");
    }
    private void ExportSlides(string id)
    {
        var folder = Path.Combine(Folder(id), "slides"); if (!Directory.Exists(folder)) return;
        var html = "<!doctype html><meta charset=utf-8><meta http-equiv=Content-Security-Policy content=\"default-src 'none'; img-src 'self'; style-src 'unsafe-inline'\"><title>LectureLens</title><style>body{font:18px system-ui;margin:32px}img{max-width:100%}figure{margin:24px 0}</style>";
        foreach (var file in Directory.GetFiles(folder, "*.slide.json").Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(file)); var data = document.RootElement;
            var number = data.GetProperty("slide").GetInt32(); if (number is < 0 or >= 720) throw new InvalidDataException("Invalid slide identifier");
            var jpeg = Convert.FromBase64String(data.GetProperty("data").GetString()!);
            if (Convert.ToHexStringLower(SHA256.HashData(jpeg)) != data.GetProperty("sha256").GetString()) throw new InvalidDataException("Slide integrity failure");
            var target = Path.Combine(folder, $"{number:D5}.jpg");
            File.WriteAllBytes(target + ".partial", jpeg); File.Move(target + ".partial", target, true);
            html += $"<figure><figcaption>#{number + 1} · {data.GetProperty("atMs").GetDouble() / 1000:F1}s · {WebUtility.HtmlEncode(data.GetProperty("reason").GetString())}</figcaption><img src='{number:D5}.jpg'></figure>";
        }
        File.WriteAllText(Path.Combine(folder, "index.html.partial"), html); File.Move(Path.Combine(folder, "index.html.partial"), Path.Combine(folder, "index.html"), true);
    }
}
