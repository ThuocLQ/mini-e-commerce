# Week 01 - Backend Core V4 Beginner Proof

## 1. Tuần này học gì?

Tuần này học nền backend .NET bằng một flow duy nhất:

```text
Client gửi request
-> ASP.NET Core nhận request
-> middleware/auth/validation xử lý
-> endpoint gọi service
-> service dùng object/data
-> service gọi DB hoặc service khác
-> async/await giúp không block thread khi chờ I/O
-> DI container cấp dependency đúng lifetime
-> API trả response hoặc error chuẩn
```

## 2. Tư tưởng bản V4

Bản này không bắt bro nhớ thuật ngữ bằng định nghĩa khô.

Mỗi khái niệm đi theo 5 bước:

```text
1. Ví dụ đời thường
2. Dịch sang thuật ngữ chuẩn
3. Ví dụ code nhỏ
4. Lỗi hay hiểu sai
5. Câu trả lời interview
```

## 3. Bộ ví dụ dễ nhớ

| Hình ảnh | Dùng để nhớ |
|---|---|
| Cam Sành | Object instance, DI lifetime, race condition |
| Team dự án | ThreadPool, Thread, Task, async/await |
| Quầy tiếp nhận hồ sơ | API pipeline, validation, status code |
| Thẻ ra vào tòa nhà | Authentication, Authorization, JWT |
| Biên lai | Event, immutable, record |
| Mẫu đơn | DTO |
| Hồ sơ nội bộ có mã | Entity, identity, lifecycle |

## 4. Thuật ngữ vẫn giữ chuẩn

Bài vẫn dùng thuật ngữ interview như:

```text
DTO
Entity
Identity
Lifecycle
Immutable
Reference type
Value type
ThreadPool
Task
async/await
CancellationToken
Transient
Scoped
Singleton
Race condition
HttpClientFactory
Middleware
Routing
Authentication
Authorization
JWT
ProblemDetails
API contract
Breaking change
```

Nhưng mỗi thuật ngữ đều được giải thích bằng tiếng dễ hiểu trước khi dùng sâu.

## 5. Output cuối Week 1

Bro cần nói được:

```text
1. DTO, Entity, Event khác nhau thế nào.
2. Identity/lifecycle/immutable nghĩa là gì.
3. Reference type dễ shared mutation ra sao.
4. async/await giúp giảm thời gian chết của thread thế nào.
5. Task không phải Thread.
6. Transient/Scoped/Singleton khác nhau bằng ví dụ Cam Sành.
7. Race condition là gì.
8. HttpClientFactory dùng để gọi service khác an toàn hơn.
9. Request đi qua middleware/routing/endpoint thế nào.
10. Authentication khác Authorization.
11. JWT cần validate những gì.
12. Validation error không nên trả 500.
13. API contract và breaking change là gì.
```
