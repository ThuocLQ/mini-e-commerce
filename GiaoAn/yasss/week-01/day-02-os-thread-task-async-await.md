# Day 02 - OS, Thread, Task, async/await

## 1. Câu chuyện đời thường

Tưởng tượng một **team dự án**.

```text
ThreadPool = cả team dự án.
Thread = một nhân sự trong team.
Task = một đầu việc.
I/O = chờ khách hàng/DB/service khác phản hồi.
await = tạm cất việc đang chờ phản hồi.
```

Nếu A đang làm Task 1 nhưng phải chờ khách phản hồi, A không nên ngồi im. A quay lại team nhận Task 2. Khi khách phản hồi Task 1, có thể A hoặc B xử lý tiếp.

Đó là tinh thần của non-blocking I/O.

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Process | Chương trình/service đang chạy | OrderingService process |
| Thread | Người chạy code | Nhân sự A |
| CPU core | Bàn làm việc thật | Lõi CPU |
| OS Scheduler | Người phân ca CPU | HĐH chọn thread nào chạy |
| Context switch | Đổi người ngồi bàn CPU | A dừng, B chạy |
| I/O | Chờ bên ngoài | DB/HTTP/Kafka |
| Blocking I/O | Nhân sự ngồi chờ | .Result/.Wait |
| Non-blocking I/O | Nhân sự quay lại pool | await DB async |
| Task | Đầu việc trong .NET | GetOrderAsync |
| ThreadPool | Team thread có sẵn | .NET ThreadPool |

---

## 3. Process, Thread, CPU core

### Process

Process = chương trình đang chạy.

Ví dụ:

```text
ApiGateway là một process/container.
OrderingService là một process/container.
ProjectionWorker là một process/container.
```

### Thread

Thread = luồng thực thi code bên trong process.

Nói dễ hiểu:

```text
Process = công ty.
Thread = nhân viên.
```

Một thread tại một thời điểm chỉ chạy một đoạn code.

### CPU core

CPU core = nơi thực sự chạy code.

Nói dễ hiểu:

```text
CPU core = bàn làm việc.
Thread = nhân viên ngồi vào bàn để làm.
```

Nếu có 8 core mà 100 thread, hệ điều hành phải luân phiên cho thread chạy.

---

## 4. OS Scheduler và Context switch

OS Scheduler = bộ lập lịch của hệ điều hành.

Nó quyết định:

```text
Thread nào được chạy.
Chạy trên CPU core nào.
Chạy trong bao lâu.
```

Context switch = chuyển CPU từ thread này sang thread khác.

Quá nhiều thread không chắc nhanh hơn, vì context switch cũng tốn chi phí.

Câu nhớ:

```text
Nhiều nhân viên hơn không có nghĩa nhanh hơn nếu bàn làm việc có hạn và cứ đổi người liên tục.
```

---

## 5. I/O, Blocking, Non-blocking

### I/O là gì?

I/O là việc app phải chờ bên ngoài.

Ví dụ MicroShop:

```text
Chờ PostgreSQL.
Chờ MongoDB.
Chờ CatalogService.
Chờ Kafka/RabbitMQ.
```

### Blocking I/O

Blocking = thread đứng chờ.

```text
A gọi khách hàng.
Khách chưa trả lời.
A ngồi im chờ.
```

Code hay gây blocking:

```csharp
var result = dbCall.Result;
```

### Non-blocking I/O

Non-blocking = không giữ thread trong lúc chờ.

Flow:

```text
A xử lý request 1.
A gọi DB async.
DB chưa trả lời.
Request 1 tạm dừng tại await.
A quay lại ThreadPool.
A xử lý request 2.
DB của request 1 xong.
Một thread rảnh chạy tiếp request 1.
```

Có thể là A, cũng có thể là B.

---

## 6. Task khác Thread thế nào?

Task không phải Thread.

Nói chuẩn nhưng dễ hiểu:

```text
Task là object đại diện cho một đầu việc đang chạy, đang chờ, đã xong, lỗi, hoặc bị cancel.
Thread là người thực thi code.
```

Một Task có thể:

```text
Đang chạy trên Thread A.
Đang chờ I/O và không chiếm thread.
Sau đó chạy tiếp trên Thread B.
```

---

## 7. async/await là gì?

`async/await` là cú pháp C# giúp viết non-blocking I/O dễ đọc.

```csharp
var order = await db.GetOrderAsync(id);
```

Nghĩa dễ hiểu:

```text
Gửi yêu cầu sang DB.
Nếu DB chưa xong, tạm dừng method.
Thread hiện tại được trả về ThreadPool.
Khi DB xong, phần sau await được chạy tiếp bởi thread rảnh.
```

### async/await không làm gì?

```text
Không làm DB nhanh hơn.
Không tạo thread mới cho mỗi request.
Không biến CPU-bound work thành nhẹ hơn.
```

Nó giúp giảm thời gian chết của thread khi chờ I/O.

---

## 8. ThreadPool starvation

ThreadPool starvation = team thread thiếu người rảnh.

Nguyên nhân:

```text
Nhiều thread bị block bởi .Result/.Wait.
Nhiều sync I/O.
Retry quá nhiều làm work queue phình to.
CPU-bound work nặng trong request.
```

Dấu hiệu:

```text
Request timeout.
Latency tăng.
CPU có thể không cao nhưng app vẫn nghẽn.
```

---

## 9. MicroShop connection

Các nơi cần async đúng:

```text
ProjectionWorker ghi MongoDB.
OrderQueryService đọc MongoDB.
BasketService gọi CatalogService.
Outbox publisher publish event.
NotificationWorker xử lý message.
```

Nếu các flow này block thread, service chịu tải kém.

---

## 10. Interview answer mẫu

```text
Task không phải Thread. Thread là luồng thực thi được OS scheduler phân CPU. ThreadPool là nhóm thread .NET tái sử dụng. Task là object đại diện cho một operation bất đồng bộ.

Với I/O-bound operation như DB/HTTP, async/await giúp method tạm dừng tại await và thread hiện tại được trả về ThreadPool thay vì bị block. Khi I/O hoàn thành, phần code sau await được đưa vào queue và một thread rảnh chạy tiếp, có thể là thread cũ hoặc thread khác.

Vì vậy async/await không làm I/O nhanh hơn, nhưng giảm thời gian chết của thread và giúp cùng số thread phục vụ nhiều request hơn.
```

## 11. Checkpoint

```text
1. ThreadPool là gì trong ví dụ team dự án?
2. Task khác Thread thế nào?
3. await I/O chưa xong thì thread đi đâu?
4. Sau await có chắc quay lại thread cũ không?
5. .Result/.Wait gây vấn đề gì?
6. Non-blocking I/O có làm DB nhanh hơn không?
```

## 12. Flashcards

```text
Process = service đang chạy.
Thread = người chạy code.
Task = đầu việc.
ThreadPool = team thread.
I/O = chờ bên ngoài.
Blocking = thread ngồi chờ.
Non-blocking = thread rảnh làm việc khác.
await = tạm dừng method khi chờ I/O.
```
