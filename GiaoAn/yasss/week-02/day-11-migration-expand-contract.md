# Day 11 - Production Migration, Expand-Contract, Rollback Thinking

## 1. Câu chuyện đời thường

Cửa hàng Cam Sành đang mở bán.

Bro muốn sửa quầy thanh toán.

Không thể:

```text
Đập quầy cũ ngay giữa giờ bán.
```

Cách an toàn:

```text
1. Mở quầy mới song song.
2. Cho nhân viên dùng dần.
3. Kiểm tra ổn.
4. Sau đó mới bỏ quầy cũ.
```

Đó là tinh thần expand-contract migration.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Migration | Thay đổi schema DB | thêm cột/bảng/index |
| Schema | Cấu trúc DB | bảng/cột/index |
| Backward-compatible | App cũ vẫn chạy | thêm cột nullable |
| Breaking change | Làm app cũ vỡ | rename/drop cột ngay |
| Expand-contract | Mở rộng trước, dọn cũ sau | add -> backfill -> switch -> drop |
| Backfill | Điền dữ liệu cũ vào field mới | fill OrderStatus |
| Rollback | Kế hoạch quay lại | rollback app/schema |
| Batch migration | Chạy từng lô nhỏ | update 10k rows/lần |

---

## 3. Vì sao migration production khó?

Vì lúc deploy có thể tồn tại:

```text
App v1 cũ đang chạy.
App v2 mới đang chạy.
DB schema mới/cũ đang chuyển tiếp.
```

Nếu schema mới không tương thích app cũ, app cũ có thể chết.

Ví dụ nguy hiểm:

```text
App v1 đọc cột Status.
Migration rename Status -> OrderStatus ngay.
App v1 vẫn chạy và query Status.
=> lỗi.
```

---

## 4. Expand-Contract Pattern

### Bước 1: Expand

Thêm cấu trúc mới nhưng chưa phá cấu trúc cũ.

```text
Add column OrderStatus nullable.
Giữ Status cũ.
```

### Bước 2: Backfill / Dual write nếu cần

Điền dữ liệu cũ vào field mới.

```text
OrderStatus = Status
```

Có thể app mới ghi cả 2 field trong giai đoạn chuyển tiếp nếu cần.

### Bước 3: Switch

Deploy app mới đọc field mới.

```text
App v2 đọc OrderStatus.
```

### Bước 4: Contract

Sau khi chắc không còn app cũ, dọn field cũ.

```text
Drop Status.
```

---

## 5. Backfill production

Backfill dữ liệu lớn cần cẩn thận.

Không nên:

```text
Update 50 triệu dòng một phát không kiểm soát.
```

Nên nghĩ:

```text
Chạy theo batch.
Có checkpoint.
Có monitoring.
Có thể resume.
Tránh lock lớn.
Chạy ngoài giờ cao điểm nếu cần.
```

---

## 6. Production index caution

Tạo index lớn trong production có thể gây:

```text
Tốn CPU/IO.
Lock hoặc làm chậm write tùy DB/cách tạo.
Tăng replication lag.
```

Cần nghĩ:

```text
DB hỗ trợ online/concurrent index không?
Index tạo vào thời điểm nào?
Có monitoring không?
Có rollback/plan nếu chậm không?
```

Không cần nhớ command cụ thể cho mọi DB, nhưng phải có mindset.

---

## 7. Rollback Thinking

Rollback app thường dễ hơn rollback DB.

Vì DB có data mới, schema mới, có thể đã được app mới ghi vào.

Do đó migration nên backward-compatible.

Câu nhớ:

```text
Đừng thiết kế migration mà rollback app xong app cũ không đọc được DB nữa.
```

---

## 8. MicroShop connection

```text
Thêm WebhookLogs cho PaymentService.
Thêm PayloadHash/SignatureValid cho webhook audit.
Thêm EventVersion vào integration event.
Thêm index ProviderEventId.
Thêm field OrderStatus mới trong read model.
```

Khi thêm field event/API:

```text
Thêm optional trước.
Consumer cũ bỏ qua field lạ.
Không rename/remove field ngay.
```

---

## 9. Interview answer mẫu

```text
Production migration cần backward compatibility vì trong quá trình deploy có thể có app version cũ và mới chạy song song. Em thường dùng expand-contract: thêm cấu trúc mới trước mà chưa phá cũ, backfill hoặc dual-write nếu cần, chuyển app sang đọc cấu trúc mới, rồi sau khi chắc không còn app cũ mới drop cấu trúc cũ. Với dữ liệu lớn, backfill nên chạy theo batch và có monitoring. Tạo index lớn trong production cũng cần cẩn thận vì có thể gây lock hoặc tải cao.
```

## 10. Checkpoint

```text
1. Vì sao migration production khác local?
2. Old/new app compatibility là gì?
3. Breaking change schema là gì?
4. Expand-contract gồm mấy bước?
5. Backfill nên chạy thế nào nếu dữ liệu lớn?
6. Tạo index lớn có risk gì?
7. Vì sao rollback DB khó hơn rollback app?
```

## 11. Flashcards

```text
Migration = đổi schema.
Schema = bảng/cột/index.
Backward-compatible = app cũ vẫn chạy.
Breaking change = làm app cũ vỡ.
Expand = thêm mới không phá cũ.
Backfill = điền dữ liệu cũ.
Contract = dọn cái cũ.
Batch = chạy từng lô nhỏ.
Rollback thinking = nghĩ đường lui trước.
```
