# Module 01 - .NET/C# Runtime - Interview Q&A V2

## Cách trả lời

Mỗi câu nên có:

```text
Định nghĩa ngắn
Vấn đề giải quyết
Trade-off/rủi ro
MicroShop example
```

---

# 1. async/await hoạt động thế nào?

`async/await` giúp viết code bất đồng bộ dễ đọc. Nó không tạo thread mới cho mỗi operation.

Khi await một task chưa hoàn thành, method tạm dừng, thread được trả về ThreadPool. Khi task hoàn thành, continuation chạy tiếp.

Trong backend, nó hữu ích cho I/O-bound work như DB, HTTP, MongoDB, message broker.

MicroShop:

```text
ProjectionWorker ghi MongoDB async.
Outbox publisher publish async.
BasketService gọi CatalogService async.
```

Red flag:

```text
async/await tạo thread mới.
```

---

# 2. Task khác Thread thế nào?

`Thread` là execution unit thật. `Task` là abstraction đại diện cho một operation sẽ hoàn thành trong tương lai.

Một Task có thể dùng thread, nhưng không đồng nghĩa với một thread.

I/O-bound Task thường không giữ thread trong lúc chờ. CPU-bound work vẫn cần thread.

---

# 3. Vì sao .Result/.Wait nguy hiểm?

`.Result` và `.Wait()` block thread. Trong backend high-throughput, việc block async có thể gây thread pool starvation, latency tăng, timeout tăng.

Nên dùng await xuyên suốt.

MicroShop:

```text
Worker/API handler không nên gọi async DB/HTTP bằng .Result/.Wait().
```

---

# 4. CancellationToken dùng để làm gì?

`CancellationToken` là tín hiệu hủy cooperative. Nó không kill thread.

Dùng khi:

```text
Client disconnect
Request timeout
App shutdown
Worker stopping
Long-running operation
```

MicroShop:

```text
ProjectionWorker dùng stoppingToken.
MongoDB write truyền cancellationToken.
HTTP call truyền cancellationToken.
```

---

# 5. Scoped/Singleton/Transient khác gì?

```text
Transient:
    tạo mới mỗi lần resolve

Scoped:
    một instance trong request/scope

Singleton:
    một instance toàn app
```

DbContext thường scoped. Singleton phải thread-safe và không giữ scoped dependency.

---

# 6. Vì sao không inject scoped service vào singleton?

Singleton sống toàn app, scoped sống theo request/scope. Nếu singleton giữ scoped service, có thể dùng object ngoài scope, object đã disposed, hoặc gây concurrency issue.

Trong BackgroundService, nếu cần scoped dependency:

```csharp
using var scope = serviceProvider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
```

---

# 7. Nullable Reference Types giải quyết gì?

NRT giúp compiler cảnh báo null risk trước runtime.

```csharp
string name;
string? optionalName;
```

`string?` thể hiện có thể null.

`!` chỉ tắt warning compiler, không đảm bảo runtime không null.

MicroShop:

```text
DTO
Event contract
Options/config
Webhook payload
```

Red flag:

```text
Dùng ! để che warning hàng loạt.
```

---

# 8. record có immutable tuyệt đối không?

Không.

`record` có value-based equality và thường dùng cho immutable data, nhưng record class vẫn là reference type. Nếu property bên trong mutable như `List<T>`, object vẫn có thể bị mutate.

MicroShop:

```text
IntegrationEvent dùng record hợp lý.
Entity có lifecycle như Order thường dùng class.
```

---

# 9. Equals/GetHashCode ảnh hưởng gì?

Nếu object dùng trong `Dictionary`, `HashSet`, `Distinct`, `GroupBy`, equality và hash code quyết định object có được coi là trùng hay không.

Nếu override `Equals` mà quên `GetHashCode`, dedup có thể sai.

MicroShop:

```text
Dedup EventId
Idempotency key
processed_events
Value object Money
```

---

# 10. IEnumerable khác IQueryable thế nào?

`IEnumerable` thường xử lý in-memory.  
`IQueryable` biểu diễn query có thể translate sang SQL/provider.

Gọi `ToList()` quá sớm có thể load nhiều data rồi filter trong memory.

MicroShop:

```text
Order query/filter/pagination nên push xuống DB khi phù hợp.
```

---

# 11. HttpClientFactory giải quyết gì?

`HttpClientFactory` quản lý lifetime của HttpClient/handler, tránh socket exhaustion, hỗ trợ typed client và resilience policies.

MicroShop:

```text
BasketService -> CatalogService nên dùng typed client/HttpClientFactory.
```

---

# 12. Options pattern dùng để làm gì?

Options pattern bind config vào class strongly-typed, dễ validate và inject.

Config quan trọng nên `ValidateOnStart` để fail fast.

MicroShop:

```text
Kafka topic
Mongo connection
JWT issuer/audience
Webhook secret
Retry thresholds
```

---

# 13. Singleton có mutable state nguy hiểm gì?

Singleton được dùng bởi nhiều request/thread. Nếu có mutable state không thread-safe, có thể race condition, data sai hoặc behavior khó đoán.

Nếu cần shared state, phải dùng thread-safe structure hoặc đồng bộ đúng. Nhưng tốt nhất là hạn chế mutable state trong singleton.

---

# 14. lock khác SemaphoreSlim thế nào?

`lock` dùng cho critical section đồng bộ, không await trong lock.

`SemaphoreSlim` hỗ trợ async wait:

```csharp
await semaphore.WaitAsync(cancellationToken);
```

Dùng khi cần giới hạn concurrency trong async flow.

---

# 15. ConcurrentDictionary có giải quyết mọi race condition không?

Không.

`ConcurrentDictionary` thread-safe cho operation đơn lẻ, nhưng workflow nhiều bước vẫn có thể race nếu không thiết kế atomic.

Ví dụ check rồi insert nhiều bước có thể vẫn sai nếu không dùng API atomic như `GetOrAdd`.

---

# 16. DateTime.UtcNow và DateTime.Now khác gì?

`DateTime.Now` phụ thuộc timezone local server.

`DateTime.UtcNow` dùng UTC, phù hợp cho event/DB core.

Trong distributed system, nên lưu:

```text
OccurredAtUtc
CreatedAtUtc
ProcessedAtUtc
TimeoutAtUtc
```

MicroShop:

```text
Webhook timestamp
Saga timeout
Outbox processed time
Event occurred time
```

---

# 17. DateTimeOffset dùng khi nào?

`DateTimeOffset` biểu diễn thời điểm kèm offset. Hữu ích khi nhận timestamp từ external system hoặc cần giữ offset gốc.

Rule thực dụng:

```text
Internal event/DB core: UTC rõ ràng
External timestamp: cân nhắc DateTimeOffset
```

---

# 18. System.Text.Json cần chú ý gì trong microservices?

Cần chú ý:

```text
Missing field
Unknown field
Case sensitivity
Enum serialization
DateTime serialization
Required field
Backward compatibility
```

Event producer đổi field có thể làm consumer cũ fail.

MicroShop:

```text
ProjectionWorker deserialize Kafka event.
Webhook payload deserialize.
```

---

# 19. ConfigureAwait(false) có cần trong ASP.NET Core không?

Trong ASP.NET Core app code hiện đại, thường không cần vì không có SynchronizationContext kiểu UI/ASP.NET classic.

Trong library code dùng rộng, có thể dùng `ConfigureAwait(false)` để tránh capture context không cần thiết.

Red flag:

```text
Bảo mọi await trong ASP.NET Core đều bắt buộc ConfigureAwait(false).
```

---

# 20. ThreadPool starvation là gì?

ThreadPool starvation xảy ra khi ThreadPool thiếu thread rảnh vì nhiều thread bị block hoặc bận quá lâu.

Nguyên nhân:

```text
.Result/.Wait()
Synchronous I/O
CPU-bound heavy work trong request
Retry storm
Task.Run bừa
```

Triệu chứng:

```text
Latency tăng
Timeout tăng
Request queue
```

---

# 21. Nói 90 giây về Module 01

Ở nền .NET runtime, em tập trung vào các phần ảnh hưởng trực tiếp tới backend production: async/await, Task vs Thread, CancellationToken, DI lifetime, HttpClientFactory, Options validation, Nullable Reference Types, equality, concurrency và time handling.

Ví dụ, async/await không tạo thread mới mà giúp non-blocking I/O. Nếu dùng `.Result` hoặc `.Wait()` trong API/worker thì có thể gây thread pool starvation. Với BackgroundService như ProjectionWorker, em cần truyền `stoppingToken` để shutdown graceful và tạo scope nếu cần scoped dependency như DbContext. Với outbound HTTP như BasketService gọi CatalogService, em ưu tiên HttpClientFactory và timeout policy.

Ngoài ra, em chú ý NRT để giảm null bug trong DTO/event/config, dùng UTC cho event/DB time như `OccurredAtUtc`, và cẩn thận serialization compatibility vì event/message có thể được consumer cũ xử lý. Những phần này không phải pattern hào nhoáng, nhưng sai là tạo lỗi production rất thật.
