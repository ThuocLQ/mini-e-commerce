# Day 13 - Clean Architecture, Dependency Direction

## 1. Câu chuyện đời thường

Tưởng tượng một nhà hàng Cam Sành.

```text
Khách:
    Đặt món.

Quầy:
    Nhận order.

Bếp:
    Xử lý nghiệp vụ.

Kho:
    Lưu cam.

Nhà cung cấp ngoài:
    Thanh toán/giao hàng.
```

Bếp không nên phụ thuộc vào từng loại máy POS, từng hãng vận chuyển, từng database cụ thể.

Clean Architecture giúp bảo vệ lõi nghiệp vụ khỏi chi tiết kỹ thuật.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Domain | Lõi nghiệp vụ | Order, Payment rule |
| Application | Điều phối use case | CreateOrderHandler |
| Infrastructure | Chi tiết kỹ thuật | EF, PostgreSQL, Redis |
| API/Presentation | Cửa nhận request | Controller/Minimal API |
| Dependency direction | Hướng phụ thuộc code | outer -> inner |
| Interface/Port | Hợp đồng lõi cần | IOrderRepository |
| Adapter | Implementation cụ thể | EfOrderRepository |
| Composition root | Nơi nối interface với implementation | Program.cs DI |
| Separation of concerns | Mỗi lớp lo một việc | API không chứa SQL |

---

## 3. Clean Architecture là gì?

Clean Architecture là cách tổ chức code để:

```text
Business rules không phụ thuộc framework/database/message broker.
Chi tiết kỹ thuật nằm bên ngoài và có thể thay đổi.
```

Cốt lõi không phải chia folder đẹp.

Cốt lõi là:

```text
Dependency direction đúng.
```

---

## 4. Dependency Direction chuẩn

Rule:

```text
Code phía ngoài phụ thuộc vào phía trong.
Code phía trong không phụ thuộc vào phía ngoài.
```

Thường:

```text
API -> Application -> Domain
Infrastructure -> interface do Application/Domain định nghĩa
```

Nói rõ hơn:

```text
Application/Domain định nghĩa interface cần dùng.
Infrastructure implement interface đó.
Domain không biết Infrastructure tồn tại.
Program.cs/Composition root nối implementation vào DI.
```

Ví dụ:

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
}
```

Interface nằm ở Application/Domain boundary.

Infrastructure implement:

```csharp
public class EfOrderRepository : IOrderRepository
{
}
```

DI nối:

```csharp
services.AddScoped<IOrderRepository, EfOrderRepository>();
```

---

## 5. Domain Layer

Domain = lõi nghiệp vụ.

Ví dụ:

```text
Order
OrderStatus
Money
Order state transition
Payment rule
```

Domain trả lời:

```text
Nghiệp vụ đúng là gì?
Trạng thái nào được chuyển?
Rule nào không được vi phạm?
```

Domain không nên gọi thẳng:

```text
DbContext
HttpClient
Kafka producer
Redis
```

---

## 6. Application Layer

Application = điều phối use case.

Ví dụ:

```text
CreateOrderHandler
CancelOrderHandler
ProcessPaymentSucceededHandler
```

Application làm:

```text
Nhận command/query.
Load entity qua interface.
Gọi domain behavior.
Commit transaction.
Tạo outbox/event.
```

---

## 7. Infrastructure Layer

Infrastructure = chi tiết kỹ thuật.

Ví dụ:

```text
EF Core repository.
PostgreSQL DbContext.
Redis cache.
Kafka producer.
MongoDB read store.
HttpClient provider.
```

Infrastructure thay đổi dễ hơn domain.

---

## 8. API Layer

API = cửa nhận request.

Nên làm:

```text
Auth/validation.
Map DTO -> command/query.
Gọi Application.
Map result -> response.
```

Không nên:

```text
Nhét business rule phức tạp vào endpoint.
Viết SQL trong controller.
Gọi Kafka trực tiếp từ endpoint nếu đó là business flow cần transaction/outbox.
```

---

## 9. MicroShop connection

```text
OrderingService:
    Domain: Order, OrderStatus.
    Application: Create/Cancel/Payment handlers.
    Infrastructure: EF/PostgreSQL/Outbox.
    API: endpoints/controllers.

PaymentService:
    Domain: Payment state.
    Infrastructure: provider/webhook storage.

OrderQueryService:
    Read model/query service có thể đơn giản hơn, không cần ép Clean Architecture nặng.
```

---

## 10. Lỗi hay hiểu sai

```text
Chia folder là đã Clean Architecture.
Domain phụ thuộc EF/HttpClient.
API endpoint chứa toàn bộ business logic.
Repository pattern bắt buộc ở mọi nơi.
Mọi service đều phải phức tạp như nhau.
```

### Câu cân bằng

```text
Clean Architecture là công cụ, không phải tôn giáo.
Service CRUD/read-only nhỏ có thể đơn giản hơn.
Service có business rule phức tạp nên cần boundary rõ hơn.
```

---

## 11. Interview answer mẫu

```text
Clean Architecture giúp tách business rules khỏi chi tiết kỹ thuật như database, framework, message broker. Điểm quan trọng là dependency direction: API/Application/Infrastructure phụ thuộc vào lõi, nhưng Domain không phụ thuộc Infrastructure. Application hoặc Domain định nghĩa interface như IOrderRepository, Infrastructure implement bằng EF/PostgreSQL, và composition root nối qua DI. Mục tiêu là giữ business rules ổn định, dễ test và ít bị ảnh hưởng khi đổi công nghệ.
```

## 12. Checkpoint

```text
1. Domain layer chứa gì?
2. Application layer làm gì?
3. Infrastructure layer chứa gì?
4. API layer nên làm gì?
5. Dependency direction đúng là gì?
6. Interface đặt ở đâu, implementation đặt ở đâu?
7. Composition root là gì?
8. Chia folder có đủ gọi là Clean Architecture không?
```

## 13. Flashcards

```text
Domain = lõi nghiệp vụ.
Application = điều phối use case.
Infrastructure = chi tiết kỹ thuật.
API = cửa nhận request.
Dependency direction = ngoài phụ thuộc trong.
Port/interface = hợp đồng.
Adapter = implementation.
Composition root = nơi nối DI.
Clean Architecture = bảo vệ business rules.
```
