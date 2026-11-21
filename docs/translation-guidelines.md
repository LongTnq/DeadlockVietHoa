# Hướng dẫn dịch Deadlock sang tiếng Việt

Tài liệu này dành cho người dịch thủ công, và đồng thời là đặc tả mà
bản dịch máy sinh cũng phải tuân theo.

---

## 1. Nguyên tắc chung

### 1.1 Dịch nghĩa, không dịch cấu trúc

Bản dịch phải **thay thế nội dung văn bản** nhưng **giữ nguyên toàn bộ
cấu trúc kỹ thuật** của chuỗi gốc. Trong bản dịch Deadlock, cấu trúc kỹ
thuật chiếm phần lớn nội dung và là phần dễ hỏng nhất.

### 1.2 Không dịch thừa

Không thêm lời giải thích, không thêm dấu ngoặc kép bao quanh kết quả,
không thêm chú thích, không thêm "Dịch:" hay tương tự. Chỉ trả về bản
dịch.

### 1.3 Giữ nguyên khoảng trắng đầu/cuối

Nếu chuỗi gốc có khoảng trắng ở đầu hoặc cuối (dùng để canh chữ trong
Panorama), bản dịch phải giữ nguyên.

---

## 2. Placeholder — phần dễ hỏng nhất

### 2.1 Các dạng placeholder

| Dạng | Ví dụ | Được dịch? |
|------|-------|-------------|
| Braced | `{s:Damage}` | ❌ Không |
| Braced có khoá | `{g:citadel_inline_attribute:'Pulling'}` | ❌ Không (toàn bộ) |
| Plural | `{count, plural, one{Item} other{Items}}` | ⚠️ Chỉ phần trong `one{}`/`other{}` |
| Printf | `%s1`, `%s2` | ❌ Không |
| Tên | `%partner%` | ❌ Không |
| Boon | `{#}` | ❌ Không |

### 2.2 Vì sao quan trọng

Nếu mất placeholder `{s:Damage}`, game sẽ hiển thị chuỗi rỗng ở chỗ đó —
người chơi không thấy con số sát thương. Nếu placeholder bị **đổi tên**
(`{s:Damage}` → `{s:damage}`), game hiển thị đúng nhưng sai giá trị.

### 2.3 Quy tắc "khoá tra cứu"

Trong `{g:citadel_inline_attribute:'Pulling'}`, chuỗi `Pulling` là **khoá
tra cứu trong bảng thuộc tính của game** — dịch nó sẽ làm game không tìm
thấy giá trị. Giữ nguyên y, kể cả khi nghe có vẻ lạ.

### 2.4 Quy tắc cho token plural

Token plural là ngoại lệ duy nhất: phần bên trong `one{...}` và `other{...}`
**là văn bản cần dịch**.

```
Gốc:   {num_commends, plural, one{player} other{players}} commended you
Dịch:  {num_commends, plural, one{người chơi} other{người chơi}} đã khen ngợi bạn
```

Tên biến (`num_commends`) giữ nguyên. Cấu trúc `one{}` / `other{}` giữ
nguyên. Chỉ nội dung bên trong được dịch.

---

## 3. Thẻ HTML / Panorama

### 3.1 Danh sách thẻ hợp lệ

```
<b>  <i>  <span>  </span>  <br>  <li>  <ul>  <ol>  <a>  <p>  <div>
<em>  <strong>  <u>  <s>  <sub>  <sup>  <h1>–<h6>  <img>  <hr>  <pre>
<code>  <font>  <table>  <tr>  <td>  <th>  <blockquote>  <center>  <small>
<panel>
```

### 3.2 Quy tắc

- Số lượng và thứ tự thẻ mở/đóng phải **giống hệt** bản gốc.
- Không thêm thẻ mới, không bỏ thẻ cũ.
- Thuộc tính (ví dụ `class="highlight"`) được phép giữ nguyên.

### 3.3 Thẻ ngữ cảnh trong VO — KHÔNG phải HTML

Trong file lời thoại (`citadel_generated_vo`), có rất nhiều thẻ dạng:

```
<laughs>   <battle cry>   <None>   <in>   <whisper>
```

Đây là **chỉ dẫn diễn xuất**, không phải HTML. Chúng **không được dịch** và
không được coi là lỗi cấu trúc thẻ.

---

## 4. Văn phong

### 4.1 Giao diện người dùng

- **Ngắn gọn.** UI là chỗ người chơi nhìn nhanh giữa trận — dịch thừa gây
  rối.
- Dùng **dạng mệnh lệnh không dấu chấm** khi bản gốc là mệnh lệnh:
  - `Buy Now` → `Mua ngay`
  - `Cancel` → `Huỷ`
  - `Confirm` → `Xác nhận`
- Với nút (button), dùng dạng **động từ ngắn** hoặc mệnh lệnh:
  - `Cancel` → `Huỷ` (không `Hủy bỏ`)

### 4.2 Mô tả vật phẩm & kỹ năng

- Dùng câu đầy đủ, có dấu chấm nếu bản gốc có.
- Giữ số liệu ở dạng số La Mã hoặc số tự nhiên như bản gốc.
- Giữ đơn vị: `+{s:SpiritPower}%` → `+{s:SpiritPower}%` (không đổi đơn vị).

### 4.3 Lời thoại (VO)

- **Không dịch máy.** Lời thoại cần giữ giọng điệu, nhịp điệu, cách nhân
  vật nói. Bản dịch máy gần như luôn hỏng phần này.
- Nếu dịch, phải giữ nguyên các thẻ ngữ cảnh.

---

## 5. Văn hoá

### 5.1 Xưng hô

- Giao diện → **không xưng hô**, dùng mệnh lệnh / danh từ.
- Vật phẩm, kỹ năng → **không xưng hô**.
- Lời thoại → dùng "tôi", "mình", "chúng ta" tuỳ nhân vật.

### 5.2 Không Việt hóa quá mức

- Không dùng tiếng Việt cổ ("ngươi", "phiền", "đại gia") — game hiện đại,
  cần giọng tự nhiên.
- Không dịch slang tiếng Anh sang tiếng Anh khác ("OP" → "OP", không phải
  "bất đắc kỳ tử").

---

## 6. Những thứ KHÔNG được dịch

| Loại | Ví dụ |
|------|-------|
| Tên hero | `Bebop`, `Haze`, `Vindicta` |
| Tên vật phẩm | `Close Quards`, `Phantom Strike` |
| Tên chế độ chơi | `Payload`, `Deathmatch` |
| Chữ viết tắt | `HP`, `DPS`, `FPS`, `UI`, `AoE` |
| Phím tắt | `Shift`, `Ctrl`, `Alt`, `E`, `Q` |
| Tên trong lore | `Yamato`, tên thần, tên địa danh |
| Thẻ ngữ cảnh VO | `<laughs>`, `<battle cry>` |
| Khoá tra cứu placeholder | `Pulling` trong `{g:...:'Pulling'}` |
| Tên file / key | `Citadel_Hud_Quickbuy` |

> **Ngoại lệ:** tên **upgrade** (vật phẩm nâng cấp) nên dịch, vì chúng mô
> tả cơ chế chứ không phải tên riêng — xem `docs/glossary.md`.

---

## 7. Checklist review

Khi duyệt một bản dịch, kiểm tra lần lượt:

- [ ] Placeholder đầy đủ, đúng tên, đúng số lần xuất hiện?
- [ ] Thẻ HTML/Panorama giữ nguyên số lượng và thứ tự?
- [ ] Thẻ ngữ cảnh VO giữ nguyên?
- [ ] Không thêm/bớt nội dung ngoài ý muốn?
- [ ] Không dịch tên riêng (hero, vật phẩm, chế độ)?
- [ ] Văn phong đúng nhóm nội dung (UI ngắn gọn, mô tả đầy đủ)?
- [ ] Tiếng Việt có dấu đầy đủ, không lỗi chính tả?
- [ ] Khoảng trắng đầu/cuối giữ nguyên?

Lệnh `deadlockvn validate` kiểm tra tự động 3 mục đầu (placeholder, thẻ
HTML, ký tự hỗ trợ) — chạy trước khi review thủ công.
