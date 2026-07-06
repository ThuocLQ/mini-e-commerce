# Day 05 - ASP.NET Core Pipeline, Middleware, Routing

## 1. Câu chuyện đời thường

Tưởng tượng API là **quầy tiếp nhận hồ sơ**.

```text
Request = hồ sơ khách nộp.
Pipeline = quy trình hồ sơ đi qua.
Middleware = từng trạm kiểm tra.
Routing = chỉ hồ sơ tới đúng quầy.
Endpoint = nhân viên xử lý cuối.
Model binding = chép dữ liệu hồ sơ vào form nội bộ.
```

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Request | Hồ sơ khách gửi | POST /orders |
| Response | Kết quả trả về | 200/400/500 |
| Pipeline | Dây chuyền xử lý request | logging -> auth -> endpoint |
| Middleware | Trạm xử lý trên dây chuyền | auth, exception handler |
| Routing | Tìm đúng endpoint | GET /orders/{id} |
| Endpoint | Code xử lý request | handler tạo order |
| Model binding | Map request data vào biến/DTO | JSON body -> CreateOrderRequest |
| Short-circuit | Chặn request không cho đi tiếp | token invalid -> trả 401 |

---

## 3. Pipeline là gì?

Pipeline = chuỗi bước request đi qua.

Ví dụ:

```text
Hồ sơ đi qua:
1. Trạm ghi log.
2. Trạm bắt lỗi.
3. Trạm tìm đúng quầy.
4. Trạm kiểm tra thẻ.
5. Quầy xử lý cuối.
```

Trong ASP.NET Core, mỗi trạm là middleware.

---

## 4. Middleware là gì?

Middleware là một đoạn code nằm giữa request và endpoint.

Nó có thể:

```text
Ghi log.
Bắt exception.
Kiểm tra authentication.
Kiểm tra authorization.
Dừng request sớm.
Gọi middleware tiếp theo.
```

### Short-circuit là gì?

Short-circuit = dừng request sớm, không gọi bước tiếp theo.

Ví dụ:

```text
Token sai -> trả 401 ngay -> không vào endpoint.
```

---

## 5. Routing và Endpoint

Routing = xem URL/method để tìm endpoint phù hợp.

Ví dụ:

```text
GET /order-summaries
GET /order-summaries/{orderId}
POST /webhooks/payment
```

Endpoint = code xử lý request sau khi route match.

---

## 6. Model binding

Model binding = ASP.NET lấy data từ request và map vào parameter/DTO.

Ví dụ:

```text
Route:
    /orders/{id} -> Guid id

Query:
    ?status=Paid -> string status

Body:
    JSON -> CreateOrderRequest
```

---

## 7. Vì sao thứ tự middleware quan trọng?

Vì bước sau có thể cần kết quả từ bước trước.

Ví dụ:

```text
Authentication phải chạy trước Authorization.
Phải biết bạn là ai trước, rồi mới biết bạn được làm gì.
```

Exception handler nên đặt sớm để bắt lỗi từ các middleware/endpoint phía sau.

---

## 8. MicroShop connection

```text
ApiGateway nhận request và route tới service.
OrderQueryService có /order-summaries.
PaymentService có /webhooks/payment.
BasketService có route validate product.
```

Gateway nên xử lý cross-cutting concern như route/auth/rate limit, không nên chứa business logic lõi.

### Cross-cutting concern là gì?

Nói dễ hiểu:

```text
Việc chung nhiều service đều cần: logging, auth, tracing, rate limit.
```

---

## 9. Interview answer mẫu

```text
ASP.NET Core xử lý request qua middleware pipeline. Mỗi middleware là một bước có thể xử lý request, gọi next hoặc short-circuit. Routing chọn endpoint dựa trên method/path. Model binding map dữ liệu request vào parameter/DTO. Thứ tự middleware quan trọng, ví dụ Authentication phải chạy trước Authorization.
```

## 10. Checkpoint

```text
1. Pipeline là gì bằng ví dụ quầy hồ sơ?
2. Middleware là gì?
3. Short-circuit là gì?
4. Routing khác Endpoint thế nào?
5. Model binding làm gì?
6. Vì sao middleware order quan trọng?
```

## 11. Flashcards

```text
Pipeline = dây chuyền request.
Middleware = trạm xử lý.
Routing = tìm đúng quầy.
Endpoint = code xử lý cuối.
Model binding = map request data vào parameter/DTO.
Short-circuit = chặn request sớm.
Cross-cutting concern = việc chung như log/auth/tracing.
```
