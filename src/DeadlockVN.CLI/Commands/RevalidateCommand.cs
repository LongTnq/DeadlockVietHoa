using DeadlockVN.Core.Entities;
using DeadlockVN.Core.Enums;
using DeadlockVN.Core.Interfaces;
using DeadlockVN.Infrastructure.Localization;
using DeadlockVN.Infrastructure.Translation;

namespace DeadlockVN.CLI.Commands;

/// <summary>
/// Lệnh <c>revalidate</c>: kiểm tra lại toàn bộ bản dịch đã lưu bằng
/// <see cref="TranslationGuard"/> và hạ những entry hỏng về trạng thái
/// <c>Pending</c> để dịch lại.
/// <para>
/// <b>Vì sao cần:</b> guard được áp dụng lúc ghi, nhưng bộ luật đã mạnh
/// dần theo thời gian (thêm phát hiện CJK, thêm phát hiện dịch nhầm sang
/// tiếng Pháp, thêm phát hiện lệch chỉ số). Những entry đã dịch bằng bộ
/// luật cũ có thể vi phạm bộ luật mới — lệnh này tìm và hạ chúng.
/// </para>
/// <para>
/// Mặc định chỉ <b>báo cáo</b>. Thêm <c>--fix</c> để thực sự hạ entry về
/// <c>Pending</c> (xóa bản dịch và ghi lại file).
/// </para>
/// </summary>
internal static class RevalidateCommand
{
    private const string DefaultDatabase = "workspace/translations/vietnamese.json";

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

        if (options.Split)
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
                    $"Không tìm thấy file bản dịch: {databasePath}");

                return 1;
            }

            document = await store.LoadAsync(databasePath);
        }

        var categories = ParseCategories(options.Categories);

        var guard = new TranslationGuard();

        var problems = new List<(TranslationEntry Entry, string Reason)>();

        // Bản dịch nào trùng với NỘI DUNG GỐC của một entry khác -> lệch
        // chỉ số trong lô dịch.
        //
        // Đây là lỗi nguy hiểm nhất của dịch máy: model bỏ sót một phần
        // tử giữa lô nhưng vẫn trả về đủ số lượng, khiến mọi bản dịch
        // từ vị trí đó trở đi bị gán nhầm. Ví dụ `Language` nhận bản dịch
        // của "Extended Magazine".
        var originalsBySourceFile = new Dictionary<
            string,
            Dictionary<string, List<TranslationEntry>>>(
            StringComparer.Ordinal);

        foreach (var entry in document.Entries)
        {
            string original = entry.OriginalText.Trim();

            if (original.Length == 0)
            {
                continue;
            }

            if (!originalsBySourceFile.TryGetValue(
                    entry.SourceFile,
                    out var byOriginal))
            {
                byOriginal = new Dictionary<string, List<TranslationEntry>>(
                    StringComparer.Ordinal);

                originalsBySourceFile[entry.SourceFile] = byOriginal;
            }

            if (!byOriginal.TryGetValue(original, out var list))
            {
                list = new List<TranslationEntry>();

                byOriginal[original] = list;
            }

            list.Add(entry);
        }

        foreach (var entry in document.Entries)
        {
            if (entry.Status == TranslationStatus.Pending ||
                string.IsNullOrWhiteSpace(entry.TranslatedText))
            {
                continue;
            }

            if (categories is not null && categories.Count > 0 &&
                !categories.Contains(entry.Category) &&
                !categories.Contains(LocalizationCategory.All))
            {
                continue;
            }

            var check = guard.Check(entry.OriginalText, entry.TranslatedText);

            if (!check.IsValid)
            {
                problems.Add((entry, string.Join(" | ", check.Errors)));

                continue;
            }

            // Kiểm tra lệch chỉ số.
            string translated = entry.TranslatedText.Trim();

            // Dấu hiệu lệch chỉ số: bản dịch của entry này TRÙNG KHỚP
            // với nội dung gốc của một entry khác trong cùng file, mà bản
            // gốc của chính entry này lại KHÁC đi.
            //
            // Cần so sánh trực tiếp "bản gốc của entry này" với "bản gốc
            // mà bản dịch đang khớp". Nếu chúng khác nhau -> model đã dịch
            // nhầm sang entry khác.
            //
            // Lưu ý: KHÔNG được chỉ dựa vào độ dài bản dịch. Ví dụ
            // "Antialiasing" -> "Anti-aliasing" là bản dịch đúng, dù
            // "Anti-aliasing" cũng là nội dung gốc của entry khác.
            if (originalsBySourceFile.TryGetValue(
                    entry.SourceFile,
                    out var byOriginal) &&
                byOriginal.TryGetValue(translated, out var owners))
            {
                foreach (TranslationEntry owner in owners)
                {
                    if (owner.Key == entry.Key)
                    {
                        continue;
                    }

                    if (string.Equals(
                            owner.OriginalText.Trim(),
                            entry.OriginalText.Trim(),
                            StringComparison.Ordinal))
                    {
                        // Nội dung gốc giống nhau -> bản dịch giống nhau là
                        // hợp lệ, không phải lệch.
                        continue;
                    }

                    // Bản dịch khớp với gốc của entry khác, trong khi gốc
                    // của entry này khác -> nghi ngờ lệch chỉ số.
                    //
                    // Nhưng trước khi báo, so sánh bản dịch của entry này
                    // với bản dịch của entry kia. Nếu hai bản dịch GIỐNG
                    // NHAU thì rất có thể chỉ là trùng lặp tự nhiên của
                    // ngôn ngữ (ví dụ "Walkers" -> "Walker" và "Walker" ->
                    // "Walker"), không phải lệch chỉ số.
                    if (string.Equals(
                            owner.TranslatedText.Trim(),
                            translated,
                            StringComparison.Ordinal))
                    {
                        // Cùng bản gốc khác nhau mà cùng bản dịch ->
                        // hợp lệ (số ít/số nhiều, dấu gạch nối, v.v.)
                        continue;
                    }

                    // Trường hợp số ít/số nhiều: "Walkers" và "Walker" là
                    // hai chuỗi gốc khác nhau nhưng gần như tương đương.
                    // Nếu chuỗi gốc chuẩn hoá (bỏ s/nhiều) trùng nhau thì
                    // bản dịch trùng nhau là hợp lý.
                    if (NormalizePlural(owner.OriginalText) ==
                        NormalizePlural(entry.OriginalText))
                    {
                        continue;
                    }

                    problems.Add((
                        entry,
                        "Bản dịch trùng với nội dung gốc của entry khác " +
                        $"({owner.Key}) — có thể đã lệch chỉ số trong lô dịch."));

                    break;
                }
            }
        }

        Console.WriteLine("=== REVALIDATE ===");
        Console.WriteLine($"Cơ sở dữ liệu : {databasePath}");
        Console.WriteLine($"Tổng entry    : {document.Entries.Count}");
        Console.WriteLine($"Entry hỏng    : {problems.Count}");
        Console.WriteLine();

        if (problems.Count == 0)
        {
            Console.WriteLine("Không có entry nào vi phạm luật.");

            return 0;
        }

        Console.WriteLine("=== Theo nhóm ===");

        foreach (var group in problems
            .GroupBy(p => p.Entry.Category)
            .OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {group.Key,-10} {group.Count(),6}");
        }

        Console.WriteLine();
        Console.WriteLine("=== Chi tiết (50 dòng đầu) ===");

        foreach (var (entry, reason) in problems.Take(50))
        {
            Console.WriteLine(
                $"  [{entry.Category}] {entry.Key} " +
                $"({entry.SourceFile})");
            Console.WriteLine($"      {reason}");
        }

        if (problems.Count > 50)
        {
            Console.WriteLine(
                $"  ... và {problems.Count - 50} entry khác.");
        }

        if (!options.Fix)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Đây chỉ là báo cáo. Thêm --fix để hạ các entry này về " +
                "'Pending' để dịch lại.");

            return 0;
        }

        foreach (var (entry, _) in problems)
        {
            entry.Status = TranslationStatus.Pending;
            entry.TranslatedText = string.Empty;
        }

        if (options.Split)
        {
            await store.SaveSplitAsync(
                document,
                options.SplitDirectory ?? ResolveDefaultSplitDirectory());
        }
        else
        {
            await store.SaveAsync(document, databasePath);
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Đã hạ {problems.Count} entry về 'Pending'. " +
            "Chạy 'auto-translate' để dịch lại.");

        return 0;
    }

    /// <summary>
    /// Chuẩn hoá chuỗi để so sánh "số ít / số nhiều".
    /// <para>
    /// "Walkers" và "Walker" là hai chuỗi gốc khác nhau, nhưng bản dịch
    /// tiếng Việt của chúng rất dễ trùng nhau. Khi phát hiện lệch chỉ số,
    /// cần bỏ qua loại khác biệt này để tránh báo nhầm.
    /// </para>
    /// </summary>
    private static string NormalizePlural(string text)
    {
        string value = text.Trim().ToLowerInvariant();

        if (value.EndsWith("ies", StringComparison.Ordinal) &&
            value.Length > 3)
        {
            return value[..^3] + "y";
        }

        if (value.EndsWith("es", StringComparison.Ordinal) &&
            value.Length > 2)
        {
            return value[..^2];
        }

        if (value.EndsWith('s') && value.Length > 1)
        {
            return value[..^1];
        }

        return value;
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

        return "workspace/translations";
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();

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

                case "--categories":
                    if (i + 1 < args.Length)
                    {
                        options.Categories = args[++i];
                    }

                    break;

                case "--fix":
                    options.Fix = true;

                    break;

                case "-h":
                case "--help":
                    options.ShowHelp = true;

                    break;

                default:
                    break;
            }
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
        Kiểm tra lại bản dịch đã lưu bằng bộ luật kiểm tra hiện hành.

        Usage:
          deadlockvn revalidate [options]

        Options:
          --db <file>          File bản dịch (mặc định workspace/translations/vietnamese.json)
          --split              Dùng bộ bản dịch tách theo nhóm
          --split-dir <dir>    Thư mục chứa bộ bản dịch tách theo nhóm
          --categories <list>  Chỉ kiểm tra nhóm này: heroes,items,abilities,ui,misc
          --fix                Hạ entry hỏng về 'Pending' để dịch lại
                              (mặc định chỉ báo cáo, không ghi file)

        Ví dụ:
          deadlockvn revalidate
          deadlockvn revalidate --categories heroes,items,misc --fix
          deadlockvn revalidate --split --fix
        """);
    }

    private sealed class Options
    {
        public string? DatabasePath { get; set; }

        public bool Split { get; set; }

        public string? SplitDirectory { get; set; }

        public string? Categories { get; set; }

        public bool Fix { get; set; }

        public bool ShowHelp { get; set; }
    }
}
