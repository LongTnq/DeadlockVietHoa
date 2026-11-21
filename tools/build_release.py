"""
Dong goi ban phat hanh cho GameBanana.

Tao thu muc `release/deadlock-vietnamese/` chua:
    citadel_vietnamese/   <- VPK + gameinfo.gi (che thang vao game/)
    install.bat           <- tu dong patch gameinfo.gi + fonts.conf
    uninstall.bat         <- go mod, khoi phuc file goc
    README.md             <- huong dan cai dat

Roi nen lai thanh `release/deadlock-vietnamese.zip`.
"""
import io
import os
import shutil
import sys
import zipfile

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
GAME = r'E:\Steam\steamapps\common\Deadlock\game'
CITADEL = os.path.join(GAME, 'citadel')
LANG_DIR = os.path.join(CITADEL, '..', 'citadel_vietnamese')
LANG_DIR = os.path.normpath(LANG_DIR)

RELEASE = os.path.join(ROOT, 'release')
PKG = os.path.join(RELEASE, 'deadlock-vietnamese')
ZIP = os.path.join(RELEASE, 'deadlock-vietnamese.zip')

INSTALL_BAT = r"""@echo off
REM ============================================================
REM  Deadlock Vietnamese Localization - Cai dat
REM ============================================================
setlocal EnableDelayedExpansion

set "SCRIPT=%~dp0"
set "GAME="

REM --- Tu dong tim thu muc game ---
if exist "%ProgramFiles(x86)%\Steam\steamapps\common\Deadlock\game\citadel\gameinfo.gi" (
    set "GAME=%ProgramFiles(x86)%\Steam\steamapps\common\Deadlock\game"
)
if exist "%ProgramFiles%\Steam\steamapps\common\Deadlock\game\citadel\gameinfo.gi" (
    set "GAME=%ProgramFiles%\Steam\steamapps\common\Deadlock\game"
)
if exist "%SystemDrive%\SteamLibrary\steamapps\common\Deadlock\game\citadel\gameinfo.gi" (
    set "GAME=%SystemDrive%\SteamLibrary\steamapps\common\Deadlock\game"
)

REM --- Cho nguoi dung nhap tay ---
if "%GAME%"=="" (
    echo.
    echo   Khong tim thay thu muc game Deadlock.
    echo.
    set /p "GAME=Nhap duong dan thu muc game (vi du D:\Steam\steamapps\common\Deadlock\game): "
)

if not exist "%GAME%\citadel\gameinfo.gi" (
    echo.
    echo   [LOI] Duong dan khong hop le: %GAME%
    echo   Khong tim thay %GAME%\citadel\gameinfo.gi
    pause
    exit /b 1
)

echo.
echo   Thu muc game: %GAME%
echo.

REM ============================================================
REM  Buoc 1: Chep thu muc ngon ngu (VPK)
REM ============================================================
echo   [1/3] Chep thu muc "citadel_vietnamese"...

if not exist "%GAME%\citadel_vietnamese" mkdir "%GAME%\citadel_vietnamese"
copy /Y "%SCRIPT%citadel_vietnamese\*" "%GAME%\citadel_vietnamese\" >nul

if errorlevel 1 (
    echo   [LOI] Khong chep duoc!
    pause
    exit /b 1
)

echo         OK.

REM ============================================================
REM  Buoc 2: Them "vietnamese" vao SupportedLanguages
REM ============================================================
echo   [2/3] Dang ky ngon ngu Tieng Viet...

findstr /c:"\"vietnamese\"" "%GAME%\citadel\gameinfo.gi" >nul 2>&1
if not errorlevel 1 (
    echo         Da co san, bo qua.
) else (
    if not exist "%GAME%\citadel\gameinfo.gi.bak" (
        copy "%GAME%\citadel\gameinfo.gi" "%GAME%\citadel\gameinfo.gi.bak" >nul
        echo         Da sao luu gameinfo.gi.bak
    )
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "$p='%GAME%\citadel\gameinfo.gi';" ^
      "$t=[IO.File]::ReadAllText($p);" ^
      "$m=[regex]::Match($t,'SupportedLanguages\s*\{');" ^
      "if(-not $m.Success){Write-Error 'Khong tim thay SupportedLanguages';exit 1};" ^
      "$d=0;$end=0;" ^
      "for($k=$m.Index;$k -lt $t.Length;$k++){" ^
      "  if($t[$k] -eq '{'){$d++}elseif($t[$k] -eq '}'){$d--;if($d -eq 0){$end=$k;break}}};" ^
      "$ins='`t`t`"vietnamese`" `"3`"`r`n';" ^
      "[IO.File]::WriteAllText($p,$t.Insert($end,$ins));" ^
      "Write-Output '         OK - da them vietnamese.'"
)

REM ============================================================
REM  Buoc 3: Font fallback
REM ============================================================
echo   [3/3] Them font fallback (Arial + Segoe UI)...

set "FC=%GAME%\citadel\panorama\fonts\fonts.conf"

if not exist "%FC%" (
    echo         [BO QUA] Khong tim thay fonts.conf.
) else (
    findstr /c:"Deadlock Vietnamese Localization" "%FC%" >nul 2>&1
    if not errorlevel 1 (
        echo         Da co san, bo qua.
    ) else (
        if not exist "%FC%.bak" copy "%FC%" "%FC%.bak" >nul
        powershell -NoProfile -ExecutionPolicy Bypass -Command ^
          "$p='%FC%';" ^
          "$t=[IO.File]::ReadAllText($p);" ^
          "$anchor='<!-- Run infctl.sh or change the symlink in current directory instead of modifying this -->';" ^
          "$block=@'`n`t<!-- Deadlock Vietnamese Localization: font fallback`n`t-->`n`t<match>`n`t`t<edit name=`"family`" mode=`"append_last`" binding=`"strong`">`n`t`t`t<string>Arial</string>`n`t`t</edit>`n`t</match>`n`t<match>`n`t`t<edit name=`"family`" mode=`"append_last`" binding=`"strong`">`n`t`t\t<string>Segoe UI</string>`n`t`t</edit>`n`t</match>`n'@;" ^
          "if($t.Contains($anchor)){[IO.File]::WriteAllText($p,$t.Replace($anchor,$block+$anchor));Write-Output '         OK - da them font fallback.'}" ^
          "else{Write-Output '         [BO QUA] Khong tim thay diem chen.'}"
    )
)

echo.
echo ============================================================
echo   HOAN TAT!
echo.
echo   1. Dong hoan toan game neu dang mo.
echo   2. Mo Deadlock.
echo   3. Settings ^> Language ^> Tieng Viet.
echo ============================================================
echo.
pause
"""

UNINSTALL_BAT = r"""@echo off
REM ============================================================
REM  Deadlock Vietnamese Localization - Go mod
REM ============================================================
setlocal EnableDelayedExpansion

set "GAME="

if exist "%ProgramFiles(x86)%\Steam\steamapps\common\Deadlock\game\citadel\gameinfo.gi" (
    set "GAME=%ProgramFiles(x86)%\Steam\steamapps\common\Deadlock\game"
)
if exist "%ProgramFiles%\Steam\steamapps\common\Deadlock\game\citadel\gameinfo.gi" (
    set "GAME=%ProgramFiles%\Steam\steamapps\common\Deadlock\game"
)
if exist "%SystemDrive%\SteamLibrary\steamapps\common\Deadlock\game\citadel\gameinfo.gi" (
    set "GAME=%SystemDrive%\SteamLibrary\steamapps\common\Deadlock\game"
)

if "%GAME%"=="" (
    set /p "GAME=Nhap duong dan thu muc game: "
)

if not exist "%GAME%\citadel\gameinfo.gi" (
    echo [LOI] Duong dan khong hop le.
    pause
    exit /b 1
)

echo.
echo   [1/3] Xoa thu muc VPK...
if exist "%GAME%\citadel_vietnamese" (
    rmdir /s /q "%GAME%\citadel_vietnamese"
    echo         OK.
) else (
    echo         Khong co, bo qua.
)

echo   [2/3] Khoi phuc gameinfo.gi...
if exist "%GAME%\citadel\gameinfo.gi.bak" (
    copy /Y "%GAME%\citadel\gameinfo.gi.bak" "%GAME%\citadel\gameinfo.gi" >nul
    echo         Da khoi phuc tu gameinfo.gi.bak
) else (
    echo         [BO QUA] Khong co file .bak
)

echo   [3/3] Khoi phuc fonts.conf...
if exist "%GAME%\citadel\panorama\fonts\fonts.conf.bak" (
    copy /Y "%GAME%\citadel\panorama\fonts\fonts.conf.bak" "%GAME%\citadel\panorama\fonts\fonts.conf" >nul
    echo         Da khoi phuc tu fonts.conf.bak
) else (
    echo         [BO QUA] Khong co file .bak
)

echo.
echo   GO MOD XONG. Game se tro lai tieng Anh.
echo.
pause
"""

README_MD = """# Deadlock — Vietnamese Localization

Bản dịch tiếng Việt cho game Deadlock (Valve).

## Cài đặt

1. **Đóng hoàn toàn game** (nếu đang mở).
2. Giải nén file `.zip` này vào **một thư mục bất kỳ**.
3. **Chạy `install.bat`** (nhấp đúp chuột).
   - Script tự tìm thư mục game Steam.
   - Nếu không tìm thấy, nó sẽ hỏi bạn nhập tay đường dẫn
     (thường là `C:\\Program Files (x86)\\Steam\\steamapps\\common\\Deadlock\\game`).
4. Mở game → **Settings → Language → Tiếng Việt**.

### Yêu cầu

- Windows 10/11
- Steam phiên bản mới nhất của Deadlock

## Gỡ mod

Chạy `uninstall.bat` trong cùng thư mục.

## Nội dung đã dịch

| Phần | Trạng thái |
|---|---|
| Giao diện (UI) | ✅ ~99% |
| Mô tả hero + lore | ✅ ~100% |
| Kỹ năng (abilities) | ✅ ~100% |
| Vật phẩm (items) | ✅ 100% |
| Chỉ số (attributes) | ✅ 100% |
| Giọng nói (voice lines) | ❌ Chưa dịch (78k dòng) |

> **Lưu ý:** một số hình ảnh trong game (banner hero, biểu tượng thành tích)
> chỉ có bản tiếng Anh — đây là giới hạn của bản dịch phần chữ.

## Vấn đề thường gặp

**Menu không hiện "Tiếng Việt"**
: Chạy lại `install.bat`. Nếu vẫn không, hãy kiểm tra
  `citadel/gameinfo.gi` có dòng `"vietnamese" "3"` không.

**Chọn được ngôn ngữ nhưng chữ vẫn là tiếng Anh**
: Game đang đọc file localization cũ. Đóng game hoàn toàn (kiểm tra
  Task Manager không còn `deadlock.exe`) rồi mở lại.

**Chữ tiếng Việt hiện thành ô vuông □**
: Bản cài của bạn thiếu font. Script đã thêm Arial + Segoe UI vào
  `fonts.conf` — hãy khởi động lại game sau khi cài.

**Sau khi Steam cập nhật game, bản dịch biến mất**
: Steam ghi đè file `gameinfo.gi` khi verify. Chạy lại `install.bat`.

## Về mod này

- Không sửa file gốc của game — tất cả nằm trong thư mục riêng
  `citadel_vietnamese/` cùng 2 file cấu hình đã backup (`.bak`).
- Bản dịch nguồn mở, có thể tự chỉnh sửa.
"""


def log(msg):
    print(msg, flush=True)


def copy_lang_dir():
    """Chép thu muc citadel_vietnamese/ vao goi phat hanh."""
    dst = os.path.join(PKG, 'citadel_vietnamese')

    if os.path.exists(dst):
        shutil.rmtree(dst)

    os.makedirs(dst, exist_ok=True)

    if not os.path.isdir(LANG_DIR):
        log(f'  [LOI] Khong tim thay: {LANG_DIR}')
        log('  Chay "python tools/deploy_lang.py" truoc de tao VPK.')
        return False

    copied = 0

    for name in os.listdir(LANG_DIR):
        src = os.path.join(LANG_DIR, name)

        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(dst, name))
            copied += 1

    log(f'  da chep {copied} file VPK')

    if copied == 0:
        return False

    return True


def write_scripts():
    io.open(os.path.join(PKG, 'install.bat'), 'w',
             encoding='ascii', newline='\r\n').write(INSTALL_BAT)

    io.open(os.path.join(PKG, 'uninstall.bat'), 'w',
             encoding='ascii', newline='\r\n').write(UNINSTALL_BAT)

    io.open(os.path.join(PKG, 'README.md'), 'w',
             encoding='utf-8', newline='\n').write(README_MD)

    log('  da viet install.bat / uninstall.bat / README.md')


def make_zip():
    if os.path.exists(ZIP):
        os.remove(ZIP)

    with zipfile.ZipFile(ZIP, 'w', zipfile.ZIP_DEFLATED) as z:
        for dirpath, _dirs, files in os.walk(PKG):
            for name in files:
                full = os.path.join(dirpath, name)
                arc = os.path.relpath(full, RELEASE).replace('\\', '/')
                z.write(full, arc)

    log(f'  ZIP: {os.path.getsize(ZIP):,} bytes -> {ZIP}')


def main():
    log('=== DONG GOI BAN PHAT HANH ===')
    log('')

    if os.path.exists(PKG):
        shutil.rmtree(PKG)

    os.makedirs(PKG, exist_ok=True)

    if not copy_lang_dir():
        return 1

    write_scripts()
    make_zip()

    log('')
    log('=== HOAN TAT ===')
    log(f'  Thu muc : {PKG}')
    log(f'  File zip: {ZIP}')
    log('')
    log('  Upload file zip len GameBanana.')
    log('  GameBanana -> Deadlock -> Mods -> Submit a new mod')
    log('')

    return 0


if __name__ == '__main__':
    sys.exit(main())
