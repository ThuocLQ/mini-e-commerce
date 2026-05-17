# HANDOFF SUMMARY — MicroShop .NET Microservices Course + Project UI + Lesson 16 Fix + Next Lesson 17

> Dùng file này để chuyển sang Codex khi session ChatGPT bị dài/treo.  
> Mục tiêu: Codex tiếp tục đúng lộ trình, không làm lệch roadmap, không revert/xóa nhầm các quyết định đã chốt.

---

## 0. Context cực ngắn

Đây là project học/build khóa **MicroShop .NET Microservices VIP Compact V2**.

Mục tiêu là vừa học vừa build một project microservices end-to-end theo lộ trình 60 buổi.

Hiện tại user đã học xong đến:

```text
Buổi 16: Checkout Flow — Basket → Order
```

Roadmap chính xác đang dùng là **Microservices .NET VIP Compact V2**.

Phase 1.4 hiện tại:

```text
Buổi 15: OrderingService
Buổi 16: Checkout Flow
Buổi 17: DiscountService + Strategy Intro
Buổi 18: PaymentService + Payment Webhook Intro
```

**Cảnh báo quan trọng:** Không được tự đổi Buổi 17 thành bài khác. Buổi 17 đúng là:

```text
DiscountService + Strategy Intro
```

Output chính:

```text
Coupon/discount rule
```

Production add-on:

```text
Strategy pattern mindset
```

---

# 1. Mục tiêu cuối cùng

## 1.1. Mục tiêu khóa học

Build project **MicroShop** theo hướng senior backend .NET microservices.

Project cuối Stage 1 cần có:

```text
Client
  ↓
ApiGateway
  ↓
CatalogService  → SQL DB
BasketService   → Redis
IdentityService → JWT/Auth → SSO/OIDC mindset
OrderingService → SQL DB + Outbox basic
DiscountService → coupon/discount rule
PaymentService  → payment giả lập + webhook intro
  ↓
RabbitMQ → NotificationWorker
  ↓
Kafka → ProjectionWorker / AnalyticsWorker
  ↓
MongoDB → Read Model
```

Stage 1 là **Foundation Build**, mục tiêu build MicroShop end-to-end.

Stage 2 là **Production Hardening**, không tạo project mới mà nâng chính MicroShop thành project senior backend.

## 1.2. Mục tiêu trước mắt

Hiện tại đã xong bài 16. Cần làm tiếp theo theo thứ tự:

```text
1. Sửa lại lesson-16-compact.md và lesson-16-interactive.html vì phần cuối đang ghi sai "Buổi 17: Service Communication Hardening Intro".
2. Tạo bài 17 đúng roadmap: DiscountService + Strategy Intro.
```

## 1.3. Mục tiêu project UI học tập

User đang có một project UI static để chứa các bài học HTML:

```text
content/
  lesson-xx-compact.md

lessons/
  lesson-xx.html
```

Project UI có script scan `lessons/*.html` và generate `lessons.json`.

Workflow đã chốt:

```text
MD = source of truth
HTML = bản interactive/e-learning để học
```

Từ nay mỗi bài nên xuất đúng 2 file riêng:

```text
lesson-xx-compact.md
lesson-xx-interactive.html
```

Không zip, không folder.

---

# 2. Kiến trúc/dự án hiện tại

## 2.1. Tên project

Tên project chuẩn:

```text
MicroShop
```

Service catalog chuẩn:

```text
CatalogService
```

Không dùng:

```text
ProductService
```

## 2.2. Services đã có hoặc đang học

Theo tiến độ học hiện tại:

```text
CatalogService
BasketService
ApiGateway
IdentityService
OrderingService
```

Sắp tới:

```text
DiscountService
PaymentService
NotificationWorker
ProjectionWorker / AnalyticsWorker
```

## 2.3. Concepts đã học

Đã học các concept:

```text
Minimal API
CQRS + MediatR
Repository Pattern + Dapper
Redis cho BasketService
REST/Refit communication
gRPC communication
REST vs gRPC + Failure Mindset
YARP API Gateway
Docker Compose cơ bản
.NET Aspire Intro
Config + Local Secrets
Clean Architecture Baseline
Validation + Explicit Mapping
IdentityService + JWT
Role/Claim + Authorization policy
OrderingService
Checkout Flow: Basket → Order
```

Cảnh báo:

```text
gRPC đã học ở Buổi 5.
Không được nói "sau này mới học gRPC từ đầu".
Sau này chỉ áp dụng/refactor/nâng cấp gRPC khi phù hợp.
```

## 2.4. Structure đang hướng tới

Mỗi service mới từ Buổi 11 trở đi nên đi theo folder-based Clean Architecture baseline:

```text
Services/<ServiceName>/
├── API/
│   └── Endpoints/
├── Application/
│   ├── Abstractions/
│   └── <Feature>/
├── Domain/
│   └── <Aggregate>/
├── Infrastructure/
│   ├── Persistence/
│   └── Clients/
├── Program.cs
└── appsettings.Development.json
```

Giai đoạn này chưa bắt buộc tách thành nhiều project:

```text
<ServiceName>.Api
<ServiceName>.Application
<ServiceName>.Domain
<ServiceName>.Infrastructure
```

Hiện tại dùng folder-based architecture để học dependency rule trước.

## 2.5. Dependency rule đã chốt

Code dependency nên theo hướng:

```text
API → Application → Domain
Infrastructure → Application
Infrastructure → Domain
Domain không phụ thuộc technical details
```

Runtime flow có thể đi từ API xuống DB, nhưng code dependency phải bảo vệ Domain/Application khỏi Infrastructure.

Không được để:

```text
Domain → Infrastructure
Domain → Dapper / EF / Redis
Application → API
Handler → DapperRepository concrete class
Endpoint → SQL
```

## 2.6. Pattern đang dùng

```text
Minimal API endpoints
MediatR command/query/handler
Dapper repository
SQLite local DB cho Catalog/Ordering ở giai đoạn học
Redis cho Basket
JWT Bearer auth
Policy-based authorization
Typed HttpClient cho internal REST call
Postman-first testing
```

---

# 3. Những file đã tạo/sửa trong các bài gần đây

> Lưu ý: Đây là summary theo lesson đã build. Codex cần kiểm tra repo thực tế trước khi edit vì user có thể chưa copy hết code vào repo.

## 3.1. Buổi 11 — Clean Architecture Baseline cho CatalogService

Các file/folder đã/đang được thiết kế:

```text
Services/CatalogService/API/Endpoints/ProductEndpoints.cs
Services/CatalogService/Application/Abstractions/IProductRepository.cs
Services/CatalogService/Application/Products/ProductDto.cs
Services/CatalogService/Application/Products/GetProducts/GetProductsQuery.cs
Services/CatalogService/Application/Products/GetProducts/GetProductsHandler.cs
Services/CatalogService/Application/Products/GetProductById/GetProductByIdQuery.cs
Services/CatalogService/Application/Products/GetProductById/GetProductByIdHandler.cs
Services/CatalogService/Application/Products/CreateProduct/CreateProductCommand.cs
Services/CatalogService/Application/Products/CreateProduct/CreateProductHandler.cs
Services/CatalogService/Application/Products/UpdateProduct/UpdateProductCommand.cs
Services/CatalogService/Application/Products/UpdateProduct/UpdateProductHandler.cs
Services/CatalogService/Application/Products/DeleteProduct/DeleteProductCommand.cs
Services/CatalogService/Application/Products/DeleteProduct/DeleteProductHandler.cs
Services/CatalogService/Domain/Products/Product.cs
Services/CatalogService/Infrastructure/Persistence/IDbConnectionFactory.cs
Services/CatalogService/Infrastructure/Persistence/SqliteConnectionFactory.cs
Services/CatalogService/Infrastructure/Persistence/DapperProductRepository.cs
Services/CatalogService/Program.cs
docs/adr/0001-clean-architecture-baseline.md
```

Important decision:

```text
Application DTO tên là ProductDto, không phải ProductResponse.
gRPC generated response có thể tên ProductResponse.
Tránh dùng ProductResponse trong Application để không bị conflict namespace/ambiguous type.
```

## 3.2. Buổi 12 — Validation + Explicit Mapping

Các file đã/đang được thiết kế:

```text
Services/CatalogService/Application/Products/ProductMapper.cs
Services/CatalogService/Application/Products/CreateProduct/CreateProductCommandValidator.cs
Services/CatalogService/Application/Products/UpdateProduct/UpdateProductCommandValidator.cs
Services/CatalogService/API/Endpoints/ProductEndpoints.cs
Services/CatalogService/Program.cs
```

Quyết định:

```text
Endpoint không bind thẳng HTTP request vào Command.
POST/PUT nhận CreateProductRequest / UpdateProductRequest.
Endpoint map Request → Command.
Endpoint gọi FluentValidation thủ công ở baseline.
Handler dùng ProductMapper để map Product → ProductDto.
Repository vẫn trả Product domain model, không trả DTO.
```

## 3.3. Buổi 13 — IdentityService + JWT

Các file đã/đang được thiết kế:

```text
Services/IdentityService/API/Endpoints/AuthEndpoints.cs
Services/IdentityService/Application/Auth/LoginCommand.cs
Services/IdentityService/Application/Auth/LoginHandler.cs
Services/IdentityService/Application/Auth/LoginResult.cs
Services/IdentityService/Domain/Users/AppUser.cs
Services/IdentityService/Infrastructure/Auth/IJwtTokenGenerator.cs
Services/IdentityService/Infrastructure/Auth/JwtTokenGenerator.cs
Services/IdentityService/Program.cs
Services/IdentityService/appsettings.Development.json
```

Endpoints:

```text
POST /auth/login
GET  /auth/me
```

Local demo user:

```text
username: admin
password: Admin@123
role: Admin
id: 11111111-1111-1111-1111-111111111111
```

JWT config:

```json
{
  "Jwt": {
    "Issuer": "MicroShop.IdentityService",
    "Audience": "MicroShop.Client",
    "SecretKey": "microshop-development-secret-key-must-be-long-enough",
    "ExpirationMinutes": 60
  }
}
```

## 3.4. Buổi 14 — Role/Claim + Secure Internal Call Intro

Các file đã/đang được thiết kế:

```text
Services/CatalogService/appsettings.Development.json
Services/CatalogService/Program.cs
Services/CatalogService/API/Endpoints/ProductEndpoints.cs
```

Các thay đổi chính:

```text
CatalogService cài JwtBearer.
CatalogService đọc Jwt:Issuer/Audience/SecretKey.
CatalogService validate token từ IdentityService.
CatalogService tạo policy AdminOnly.
Program.cs có UseAuthentication() trước UseAuthorization().
POST/PUT/DELETE /products gắn .RequireAuthorization("AdminOnly").
GET /products và GET /products/{id} vẫn public.
```

## 3.5. Buổi 15 — OrderingService Foundation

Các file đã/đang được thiết kế:

```text
Services/OrderingService/OrderingService.csproj
Services/OrderingService/Program.cs
Services/OrderingService/appsettings.Development.json

Services/OrderingService/API/Endpoints/OrderEndpoints.cs

Services/OrderingService/Application/Abstractions/IOrderRepository.cs
Services/OrderingService/Application/Orders/OrderDto.cs
Services/OrderingService/Application/Orders/OrderMapper.cs

Services/OrderingService/Application/Orders/CreateOrder/CreateOrderCommand.cs
Services/OrderingService/Application/Orders/CreateOrder/CreateOrderHandler.cs

Services/OrderingService/Application/Orders/GetOrders/GetOrdersQuery.cs
Services/OrderingService/Application/Orders/GetOrders/GetOrdersHandler.cs

Services/OrderingService/Application/Orders/GetOrderById/GetOrderByIdQuery.cs
Services/OrderingService/Application/Orders/GetOrderById/GetOrderByIdHandler.cs

Services/OrderingService/Domain/Orders/Order.cs
Services/OrderingService/Domain/Orders/OrderItem.cs
Services/OrderingService/Domain/Orders/OrderStatus.cs

Services/OrderingService/Infrastructure/Persistence/IDbConnectionFactory.cs
Services/OrderingService/Infrastructure/Persistence/SqliteConnectionFactory.cs
Services/OrderingService/Infrastructure/Persistence/DatabaseInitializer.cs
Services/OrderingService/Infrastructure/Persistence/DapperOrderRepository.cs
```

Endpoints:

```text
POST /orders
GET /orders
GET /orders/{id}
```

Port gợi ý:

```text
OrderingService http://localhost:5004
```

DB:

```text
ordering.db
Tables: Orders, OrderItems
```

## 3.6. Buổi 16 — Checkout Flow: Basket → Order

Các file đã/đang được thiết kế:

```text
Services/OrderingService/appsettings.Development.json

Services/OrderingService/Application/Baskets/BasketDto.cs
Services/OrderingService/Application/Abstractions/IBasketClient.cs

Services/OrderingService/Infrastructure/Clients/HttpBasketClient.cs

Services/OrderingService/Application/Orders/Checkout/CheckoutCommand.cs
Services/OrderingService/Application/Orders/Checkout/CheckoutHandler.cs

Services/OrderingService/API/Endpoints/CheckoutEndpoints.cs

Services/OrderingService/Program.cs
```

Config cần thêm:

```json
{
  "Services": {
    "BasketServiceBaseUrl": "http://localhost:5002"
  }
}
```

Endpoint mới:

```text
POST /checkout
```

Flow:

```text
Client
→ POST /checkout
→ CheckoutEndpoint
→ CheckoutCommand
→ CheckoutHandler
→ IBasketClient.GetBasketAsync(customerId)
→ tạo Order từ basket items
→ IOrderRepository.CreateAsync(order)
→ IBasketClient.ClearBasketAsync(customerId)
→ trả OrderDto
```

Important:

```text
OrderingService không query trực tiếp Redis/database của BasketService.
OrderingService gọi BasketService qua API/client abstraction.
```


---

# 4. Những file học liệu đã tạo/generate

User muốn mỗi bài có 2 file:

```text
lesson-xx-compact.md
lesson-xx-interactive.html
```

Đã tạo/generate trước đó:

```text
lesson-14-compact-sample.md
lesson-14-interactive.html

lesson-15-compact.md
lesson-15-interactive.html

lesson-16-compact.md
lesson-16-interactive.html
```

User muốn sau này:

```text
Không zip
Không folder
Chỉ file .md và .html riêng
```

Project UI copy vào:

```text
content/lesson-16-compact.md
lessons/lesson-16.html
```

Sau đó chạy:

```bash
node scripts/generate-lessons.js
```

---

# 5. Những lỗi còn tồn tại / cần sửa ngay

## 5.1. Lỗi quan trọng: Lesson 16 ghi sai bài tiếp theo

Trong `lesson-16-compact.md` và `lesson-16-interactive.html`, phần cuối hiện đang ghi sai:

```text
Buổi 17: Service Communication Hardening Intro
```

Phải sửa thành:

```text
Buổi 17: DiscountService + Strategy Intro
```

Và mục tiêu đúng:

```text
Tạo DiscountService cơ bản, có API kiểm tra coupon/discount rule, áp dụng Strategy Pattern mindset, chuẩn bị cho PaymentService và checkout flow sau này.
```

Không tạo bài 17 về Service Communication Hardening vì roadmap quy định Buổi 17 là DiscountService + Strategy Intro.

## 5.2. Service Communication Hardening không bị bỏ, nhưng không nằm ở Buổi 17

Nếu muốn nhắc timeout/retry trong Stage 1 thì chỉ nhắc rất ngắn dưới dạng production add-on/mindset.

Phần học sâu nằm ở Stage 2:

```text
Buổi 45: Timeout / Retry / Circuit Breaker
```

## 5.3. Lesson 15 HTML có skeleton DapperOrderRepository

Trong `lesson-15-interactive.html`, phần `DapperOrderRepository` có thể đang để skeleton:

```csharp
throw new NotImplementedException();
```

Ghi chú trong HTML nói dùng bản full trong MD.

Nếu Codex đang chỉnh học liệu HTML, nên cân nhắc:

```text
Option A: Giữ HTML gọn, nhưng có warning rõ "copy full repository trong MD".
Option B: Đưa full DapperOrderRepository vào HTML để học viên copy được trọn vẹn.
```

Ưu tiên của user:

```text
Nội dung đầy đủ, dễ học, không tối giản đến mức khó hiểu.
```

Vậy tốt hơn là:

```text
HTML cũng nên có full code hoặc có link/section collapsible full code.
```

## 5.4. Font tiếng Việt ở HTML bài 14 bị lỗi

User báo HTML bài 14 bị lỗi font tiếng Việt.

Bài 15/16 đã dùng font stack an toàn hơn:

```css
font-family: "Segoe UI", Arial, "Helvetica Neue", sans-serif;
```

Và có:

```html
<meta charset="UTF-8" />
<meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
```

Nếu chỉnh lại bài 14, áp dụng cùng chuẩn font/charset như bài 15/16.

## 5.5. Canvas bài 14 có thể bị incomplete/truncated JS

Trong canvas có một HTML bài 14 từng được chỉnh thủ công. Nội dung canvas có vẻ bị cắt ở cuối JS đoạn:

```js
classList.add('wro
```

Nếu cần dùng bài 14 HTML, không nên tin hoàn toàn canvas hiện tại. Hãy dùng file HTML generated hoặc regenerate lại từ đầu với font stack an toàn.

## 5.6. Roadmap status trong Notion/MD cũ bị stale

Trong roadmap file có dòng cũ:

```text
Done: Buổi 1 → Buổi 6 + Checkpoint
Current: Buổi 8 - Docker Compose cơ bản
Next: Buổi 9 - .NET Aspire Intro
```

Nhưng tiến độ hiện tại đã là:

```text
Done: Buổi 1 → Buổi 16
Current: Buổi 17 - DiscountService + Strategy Intro
Next: Buổi 18 - PaymentService + Payment Webhook Intro
```

Nếu Codex sửa roadmap docs, cần update status này.

---

# 6. Lệnh đã chạy / lệnh cần chạy / kết quả chính

## 6.1. IdentityService

```bash
dotnet new webapi -n IdentityService -o Services/IdentityService
dotnet sln add Services/IdentityService/IdentityService.csproj

dotnet add Services/IdentityService/IdentityService.csproj package MediatR
dotnet add Services/IdentityService/IdentityService.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add Services/IdentityService/IdentityService.csproj package System.IdentityModel.Tokens.Jwt

dotnet build Services/IdentityService/IdentityService.csproj
dotnet run --project Services/IdentityService/IdentityService.csproj
```

Expected:

```text
IdentityService chạy được ở http://localhost:5003.
POST /auth/login trả accessToken.
GET /auth/me không token trả 401.
GET /auth/me có token trả user claims.
```

## 6.2. CatalogService JWT/Authz

```bash
dotnet add Services/CatalogService/CatalogService.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet build Services/CatalogService/CatalogService.csproj
dotnet run --project Services/CatalogService/CatalogService.csproj
```

Expected:

```text
GET /products public.
POST /products không token trả 401.
POST /products với Admin token trả 201.
POST /products với Customer token trả 403.
```

## 6.3. OrderingService

```bash
dotnet new webapi -n OrderingService -o Services/OrderingService
dotnet sln add Services/OrderingService/OrderingService.csproj

dotnet add Services/OrderingService/OrderingService.csproj package MediatR
dotnet add Services/OrderingService/OrderingService.csproj package Dapper
dotnet add Services/OrderingService/OrderingService.csproj package Microsoft.Data.Sqlite

dotnet build Services/OrderingService/OrderingService.csproj
dotnet run --project Services/OrderingService/OrderingService.csproj
```

Expected:

```text
OrderingService chạy ở http://localhost:5004.
POST /orders trả 201.
GET /orders trả list.
GET /orders/{id} trả detail.
```

## 6.4. Checkout Flow

```bash
dotnet build Services/OrderingService/OrderingService.csproj
dotnet run --project Services/BasketService/BasketService.csproj
dotnet run --project Services/OrderingService/OrderingService.csproj
```

Expected:

```text
BasketService chạy.
OrderingService chạy.
POST /checkout gọi được.
Checkout lấy basket, tạo order, clear basket.
```

## 6.5. Project UI bài học

Khi copy HTML vào `lessons/`:

```bash
node scripts/generate-lessons.js
```

Nếu muốn serve local:

```bash
npx serve .
```

Expected:

```text
lessons.json được generate/cập nhật.
index.html tự hiển thị lesson mới.
Không cần sửa index.html thủ công.
```

---

# 7. Những quyết định kỹ thuật đã chốt

## 7.1. Naming

```text
Project: MicroShop
Catalog service: CatalogService
Không dùng ProductService.
Ordering service: OrderingService
Discount service: DiscountService
Payment service: PaymentService
```

## 7.2. Lesson format

MD compact docs-style là chuẩn:

```text
# Buổi X: Tên bài

## 1. Mục tiêu
## 2. Bài này giải quyết vấn đề gì?
## 3. Ý tưởng chính
## 4. Flow tổng quan
## 5. Thực hành
## 6. Test bằng Postman
## 7. Checklist hoàn thành
## 8. Bài tập
## 9. Quiz / Review
## 10. Chưa học trong bài này
## 11. Học phần nâng cao ở đâu?
## Điều kiện mở khóa Buổi tiếp theo
```

Không viết quá nhiều block kiểu:

```text
Method:
URL:
Body:
Expected:
```

Postman lab nên gom bằng bảng.

## 7.3. HTML lesson format

Mỗi HTML nên là standalone:

```text
- Có metadata comment lesson-meta ở đầu file.
- Có light/dark mode.
- Sidebar navigation.
- Progress checklist bằng localStorage.
- Tabs cho lý thuyết.
- Accordion cho thực hành.
- Code block có nút copy.
- Postman Lab trình bày bằng bảng/cards.
- Quiz tương tác.
- Review note.
- Font tiếng Việt an toàn.
```

Metadata example:

```html
<!--
lesson-meta:
  lesson: 17
  title: "DiscountService + Strategy Intro"
  subtitle: "Coupon/discount rule + Strategy Pattern mindset"
  phase: "Ordering + Checkout + Payment/Webhook Intro"
  duration: "90–120 phút"
  status: "ready"
  order: 17
-->
```

## 7.4. Font HTML

Dùng font stack an toàn cho tiếng Việt:

```css
font-family: "Segoe UI", Arial, "Helvetica Neue", sans-serif;
```

Không dùng font ngoài qua CDN nếu không cần.

## 7.5. Postman-first

Tất cả bài học từ nay phải có Postman lab.

Không ưu tiên `.http` file là chính. Có thể nhắc `.http` phụ, nhưng testing chính là Postman.

## 7.6. Clean Architecture baseline

Hiện tại dùng folder-based architecture trong cùng một project service.

Chưa tách multi-project trừ khi roadmap yêu cầu sau.

## 7.7. DTO naming

Application DTO nên là:

```text
ProductDto
OrderDto
OrderItemDto
```

Không dùng `ProductResponse` trong Application vì dễ conflict với gRPC generated `ProductResponse`.

## 7.8. Communication

REST giữa `OrderingService` và `BasketService` trong Buổi 16 là baseline đúng.

Không coi REST là kiến trúc cuối cùng.

Nhưng cũng không học lại gRPC từ đầu vì Buổi 5 đã học gRPC.

Các phần hardening như timeout/retry/circuit breaker học sâu ở Stage 2/Buổi 45.

## 7.9. Production topics không tách tùy tiện

Roadmap có nguyên tắc không phân mảnh quá nhiều buổi.

Mỗi buổi phải có learning outcome chính + output rõ ràng.

Production topic nhỏ nên đưa vào production add-on của bài liên quan, không tự chen thành bài mới.


---

# 8. Các bước tiếp theo theo thứ tự ưu tiên

## Ưu tiên 1 — Sửa lesson 16 sai roadmap

Sửa trong:

```text
lesson-16-compact.md
lesson-16-interactive.html
```

Tìm đoạn:

```text
Buổi 17:
Service Communication Hardening Intro
```

Đổi thành:

```text
Buổi 17:
DiscountService + Strategy Intro
```

Sửa mục tiêu:

```text
Tạo DiscountService cơ bản, có API kiểm tra coupon/discount rule, áp dụng Strategy Pattern mindset, chuẩn bị cho PaymentService và checkout flow sau này.
```

Sửa phần học nâng cao nếu có:

```text
Timeout / Retry / Circuit Breaker → Buổi 45
Distributed Consistency → Buổi 42
Outbox → Buổi 23 basic, Buổi 38 advanced
Idempotency → Buổi 22 basic, Buổi 39 advanced
Saga → Buổi 43/44
```

## Ưu tiên 2 — Tạo bài 17 đúng roadmap

Tạo 2 file:

```text
lesson-17-compact.md
lesson-17-interactive.html
```

Chủ đề:

```text
Buổi 17: DiscountService + Strategy Intro
```

Output chính theo roadmap:

```text
Coupon/discount rule
```

Production add-on:

```text
Strategy pattern mindset
```

Nội dung bài 17 nên gồm:

```text
- Tạo DiscountService.
- Tạo API kiểm tra mã giảm giá.
- Tạo coupon/discount rule cơ bản.
- Áp dụng Strategy Pattern mindset.
- Tạo discount strategies:
  - PercentageDiscountStrategy
  - FixedAmountDiscountStrategy
  - NoDiscountStrategy hoặc InvalidCoupon behavior
- Tạo repository/persistence đơn giản cho coupons.
- Test bằng Postman.
- Chưa tích hợp sâu vào checkout nếu làm quá tải.
- Có section "sau này integrate vào Checkout/Payment".
```

Endpoint gợi ý:

```text
POST /discounts/apply
GET /discounts/{code}
POST /discounts/seed hoặc seed khi app start
```

Request gợi ý:

```json
{
  "couponCode": "SAVE10",
  "orderAmount": 2197
}
```

Response gợi ý:

```json
{
  "couponCode": "SAVE10",
  "isValid": true,
  "discountAmount": 219.7,
  "finalAmount": 1977.3,
  "message": "Coupon applied."
}
```

## Ưu tiên 3 — Kiểm tra lesson 15 HTML có cần full repository code không

Nếu user dùng HTML để học/code trực tiếp, nên thay skeleton `DapperOrderRepository` bằng full code từ MD hoặc thêm collapsible “Full DapperOrderRepository”.

## Ưu tiên 4 — Sửa font bài 14 nếu còn dùng

Regenerate hoặc patch `lesson-14-interactive.html`:

```html
<meta charset="UTF-8" />
<meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
```

```css
body {
  font-family: "Segoe UI", Arial, "Helvetica Neue", sans-serif;
}
```

Không dùng canvas bị truncate.

## Ưu tiên 5 — Update roadmap status

Nếu có file roadmap trong repo/docs, update status:

```text
Done: Buổi 1 → Buổi 16
Current: Buổi 17 - DiscountService + Strategy Intro
Next: Buổi 18 - PaymentService + Payment Webhook Intro
```

## Ưu tiên 6 — Sau bài 17, tạo bài 18

Theo roadmap:

```text
Buổi 18: PaymentService + Payment Webhook Intro
Output: Payment success/fail giả lập + POST /webhooks/payment demo
Production add-on: Webhook khác RabbitMQ/Kafka, webhook log mindset
```

Webhook intro được gắn vào Buổi 18.

Webhook production handling đầy đủ nằm ở Buổi 44.

---

# 9. Cảnh báo: việc gì không được revert/xóa/làm lại

## 9.1. Không đổi roadmap

Không được tự đổi:

```text
Buổi 17 = Service Communication Hardening
```

Buổi 17 phải là:

```text
DiscountService + Strategy Intro
```

## 9.2. Không học lại gRPC từ đầu

Buổi 5 đã học gRPC.

Nếu nhắc gRPC, chỉ nói:

```text
Đã học intro.
Sau này có thể áp dụng/refactor internal call khi phù hợp.
```

Không viết như chưa học.

## 9.3. Không đổi `CatalogService` thành `ProductService`

Project đã chốt dùng:

```text
CatalogService
```

Không tạo:

```text
ProductService
```

## 9.4. Không đổi `MicroShop` thành tên khác

Tên project chuẩn là:

```text
MicroShop
```

## 9.5. Không dùng `ProductResponse` trong Application DTO

Dùng:

```text
ProductDto
```

`ProductResponse` có thể là generated type từ gRPC/proto, tránh conflict.

## 9.6. Không xóa Postman Lab

Toàn khóa đã chốt Postman-first.

Mỗi bài cần có Postman testing steps.

## 9.7. Không zip file học liệu

User yêu cầu:

```text
1 file md
1 file html
```

Không zip, không folder.

## 9.8. Không làm MD quá dài kiểu tách quá nhiều mục nhỏ

User không thích format quá giáo trình hóa kiểu:

```text
Mục tiêu
File cần sửa
Thực hiện
Giải thích
Verify
Lỗi hay gặp
```

lặp lại ở mọi bước.

Dùng compact docs-style. Postman dùng table.

## 9.9. Không làm HTML lược bỏ nội dung quá nhiều

HTML có thể đẹp/gọn nhưng không được thiếu code quan trọng khiến học viên phải đoán.

Nếu code dài, dùng accordion/collapsible, không bỏ mất.

## 9.10. Không xóa localStorage progress/quiz behavior trong HTML

HTML bài học cần giữ:

```text
- Checklist progress
- Quiz interactive
- Review note
- Copy code
- Light/dark mode
```

## 9.11. Không di chuyển hardening topics thành bài riêng trong Stage 1 nếu roadmap không nói

Timeout/retry/circuit breaker học sâu ở Buổi 45.

Outbox/idempotency/saga theo roadmap:

```text
Buổi 22: Retry/DLQ + Idempotency Basic
Buổi 23: Outbox Basic + Background Publisher Intro
Buổi 38: Transactional Outbox chuẩn
Buổi 39: Outbox Publisher + Idempotency nâng cao + Inbox/WebhookLog
Buổi 43/44: Saga
```

---

# 10. Acceptance criteria cho Codex

## Sau khi sửa lesson 16

```text
[ ] lesson-16-compact.md không còn "Service Communication Hardening Intro" ở phần next lesson.
[ ] lesson-16-interactive.html không còn "Service Communication Hardening Intro" ở phần next lesson.
[ ] Next lesson trong cả MD/HTML là "DiscountService + Strategy Intro".
[ ] Các link/metadata lesson 16 vẫn đúng.
[ ] HTML vẫn chạy standalone.
```

## Sau khi tạo lesson 17

```text
[ ] Có lesson-17-compact.md.
[ ] Có lesson-17-interactive.html.
[ ] HTML có lesson-meta đầy đủ.
[ ] HTML dùng UTF-8 và font tiếng Việt an toàn.
[ ] Nội dung bám roadmap Buổi 17.
[ ] Có Postman Lab.
[ ] Có checklist.
[ ] Có bài tập.
[ ] Có quiz/review.
[ ] Có phần "Chưa học trong bài này".
[ ] Có "Điều kiện mở khóa Buổi 18".
[ ] Không nhồi Payment/Webhook production vào bài 17.
```

## Nếu update project UI

```text
[ ] Copy lesson-17-compact.md vào content/
[ ] Copy lesson-17-interactive.html thành lessons/lesson-17.html
[ ] Chạy node scripts/generate-lessons.js
[ ] lessons.json có bài 17.
[ ] index.html hiển thị bài 17.
```

---

# 11. Gợi ý scope chuẩn cho lesson 17

## Lesson 17 nên tên

```text
Buổi 17: DiscountService + Strategy Intro
```

## Mục tiêu

```text
Tạo DiscountService cơ bản và hiểu cách dùng Strategy Pattern để xử lý nhiều loại discount.
```

## Không nên làm quá scope

Không làm:

```text
- PaymentService
- Webhook
- RabbitMQ
- Saga
- Outbox
- Full checkout integration phức tạp
```

Có thể chỉ thêm section:

```text
Sau này OrderingService/Checkout sẽ gọi DiscountService để apply coupon trước khi tạo order/payment.
```

## Flow gợi ý

```text
Client/Postman
→ POST /discounts/apply
→ ApplyDiscountCommand
→ ApplyDiscountHandler
→ IDiscountRepository
→ Coupon
→ IDiscountStrategy
→ DiscountResultDto
```

## Folder structure gợi ý

```text
Services/DiscountService/
├── API/
│   └── Endpoints/
│       └── DiscountEndpoints.cs
├── Application/
│   ├── Abstractions/
│   │   └── IDiscountRepository.cs
│   └── Discounts/
│       ├── ApplyDiscount/
│       │   ├── ApplyDiscountCommand.cs
│       │   └── ApplyDiscountHandler.cs
│       ├── DiscountResultDto.cs
│       └── DiscountMapper.cs
├── Domain/
│   └── Discounts/
│       ├── Coupon.cs
│       ├── DiscountType.cs
│       ├── IDiscountStrategy.cs
│       ├── PercentageDiscountStrategy.cs
│       └── FixedAmountDiscountStrategy.cs
├── Infrastructure/
│   └── Persistence/
│       └── InMemoryDiscountRepository.cs
├── Program.cs
└── appsettings.Development.json
```

Có thể dùng in-memory repository ở bài 17 để không loãng.

Nếu muốn DB thì chỉ làm rất nhẹ.

## Endpoint gợi ý

```text
POST /discounts/apply
GET /discounts/{code}
```

## Coupon seed gợi ý

```text
SAVE10     Percentage 10%
WELCOME50  FixedAmount 50
FREESHIP   FixedAmount 5 hoặc để sau
```

---

# 12. Final project parity checklist

Final project parity checklist vẫn cần có:

```text
DiscountService
PaymentService
gRPC internal communication
REST public/debug API
JWT auth
Outbox
Inbox/WebhookLog
Idempotent Consumer
Saga
Gateway rate limiting/load balancing
Observability
Testing
OpenAPI
ADR
Runbooks
```

Không được bỏ các mục này, nhưng phải đặt đúng buổi theo roadmap.

---

# 13. Current status để mở phiên mới

Trạng thái hiện tại nên hiểu như sau:

```text
Done: Buổi 1 → Buổi 16
Current: Buổi 17 - DiscountService + Strategy Intro
Next: Buổi 18 - PaymentService + Payment Webhook Intro
```

Bài gần nhất đã tạo:

```text
lesson-16-compact.md
lesson-16-interactive.html
```

Nhưng cần sửa phần cuối bài 16 vì đang ghi sai next lesson.

Bài tiếp theo cần tạo:

```text
lesson-17-compact.md
lesson-17-interactive.html
```

Theo đúng compact docs-style + interactive HTML style đã chốt.

---

# 14. Message ngắn cho Codex nếu cần

Nếu cần prompt ngắn để Codex bắt đầu:

```text
Bạn đang tiếp tục project MicroShop .NET Microservices course. Hãy đọc file handoff này trước. Việc đầu tiên: sửa lesson-16-compact.md và lesson-16-interactive.html để next lesson đúng là "Buổi 17: DiscountService + Strategy Intro", không phải "Service Communication Hardening Intro". Sau đó tạo lesson-17-compact.md và lesson-17-interactive.html theo format compact docs-style + interactive HTML, chủ đề DiscountService + Strategy Intro, có Postman Lab, checklist, bài tập, quiz/review, không zip, không folder.
```
