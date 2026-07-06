# Day 01 - Object/Data: DTO, Entity, Event, Nullable

## 1. Câu chuyện đời thường

Tưởng tượng bro mở cửa hàng bán **Cam Sành**.

Có 3 loại giấy tờ:

```text
1. Mẫu đơn khách gửi:
    Khách ghi muốn mua bao nhiêu quả cam.

2. Hồ sơ nội bộ của quả cam/đơn hàng:
    Có mã riêng, trạng thái riêng.

3. Biên lai/thông báo:
    Ghi lại việc "đơn hàng đã được tạo".
```

Trong backend:

```text
Mẫu đơn khách gửi       -> DTO
Hồ sơ nội bộ có mã      -> Entity
Biên lai/thông báo      -> Event
```

Nếu hiểu được 3 loại này, Day 01 sẽ dễ hơn nhiều.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ MicroShop |
|---|---|---|
| DTO | Mẫu dữ liệu ra/vào API | CreateOrderRequest |
| Entity | Object nghiệp vụ có mã riêng | Order |
| Identity | Danh tính/mã riêng | OrderId |
| Lifecycle | Vòng đời trạng thái | Created -> Paid -> Cancelled |
| Event | Thông báo việc đã xảy ra | OrderCreatedIntegrationEvent |
| Immutable | Không sửa sau khi tạo | Event giống biên lai |
| Value type | Copy giá trị | int, decimal |
| Reference type | Copy địa chỉ object | class Order |
| Nullable | Có thể thiếu/null | string? ProviderEventId |

---

## 3. DTO là gì?

DTO = Data Transfer Object.

Nói dễ hiểu:

```text
DTO là mẫu đơn để chuyển dữ liệu qua biên API.
```

Ví dụ:

```csharp
public record CreateOrderRequest(
    Guid CustomerId,
    List<OrderItemRequest> Items
);
```

Nó chỉ là dữ liệu client gửi lên.

Không nên coi DTO là object nghiệp vụ thật.

### Lỗi hay gặp

```text
Dùng DTO như Entity.
Expose thẳng Entity ra API.
Cho DTO chứa quá nhiều business behavior.
```

### Interview nói sao?

```text
DTO là object dùng để transfer data giữa client và API hoặc giữa các layer. Nó giúp tách API contract khỏi domain model.
```

---

## 4. Entity, Identity, Lifecycle

### Entity là gì?

Entity là object nghiệp vụ có danh tính riêng.

### Identity là gì?

Identity = mã riêng giúp nhận ra object đó.

Ví dụ Cam Sành:

```text
Cam #CAM001 hôm nay còn nguyên.
Mai bị bán.
Mốt đã giao cho khách.
```

Dù trạng thái đổi, nó vẫn là Cam #CAM001.

Ví dụ MicroShop:

```text
OrderId = 123
Status ban đầu = Created
Sau đó = Paid
```

Dù status đổi, nó vẫn là order 123.

### Lifecycle là gì?

Lifecycle = vòng đời/trạng thái thay đổi theo thời gian.

Ví dụ:

```text
Created -> PendingPayment -> Paid -> Cancelled
```

### Code ví dụ

```csharp
public class Order
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "Created";
}
```

### Interview nói sao?

```text
Entity là object nghiệp vụ có identity và lifecycle. Ví dụ Order có OrderId riêng và trạng thái thay đổi từ Created sang Paid hoặc Cancelled.
```

---

## 5. Event và Immutable

### Event là gì?

Event là thông báo một việc đã xảy ra.

Ví dụ đời thường:

```text
Biên lai ghi: "Đã bán Cam #CAM001".
```

Ví dụ MicroShop:

```csharp
public record OrderCreatedIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    DateTimeOffset OccurredAtUtc
);
```

Nó nghĩa là:

```text
Order đã được tạo.
```

### Immutable là gì?

Immutable = không sửa sau khi tạo.

Nói dễ hiểu:

```text
Biên lai đã in thì không tẩy sửa.
Nếu sai, tạo phiếu điều chỉnh.
```

Event nên gần với immutable vì event là sự thật đã xảy ra.

### record có immutable không?

Record giúp viết dữ liệu kiểu ít sửa dễ hơn, nhưng không đảm bảo immutable tuyệt đối.

```csharp
public record Demo(List<string> Items);
```

`Items` vẫn sửa được:

```csharp
demo.Items.Add("new item");
```

### Interview nói sao?

```text
Event là thông báo việc đã xảy ra. Event nên được thiết kế gần immutable để consumer nhận dữ liệu đáng tin. Record phù hợp cho event/DTO, nhưng không đảm bảo deep immutability nếu chứa object mutable như List.
```

---

## 6. Value type vs Reference type

### Value type

Giống như copy một tờ giấy.

```csharp
int a = 10;
int b = a;
b = 20;

Console.WriteLine(a); // 10
```

`b` đổi, `a` không đổi.

### Reference type

Giống như hai người cùng cầm một quả cam thật.

```csharp
var cam1 = new CamSanh { TrangThai = "Nguyen ven" };
var cam2 = cam1;

cam2.TrangThai = "Bi bop";

Console.WriteLine(cam1.TrangThai); // Bi bop
```

Vì `cam1` và `cam2` cùng trỏ tới một object.

### Shared mutation là gì?

Shared mutation = nhiều nơi cùng giữ một object, một nơi sửa thì nơi khác bị ảnh hưởng.

Câu dễ nhớ:

```text
Value type = copy tờ giấy.
Reference type = cùng cầm một quả cam.
```

---

## 7. Nullable

Null = không có dữ liệu.

Dấu `?` nghĩa là field có thể null.

```csharp
string? PhoneNumber = null;
```

Nói đời thường:

```text
Ô số điện thoại trên mẫu đơn có thể bị bỏ trống.
```

`null!` nghĩa là:

```text
Tôi bảo compiler đừng cảnh báo, tôi tự chịu trách nhiệm.
```

Nhưng runtime vẫn có thể lỗi nếu giá trị thật sự null.

### MicroShop

```text
ProviderEventId trong webhook có bắt buộc không?
Items trong CreateOrderRequest có được null/rỗng không?
JwtOptions.Issuer có bị thiếu config không?
```

### Interview nói sao?

```text
Nullable Reference Types giúp compiler cảnh báo null risk và làm rõ field nào optional. Nhưng với request, webhook, config vẫn cần validation runtime.
```

---

## 8. Checkpoint

Tự trả lời:

```text
1. DTO khác Entity ở đâu?
2. Vì sao Order là Entity?
3. Event khác command/request ở đâu?
4. Immutable nghĩa là gì?
5. record có immutable tuyệt đối không?
6. Reference type vì sao dễ shared mutation?
7. string? nghĩa là gì?
```

## 9. Flashcards

```text
DTO = mẫu đơn API.
Entity = object nghiệp vụ có mã riêng.
Identity = mã riêng.
Lifecycle = vòng đời trạng thái.
Event = thông báo việc đã xảy ra.
Immutable = không sửa sau khi tạo.
Reference type = nhiều biến có thể cùng trỏ một object.
Nullable = có thể null.
```
