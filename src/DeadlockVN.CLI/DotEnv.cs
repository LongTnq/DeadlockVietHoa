/// <summary>
/// Nạp biến môi trường từ file <c>.env</c> ở thư mục gốc dự án.
/// <para>
/// <b>Vì sao cần:</b> API key của provider dịch (OpenRouter, OpenAI,
/// Groq…) là thông tin bí mật. Nếu truyền bằng tham số dòng lệnh
/// (<c>--api-key</c>) thì nó bị ghi vào lịch sử PowerShell và có thể
/// lọt vào ảnh chụp màn hình hay log. File <c>.env</c> nằm trong
/// <c>.gitignore</c> nên an toàn hơn nhiều.
/// </para>
/// <para>
/// <b>Quy tắc:</b> biến đã có sẵn trong môi trường thì <b>không</b> bị
/// ghi đè — nhờ vậy người dùng vẫn có thể ghi đè tạm thời bằng
/// <c>$env:DEADLOCKVN_API_KEY = "..."</c>.
/// </para>
/// </summary>
internal static class DotEnv
{    /// <summary>
    /// Tên file cấu hình.
    /// </summary>
    private const string FileName = ".env";

    /// <summary>
    /// Nạp <c>.env</c> từ thư mục hiện tại và các thư mục cha.
    /// </summary>
    /// <returns>Đường dẫn file đã nạp, hoặc <c>null</c> nếu không có.</returns>
    public static string? Load()
    {
        string? path = FindFile();

        if (path is null)
        {
            return null;
        }

        try
        {
            foreach (string line in File.ReadAllLines(path))
            {
                if (!TryParse(line, out string name, out string value))
                {
                    continue;
                }

                // Không ghi đè biến đã tồn tại trong môi trường.
                if (Environment.GetEnvironmentVariable(name) is not null)
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(name, value);
            }

            return path;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tìm file <c>.env</c> bắt đầu từ thư mục hiện tại đi lên.
    /// </summary>
    private static string? FindFile()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, FileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Phân tích một dòng <c>NAME=value</c>.
    /// </summary>
    private static bool TryParse(
        string line,
        out string name,
        out string value)
    {
        name = string.Empty;
        value = string.Empty;

        string trimmed = line.Trim();

        if (trimmed.Length == 0 ||
            trimmed.StartsWith('#') ||
            !trimmed.Contains('='))
        {
            return false;
        }

        int separator = trimmed.IndexOf('=');

        name = trimmed[..separator].Trim();

        if (name.Length == 0)
        {
            return false;
        }

        value = trimmed[(separator + 1)..].Trim();

        // Bỏ dấu nháy bao quanh nếu có.
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        return value.Length > 0;
    }
}
