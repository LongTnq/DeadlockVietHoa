using DeadlockVN.CLI.Commands;

// Nạp biến môi trường từ file .env (nếu có) TRƯỚC khi xử lý lệnh,
// để API key không phải truyền trực tiếp trên dòng lệnh.
DotEnv.Load();

if (args.Length == 0)
{
    PrintUsage();

    return 0;
}

string command = args[0].ToLowerInvariant();

switch (command)
{
    case "parse":
        return await ParseCommand.ExecuteAsync(args[1..]);

    case "validate":
        return await ValidateCommand.ExecuteAsync(args[1..]);

    case "export":
    case "import":
    case "sync":
    case "report":
        return await TranslationCommand.ExecuteAsync(args);

    case "export-to-game":
        return await ExportToGameCommand.ExecuteAsync(args[1..]);

    case "import-from-staging":
        return await ImportFromStagingCommand.ExecuteAsync(args[1..]);

    case "package":
        return await PackageCommand.ExecuteAsync(args[1..]);

    case "pack-vpk":
        return await PackVpkCommand.ExecuteAsync(args[1..]);

    case "auto-translate":
        return await AutoTranslateCommand.ExecuteAsync(args[1..]);

    case "revalidate":
        return await RevalidateCommand.ExecuteAsync(args[1..]);

    case "review":
    case "review-export":
    case "review-import":
    case "approve":
        return await ReviewCommand.ExecuteAsync(args[1..]);

    case "-h":
    case "--help":
    case "help":
        PrintUsage();

        return 0;

    default:
        Console.Error.WriteLine($"Unknown command: {command}");

        PrintUsage();

        return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
    Deadlock Vietnamese Localization Toolkit

    Usage:
      deadlockvn parse     [<file-or-directory>]
      deadlockvn validate  <translations.json-or-dir> [--self-check]
      deadlockvn export    [--source <dir>] [--output <dir-or-file>] [--split]
      deadlockvn import    <file-or-dir> [--target <file-or-dir>] [--overwrite] [--split]
      deadlockvn sync      [--source <dir>] [--output <file-or-dir>] [--split]
      deadlockvn report    [<file-or-dir>] [--split]
      deadlockvn export-to-game [--source <dir>] [--output <dir>] [--split]
                              [--language <name>]
      deadlockvn import-from-staging [--staging <dir>] [--db <file>]
      deadlockvn package    [--source <dir>] [--output <dir>] [--translations <dir>]
                              [--game <game-dir>] [--language <name>] [--split]
      deadlockvn pack-vpk  [--staging <dir>] [--output <dir>] [--name <file.vpk>]
                              [--vpkedit <vpkeditcli.exe>]
      deadlockvn auto-translate [--db <file>] [--split] [--provider <name>]
                              [--model <name>] [--endpoint <url>] [--batch <n>]
                              [--retries <n>] [--timeout <n>]
                              [--categories <list>] [--dry-run]
      deadlockvn revalidate     [--db <file>] [--split] [--categories <list>] [--fix]
      deadlockvn review         [--db <file>] [--categories <list>] [--all] [--limit <n>]
      deadlockvn review-export  [--db <file>] [--output <file>] [--categories <list>]
      deadlockvn review-import  <file> [--db <file>]
      deadlockvn approve        [--db <file>] [--categories <list>] --yes

    Commands:
      parse      Parse KeyValues localization file(s) and print statistics.

      validate   Check translation quality: placeholder matching, HTML tag
                 structure, Vietnamese character support, length, whitespace,
                 duplicate keys. Accepts a single JSON file or a directory
                 of per-category files.
                 --self-check runs the validator against the original English
                 files to prove it produces no false positives.

      export     Create a translation database (JSON) from the original
                 English localization files.
                 --split writes one file per content category, and splits the
                 Voice category further into one file per hero.

      import     Import translations from another JSON file or directory into
                 the current translation database. Existing translations are
                 kept unless --overwrite is given.

      sync       Sync the translation database with a newer version of the
                 game files: report new keys, changed originals and removed
                 keys. Changed originals downgrade Approved entries to
                 Reviewed so they get looked at again.

      report     Show Vietnamese localization progress by category.

      export-to-game
                 Export the translation database into a staging folder that
                 mirrors the game layout (each *_english.txt becomes
                 *_vietnamese.txt), ready to be packed into a VPK with
                 VPKEdit. Only the value of each key is replaced — comments,
                 indentation, escape order and BOM of the original file are
                 preserved byte-for-byte.
                 --split reads the split translation database.
                 --language sets the language name written to the "Language"
                 header (default: vietnamese).

      package
                 Build a ready-to-pack mod folder: exports the 11
                 *_vietnamese.txt files (mirroring the game layout), and
                 optionally patches the game's gameinfo.gi (to register the
                 language in SupportedLanguages) and fonts.conf (to add a
                 font fallback for Vietnamese glyphs).
                 --game <game-dir> points at the Deadlock "game" folder to
                 enable the config patches. Both patches create a .bak
                 backup before touching the original file.
                 --no-patch-gameinfo / --no-patch-fonts skip a patch.

      import-from-staging
                 Read the *_vietnamese.txt files in the staging folder back
                 into the translation database. Use this after hand-editing
                 the staged files to fix wording — the edits are validated
                 (placeholders, HTML structure) and marked Approved.
                 Re-running export-to-game afterwards would overwrite the
                 hand edits, so always import first.

      pack-vpk
                 Pack the staging folder into a VPK file using
                 vpkeditcli.exe (the command-line tool shipped with
                 VPKEdit). The "resource" folder of the staging directory
                 becomes the VPK root, so the paths inside the VPK are
                 resource/localization/... as required by the game.
                 After packing, the VPK tree is verified to contain the
                 *_vietnamese.txt files.
                 --vpkedit points at a custom vpkeditcli.exe path.

      auto-translate
                 Translate the remaining pending entries automatically using
                 a pluggable provider. Only 'ollama' is implemented so far
                 (local, no API key). Placeholders and HTML tags are split
                 out before sending, so the model never sees them and
                 cannot damage them; every result is validated afterwards
                 and broken translations are retried and never stored.
                 Machine translations are saved with status 'Reviewed', so
                 they still need a human pass before being 'Approved'.
                 --dry-run reports how many entries would be translated.
                 --categories restricts the work, e.g. --categories
                 heroes,items,misc.

      revalidate
                 Re-check the translations already stored against the
                 current validation rules and report the ones that fail
                 (missing placeholders, broken HTML, leftover CJK or
                 French characters, still-untranslated English).
                 --fix downgrades those entries back to 'Pending' and
                 clears their translation so they get translated again.

      review        Print the entries that are waiting for review, grouped
                 by content category. Only machine-translated entries
                 (status Reviewed) are shown unless --all is given.

      review-export Export the entries waiting for review into a JSON file
                 that a human translator can edit. The translator fills in
                 the 'newTranslation' and 'newStatus' fields.

      review-import Read back a review file produced by review-export and
                 apply the edits to the translation database. Entries with
                 newStatus = Approved are counted as approved.

      approve       Mark all Reviewed entries as Approved in bulk. Requires
                 --yes to confirm.

    Notes:
      - If <file-or-directory> is omitted, the toolkit looks for
        'workspace/source/original' starting from the current directory.
      - Default translation database: workspace/translations/vietnamese.json
      - Default split database: workspace/translations/
      - Default staging folder: workspace/staging/localization
      - API key cho provider dịch đọc từ biến môi trường
        DEADLOCKVN_API_KEY, hoặc từ file .env ở thư mục gốc dự án.
        Biến môi trường được ưu tiên và không bị .env ghi đè.

    Examples:
      deadlockvn parse ".\workspace\source\original"
      deadlockvn validate --self-check
      deadlockvn export --split
      deadlockvn report --split
      deadlockvn export-to-game --split
      deadlockvn package --game "E:\Steam\steamapps\common\Deadlock\game"
      deadlockvn pack-vpk
      deadlockvn auto-translate --dry-run --categories heroes,items,misc
    """);
}
