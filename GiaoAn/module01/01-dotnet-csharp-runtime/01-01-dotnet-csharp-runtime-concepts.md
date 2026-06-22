# Module 01 - .NET/C# Runtime - Concepts V2

## 0. Mục tiêu module

Module này giúp nắm chắc nền .NET/C# cho Senior Backend Interview.

Không học C# kiểu nhập môn. Tập trung vào các điểm ảnh hưởng trực tiếp đến backend production:

```text
async/await
Task vs Thread
CancellationToken
DI lifetime
HttpClientFactory
IQueryable/LINQ
Nullable Reference Types
Equality
Concurrency basics
DateTime/UTC
Serialization
ThreadPool starvation
Configuration validation
```

Nếu học Kafka/Saga/Outbox nhưng yếu các phần này, interview Senior .NET vẫn dễ bị lộ.

---

# 1. Value type vs Reference type

## Là gì?

`Value type` copy giá trị.

```csharp
int x = 10;
int y = x;
y = 20;
// x vẫn là 10
```

`Reference type` copy reference tới object.

```csharp
var a = new Customer { Name = "A" };
var b = a;
b.Name = "B";
// a.Name cũng là "B"
```

## Vì sao quan trọng?

Ảnh hưởng tới:

```text
Mutation bug
NullReferenceException
Equality
Memory allocation
Performance
Thread-safety
```

## Caveat senior cần nói

Không nên nói tuyệt đối:

```text
Value type luôn ở stack.
Reference type luôn ở heap.
```

Nói đúng hơn:

```text
Reference type object thường nằm trên heap.
Value type có thể nằm inline trong object/array hoặc stack tùy context.
```

## Red flag

```text
Nói value type luôn nhanh hơn reference type.
```

Không luôn đúng. Struct lớn, mutable struct, boxing đều có thể gây vấn đề.

---

# 2. class vs record vs struct

## class

`class` là reference type. Phù hợp cho entity/object có identity/lifecycle.

```csharp
public class Order
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
}
```

## record

`record` thường phù hợp cho DTO, command, event, immutable data.

```csharp
public record OrderCreatedIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    DateTimeOffset OccurredAtUtc
);
```

Record có value-based equality mặc định.

## Caveat về record

`record class` vẫn là reference type.

Record không tự động immutable tuyệt đối nếu property bên trong mutable.

Ví dụ:

```csharp
public record Demo(List<string> Items);
```

`Items` vẫn mutable.

## struct

`struct` là value type. Phù hợp cho value nhỏ, immutable, không identity.

Không nên lạm dụng struct trong business app nếu không có lý do rõ.

## MicroShop mapping

```text
Order entity -> class
OrderCreatedIntegrationEvent -> record
Money/Price value object -> record/readonly struct tùy thiết kế
```

---

# 3. Nullable Reference Types

## Là gì?

Nullable Reference Types giúp compiler cảnh báo null risk.

```csharp
string name = "Thuoc";
string? optionalName = null;
```

`string` nghĩa là không kỳ vọng null.  
`string?` nghĩa là có thể null.

## Nó giải quyết gì?

Giảm lỗi:

```text
NullReferenceException
DTO thiếu field
Config thiếu giá trị
Event contract thiếu field
```

## null-forgiving operator `!`

```csharp
public string Name { get; set; } = null!;
```

Nó tắt warning compiler, không đảm bảo runtime không null.

Dùng bừa `!` là smell.

## required property

```csharp
public class CreateOrderRequest
{
    public required Guid CustomerId { get; init; }
}
```

Giúp ép object initializer phải set property.

## MicroShop liên hệ

```text
Request DTO
Response DTO
Options/config
Integration event contract
Webhook payload
MongoDB read model
```

## Interview answer ngắn

Nullable Reference Types giúp compiler phát hiện khả năng null trước runtime. `string?` thể hiện giá trị có thể null. Dùng `!` chỉ nên khi mình chắc chắn framework/serializer/DI sẽ set giá trị, không nên dùng để che warning bừa.

---

# 4. Equality: Reference equality vs Value equality

## Reference equality

Hai biến trỏ cùng một object.

```csharp
var a = new Customer("A");
var b = a;
ReferenceEquals(a, b); // true
```

## Value equality

Hai object khác nhau nhưng giá trị bên trong bằng nhau.

```csharp
var a = new Money(100, "VND");
var b = new Money(100, "VND");
// a và b nên được coi là bằng nhau nếu Money là value object
```

## Equals/GetHashCode

Nếu override `Equals`, thường phải override `GetHashCode`.

Rất quan trọng khi dùng:

```text
Dictionary
HashSet
Distinct
GroupBy
Deduplication
```

## record equality

Record hỗ trợ value-based equality mặc định.

```csharp
public record EventKey(Guid EventId);

var a = new EventKey(id);
var b = new EventKey(id);
// a == b true
```

## MicroShop liên hệ

```text
Dedup eventId
HashSet processed events
Dictionary<OrderId, State>
Value object Money
Idempotency key
```

## Red flag

```text
Dùng object mutable làm Dictionary key.
Override Equals nhưng quên GetHashCode.
Không hiểu record equality khi dedup.
```

---

# 5. async/await

## Là gì?

`async/await` giúp viết code bất đồng bộ dễ đọc.

Nó không có nghĩa là tạo thread mới cho mỗi operation.

```csharp
public async Task<Order?> GetOrderAsync(Guid id, CancellationToken cancellationToken)
{
    return await repository.GetByIdAsync(id, cancellationToken);
}
```

## Nó giải quyết vấn đề gì?

Backend thường chờ I/O:

```text
Database
HTTP call
Message broker
MongoDB
File/network
```

Trong lúc chờ I/O, `await` cho phép thread được trả về ThreadPool thay vì bị block.

## Cách hoạt động ở mức interview

```text
Gặp await với Task chưa xong
-> method tạm dừng
-> thread trả về ThreadPool
-> khi task hoàn thành, continuation chạy tiếp
```

## Sai lầm phổ biến

```csharp
var result = service.GetAsync().Result;
service.GetAsync().Wait();
```

Rủi ro:

```text
Block thread
ThreadPool starvation
Deadlock trong một số context cũ
```

## async void

Không dùng `async void` trong backend trừ event handler đặc biệt.

## MicroShop liên hệ

```text
ProjectionWorker consume event async
MongoDB write async
Outbox publisher publish async
BasketService gọi CatalogService async
```

---

# 6. ConfigureAwait trong ASP.NET Core

## Cần biết gì?

Trong ASP.NET Core hiện đại, thường không cần `ConfigureAwait(false)` trong application code vì không có SynchronizationContext kiểu UI/ASP.NET classic.

Trong library code, `ConfigureAwait(false)` vẫn có thể dùng để tránh capture context không cần thiết.

## Interview answer thực dụng

```text
Trong ASP.NET Core app code, ConfigureAwait(false) thường không cần thiết như .NET Framework/UI app. Em ưu tiên viết async/await đúng, không block bằng .Result/.Wait. Với library code dùng rộng, ConfigureAwait(false) có thể phù hợp.
```

## Red flag

```text
Bảo mọi await trong ASP.NET Core đều bắt buộc ConfigureAwait(false).
```

Không đúng.

---

# 7. Task vs Thread

## Thread

Thread là execution unit thật của OS/runtime.

## Task

Task là abstraction đại diện cho operation sẽ hoàn thành trong tương lai.

Task có thể là:

```text
I/O-bound: không giữ thread trong lúc chờ
CPU-bound: cần thread để chạy
```

## Không nên nói

```text
Một Task = một Thread.
```

## CPU-bound work

Không dùng `Task.Run` bừa trong ASP.NET Core request để "làm async".

Nếu CPU-bound nặng, cần nghĩ tới:

```text
Background processing
Queue
Separate worker
Rate limit
Scale compute
```

---

# 8. CancellationToken

## Là gì?

`CancellationToken` là tín hiệu hủy cooperative.

Nó không kill thread.

```csharp
await db.SaveChangesAsync(cancellationToken);
```

## Dùng khi nào?

```text
Client disconnect
Request timeout
App shutting down
Worker stopping
Long-running loop
```

## Worker pattern

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ProcessAsync(stoppingToken);
    }
}
```

## MicroShop liên hệ

```text
ProjectionWorker dùng stoppingToken
NotificationWorker shutdown graceful
HTTP call truyền cancellationToken
MongoDB write truyền cancellationToken
```

---

# 9. Exception handling

## Nguyên tắc

Không nuốt exception im lặng.

Sai:

```csharp
try
{
    await ProcessAsync();
}
catch
{
}
```

Đúng hơn:

```csharp
try
{
    await ProcessAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to process event {EventId}", eventId);
    throw;
}
```

Hoặc nếu không throw lại thì phải có:

```text
failure store
metric
retry/DLQ
audit
```

## API exception mapping

Nên phân biệt:

```text
Validation/domain error -> 400
Conflict/concurrency/idempotency conflict -> 409
Unauthorized -> 401
Forbidden -> 403
Not found -> 404
Unexpected -> 500
```

## MicroShop liên hệ

```text
API standard error format
Projection failure store
Webhook invalid signature
Outbox publish failure
```

---

# 10. LINQ pitfalls: IEnumerable vs IQueryable

## Deferred execution

```csharp
var query = orders.Where(x => x.Status == Paid);
// chưa chạy ngay
```

Chạy khi enumerate:

```csharp
var list = query.ToList();
```

## IEnumerable

Thường xử lý in-memory.

## IQueryable

Biểu diễn query có thể translate sang SQL/provider.

## Sai

```csharp
var orders = db.Orders.ToList();
var paid = orders.Where(x => x.Status == Paid);
```

Load hết rồi filter trong memory.

## Đúng hơn

```csharp
var paid = await db.Orders
    .Where(x => x.Status == Paid)
    .ToListAsync(cancellationToken);
```

## N+1 problem

```csharp
foreach (var order in orders)
{
    var items = await db.OrderItems
        .Where(x => x.OrderId == order.Id)
        .ToListAsync();
}
```

Rủi ro:

```text
Query tăng theo số record
API chậm
DB quá tải
```

---

# 11. IDisposable / IAsyncDisposable

## Là gì?

Giải phóng resource:

```text
Stream
File handle
DB connection
Socket
```

```csharp
using var stream = File.OpenRead(path);
```

Async:

```csharp
await using var resource = await CreateAsyncResource();
```

## Với DI container

Không tự dispose service do DI container quản lý.

Container/scope quản lý lifecycle.

## Red flag

```text
Dispose DbContext lấy từ DI bằng tay trong request.
```

---

# 12. Dependency Injection lifetime

## Ba lifetime

```text
Transient:
    tạo mới mỗi lần resolve

Scoped:
    một instance trong scope/request

Singleton:
    một instance toàn app
```

## DbContext

Thường là scoped.

## Lỗi scoped vào singleton

```csharp
public class MySingleton
{
    public MySingleton(AppDbContext db) {}
}
```

Sai vì singleton sống lâu hơn scoped.

## BackgroundService dùng scoped dependency

```csharp
using var scope = serviceProvider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
```

## Thread-safety

Singleton phải thread-safe. Nếu singleton có mutable state, cần cực kỳ cẩn thận.

---

# 13. Concurrency basics

## Race condition

Xảy ra khi nhiều thread/task truy cập shared mutable state và kết quả phụ thuộc timing.

Ví dụ sai:

```csharp
private int _count;

public void Increment()
{
    _count++;
}
```

`_count++` không atomic.

## lock

Dùng cho critical section sync.

```csharp
private readonly object _lock = new();

lock (_lock)
{
    _count++;
}
```

Không `await` trong `lock`.

## SemaphoreSlim

Dùng được với async.

```csharp
await semaphore.WaitAsync(cancellationToken);
try
{
    await DoSomethingAsync();
}
finally
{
    semaphore.Release();
}
```

## ConcurrentDictionary

Thread-safe cho operation đơn lẻ, nhưng không giải quyết mọi race ở workflow phức tạp.

## MicroShop liên hệ

```text
Singleton service có mutable state
In-memory dedup cache
Background worker shared state
Rate limit/local cache
```

## Red flag

```text
ConcurrentDictionary có nghĩa là toàn bộ workflow thread-safe.
```

Không đúng.

---

# 14. Options pattern + Configuration validation

## Vì sao cần?

Production config sai có thể gây lỗi nặng:

```text
Wrong JWT issuer
Wrong Kafka topic
Wrong Mongo connection string
Webhook secret thiếu
Retry count quá cao
```

## Options pattern

```csharp
public class KafkaOptions
{
    public string BootstrapServers { get; set; } = default!;
    public string TopicName { get; set; } = default!;
}
```

Register:

```csharp
builder.Services
    .AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## Senior point

Config quan trọng nên fail fast khi startup, không để runtime mới nổ.

---

# 15. HttpClientFactory

## Vì sao không new HttpClient bừa?

```csharp
using var client = new HttpClient();
```

Trong app nhiều request, cách này có thể gây socket exhaustion hoặc DNS/lifetime issue.

## Cách đúng

```csharp
builder.Services.AddHttpClient<CatalogClient>(client =>
{
    client.BaseAddress = new Uri("http://catalogservice");
});
```

## Kết hợp resilience

Có thể gắn:

```text
Timeout
Retry
Circuit breaker
```

qua Polly hoặc `Microsoft.Extensions.Http.Resilience`.

## MicroShop liên hệ

```text
BasketService -> CatalogService
OrderingService -> Discount/Payment nếu sync call
Gateway downstream
```

---

# 16. DateTime, DateTimeOffset, UTC, TimeProvider

## Vấn đề

Thời gian sai có thể gây lỗi production:

```text
Saga timeout sai
Webhook timestamp sai
Report lệch ngày
Event ordering sai
Outbox cleanup sai
```

## DateTime.Now vs UtcNow

`DateTime.Now` phụ thuộc timezone local server.

`DateTime.UtcNow` dùng UTC.

## DateTimeOffset

Lưu thời điểm kèm offset. Thường an toàn hơn khi làm việc với external timestamp.

## Rule thực dụng

```text
Event field: OccurredAtUtc
DB field: CreatedAtUtc, ProcessedAtUtc
Timeout field: ExpiresAtUtc, TimeoutAtUtc
Không dùng local time cho event/DB core
```

## TimeProvider

Trong .NET hiện đại, có thể dùng `TimeProvider` để test logic liên quan thời gian.

## MicroShop liên hệ

```text
Outbox CreatedAtUtc/ProcessedAtUtc
Webhook OccurredAtUtc
Saga TimeoutAtUtc
Projection ProcessedAtUtc
```

## Red flag

```text
Dùng DateTime.Now trong event contract.
```

---

# 17. System.Text.Json / Serialization basics

## Vì sao cần?

Microservices dùng nhiều JSON:

```text
HTTP request/response
Kafka event
RabbitMQ message
Webhook payload
MongoDB document
```

## Cần chú ý

```text
Property naming policy
Case sensitivity
Missing field
Unknown field
Enum serialization
DateTime/DateTimeOffset serialization
Required field
Backward compatibility
```

## Unknown field

Consumer nên tolerant với field mới nếu có thể.

## Enum risk

Enum string mới có thể làm consumer cũ deserialize fail nếu không handle.

## MicroShop liên hệ

```text
OrderCreated event
PaymentSucceeded event
Webhook payload
ProjectionWorker deserialize Kafka message
```

## Red flag

```text
Producer đổi tên field event và nghĩ consumer cũ vẫn ổn.
```

---

# 18. ThreadPool starvation

## Là gì?

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
Request timeout
Queue tăng
CPU không nhất thiết cao
```

## MicroShop liên hệ

```text
Outbox publisher retry storm
ProjectionWorker block sync
HTTP call không async
```

---

# 19. Tổng kết Module 01

Nếu chỉ nhớ 12 ý:

```text
1. async/await không tạo thread mới.
2. Task không phải Thread.
3. CancellationToken là cooperative cancellation.
4. Không block async bằng .Result/.Wait().
5. DI lifetime phải đúng, không inject scoped vào singleton.
6. Singleton có mutable state phải thread-safe.
7. HttpClientFactory nên dùng cho outbound HTTP.
8. Options/config quan trọng nên validate on startup.
9. IEnumerable/IQueryable khác nhau ở nơi query chạy.
10. Nullable Reference Types giúp giảm null bug nhưng không thay validation.
11. DateTime dùng UTC cho event/DB core.
12. Serialization/event contract phải nghĩ backward compatibility.
```
