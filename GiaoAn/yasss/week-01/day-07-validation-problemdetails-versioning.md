# Day 07 - Validation, ProblemDetails, Status code, API Versioning

## 1. Câu chuyện đời thường

API giống **quầy tiếp nhận hồ sơ**.

```text
Request = hồ sơ khách nộp.
Validation = kiểm tra hồ sơ có đủ giấy tờ không.
Status code = mã lý do nhận/từ chối.
ProblemDetails = phiếu báo lỗi thống nhất.
API contract = mẫu thỏa thuận dữ liệu với client.
Breaking change = đổi mẫu làm client cũ không dùng được.
Versioning = tạo phiên bản mẫu mới.
```

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Validation | Kiểm tra input | Items không được rỗng |
| ProblemDetails | Phiếu báo lỗi chuẩn | status, message, traceId |
| Status code | Mã kết quả HTTP | 400, 404, 409 |
| API contract | Thỏa thuận dữ liệu API-client | response có orderId |
| Breaking change | Đổi làm client cũ vỡ | rename orderId -> id |
| Versioning | Phiên bản API | /v1/orders, /v2/orders |
| Backward compatibility | Client cũ vẫn chạy | thêm field optional |

---

## 3. Validation

Validation = kiểm tra input trước khi xử lý nghiệp vụ.

Ví dụ CreateOrderRequest:

```text
CustomerId không được rỗng.
Items không được null/rỗng.
Quantity phải > 0.
```

Nếu input sai:

```text
Trả 400 Bad Request.
Không để đi sâu rồi nổ 500.
```

---

## 4. Status code

Dùng thực dụng:

```text
200:
    Thành công.

400:
    Input sai/validation fail.

401:
    Chưa xác thực/token sai.

403:
    Không đủ quyền.

404:
    Không tìm thấy resource.

409:
    Conflict, ví dụ duplicate/idempotency/concurrency.

500:
    Lỗi server không mong muốn.
```

### Conflict là gì?

Conflict = request hợp lệ về format nhưng xung đột với trạng thái hiện tại.

Ví dụ:

```text
Tạo lại order với cùng IdempotencyKey.
Update order đã bị người khác sửa trước.
Webhook ProviderEventId đã xử lý.
```

---

## 5. ProblemDetails

ProblemDetails = format chuẩn để trả lỗi.

Nói đời thường:

```text
Khách bị từ chối hồ sơ thì nhận một phiếu lỗi rõ ràng,
không phải mỗi quầy nói một kiểu.
```

Nên có:

```text
status
code/title
message/detail
traceId hoặc correlationId
validation errors nếu có
```

### traceId/correlationId là gì?

Giải thích ngắn:

```text
Mã để lần theo request trong log/tracing.
```

Khi user báo lỗi, support/dev dùng mã này để tìm log.

---

## 6. API contract

API contract = thỏa thuận dữ liệu giữa API và client.

Ví dụ:

```json
{
  "orderId": "123",
  "status": "Paid",
  "totalAmount": 100000
}
```

Client viết code dựa vào field này.

Nếu server đổi `orderId` thành `id`, client cũ có thể lỗi.

---

## 7. Breaking change và Versioning

Breaking change = thay đổi làm client cũ không chạy đúng.

Ví dụ:

```text
Rename field.
Remove field.
Đổi kiểu dữ liệu.
Đổi response shape.
Đổi meaning của status code.
```

Versioning = tạo phiên bản API khi thay đổi lớn.

```text
/v1/orders
/v2/orders
```

Không phải thay đổi nào cũng cần version.

Thêm field optional thường backward-compatible hơn.

### Backward-compatible là gì?

Nghĩa là client cũ vẫn dùng được.

Ví dụ:

```text
Thêm field note optional vào response.
Client cũ không đọc field này vẫn chạy bình thường.
```

---

## 8. MicroShop connection

```text
CreateOrderRequest thiếu Items -> 400.
Order không tồn tại -> 404.
Duplicate IdempotencyKey -> 409.
Unexpected exception -> 500 kèm traceId.
Webhook payload sai -> reject rõ ràng.
API response thay đổi phải nghĩ backward compatibility.
```

---

## 9. Interview answer mẫu

```text
Validation giúp chặn input sai sớm và trả 400 thay vì để lỗi đi sâu thành 500. ProblemDetails chuẩn hóa error response để client dễ parse và dev dễ debug qua traceId/correlationId. Status code cần dùng đúng: 400 cho validation, 404 cho not found, 409 cho conflict, 500 cho lỗi server không mong muốn. API contract cần giữ backward compatibility hoặc dùng versioning khi có breaking change.
```

## 10. Checkpoint

```text
1. Validation dùng để làm gì?
2. Vì sao validation error không nên trả 500?
3. 400 khác 409 thế nào?
4. ProblemDetails giúp gì?
5. API contract là gì?
6. Breaking change là gì?
7. Backward-compatible nghĩa là gì?
```

## 11. Flashcards

```text
Validation = kiểm tra input.
400 = input sai.
404 = không tìm thấy.
409 = conflict.
500 = lỗi server không mong muốn.
ProblemDetails = phiếu lỗi chuẩn.
Contract = thỏa thuận dữ liệu API-client.
Breaking change = đổi làm client cũ vỡ.
Versioning = tạo phiên bản API.
```
