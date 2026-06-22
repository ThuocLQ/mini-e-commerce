# Module 01 - .NET/C# Runtime - Practice Checklist V2

## 1. Repo audit commands

### Blocking async

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "\.Result|\.Wait\("
```

Ghi:

```text
Có không?
Production code hay test?
Có cần refactor await không?
```

### BackgroundService + CancellationToken

```powershell
Get-ChildItem Workers -Recurse -Filter *.cs |
  Select-String -Pattern "BackgroundService|ExecuteAsync|CancellationToken|stoppingToken"
```

Ghi:

```text
Worker có dừng graceful không?
Có truyền token xuống call con không?
```

### DI lifetime

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "AddSingleton|AddScoped|AddTransient|CreateScope|IServiceScopeFactory"
```

Ghi:

```text
Có scoped -> singleton risk không?
BackgroundService tạo scope đúng không?
```

### HttpClient

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "HttpClient|IHttpClientFactory|AddHttpClient|Refit|Grpc"
```

Ghi:

```text
Outbound call dùng gì?
Có HttpClientFactory không?
```

### Nullable

```powershell
Get-ChildItem . -Recurse -Include *.csproj,*.cs |
  Select-String -Pattern "<Nullable>|string\?|null!|required "
```

Ghi:

```text
Nullable bật chưa?
Có null! lạm dụng không?
```

### Equality / Idempotency

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "record |Equals\(|GetHashCode|HashSet|Dictionary|EventId|ProviderEventId|Idempotency"
```

Ghi:

```text
Dedup dựa vào gì?
Equality có ảnh hưởng không?
```

### DateTime / UTC

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "DateTime.Now|DateTime.UtcNow|DateTimeOffset|OccurredAtUtc|CreatedAtUtc|ProcessedAtUtc|TimeoutAtUtc|TimeProvider"
```

Ghi:

```text
Có DateTime.Now trong core flow không?
Event/Outbox/Webhook/Saga dùng UTC chưa?
```

### Serialization

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "JsonSerializer|JsonStringEnumConverter|JsonPropertyName|Deserialize|Serialize"
```

Ghi:

```text
Deserialize event ở đâu?
Unknown field/version/enum xử lý thế nào?
```

---

## 2. Bài tập nói 60-90 giây

```text
1. async/await hoạt động thế nào?
2. Task khác Thread thế nào?
3. Vì sao .Result/.Wait nguy hiểm?
4. CancellationToken dùng để làm gì?
5. DI lifetime khác nhau thế nào?
6. Vì sao không inject scoped vào singleton?
7. Nullable Reference Types giải quyết gì?
8. record có immutable tuyệt đối không?
9. Equals/GetHashCode ảnh hưởng HashSet/Dictionary thế nào?
10. lock vs SemaphoreSlim khác gì?
11. DateTime.UtcNow và DateTime.Now khác gì?
12. HttpClientFactory giải quyết gì?
13. System.Text.Json trong event-driven cần chú ý gì?
```

---

## 3. Evidence tối thiểu cần có

```text
[ ] 1 evidence về blocking async hoặc xác nhận không có
[ ] 1 evidence về BackgroundService + CancellationToken
[ ] 1 evidence về DI lifetime/scope
[ ] 1 evidence về HttpClientFactory/outbound call
[ ] 1 evidence về DateTime UTC
[ ] 1 evidence về serialization/event contract nếu có
```

## 4. Done checklist

```text
[ ] Tôi giải thích được async/await không tạo thread mới.
[ ] Tôi phân biệt được Task vs Thread.
[ ] Tôi biết vì sao .Result/.Wait nguy hiểm.
[ ] Tôi biết dùng CancellationToken trong worker/request.
[ ] Tôi biết DI lifetime và lỗi scoped -> singleton.
[ ] Tôi biết HttpClientFactory dùng để làm gì.
[ ] Tôi biết IEnumerable vs IQueryable.
[ ] Tôi hiểu Nullable Reference Types.
[ ] Tôi hiểu equality/GetHashCode/record equality.
[ ] Tôi biết concurrency basics: lock/SemaphoreSlim/race condition.
[ ] Tôi biết dùng UTC/DateTimeOffset đúng chỗ.
[ ] Tôi hiểu serialization compatibility cơ bản.
[ ] Tôi có ít nhất 3 evidence từ MicroShop.
```
