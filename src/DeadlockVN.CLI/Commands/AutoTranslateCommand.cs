using DeadlockVN.Application.Services;
using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Localization;
using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>auto-translate</c>: dịch tự động các entry chưa được dịch bằng
/// provider (mặc định là Ollama chạy local).
/// <para>
/// Bản dịch sinh ra luôn ở trạng thái <c>Reviewed</c> — nghĩa là "đã dịch
/// bằng máy, cần người kiểm tra". Không bao giờ tự động <c>Approved</c>.
/// </para>
/// </summary>
internal static class AutoTranslateCommand
{
    private const string DefaultDatabase = "workspace/translations/vietnamese.json";

    private const string DefaultSplitDirectory = "workspace/translations";

    public static async Task<int> ExecuteAsync(string[] args)
    {
        var options = ParseOptions(args);

        if (options.ShowHelp)
        {
            PrintUsage();

            return 0;
        }

        string databasePath = options.DatabasePath
            ?? ResolveDefaultDatabase();

        var store = new JsonTranslationStore();

        TranslationDocument document;

        bool split = options.Split;

        if (split)
        {
            string root = options.SplitDirectory
                ?? ResolveDefaultSplitDirectory();

            document = await store.LoadSplitAsync(root);
        }
        else
        {
            if (!File.Exists(databasePath))
            {
                Console.Error.WriteLine(
                    $"Không tìm thấy file bản dịch: {databasePath}\n" +
                    "Chạy 'deadlockvn export' trước để tạo.");

                return 1;
            }

            document = await store.LoadAsync(databasePath);
        }

        var categories = ParseCategories(options.Categories);

        Console.WriteLine("=== AUTO TRANSLATE: dịch tự động ===");
        Console.WriteLine($"Cơ sở dữ liệu : {databasePath}");
        Console.WriteLine(
            $"Provider     : {options.Provider} / {options.Model}");
        Console.WriteLine($"Endpoint     : {options.Endpoint}");
        Console.WriteLine($"Kích thước lô: {options.BatchSize}");
        Console.WriteLine(
            $"Nhóm nội dung: " +
            (categories is null
                ? "tất cả (trừ Voice)"
                : string.Join(", ", categories)));
        Console.WriteLine();

        if (options.DryRun)
        {
            PrintDryRun(document, categories);

            return 0;
        }

        var providerConfig = new TranslationProviderConfig
        {
            Provider = options.Provider,
            Model = options.Model,
            Endpoint = options.Endpoint,
            BatchSize = options.BatchSize,
            MaxRetries = options.MaxRetries,
            TimeoutSeconds = options.TimeoutSeconds,
            MaxTokens = options.MaxTokens
        };

        ITranslationProvider provider = options.Provider switch
        {
            "ollama" => new OllamaTranslationProvider(providerConfig),

            // Mọi API tương thích OpenAI: OpenAI, Groq, OpenRouter,
            // Gemini (endpoint tương thích OpenAI của Google).
            "openai" or "groq" or "openrouter" or "gemini" =>
                new OpenAiCompatibleTranslationProvider(
                    providerConfig,
                    options.ApiKey),

            _ => throw new NotSupportedException(
                $"Provider chưa hỗ trợ: {options.Provider}. " +
                "Hỗ trợ: ollama, openai, groq, openrouter, gemini.")
        };

        if (!await provider.IsAvailableAsync())
        {
            Console.Error.WriteLine(
                $"Không kết nối được provider '{provider.Name}' " +
                $"tại {options.Endpoint}.");

            if (options.Provider == "ollama")
            {
                Console.Error.WriteLine(
                    "Kiểm tra:\n" +
                    "  1. Ollama đã chạy chưa: ollama serve\n" +
                    "  2. Model đã tải chưa: ollama pull " + options.Model);
            }
            else
            {
                Console.Error.WriteLine(
                    "Kiểm tra:\n" +
                    "  1. Đã đặt API key chưa: --api-key <key> hoặc biến môi " +
                    $"trường {OpenAiCompatibleTranslationProvider.ApiKeyEnvironmentVariable}\n" +
                    "  2. Endpoint có đúng không: --endpoint <url>");
            }

            return 1;
        }

        Console.WriteLine($"Đã kết nối: {provider.Name}");
        Console.WriteLine();

        var service = new AutoTranslationService(
            provider,
            new TranslationGuard(),
            providerConfig);

        // Lưu sau mỗi lô để không mất công sức khi bị hủy (Ctrl+C).
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;

            Console.WriteLine();
            Console.WriteLine("Đang dừng — lưu tiến độ hiện tại...");

            cts.Cancel();
        };

        var result = await service.TranslateAsync(
            document,
            categories,
            (done, total, message) =>
            {
                Console.WriteLine($"  [{done}/{total}] {message}");
            },
            async () =>
            {
                // Lưu sau mỗi lô — để Ctrl+C không mất công sức.
                await SaveAsync(store, document, databasePath, split, options);
            },
            cts.Token);

        await SaveAsync(store, document, databasePath, split, options);

        Console.WriteLine();
        Console.WriteLine("=== KẾT QUẢ ===");
        Console.WriteLine($"Đã dịch    : {result.Translated}");
        Console.WriteLine($"Thất bại   : {result.Failed}");
        Console.WriteLine($"Bỏ qua     : {result.Skipped}");
        Console.WriteLine($"Số lô      : {result.Batches}");

        if (result.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("=== LỖI (20 dòng đầu) ===");

            foreach (string error in result.Errors.Take(20))
            {
                Console.WriteLine("  " + error);
            }

            if (result.Errors.Count > 20)
            {
                Console.WriteLine($"  ... và {result.Errors.Count - 20} lỗi khác.");
            }
        }

        return result.Failed > 0 ? 2 : 0;
    }

    private static async Task SaveAsync(
        JsonTranslationStore store,
        TranslationDocument document,
        string databasePath,
        bool split,
        Options options)
    {
        if (split)
        {
            await store.SaveSplitAsync(
                document,
                options.SplitDirectory ?? ResolveDefaultSplitDirectory());

            return;
        }

        await store.SaveAsync(document, databasePath);
    }

    private static void PrintDryRun(
        TranslationDocument document,
        IReadOnlyCollection<LocalizationCategory>? categories)
    {
        var pending = document.Entries
            .Where(entry => entry.Status == TranslationStatus.Pending)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.OriginalText))
            .Where(entry =>
            {
                if (categories is null || categories.Count == 0)
                {
                    return entry.Category != LocalizationCategory.Voice;
                }

                return categories.Contains(entry.Category) ||
                       categories.Contains(LocalizationCategory.All);
            })
            .ToList();

        Console.WriteLine($"Số entry cần dịch: {pending.Count}");
        Console.WriteLine();

        foreach (var group in pending.GroupBy(entry => entry.Category))
        {
            Console.WriteLine($"  {group.Key,-8} {group.Count(),8}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Ước tính số lô: " +
            $"{(pending.Count + 29) / 30} (batch 30)");

        Console.WriteLine();
        Console.WriteLine("Không có gì được ghi — đây là chế độ --dry-run.");
    }

    private static IReadOnlyCollection<LocalizationCategory>? ParseCategories(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var categories = new List<LocalizationCategory>();

        foreach (string part in value.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(part, ignoreCase: true, out LocalizationCategory category))
            {
                categories.Add(category);

                continue;
            }

            // Cho phép viết tắt thường dùng.
            switch (part.ToLowerInvariant())
            {
                case "item":
                    categories.Add(LocalizationCategory.Items);
                    break;
                case "hero":
                    categories.Add(LocalizationCategory.Heroes);
                    break;
                case "ability":
                    categories.Add(LocalizationCategory.Abilities);
                    break;
                case "vo":
                case "voice":
                    categories.Add(LocalizationCategory.Voice);
                    break;
                default:
                    Console.Error.WriteLine(
                        $"Không hiểu nhóm nội dung: {part}");

                    return null;
            }
        }

        return categories.Count > 0 ? categories : null;
    }

    private static string ResolveDefaultDatabase()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "translations",
                "vietnamese.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return DefaultDatabase;
    }

    private static string ResolveDefaultSplitDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "workspace",
                "translations");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return DefaultSplitDirectory;
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();

        bool modelExplicitlySet = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db":
                case "--database":
                    if (i + 1 < args.Length)
                    {
                        options.DatabasePath = args[++i];
                    }

                    break;

                case "--split":
                    options.Split = true;

                    break;

                case "--split-dir":
                    if (i + 1 < args.Length)
                    {
                        options.Split = true;
                        options.SplitDirectory = args[++i];
                    }

                    break;

                case "--provider":
                    if (i + 1 < args.Length)
                    {
                        options.Provider = args[++i];
                        options.Endpoint = DefaultEndpointFor(options.Provider);
                    }

                    break;

                case "--api-key":
                    if (i + 1 < args.Length)
                    {
                        options.ApiKey = args[++i];
                    }

                    break;

                case "--model":
                    if (i + 1 < args.Length)
                    {
                        options.Model = args[++i];
                        modelExplicitlySet = true;
                    }

                    break;

                case "--endpoint":
                    if (i + 1 < args.Length)
                    {
                        options.Endpoint = args[++i];
                    }

                    break;

                case "--batch":
                case "--batch-size":
                    if (i + 1 < args.Length &&
                        int.TryParse(args[++i], out int batchSize) &&
                        batchSize > 0)
                    {
                        options.BatchSize = batchSize;
                    }

                    break;

                case "--retries":
                    if (i + 1 < args.Length &&
                        int.TryParse(args[++i], out int retries) &&
                        retries > 0)
                    {
                        options.MaxRetries = retries;
                    }

                    break;

                case "--timeout":
                    if (i + 1 < args.Length &&
                        int.TryParse(args[++i], out int timeout) &&
                        timeout > 0)
                    {
                        options.TimeoutSeconds = timeout;
                    }

                    break;

                case "--max-tokens":
                    if (i + 1 < args.Length &&
                        int.TryParse(args[++i], out int maxTokens) &&
                        maxTokens > 0)
                    {
                        options.MaxTokens = maxTokens;
                    }

                    break;

                case "--categories":
                    if (i + 1 < args.Length)
                    {
                        options.Categories = args[++i];
                    }

                    break;

                case "--dry-run":
                    options.DryRun = true;

                    break;

                case "-h":
                case "--help":
                    options.ShowHelp = true;

                    break;

                default:
                    break;
            }
        }

        // Nếu user không chỉ định model, chọn model mặc định hợp lý cho
        // provider đang dùng.
        if (!modelExplicitlySet)
        {
            options.Model = DefaultModelFor(options.Provider);
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
        Dịch tự động các entry chưa được dịch, dùng Ollama chạy local.

        Usage:
          deadlockvn auto-translate [options]

        Options:
          --db <file>          File bản dịch (mặc định workspace/translations/vietnamese.json)
          --split              Dùng bộ bản dịch tách theo nhóm
          --split-dir <dir>    Thư mục chứa bộ bản dịch tách theo nhóm
          --provider <name>    Provider: ollama (mặc định), openai, groq,
                               openrouter, gemini
          --model <name>       Model (mặc định qwen2.5:7b-instruct)
          --endpoint <url>     Endpoint (mặc định theo provider)
          --api-key <key>      API key cho provider cloud. Nếu không truyền,
                               sẽ đọc từ biến môi trường DEADLOCKVN_API_KEY
                               hoặc từ file .env ở thư mục gốc dự án
                               (biến môi trường được ưu tiên, không bị .env ghi đè)
          --batch <n>          Số chuỗi mỗi lô (mặc định 30)
          --retries <n>        Số lần thử lại khi bản dịch hỏng (mặc định 3)
          --timeout <n>        Timeout mỗi lời gọi provider, giây (mặc định 300)
          --max-tokens <n>     Token tối đa mỗi lời gọi (mặc định 2048)
          --categories <list>  Nhóm cần dịch: heroes,items,abilities,ui,misc,voice
                               (mặc định: tất cả trừ voice)
          --dry-run            Chỉ báo số lượng cần dịch, không gọi provider

        Ví dụ:
          deadlockvn auto-translate --dry-run --categories heroes,items,misc
          deadlockvn auto-translate --categories heroes,items,misc
          deadlockvn auto-translate --categories abilities,ui --batch 20

          # Dùng API cloud (nhanh hơn nhiều lần so với Ollama local):
          # Key đọc từ biến môi trường DEADLOCKVN_API_KEY hoặc file .env
          $env:DEADLOCKVN_API_KEY = "sk-..."
          deadlockvn auto-translate --provider groq `
              --model llama-3.3-70b-versatile `
              --categories abilities,ui --batch 40
        """);
    }

    /// <summary>
    /// Endpoint mặc định của từng provider.
    /// <para>
    /// Tất cả đều dùng chuẩn <c>POST /v1/chat/completions</c> nên chỉ
    /// khác nhau ở địa chỉ host.
    /// </para>
    /// </summary>
    private static string DefaultEndpointFor(string provider) => provider switch
    {
        "ollama" => "http://localhost:11434",
        "openai" => "https://api.openai.com/v1",
        "groq" => "https://api.groq.com/openai/v1",
        "openrouter" => "https://openrouter.ai/api/v1",
        "gemini" => "https://generativelanguage.googleapis.com/v1beta/openai/v1",
        _ => "http://localhost:11434"
    };

    /// <summary>
    /// Model mặc định đề xuất cho từng provider.
    /// <para>
    /// Các model này đều mạnh về tiếng Việt và tuân thủ chỉ dẫn
    /// "trả về JSON" khá tốt.
    /// </para>
    /// </summary>
    private static string DefaultModelFor(string provider) => provider switch
    {
        "openai" => "gpt-4o-mini",
        "groq" => "llama-3.3-70b-versatile",
        "openrouter" => "google/gemini-2.0-flash-001",
        "gemini" => "gemini-2.0-flash",
        _ => "qwen2.5:7b-instruct"
    };

    private sealed class Options
    {
        public string? DatabasePath { get; set; }

        public bool Split { get; set; }

        public string? SplitDirectory { get; set; }

        public string Provider { get; set; } = "ollama";

        public string Model { get; set; } = "qwen2.5:7b-instruct";

        public string Endpoint { get; set; } = "http://localhost:11434";

        public string? ApiKey { get; set; }

        public int BatchSize { get; set; } = 30;

        public int MaxRetries { get; set; } = 3;

        public int TimeoutSeconds { get; set; } = 120;

        public int MaxTokens { get; set; } = 2048;

        public string? Categories { get; set; }

        public bool DryRun { get; set; }

        public bool ShowHelp { get; set; }
    }
}
