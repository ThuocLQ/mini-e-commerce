# Day 09 - Index, Query, Pagination, IQueryable

## 1. Câu chuyện đời thường

Có một cuốn sổ bán cam rất dày.

Muốn tìm đơn của khách `Thuoc`, có 2 cách:

```text
Cách 1:
    Lật từng trang từ đầu tới cuối.

Cách 2:
    Dùng mục lục theo tên khách rồi nhảy tới đúng trang.
```

Index trong database giống **mục lục sách**.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Index | Mục lục giúp tìm nhanh | Index CustomerId |
| Query plan | Kế hoạch DB chạy query | Dùng index hay scan |
| Table scan | Quét toàn bảng | Lật từng trang |
| IQueryable | Query còn có thể dịch xuống DB | db.Orders.Where(...) |
| IEnumerable | Dữ liệu xử lý trong memory | list.Where(...) |
| Materialize | Lấy data thật về memory | ToList() |
| Offset pagination | Bỏ qua N dòng rồi lấy tiếp | Skip/Take |
| Cursor pagination | Lấy tiếp sau một mốc | after id/date |
| N+1 query | 1 query chính kéo theo nhiều query nhỏ | Loop order rồi query items |

---

## 3. Index là gì?

Index giúp DB tìm dữ liệu nhanh hơn.

Ví dụ query thường gặp:

```text
Tìm order theo CustomerId.
Tìm order theo Status.
Tìm webhook theo ProviderEventId.
Tìm order theo CreatedAt.
```

Nếu có index phù hợp, DB không cần quét toàn bảng.

### Trade-off

Index không miễn phí:

```text
Tốn thêm storage.
Insert/update/delete chậm hơn vì phải cập nhật index.
Index sai query pattern thì ít tác dụng.
```

Interview nói:

```text
Index giúp giảm số dòng DB phải scan, nhưng có trade-off về storage và write overhead. Em chọn index dựa trên query pattern thực tế, không tạo bừa.
```

---

## 4. Query plan mindset

Query plan là kế hoạch DB chọn để chạy query.

Nói dễ hiểu:

```text
DB quyết định dùng mục lục hay lật từng trang.
```

Nếu query chậm, đừng đoán mò. Cần xem:

```text
Query plan.
Index có được dùng không.
Số dòng scan.
Sort/filter có đẩy xuống DB không.
```

---

## 5. IQueryable vs IEnumerable

### IQueryable

IQueryable giống tờ yêu cầu gửi cho DB:

```text
DB ơi, hãy lọc/sort/paging giúp tôi.
```

Ví dụ:

```csharp
var query = db.Orders
    .Where(x => x.Status == "Paid");
```

Nếu chưa `ToList`, provider còn có thể dịch sang SQL.

### IEnumerable

IEnumerable thường là dữ liệu đã ở memory.

Ví dụ xấu:

```csharp
var orders = db.Orders.ToList();
var paid = orders.Where(x => x.Status == "Paid");
```

Lỗi:

```text
Bê cả kho cam về nhà rồi mới chọn cam ngon.
```

Ví dụ tốt hơn:

```csharp
var paid = await db.Orders
    .Where(x => x.Status == "Paid")
    .ToListAsync();
```

Filter được đẩy xuống DB.

---

## 6. IQueryable không phải magic

Điểm senior cần nói:

```text
IQueryable tốt khi expression có thể được provider translate sang SQL/query backend.
```

Không phải logic C# nào DB cũng hiểu.

Ví dụ risk:

```text
Gọi method C# tự viết trong Where.
Xử lý string/date phức tạp không translate được.
Dùng logic business dài trong LINQ query.
```

Kết quả có thể:

```text
Provider báo lỗi không translate được.
Hoặc query bị xử lý phía client nếu framework/setting cho phép.
```

Câu nhớ:

```text
IQueryable giúp đẩy query xuống DB, nhưng phải viết query mà provider hiểu.
```

---

## 7. Materialize và ToList quá sớm

Materialize = lấy dữ liệu thật về memory.

Ví dụ:

```text
ToList()
First()
Single()
Count()
```

Không phải `ToList` luôn sai. Sai là `ToList` quá sớm trước khi filter/sort/paging.

Rule:

```text
Where/OrderBy/Skip/Take trước.
ToList sau.
```

---

## 8. Pagination

Pagination = chia dữ liệu thành trang.

### Offset pagination

```csharp
Skip(100).Take(20)
```

Ưu điểm:

```text
Dễ làm.
Dễ nhảy page.
```

Nhược điểm:

```text
Page sâu có thể chậm.
Dữ liệu thay đổi liên tục có thể bị trùng/mất dòng.
```

### Cursor pagination

Lấy tiếp sau một mốc:

```text
Lấy 20 order sau CreatedAt/OrderId cuối cùng đã thấy.
```

Ưu điểm:

```text
Tốt hơn cho dữ liệu lớn/feed liên tục.
Ổn định hơn khi dữ liệu thay đổi.
```

---

## 9. N+1 query

N+1 là lỗi query phổ biến.

Ví dụ:

```text
Query 1 lần lấy 100 orders.
Sau đó loop từng order để query items.
=> 1 + 100 queries.
```

Rủi ro:

```text
API chậm.
DB bị spam query nhỏ.
Khó thấy khi dữ liệu ít.
```

Cách nghĩ:

```text
Load dữ liệu liên quan có chủ đích.
Projection DTO đúng nhu cầu.
Dùng include/select phù hợp.
```

---

## 10. MicroShop connection

```text
OrderQueryService:
    Query MongoDB order_summaries.
    Cần filter/paging theo customerId/status/date.

PaymentService:
    WebhookLog cần unique/index theo ProviderEventId.

Outbox:
    Index theo status/createdAt để publisher lấy message pending.

Admin/recovery:
    Query log/failure cần pagination.
```

---

## 11. Interview answer mẫu

```text
Index giống mục lục giúp DB tìm dữ liệu nhanh hơn, nhưng có trade-off về storage và write overhead nên cần dựa trên query pattern. Với EF, IQueryable cho phép provider dịch filter/sort/paging xuống DB, còn IEnumerable thường xử lý trong memory. Em tránh ToList quá sớm trước khi Where/OrderBy/Skip/Take. Tuy nhiên IQueryable không phải magic; expression phải translate được sang SQL. Với dữ liệu lớn, offset pagination dễ làm nhưng page sâu có thể chậm, còn cursor pagination phù hợp hơn cho feed hoặc dữ liệu thay đổi liên tục.
```

## 12. Checkpoint

```text
1. Index giúp gì?
2. Index có trade-off gì?
3. Query plan là gì?
4. IQueryable khác IEnumerable thế nào?
5. Vì sao ToList quá sớm nguy hiểm?
6. IQueryable không phải magic nghĩa là gì?
7. Offset pagination yếu ở đâu?
8. N+1 query là gì?
```

## 13. Flashcards

```text
Index = mục lục.
Query plan = kế hoạch DB chạy query.
Table scan = quét toàn bảng.
IQueryable = query còn đẩy được xuống DB.
IEnumerable = xử lý trong memory.
Materialize = lấy data thật về memory.
Offset = Skip/Take.
Cursor = lấy tiếp sau một mốc.
N+1 = 1 query chính + nhiều query phụ.
```
