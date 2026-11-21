using System.Security.Cryptography;
using System.Text.Json;
using DeadlockVN.Core.Entities;

namespace DeadlockVN.Infrastructure.FileSystem;

public sealed class LocalizationInventoryService
{
    public async Task<int> ExtractAsync(
        string gameDirectory,
        string workspaceDirectory,
        CancellationToken cancellationToken = default)
    {
        var localizationDirectory = Path.Combine(
            gameDirectory,
            "game",
            "citadel",
            "resource",
            "localization"
        );

        if (!Directory.Exists(localizationDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Localization directory not found: {localizationDirectory}"
            );
        }

        var outputDirectory = Path.Combine(
            workspaceDirectory,
            "source",
            "original"
        );

        Directory.CreateDirectory(outputDirectory);

        var files = Directory
            .GetFiles(
                localizationDirectory,
                "*_english.txt",
                SearchOption.AllDirectories
            )
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var manifest = new List<LocalizationFileInfo>();

        foreach (var sourceFile in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(
                localizationDirectory,
                sourceFile
            );

            var destinationFile = Path.Combine(
                outputDirectory,
                relativePath
            );

            var destinationDirectory = Path.GetDirectoryName(
                destinationFile
            )!;

            Directory.CreateDirectory(destinationDirectory);

            // Copy original localization file
            File.Copy(
                sourceFile,
                destinationFile,
                overwrite: true
            );

            // Calculate SHA256
            await using var stream = File.OpenRead(destinationFile);

            var hash = await SHA256.HashDataAsync(
                stream,
                cancellationToken
            );

            manifest.Add(new LocalizationFileInfo
            {
                RelativePath = relativePath,
                FileName = Path.GetFileName(sourceFile),
                FileSize = new FileInfo(sourceFile).Length,
                Sha256 = Convert.ToHexString(hash)
            });

            Console.WriteLine($"[EXTRACTED] {relativePath}");
        }

        // Export manifest
        var manifestPath = Path.Combine(
            workspaceDirectory,
            "source",
            "manifest.json"
        );

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(
            manifest,
            jsonOptions
        );

        await File.WriteAllTextAsync(
            manifestPath,
            json,
            cancellationToken
        );

        Console.WriteLine();
        Console.WriteLine($"Total files: {manifest.Count}");
        Console.WriteLine($"Manifest: {manifestPath}");

        return manifest.Count;
    }
}