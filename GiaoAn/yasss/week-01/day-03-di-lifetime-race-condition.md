# Day 03 - DI lifetime, CancellationToken, Race condition

## 1. Câu chuyện đời thường

Dùng ví dụ **Cam Sành** để hiểu DI lifetime.

```text
DI container = kho phát cam.
Service = người xin cam.
Object instance = quả cam cụ thể.
```

Ba kiểu phát cam:

```text
Transient:
    Mỗi lần xin một quả mới.

Scoped:
    Một request dùng chung một quả.

Singleton:
    Cả app dùng chung một quả.
```

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ Cam Sành |
|---|---|---|
| DI container | Kho cấp object | Kho phát cam |
| Dependency | Thứ class cần dùng | Repository/DbContext |
| Lifetime | Object sống bao lâu | Quả cam dùng trong phạm vi nào |
| Transient | Mỗi lần resolve tạo mới | Xin lần nào quả mới lần đó |
| Scoped | Một scope/request một object | Một request một quả |
| Singleton | Cả app một object | Cả app một quả |
| Mutable state | Dữ liệu sửa được | SoLanBiVat |
| Race condition | Nhiều thread sửa chung gây sai | 2 người cùng tăng số lần vắt |
| CancellationToken | Tín hiệu dừng | Khách hủy đơn |

---

## 3. DI là gì?

DI = Dependency Injection.

Nói dễ hiểu:

```text
Class không tự tạo đồ nó cần.
Nó xin đồ từ DI container.
```

Ví dụ:

```csharp
public class OrderService
{
    private readonly IOrderRepository _repo;

    public OrderService(IOrderRepository repo)
    {
        _repo = repo;
    }
}
```

`OrderService` cần repository. DI đưa repository vào.

---

## 4. Transient

```csharp
builder.Services.AddTransient<CamSanh>();
```

Mỗi lần xin là một quả mới.

```text
Request 1:
    Service A xin -> Cam #1
    Service B xin -> Cam #2

Request 2:
    Service A xin -> Cam #3
```

Dùng khi object nhẹ, không cần giữ state chung.

---

## 5. Scoped

```csharp
builder.Services.AddScoped<CamSanh>();
```

Trong cùng request dùng chung một quả.

```text
Request 1:
    Service A xin -> Cam #1
    Service B xin -> vẫn Cam #1

Request 2:
    Service A xin -> Cam #2
```

`DbContext` thường scoped vì trong một request, nhiều repository nên cùng dùng chung một unit of work.

### Unit of work là gì?

Nói dễ hiểu:

```text
Một nhóm thay đổi dữ liệu trong cùng một lượt xử lý.
```

Ví dụ tạo order:

```text
Tạo Order.
Tạo OrderItems.
Lưu thay đổi cùng một request/transaction.
```

---

## 6. Singleton

```csharp
builder.Services.AddSingleton<CamSanh>();
```

Cả app dùng chung một quả.

```text
App start -> Cam #1
Request 1 -> Cam #1
Request 2 -> Cam #1
Worker -> Cam #1
App tắt -> Cam #1 hết vòng đời
```

Singleton hợp với object stateless hoặc thread-safe.

### Stateless là gì?

Stateless = không giữ dữ liệu thay đổi theo request.

Ví dụ:

```text
Service chỉ chứa method tính toán.
Không lưu CustomerId hiện tại.
Không lưu SoLanBiVat.
```

---

## 7. Mutable state và Race condition

### Mutable state

Mutable state = dữ liệu bên trong object có thể sửa.

```csharp
public class CamSanh
{
    public int SoLanBiVat { get; set; }
}
```

### Race condition

Race condition = nhiều thread/request cùng sửa dữ liệu chung, kết quả sai vì thứ tự chạy không đoán được.

Ví dụ Singleton Cam:

```text
SoLanBiVat = 0

Request 1 đọc 0.
Request 2 cũng đọc 0.

Request 1 tính 0 + 1 = 1.
Request 2 tính 0 + 1 = 1.

Request 1 ghi 1.
Request 2 ghi 1.
```

Kết quả cuối = 1, đúng ra phải = 2.

Câu nhớ:

```text
Singleton không nguy hiểm vì singleton.
Singleton nguy hiểm khi nó giữ mutable state và nhiều thread cùng sửa.
```

---

## 8. Scoped service trong Singleton

Lỗi hay gặp:

```csharp
public class ProjectionWorker : BackgroundService
{
    private readonly AppDbContext _db;

    public ProjectionWorker(AppDbContext db)
    {
        _db = db;
    }
}
```

`BackgroundService` sống lâu như singleton. `DbContext` thường scoped.

Nói dễ hiểu:

```text
Worker sống cả đời app.
Nhưng nó cầm quả cam chỉ nên dùng trong một request.
Cầm quá lâu là sai vòng đời.
```

Cách đúng:

```csharp
using var scope = serviceProvider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
```

---

## 9. CancellationToken

CancellationToken = tín hiệu dừng.

Ví dụ đời thường:

```text
Khách hủy đơn.
Không cần tiếp tục đóng gói cam nữa.
```

Trong code:

```csharp
await db.SaveChangesAsync(cancellationToken);
```

Nó không kill thread. Code phải tự hợp tác bằng cách truyền token xuống DB/HTTP/message calls hoặc check token trong loop.

---

## 10. MicroShop connection

```text
DbContext/repository thường Scoped.
ProjectionWorker/NotificationWorker là BackgroundService.
Worker cần scoped dependency thì tạo scope.
Singleton service nên stateless hoặc thread-safe.
CancellationToken dùng để shutdown worker/request an toàn.
```

---

## 11. Interview answer mẫu

```text
Transient tạo instance mới mỗi lần resolve. Scoped tạo một instance trong một scope, với web app thường là một request. Singleton tạo một instance dùng chung toàn app.

DbContext thường scoped. Không nên inject scoped service vào singleton như BackgroundService vì lifetime mismatch. Nếu worker cần DbContext/repository thì tạo scope mới. Singleton nên stateless hoặc thread-safe, nếu giữ mutable state thì nhiều request/thread cùng sửa có thể gây race condition.
```

## 12. Checkpoint

```text
1. Transient là mỗi lần xin quả mới hay dùng chung?
2. Scoped trong cùng request có dùng chung object không?
3. Singleton nguy hiểm khi nào?
4. Race condition là gì bằng ví dụ SoLanBiVat?
5. Vì sao BackgroundService không nên giữ DbContext scoped trực tiếp?
6. CancellationToken có kill thread không?
```

## 13. Flashcards

```text
DI = kho cấp dependency.
Lifetime = object sống bao lâu.
Transient = mỗi lần resolve instance mới.
Scoped = một request/scope một instance.
Singleton = cả app một instance.
Mutable state = dữ liệu sửa được.
Race condition = nhiều thread sửa chung gây kết quả sai.
CancellationToken = tín hiệu dừng cooperative.
```
