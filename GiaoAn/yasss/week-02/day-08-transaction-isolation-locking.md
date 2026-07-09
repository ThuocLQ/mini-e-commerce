# Day 08 - Transaction, Isolation, Locking

## 1. Câu chuyện đời thường

Cửa hàng Cam Sành có một **sổ cái**.

Khi khách mua cam, nhân viên phải:

```text
1. Tạo đơn hàng.
2. Trừ cam trong kho.
3. Ghi thanh toán.
4. In biên lai.
```

Nếu làm tới bước 2 rồi lỗi, dữ liệu sẽ sai.

Transaction giúp:

```text
Hoặc tất cả cùng xong.
Hoặc tất cả cùng hủy.
```

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Transaction | Một gói thao tác dữ liệu | Tạo Order + OrderItems + Outbox |
| Commit | Chốt thay đổi | Ghi chính thức vào sổ |
| Rollback | Quay lại trước khi làm | Hủy các bước đã làm |
| ACID | 4 tính chất transaction | Atomicity, Consistency, Isolation, Durability |
| Isolation | Các transaction nhìn thấy nhau thế nào | Người này sửa sổ, người kia thấy gì |
| Locking | Khóa dữ liệu khi đọc/sửa | Khóa trang sổ |
| Deadlock | Hai transaction khóa chéo nhau | A chờ B, B chờ A |
| Dirty read | Đọc dữ liệu chưa commit | Đọc nháp chưa chốt |
| Non-repeatable read | Đọc cùng dòng 2 lần ra khác nhau | Lần 1 Pending, lần 2 Paid |
| Phantom read | Query cùng điều kiện 2 lần ra số dòng khác | Tự nhiên xuất hiện thêm dòng |

---

## 3. Transaction là gì?

Transaction = một nhóm thao tác dữ liệu được xử lý như một đơn vị.

Ví dụ MicroShop:

```text
Tạo Order.
Tạo OrderItems.
Lưu OutboxMessage.
```

Ba việc này nên cùng commit.

Nếu Order được lưu nhưng OutboxMessage không được lưu, event có thể bị mất.

### Interview nói sao?

```text
Transaction đảm bảo một nhóm thay đổi dữ liệu cùng thành công hoặc cùng rollback. Trong MicroShop, tạo order và lưu outbox message nên cùng transaction để tránh DB commit xong nhưng event bị mất.
```

---

## 4. ACID nói dễ hiểu

```text
Atomicity:
    Tất cả hoặc không gì cả.

Consistency:
    Dữ liệu sau transaction vẫn đúng rule.

Isolation:
    Transaction này không phá transaction kia.

Durability:
    Commit rồi thì dữ liệu phải được lưu bền vững.
```

Ví dụ:

```text
Không nên có Order đã Paid nhưng không có payment record nếu rule yêu cầu phải có.
```

---

## 5. Isolation anomaly: 3 lỗi cần biết tên

Không cần học quá sâu ở Week 2, nhưng đi interview nên biết 3 lỗi này.

### Dirty read

Đọc dữ liệu chưa commit.

Ví dụ:

```text
Transaction A đổi Order thành Paid nhưng chưa commit.
Transaction B đọc thấy Paid.
Sau đó A rollback.
B đã đọc một dữ liệu không bao giờ thật sự tồn tại.
```

### Non-repeatable read

Đọc cùng một dòng 2 lần trong cùng transaction nhưng kết quả khác.

Ví dụ:

```text
Lần 1 đọc Order status = Pending.
Transaction khác commit status = Paid.
Lần 2 đọc lại thấy Paid.
```

### Phantom read

Query cùng điều kiện 2 lần nhưng số dòng khác.

Ví dụ:

```text
Lần 1 query orders status = Pending thấy 10 dòng.
Transaction khác insert thêm một Pending order.
Lần 2 query thấy 11 dòng.
```

---

## 6. Isolation level basic

Isolation level là mức cô lập transaction.

Biết mức cơ bản:

```text
Read Committed:
    Tránh dirty read.
    Thường là default phổ biến ở nhiều DB.

Repeatable Read:
    Giữ việc đọc cùng dòng ổn định hơn trong transaction.

Serializable:
    Mức cô lập mạnh nhất về mặt logic.
    An toàn hơn nhưng có thể lock nhiều/chậm hơn.
```

Câu nhớ:

```text
Isolation càng mạnh -> an toàn hơn nhưng có thể giảm throughput.
Isolation càng yếu -> nhanh hơn nhưng dễ gặp anomaly hơn.
```

---

## 7. Locking và Deadlock

Locking = DB khóa dữ liệu để tránh sửa loạn.

Deadlock = hai transaction chờ nhau.

Ví dụ:

```text
Transaction A khóa Order rồi muốn khóa Payment.
Transaction B khóa Payment rồi muốn khóa Order.
A chờ B.
B chờ A.
```

Cách giảm risk:

```text
Transaction ngắn.
Truy cập resource theo thứ tự nhất quán.
Index tốt để giảm scan/lock rộng.
Retry transaction nếu gặp deadlock và nghiệp vụ cho phép.
```

---

## 8. Không gọi HTTP trong transaction

Nói đời thường:

```text
Đang khóa sổ cái mà còn gọi điện sang phòng khác chờ họ trả lời.
Trong lúc chờ, người khác không sửa được sổ.
```

Rủi ro:

```text
Giữ lock lâu.
Timeout.
Deadlock.
Throughput kém.
```

Trong microservices, không dùng DB transaction để ôm cả HTTP call sang service khác. Dùng Outbox/Saga/Eventual Consistency ở các bài sau.

---

## 9. MicroShop connection

```text
OrderingService:
    Tạo Order + OrderItems + OutboxMessage cùng transaction.

PaymentService:
    Ghi Payment/WebhookLog/Event cần atomic nếu cùng DB.

Outbox:
    Business data và OutboxMessage cùng commit.

Saga:
    Không dùng một DB transaction xuyên nhiều service.
```

---

## 10. Interview answer mẫu

```text
Transaction đảm bảo một nhóm thay đổi dữ liệu cùng thành công hoặc rollback. Isolation quyết định các transaction nhìn thấy thay đổi của nhau như thế nào. Các anomaly cần biết là dirty read, non-repeatable read và phantom read. Isolation mạnh hơn giúp an toàn hơn nhưng có thể giảm throughput vì lock nhiều hơn.

Trong MicroShop, tạo Order và OutboxMessage nên nằm cùng transaction. Nhưng em tránh giữ transaction quá lâu hoặc gọi HTTP trong transaction vì có thể giữ lock lâu, timeout hoặc deadlock.
```

## 11. Checkpoint

```text
1. Transaction giải quyết lỗi gì?
2. Commit/Rollback là gì?
3. Dirty read là gì?
4. Non-repeatable read là gì?
5. Phantom read là gì?
6. Read Committed tránh được lỗi nào cơ bản?
7. Serializable trade-off là gì?
8. Vì sao không gọi HTTP trong transaction?
```

## 12. Flashcards

```text
Transaction = một gói thay đổi.
Commit = chốt.
Rollback = quay lại.
Isolation = transaction nhìn thấy nhau thế nào.
Dirty read = đọc dữ liệu chưa commit.
Non-repeatable read = đọc cùng dòng 2 lần ra khác.
Phantom read = query lại tự có thêm/mất dòng.
Deadlock = khóa chéo chờ nhau.
```
