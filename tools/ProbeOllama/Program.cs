using System.Text;

using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Localization;
using DeadlockVN.Infrastructure.Translation;

// Probe: kiểm tra chất lượng bản dịch của provider với prompt mới.
// Chạy: dotnet run --project tools/ProbeOllama -- <model> [batch]

string model = args.Length > 0 ? args[0] : "qwen2.5:7b-instruct-q4_K_M";
int batchSize = args.Length > 1 ? int.Parse(args[1]) : 10;

var provider = new OllamaTranslationProvider(new TranslationProviderConfig
{
    Provider = "ollama",
    Model = model,
    Endpoint = "http://localhost:11434",
    BatchSize = batchSize,
    MaxRetries = 1,
    TimeoutSeconds = 300,
    MaxTokens = 2048
});

if (!await provider.IsAvailableAsync())
{
    Console.WriteLine("Ollama khong phan ung.");

    return 1;
}

Console.WriteLine($"Model: {model}  Batch: {batchSize}");

var store = new JsonTranslationStore();
var doc = await store.LoadAsync("workspace/translations/vietnamese.json");

var guard = new TranslationGuard();

// Lay mot mau deu viet deu phan bo theo nhau.
var pending = doc.Entries
    .Where(e => e.Status == TranslationStatus.Pending)
    .Where(e => !string.IsNullOrWhiteSpace(e.OriginalText))
    .Where(e => e.OriginalText.Length > 15)
    .OrderBy(e => e.Key)
    .ToList();

Console.WriteLine($"Pending co the dich: {pending.Count}");

var sample = pending.Take(batchSize * 3).ToList();

var texts = sample.Select(e => e.OriginalText).ToList();

var sw = System.Diagnostics.Stopwatch.StartNew();
var results = await provider.TranslateBatchAsync(
    texts,
    "Ten va mo ta vat pham cua tro choi Deadlock.",
    CancellationToken.None);
sw.Stop();

Console.WriteLine($"Thoi gian: {sw.ElapsedMilliseconds}ms cho {texts.Count} chuoi\n");

var sb = new StringBuilder();
int ok = 0, bad = 0;

for (int i = 0; i < sample.Count; i++)
{
    string translated = i < results.Count ? results[i] : "";
    var check = guard.Check(sample[i].OriginalText, translated);

    if (check.IsValid) ok++; else bad++;

    sb.AppendLine($"[{sample[i].Category}] {sample[i].Key}");
    sb.AppendLine($"   EN: {sample[i].OriginalText}");
    sb.AppendLine($"   VI: {translated}");
    if (!check.IsValid)
    {
        sb.AppendLine($"   !! {string.Join(" | ", check.Errors)}");
    }
    sb.AppendLine();
}

sb.AppendLine($"Hop le: {ok}  Loi: {bad}");

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine(sb.ToString());
File.WriteAllText(
    "workspace/probe-output.txt",
    sb.ToString(),
    new UTF8Encoding(false));

Console.WriteLine("Da ghi workspace/probe-output.txt");

return 0;
