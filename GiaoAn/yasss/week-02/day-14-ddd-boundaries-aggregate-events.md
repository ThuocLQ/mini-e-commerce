# Day 14 - DDD Boundaries, Aggregate, Domain Event vs Integration Event

## 1. Câu chuyện đời thường

MicroShop giống một khu chợ nhiều quầy:

```text
Catalog:
    Quản lý sản phẩm.

Basket:
    Quản lý giỏ hàng.

Ordering:
    Quản lý đơn hàng.

Payment:
    Quản lý thanh toán.
```

Mỗi quầy có ngôn ngữ và luật riêng.

DDD giúp chia đúng ranh giới nghiệp vụ.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| DDD | Thiết kế theo nghiệp vụ | Model theo Ordering/Payment |
| Domain | Vùng nghiệp vụ | Ordering domain |
| Bounded Context | Ranh giới model/ngôn ngữ | Catalog vs Ordering |
| Aggregate | Cụm object cần nhất quán | Order + OrderItems |
| Aggregate Root | Object gốc kiểm soát aggregate | Order |
| Invariant | Luật luôn phải đúng | Total = sum items |
| Entity | Object có identity | Order |
| Value Object | Object so bằng giá trị | Money |
| Domain Event | Event nội bộ domain | OrderPaidDomainEvent |
| Integration Event | Event gửi ra service khác | OrderPaidIntegrationEvent |
| Anti-corruption Layer | Lớp dịch model ngoài | Provider status -> PaymentStatus |

---

## 3. Bounded Context

Bounded Context = ranh giới nơi một model/ngôn ngữ có ý nghĩa riêng.

Ví dụ:

```text
Catalog hiểu Product là thông tin bán hàng.
Basket hiểu Product là item trong giỏ.
Ordering hiểu Product là snapshot trong order item.
```

Cùng từ "Product", nhưng ý nghĩa khác nhau theo context.

Không nên ép một model Product dùng chung mọi service nếu rule khác nhau.

---

## 4. Aggregate và Aggregate Root

Aggregate = cụm object cần nhất quán.

Ví dụ:

```text
Order
    OrderItems
```

Aggregate Root = object gốc kiểm soát thay đổi.

```text
Order là aggregate root.
Muốn thêm/sửa OrderItem nên đi qua Order.
```

### Aggregate là consistency boundary

Điểm interview quan trọng:

```text
Aggregate thường là ranh giới consistency/transaction trong domain.
```

Nói dễ hiểu:

```text
Những thứ phải đúng cùng nhau ngay lập tức nên nằm trong cùng aggregate.
Những thứ có thể eventual consistency thì không nên nhét chung.
```

Ví dụ:

```text
Order và OrderItems cần nhất quán cùng nhau.
Payment không nhất thiết nằm trong Order aggregate nếu Payment là context/service riêng.
CatalogProduct cũng không nên nhét vào Order aggregate.
```

Nếu aggregate quá to:

```text
Transaction lớn.
Lock nhiều.
Khó scale.
Dễ coupling giữa context.
```

---

## 5. Invariant

Invariant = luật luôn phải đúng.

Ví dụ:

```text
Order total = tổng OrderItems.
Quantity phải > 0.
Không được Paid một Order đã Cancelled.
Không được thêm item sau khi Order đã Paid.
```

Aggregate Root bảo vệ invariant.

---

## 6. Entity vs Value Object

### Entity

Có identity.

```text
Order có OrderId.
Customer có CustomerId.
```

### Value Object

So bằng giá trị, không quan trọng ID riêng.

```text
Money(100000, "VND")
Address
DateRange
```

Hai Money cùng amount/currency thì coi như bằng nhau.

---

## 7. Domain Event vs Integration Event

### Domain Event

Dùng nội bộ domain/service.

Ví dụ:

```text
OrderPaidDomainEvent
```

Nói dễ hiểu:

```text
Nói chuyện trong nhà.
```

### Integration Event

Gửi ra ngoài service.

Ví dụ:

```text
OrderPaidIntegrationEvent
```

Nói dễ hiểu:

```text
Gửi thông báo ra ngoài xóm.
```

Integration Event là contract với service khác, nên phải ổn định và backward-compatible hơn.

---

## 8. Anti-corruption Layer

Anti-corruption Layer = lớp dịch để model ngoài không làm bẩn model trong.

Ví dụ:

```text
Payment provider trả:
    SETTLED, FAILED, PENDING.

MicroShop dùng:
    Paid, Failed, Pending.
```

Không nên để provider status lẫn khắp domain.

Nên có adapter/mapper:

```text
Provider response -> internal PaymentStatus
```

---

## 9. MicroShop connection

```text
CatalogService:
    Product context.

BasketService:
    Basket context.

OrderingService:
    Order aggregate, OrderItems, OrderStatus.

PaymentService:
    Payment state, webhook provider mapping.

OrderCreatedIntegrationEvent:
    Integration event gửi ra ngoài.

OrderPaidDomainEvent:
    Nếu dùng nội bộ trong Ordering domain.
```

---

## 10. Lỗi hay hiểu sai

```text
DDD là phải tạo nhiều class phức tạp.
Microservice nào cũng cần DDD nặng.
Bounded Context chỉ là folder/service name.
Aggregate là object lớn chứa mọi thứ.
Aggregate càng to càng tốt.
Domain Event và Integration Event giống nhau.
Dùng chung một model Product cho mọi service luôn tốt.
```

### Câu cân bằng

```text
DDD phù hợp khi nghiệp vụ phức tạp.
Service CRUD/read-only đơn giản không cần ép tactical DDD quá nặng.
```

---

## 11. Interview answer mẫu

```text
DDD giúp chia model theo nghiệp vụ. Bounded Context là ranh giới nơi một model/ngôn ngữ có ý nghĩa riêng, ví dụ Catalog, Basket, Ordering, Payment. Aggregate là cụm object cần nhất quán và thường là consistency/transaction boundary, được kiểm soát bởi Aggregate Root. Ví dụ Order quản lý OrderItems và invariant như total/status transition. Domain Event dùng nội bộ domain, còn Integration Event là contract gửi ra service khác nên cần ổn định và backward-compatible hơn.
```

## 12. Checkpoint

```text
1. Bounded Context là gì?
2. Vì sao Product ở Catalog và Ordering có thể khác nhau?
3. Aggregate là gì?
4. Aggregate Root làm gì?
5. Aggregate là consistency boundary nghĩa là gì?
6. Invariant là gì?
7. Domain Event khác Integration Event thế nào?
8. Anti-corruption Layer dùng khi nào?
```

## 13. Flashcards

```text
DDD = thiết kế theo nghiệp vụ.
Bounded Context = ranh giới model/ngôn ngữ.
Aggregate = cụm object cần nhất quán.
Aggregate Root = object gốc kiểm soát thay đổi.
Consistency boundary = phạm vi cần đúng ngay.
Invariant = luật luôn đúng.
Entity = có identity.
Value Object = so bằng giá trị.
Domain Event = nội bộ domain.
Integration Event = gửi ra ngoài service.
ACL = lớp dịch chống model ngoài làm bẩn domain.
```
