# Day 04 - HttpClientFactory, Outbound call, Timeout/Retry

## 1. Câu chuyện đời thường

Tưởng tượng MicroShop là một công ty có nhiều phòng ban.

```text
BasketService = phòng giỏ hàng.
CatalogService = phòng danh mục sản phẩm.
PaymentService = phòng thanh toán.
```

Khi BasketService cần hỏi sản phẩm còn hợp lệ không, nó gọi sang CatalogService.

Đó là **outbound call**.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Outbound call | Gọi ra service khác | Basket -> Catalog |
| Downstream service | Service bị mình gọi | Catalog là downstream của Basket |
| HttpClient | Công cụ gọi HTTP | Cái điện thoại |
| HttpClientFactory | Nhà máy/tổng đài cấp HttpClient | Quản lý điện thoại |
| Timeout | Không chờ mãi | 2s không nghe thì dừng |
| Retry | Thử lại | Gọi lại lần 2 |
| Circuit breaker | Cầu dao ngắt tạm | Catalog lỗi liên tục thì tạm ngưng gọi |
| Idempotent | Làm lại nhiều lần kết quả vẫn như một lần | GET product |

---

## 3. Outbound call và Downstream

Outbound call = service hiện tại gọi ra ngoài.

```text
BasketService -> CatalogService
```

CatalogService là downstream của BasketService.

Nếu downstream chậm hoặc chết, service gọi nó có thể bị ảnh hưởng.

---

## 4. Timeout

Timeout = giới hạn thời gian chờ.

Ví dụ:

```text
Gọi CatalogService.
Sau 2 giây không trả lời.
BasketService dừng chờ và trả lỗi có kiểm soát.
```

Nếu không timeout:

```text
Request treo lâu.
Thread/request bị giữ.
User chờ.
Hệ thống dễ nghẽn.
```

---

## 5. Retry

Retry = thử lại.

Retry hợp lý khi lỗi có thể tạm thời:

```text
Mạng chập chờn.
Service downstream restart nhanh.
Timeout ngắn do spike tạm thời.
```

Retry nguy hiểm khi:

```text
Retry quá nhanh/quá nhiều.
Downstream đang quá tải.
Operation tạo side effect nhưng không idempotent.
```

### Idempotent là gì?

Idempotent = làm lại nhiều lần mà kết quả cuối vẫn như làm một lần.

Ví dụ:

```text
GET product:
    gọi nhiều lần vẫn chỉ đọc dữ liệu.

POST create payment:
    gọi nhiều lần có thể tạo nhiều payment nếu không có idempotency key.
```

---

## 6. Circuit breaker

Circuit breaker = cầu dao ngắt tạm.

Nếu CatalogService lỗi liên tục:

```text
BasketService tạm ngưng gọi CatalogService.
Fail fast trong vài giây.
Sau đó thử lại.
```

Lợi ích:

```text
Không làm downstream chết thêm.
Không để request nào cũng chờ timeout.
Hệ thống hồi phục dễ hơn.
```

---

## 7. HttpClientFactory

HttpClientFactory = cách .NET quản lý HttpClient tốt hơn.

Nói dễ hiểu:

```text
Thay vì mỗi nhân viên tự mua điện thoại,
công ty có tổng đài cấp và quản lý điện thoại.
```

Nó giúp:

```text
Tái sử dụng handler hợp lý.
Tránh tạo/dispose HttpClient bừa.
Cấu hình base URL/header tập trung.
Dễ gắn timeout/retry/circuit breaker.
```

### Handler lifetime là gì?

Giải thích đơn giản:

```text
HttpClient dùng handler bên dưới để quản lý kết nối mạng.
Handler sống quá ngắn/quá dài đều có thể gây vấn đề.
HttpClientFactory giúp quản lý vòng đời này hợp lý hơn.
```

Không cần nhớ sâu ở Week 1. Chỉ cần nhớ:

```text
Gọi HTTP thường xuyên trong backend -> ưu tiên HttpClientFactory/typed client.
```

---

## 8. MicroShop connection

```text
BasketService gọi CatalogService validate product.
Gateway gọi downstream service.
OrderingService có thể gọi Payment/Discount nếu sync flow.
```

Mỗi call nên nghĩ tới:

```text
Có timeout không?
Có cancellation token không?
Retry có an toàn không?
Nếu downstream chết thì user nhận gì?
```

---

## 9. Interview answer mẫu

```text
Outbound HTTP call có thể bị chậm, timeout hoặc lỗi mạng. Em dùng HttpClientFactory để quản lý HttpClient/handler lifetime, cấu hình typed client và dễ gắn policy như timeout, retry, circuit breaker. Retry cần cẩn thận, chỉ nên dùng khi operation idempotent hoặc đã có cơ chế idempotency, để tránh tạo duplicate side effect hoặc retry storm.
```

## 10. Checkpoint

```text
1. Outbound call là gì?
2. Downstream service là gì?
3. Vì sao không chờ HTTP call vô hạn?
4. Retry khi nào nguy hiểm?
5. Idempotent nghĩa là gì?
6. Circuit breaker giúp gì?
7. HttpClientFactory giải quyết gì?
```

## 11. Flashcards

```text
Outbound call = gọi ra service khác.
Downstream = service bị gọi.
Timeout = không chờ mãi.
Retry = thử lại có kiểm soát.
Idempotent = làm lại không đổi kết quả cuối.
Circuit breaker = tạm ngắt khi lỗi liên tục.
HttpClientFactory = quản lý HttpClient đúng hơn.
```
