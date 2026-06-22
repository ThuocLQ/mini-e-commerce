# Module 01 - .NET/C# Runtime - Red Flags V2

## async/await

Sai:

```text
async/await tạo thread mới.
```

Đúng:

```text
async/await giúp non-blocking với I/O. Nó không đồng nghĩa tạo thread mới.
```

---

## Task vs Thread

Sai:

```text
Một Task là một Thread.
```

Đúng:

```text
Task là abstraction cho operation. Nó có thể dùng thread, nhưng không đồng nghĩa thread.
```

---

## CancellationToken

Sai:

```text
CancellationToken tự kill task/thread.
```

Đúng:

```text
CancellationToken là cooperative cancellation.
```

---

## Nullable Reference Types

Sai:

```text
string? chỉ là syntax, không quan trọng.
```

Đúng:

```text
NRT giúp compiler cảnh báo null risk, rất hữu ích cho DTO/config/event contract.
```

Sai:

```text
Dùng ! để tắt warning hàng loạt.
```

---

## record

Sai:

```text
record luôn immutable tuyệt đối.
```

Đúng:

```text
record có value equality, nhưng không tự đảm bảo deep immutability nếu property mutable.
```

---

## Equality

Sai:

```text
Override Equals là đủ.
```

Đúng:

```text
Nếu override Equals, thường phải override GetHashCode, đặc biệt khi dùng HashSet/Dictionary.
```

---

## DI lifetime

Sai:

```text
Inject DbContext vào Singleton cho tiện.
```

Đúng:

```text
DbContext scoped. BackgroundService cần tạo scope khi dùng scoped dependency.
```

---

## Concurrency

Sai:

```text
ConcurrentDictionary giúp toàn bộ workflow thread-safe.
```

Đúng:

```text
Nó thread-safe cho operation đơn lẻ, workflow nhiều bước vẫn có thể race.
```

---

## DateTime

Sai:

```text
Dùng DateTime.Now cho event cũng được.
```

Đúng:

```text
Event/DB core nên dùng UTC rõ ràng: OccurredAtUtc, CreatedAtUtc, ProcessedAtUtc.
```

---

## HttpClient

Sai:

```text
using var client = new HttpClient() trong mỗi request.
```

Đúng:

```text
Dùng HttpClientFactory/typed client.
```

---

## Serialization

Sai:

```text
Producer thêm/sửa field event thì consumer tự hiểu.
```

Đúng:

```text
Event contract cần backward compatibility, unknown field/version/enum phải nghĩ trước.
```

---

## Config

Sai:

```text
Config sai thì runtime lỗi rồi sửa sau.
```

Đúng:

```text
Config quan trọng nên validate on startup để fail fast.
```

---

# Final red flag checklist

```text
[ ] Có nói quá tuyệt đối không?
[ ] Có nhầm Task với Thread không?
[ ] Có quên nullability/equality/concurrency/time không?
[ ] Có ví dụ MicroShop không?
[ ] Có nói failure/debug không?
[ ] Có claim kiểu "luôn luôn" mà không có điều kiện không?
```
