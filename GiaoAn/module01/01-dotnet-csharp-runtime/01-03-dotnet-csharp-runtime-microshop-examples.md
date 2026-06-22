# Module 01 - .NET/C# Runtime - MicroShop Examples V2

## 1. Blocking async

### Command

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "\.Result|\.Wait\("
```

### Cần đánh giá

```text
Có xuất hiện không?
Nằm trong production code hay test/setup?
Có block DB/HTTP/message async không?
```

### Interview story

```text
Em kiểm tra blocking async vì .Result/.Wait trong backend có thể gây thread pool starvation.
```

---

## 2. BackgroundService + CancellationToken

### Command

```powershell
Get-ChildItem Workers -Recurse -Filter *.cs |
  Select-String -Pattern "BackgroundService|ExecuteAsync|CancellationToken|stoppingToken|await"
```

### Cần đánh giá

```text
Worker có dùng stoppingToken không?
Có truyền token xuống DB/Mongo/HTTP/broker call không?
Loop có dừng graceful không?
```

### MicroShop location

```text
ProjectionWorker
NotificationWorker
```

---

## 3. DI lifetime trong worker

### Command

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "AddSingleton|AddScoped|AddTransient|CreateScope|IServiceScopeFactory|BackgroundService"
```

### Cần đánh giá

```text
BackgroundService có inject trực tiếp scoped dependency không?
Nếu cần DbContext/repository scoped, có tạo scope không?
Singleton service có mutable state không?
```

---

## 4. HttpClientFactory / outbound calls

### Command

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "HttpClient|IHttpClientFactory|AddHttpClient|Refit|Grpc|GetAsync|PostAsync|CancellationToken"
```

### Cần đánh giá

```text
BasketService gọi CatalogService bằng gì?
Có HttpClientFactory/typed client không?
Có truyền CancellationToken không?
Có new HttpClient per request không?
```

---

## 5. Options/config validation

### Command

```powershell
Get-ChildItem . -Recurse -Include *.cs,*.json |
  Select-String -Pattern "IOptions|IOptionsMonitor|ValidateOnStart|ValidateDataAnnotations|Configuration.GetSection|Kafka|Mongo|Jwt|Webhook"
```

### Cần đánh giá

```text
Kafka topic lấy từ config hay hardcode?
Mongo connection string validate không?
JWT issuer/audience validate không?
Webhook secret thiếu thì fail fast không?
```

---

## 6. Nullable Reference Types

### Command

```powershell
Get-ChildItem . -Recurse -Include *.csproj,*.cs |
  Select-String -Pattern "<Nullable>|#nullable|string\?|null!|required "
```

### Cần đánh giá

```text
Project có bật Nullable không?
DTO/event/config có dùng string? đúng nghĩa không?
Có dùng null! quá nhiều để che warning không?
```

---

## 7. Equality / Idempotency key

### Command

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "record |Equals\(|GetHashCode|HashSet|Dictionary|Distinct|GroupBy|EventId|ProviderEventId|Idempotency"
```

### Cần đánh giá

```text
Dedup dùng unique constraint hay in-memory collection?
Nếu dùng HashSet/Dictionary key, equality có đúng không?
Record event có value equality phù hợp không?
```

---

## 8. DateTime / UTC

### Command

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "DateTime.Now|DateTime.UtcNow|DateTimeOffset|OccurredAtUtc|CreatedAtUtc|ProcessedAtUtc|TimeoutAtUtc|TimeProvider"
```

### Cần đánh giá

```text
Có DateTime.Now trong event/DB core không?
Event có OccurredAtUtc không?
Outbox/Webhook/Saga dùng UTC không?
Timeout logic có test được không?
```

---

## 9. Serialization

### Command

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "JsonSerializer|JsonStringEnumConverter|PropertyNamingPolicy|JsonPropertyName|Deserialize|Serialize"
```

### Cần đánh giá

```text
Event/message deserialize ở đâu?
Unknown field có làm fail không?
Enum mới có làm consumer cũ chết không?
DateTime serialized theo UTC chưa?
```

---

## 10. LINQ/IQueryable

### Command

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "ToList\(|AsEnumerable\(|IQueryable|Include\(|Where\(|Select\("
```

### Cần đánh giá

```text
Có ToList quá sớm không?
Filter/paging có push xuống DB không?
Có N+1 rõ ràng không?
```

---

# Evidence template

```text
Topic:
Repo location:
Command:
Observed:
Risk:
Fix/Decision:
Interview story:
Limitation:
```
