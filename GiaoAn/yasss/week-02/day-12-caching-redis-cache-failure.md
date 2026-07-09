# Day 12 - Caching, Redis, Cache Invalidation, Cache Failure

## 1. Câu chuyện đời thường

Cửa hàng Cam Sành có một **tủ đồ gần quầy**.

Thay vì mỗi lần hỏi giá lại chạy vào kho, nhân viên để bảng giá gần quầy.

Đó là cache.

Cache giúp nhanh hơn, nhưng nếu bảng giá cũ, nhân viên có thể báo sai.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Cache | Bản sao để đọc nhanh | Redis basket |
| Redis | Kho cache ngoài app | BasketService dùng Redis |
| Cache hit | Có trong cache | Tìm thấy basket |
| Cache miss | Không có trong cache | Phải đọc DB/service |
| TTL | Thời gian sống | 10 phút hết hạn |
| Invalidation | Xóa/cập nhật cache cũ | Product đổi giá |
| Stale data | Dữ liệu cũ | Giá cũ |
| Cache-aside | Miss thì đọc source rồi set cache | app tự quản |
| Cache stampede | Nhiều request cùng miss đập DB | hot key hết hạn |
| Source of truth | Nơi dữ liệu thật nằm | DB/service owner |

---

## 3. Cache là gì?

Cache = bản sao dữ liệu để đọc nhanh hơn.

Nhưng cache thường không phải source of truth.

Ví dụ:

```text
Basket trong Redis:
    cache/store nhanh cho giỏ hàng.

Order/payment state:
    phải rất cẩn thận nếu cache, vì stale data có thể làm user hiểu sai trạng thái.
```

---

## 4. Cache-aside Pattern

Flow:

```text
1. App đọc cache.
2. Nếu có -> trả luôn.       (cache hit)
3. Nếu không có -> đọc DB/service. (cache miss)
4. Set cache.
5. Trả kết quả.
```

Ví dụ:

```text
BasketService đọc basket từ Redis.
Nếu không có, tạo basket rỗng hoặc đọc source tùy design.
```

---

## 5. TTL và Stale Data

TTL = thời gian cache sống.

TTL dài:

```text
Giảm tải DB/service.
Nhưng dữ liệu dễ cũ.
```

TTL ngắn:

```text
Dữ liệu mới hơn.
Nhưng cache hit thấp hơn.
```

Stale data = dữ liệu cũ.

Ví dụ:

```text
Catalog đổi giá cam.
Cache vẫn giữ giá cũ.
Client thấy giá sai.
```

---

## 6. Cache Invalidation

Invalidation = xóa/cập nhật cache cũ khi dữ liệu thật đổi.

Cache invalidation khó vì:

```text
Cache có thể ở nhiều nơi.
Event update có thể đến muộn.
Xóa cache có thể fail.
Race condition giữa update DB và update cache.
```

Câu nhớ:

```text
Cache nhanh, nhưng cái khó là làm sao không dùng dữ liệu cũ sai lúc quan trọng.
```

---

## 7. Cache consistency risk: order/payment

Không phải dữ liệu nào cũng cache giống nhau.

### Tương đối an toàn hơn

```text
Product list ít thay đổi.
Basket có thể chịu trade-off tùy design.
Feature flags/config có TTL.
```

### Nguy hiểm hơn

```text
Payment status.
Order final state.
Inventory chính xác cao.
Balance/money.
```

Với dữ liệu tiền/trạng thái quan trọng, nếu cache stale, user hoặc hệ thống có thể ra quyết định sai.

Câu interview tốt:

```text
Em không xem cache là source of truth, đặc biệt với order/payment. Cache phải có TTL/invalidation rõ và fallback/consistency strategy phù hợp.
```

---

## 8. Cache Failure

Redis/cache có thể down.

Cần hỏi:

```text
Redis down thì app chết hay degrade?
Có timeout khi gọi Redis không?
Có fallback DB/source không?
Có metric/log không?
```

Nếu cache chỉ để tối ưu performance, cache down không nên kéo sập toàn hệ thống.

---

## 9. Cache Stampede

Cache stampede = nhiều request cùng cache miss và cùng gọi DB/service.

Ví dụ:

```text
Hot key hết hạn.
1000 request cùng không thấy cache.
1000 request cùng gọi DB.
```

Cách giảm:

```text
Jitter TTL.
Lock/single-flight.
Refresh ahead.
Rate limit.
```

Không cần implement sâu ngay, nhưng phải biết risk.

---

## 10. MicroShop connection

```text
BasketService:
    Redis lưu basket.

Catalog/product validation:
    Có thể cache product info nhưng cần TTL/invalidation.

OrderQueryService:
    Nếu cache order summary, phải nghĩ stale data.

PaymentService:
    Tránh cache bừa payment truth state nếu không có strategy rõ.
```

---

## 11. Interview answer mẫu

```text
Cache giúp giảm latency và giảm tải DB/service bằng cách lưu bản sao dữ liệu đọc nhiều. Với cache-aside, app đọc cache trước, nếu miss thì đọc DB/service rồi set cache. Nhưng cache có trade-off là stale data và invalidation khó. Em không xem cache là source of truth, đặc biệt với order/payment/balance. Redis/cache failure cũng cần timeout/fallback nếu cache chỉ là optimization. Với hot key, cần chú ý cache stampede khi nhiều request cùng miss.
```

## 12. Checkpoint

```text
1. Cache là gì?
2. Source of truth là gì?
3. Cache-aside flow thế nào?
4. TTL trade-off thế nào?
5. Stale data nguy hiểm khi nào?
6. Payment status cache bừa có nguy hiểm không?
7. Cache stampede là gì?
8. Redis down thì nên nghĩ gì?
```

## 13. Flashcards

```text
Cache = bản sao đọc nhanh.
Redis = kho cache ngoài app.
Source of truth = nơi dữ liệu thật nằm.
Hit = có cache.
Miss = không có cache.
TTL = thời gian sống.
Invalidation = xóa/cập nhật cache cũ.
Stale data = dữ liệu cũ.
Stampede = nhiều request cùng miss.
```
