# Day 10 - Optimistic Concurrency, Unique Constraint, Idempotency Key

## 1. Câu chuyện đời thường

Có một phiếu order trên bảng.

```text
A đọc: Order đang Pending.
B cũng đọc: Order đang Pending.

A đổi thành Paid.
B đổi thành Cancelled.
```

Nếu không kiểm soát, người ghi sau có thể đè người ghi trước.

Đây là vấn đề concurrency.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Concurrency | Nhiều việc xảy ra cùng lúc | 2 request update cùng order |
| Lost update | Update bị ghi đè mất | Paid bị Cancelled đè |
| Optimistic concurrency | Khi ghi mới kiểm tra có ai sửa chưa | RowVersion |
| RowVersion/Version | Số phiên bản record | version = 5 |
| Unique constraint | DB không cho trùng | ProviderEventId unique |
| Idempotency | Làm lại không tạo thêm side effect | Retry checkout không tạo 2 order |
| Idempotency-Key | Mã chống request trùng | checkout-key-abc |
| Duplicate webhook | Provider gửi lại cùng event | evt_001 gửi 2 lần |
| 409 Conflict | Xung đột trạng thái/dữ liệu | Version mismatch |

---

## 3. Optimistic Concurrency

Optimistic concurrency = không khóa record từ đầu, nhưng khi update thì kiểm tra version.

Flow:

```text
A đọc Order version = 5.
B cũng đọc Order version = 5.

A update Paid nếu version vẫn = 5.
DB update thành công, version = 6.

B update Cancelled nếu version vẫn = 5.
DB thấy version hiện tại = 6.
B fail.
```

Cách xử lý thường gặp:

```text
Trả 409 Conflict.
Hoặc retry nếu nghiệp vụ cho phép.
Hoặc reload dữ liệu mới và yêu cầu user xác nhận lại.
```

---

## 4. Lost update

Lost update = update của người này bị người khác ghi đè mất.

Ví dụ:

```text
A chuyển Order -> Paid.
B dựa trên dữ liệu cũ chuyển Order -> Cancelled.
Kết quả cuối Cancelled.
Thông tin Paid bị mất.
```

Optimistic concurrency giúp phát hiện việc này.

---

## 5. Unique Constraint

Unique constraint = luật DB không cho trùng.

Ví dụ:

```text
ProviderEventId không được trùng.
IdempotencyKey không được trùng.
Email không được trùng.
```

Vì sao cần DB giữ luật?

```text
Check bằng code trước rồi insert sau vẫn có race condition.
Hai request cùng check "chưa có", rồi cùng insert.
DB unique constraint là chốt cuối đáng tin.
```

---

## 6. Idempotency

Idempotency = làm lại nhiều lần nhưng kết quả cuối vẫn như một lần.

Ví dụ:

```text
Khách bấm checkout 2 lần do mạng lag.
Hệ thống không được tạo 2 order.
```

Idempotency-Key = mã để nhận diện cùng một request logic.

Flow:

```text
Client gửi Idempotency-Key = abc123.
Server xử lý lần đầu và lưu key + response/result.
Client retry cùng key.
Server không tạo side effect mới.
Server có thể trả lại response cũ.
```

---

## 7. Duplicate webhook behavior

Payment provider thường retry webhook.

Ví dụ:

```text
Provider gửi PaymentSucceeded evt_001.
Network lỗi hoặc provider không nhận được response.
Provider gửi lại evt_001.
```

Cách xử lý đúng:

```text
Validate webhook.
Insert ProviderEventId với unique constraint.
Nếu insert thành công -> xử lý side effect.
Nếu duplicate -> không xử lý side effect lần 2.
Thường trả 2xx để provider không retry mãi.
```

Điểm quan trọng:

```text
Duplicate webhook không nhất thiết là lỗi client.
Nó là behavior bình thường trong distributed system.
```

---

## 8. 409 Conflict dùng khi nào?

409 Conflict dùng khi request hợp lệ về format nhưng xung đột với trạng thái hiện tại.

Ví dụ:

```text
Optimistic concurrency version mismatch.
Duplicate Idempotency-Key nhưng payload khác.
Order đã Cancelled mà client muốn Paid.
```

Với webhook duplicate nội bộ, thường không cần trả 409 cho provider. Có thể trả 2xx và bỏ qua duplicate side effect.

---

## 9. MicroShop connection

```text
PaymentService:
    WebhookLog unique ProviderEventId.

ProjectionWorker:
    processed_events unique EventId.

Checkout:
    Có thể dùng Idempotency-Key để tránh tạo order trùng.

Order state:
    Có thể dùng version để tránh update đè trạng thái.
```

---

## 10. Interview answer mẫu

```text
Optimistic concurrency dùng version/rowversion để phát hiện lost update khi nhiều request cùng sửa một record. Nếu version mismatch, thường trả 409 Conflict hoặc xử lý theo nghiệp vụ.

Idempotency giúp retry an toàn, đảm bảo cùng một request logic không tạo side effect nhiều lần. Em thường dùng Idempotency-Key hoặc ProviderEventId kết hợp unique constraint ở DB, vì check duplicate bằng memory hoặc check-then-insert trong code không đủ an toàn. Với duplicate webhook, em không xử lý side effect lần 2 và thường trả 2xx để provider không retry mãi.
```

## 11. Checkpoint

```text
1. Lost update là gì?
2. Optimistic concurrency dùng version để làm gì?
3. Unique constraint vì sao đáng tin hơn check memory?
4. Idempotency là gì?
5. Idempotency-Key dùng khi nào?
6. Duplicate webhook nên trả gì?
7. 409 Conflict phù hợp khi nào?
```

## 12. Flashcards

```text
Concurrency = nhiều việc cùng lúc.
Lost update = update bị ghi đè mất.
Optimistic concurrency = kiểm tra version khi ghi.
Unique constraint = DB không cho trùng.
Idempotency = làm lại không tạo thêm side effect.
Idempotency-Key = mã chống request trùng.
Duplicate webhook = event gửi lại.
409 = conflict trạng thái/dữ liệu.
```
