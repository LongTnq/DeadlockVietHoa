# Deadlock Vietnamese Localization

Bộ công cụ Việt hóa game **Deadlock** (Valve Source 2), viết bằng C# / .NET 10 LTS.

## Cấu trúc

```
src/
  DeadlockVN.Core           Entities, Enums, Interfaces, Common helpers
  DeadlockVN.Infrastructure  Parser KV1, Validator, File I/O, Translation store
  DeadlockVN.Application    Use cases (export / import / sync / report)
  DeadlockVN.CLI            Composition root
tests/
  DeadlockVN.UnitTests      128 unit test
docs/
  architecture.md           Kiến trúc, định dạng game, quyết định thiết kế
  translation-guidelines.md Hướng dẫn dịch
  glossary.md               Bảng thuật ngữ
workspace/
  source/original/           File localization gốc trích từ game
  translations/              Bộ bản dịch tiếng Việt (sinh ra bằng `export`)
```

`Application` không tham chiếu `Infrastructure` — composition root nằm ở `CLI`.

## Cài đặt

```powershell
dotnet build
dotnet test
```

## Lệnh

```powershell
# Thống kê file gốc
dotnet run --project src/DeadlockVN.CLI -- parse

# Tự kiểm tra validator trên bản gốc (phải PASS)
dotnet run --project src/DeadlockVN.CLI -- validate --self-check

# Tạo bộ bản dịch (tách theo nhóm, VO tách theo hero)
dotnet run --project src/DeadlockVN.CLI -- export --split

# Đồng bộ với bản cập nhật mới của game
dotnet run --project src/DeadlockVN.CLI -- sync --split

# Nạp bản dịch từ người dịch gửi
dotnet run --project src/DeadlockVN.CLI -- import <file> --target workspace/translations --split

# Xem tiến độ
dotnet run --project src/DeadlockVN.CLI -- report --split

# Kiểm tra chất lượng bản dịch
dotnet run --project src/DeadlockVN.CLI -- validate workspace/translations
```

## Bản dịch

Bộ bản dịch nằm ở `workspace/translations/`, tách theo nhóm nội dung:

| Thư mục | Nội dung |
|---|---|
| `abilities/` | Mô tả kỹ năng (3 983 entry) |
| `heroes/` | Tên hero (308) |
| `items/` | Tên & mô tả vật phẩm (619) |
| `ui/` | Giao diện, thuộc tính (8 046) |
| `voice/` | Lời thoại, tách tiếp theo từng hero (78 438) |
| `misc/` | Patch notes, sự kiện (63) |

Định danh một entry là cặp `(sourceFile, key)` — vì key `Language` tồn tại ở cả 10 file game.

Chi tiết về định dạng, quy trình dịch và các quyết định thiết kế: xem [`docs/architecture.md`](docs/architecture.md).

## Lưu ý bản quyền

Deadlock là sản phẩm của Valve. Dự án này không chứa hay phân phối file gốc của game
(`workspace/source/original/` bị `.gitignore` loại bỏ). Người dùng tự trích xuất file
từ bản cài của mình.
