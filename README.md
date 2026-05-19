# MicroShop

MicroShop is a learning-oriented microservices e-commerce backend built with ASP.NET Core, .NET 10, Aspire, YARP, MediatR, Dapper, SQLite, Redis, REST, and gRPC.

MicroShop là backend thương mại điện tử dạng microservices dùng để học và thực hành thiết kế hệ thống với ASP.NET Core, .NET 10, Aspire, YARP, MediatR, Dapper, SQLite, Redis, REST và gRPC.

## 1. System Overview / Tổng Quan Hệ Thống

The system is split into small services. Each service owns one business capability and keeps its own data model. Clients call `ApiGateway`, then the gateway forwards requests to the correct backend service.

Hệ thống được chia thành nhiều service nhỏ. Mỗi service phụ trách một năng lực nghiệp vụ riêng và tự quản lý dữ liệu của mình. Client gọi vào `ApiGateway`, sau đó gateway chuyển tiếp request tới service phù hợp.

```text
Client / Postman
      |
      v
  ApiGateway (YARP)
      |
      +--> CatalogService   -> SQLite
      +--> BasketService    -> Redis, calls CatalogService by REST/gRPC
      +--> OrderingService  -> SQLite, calls BasketService during checkout
      +--> DiscountService  -> SQLite
      +--> IdentityService  -> JWT/auth learning service

AppHost (Aspire) starts and wires services, Redis, and RabbitMQ.
```

## 2. Main Design Goals / Mục Tiêu Thiết Kế

- Keep each service focused on one business area.
- Use Clean Architecture style inside business services: `API`, `Application`, `Domain`, `Infrastructure`.
- Keep endpoints thin; business rules live in handlers/domain.
- Use REST for public APIs because it is simple to test with Postman.
- Use gRPC for typed internal communication where useful, currently BasketService to CatalogService.
- Use Aspire AppHost for local orchestration and service discovery.
- Keep the project easy to debug while still showing realistic production patterns.

- Mỗi service chỉ tập trung vào một mảng nghiệp vụ.
- Các service nghiệp vụ dùng style Clean Architecture: `API`, `Application`, `Domain`, `Infrastructure`.
- Endpoint mỏng; logic nghiệp vụ nằm trong handler/domain.
- Public API dùng REST để dễ test bằng Postman.
- Internal communication có thể dùng gRPC khi cần contract chặt hơn, hiện BasketService gọi CatalogService bằng REST/gRPC.
- Aspire AppHost dùng để chạy local và nối service với dependency.
- Project ưu tiên dễ học, dễ debug, nhưng vẫn bám theo pattern gần production.

## 3. Services / Các Service

| Service | Responsibility / Trách nhiệm | Storage / Lưu trữ | Main routes / Route chính |
|---|---|---|---|
| `ApiGateway` | Public entry point, reverse proxy with YARP / Cổng vào public, reverse proxy bằng YARP | None | `/catalog`, `/cart`, `/orders`, `/discounts` |
| `CatalogService` | Product catalog, product lookup, REST and gRPC APIs / Quản lý sản phẩm, cung cấp REST và gRPC | SQLite | `/products`, gRPC `CatalogGrpc` |
| `BasketService` | Shopping cart, validates products through Catalog / Quản lý giỏ hàng, validate sản phẩm qua Catalog | Redis | `/basket/...` internally, `/cart/...` through gateway |
| `OrderingService` | Orders and checkout from basket snapshot / Quản lý order và checkout từ basket snapshot | SQLite | `/orders`, `/orders/checkout` |
| `DiscountService` | Coupons and discount calculation / Quản lý coupon và tính giảm giá | SQLite | `/discounts/{code}`, `/discounts/apply` |
| `IdentityService` | JWT/auth learning service / Service học authentication/JWT | App-specific | Auth endpoints |

## 4. Repository Structure / Cấu Trúc Source

```text
MicroShop.sln
MicroShop.AppHost/               # Aspire orchestration
MicroShop.ServiceDefaults/       # Shared health checks, telemetry, service discovery
Services/
  ApiGateway/
  CatalogService/
  BasketService/
  OrderingService/
  DiscountService/
  IdentityService/
postman/
  MicroShop.OrderFlow.postman_collection.json
  MicroShop.DiscountFlow.postman_collection.json
```

Business services follow this folder style:

Các service nghiệp vụ đi theo cấu trúc:

```text
API/              # Minimal endpoints, contracts, exception handling
Application/      # Use cases, commands, queries, abstractions
Domain/           # Business entities and domain rules
Infrastructure/   # Persistence, HTTP/gRPC clients, external dependencies
Program.cs        # Compose layers only
```

## 5. Gateway Design / Thiết Kế Gateway

`ApiGateway` uses YARP to expose stable public paths:

`ApiGateway` dùng YARP để expose route public ổn định:

| Public path / Đường public | Target service / Service đích |
|---|---|
| `/catalog/{**catch-all}` | `CatalogService` |
| `/cart/{**catch-all}` | `BasketService`, transformed to `/basket/{**catch-all}` |
| `/orders/{**catch-all}` | `OrderingService` |
| `/discounts/{**catch-all}` | `DiscountService` |

The gateway adds the `X-Gateway: YARP` response header so it is easy to verify that the request passed through the proxy.

Gateway thêm response header `X-Gateway: YARP` để dễ biết request đã đi qua proxy.

## 6. Communication Decisions / Quyết Định Giao Tiếp

- Public API: REST, because it is easy for frontend/mobile clients and simple to debug.
- Internal service-to-service: gRPC where a strong contract is useful. BasketService can call CatalogService through gRPC.
- Debug/simple integration: REST/Refit is still kept for readability and quick local testing.
- Async workflow: RabbitMQ is available in AppHost and can be used later for events such as `OrderCreated`.

- Public API: REST vì dễ dùng cho frontend/mobile và dễ debug.
- Internal service-to-service: dùng gRPC khi cần contract rõ và type-safe. BasketService có thể gọi CatalogService bằng gRPC.
- Debug/tích hợp đơn giản: vẫn giữ REST/Refit để dễ đọc và test local.
- Async workflow: RabbitMQ đã có trong AppHost, có thể dùng sau cho event như `OrderCreated`.

## 7. Important Flows / Các Luồng Chính

### Catalog Flow / Luồng Catalog

English:
1. Client calls `GET /catalog/products`.
2. ApiGateway removes `/catalog` prefix.
3. CatalogService receives `GET /products`.
4. Application handler reads products from SQLite through Dapper.

Tiếng Việt:
1. Client gọi `GET /catalog/products`.
2. ApiGateway bỏ prefix `/catalog`.
3. CatalogService nhận `GET /products`.
4. Application handler đọc product từ SQLite qua Dapper.

### Basket Flow / Luồng Basket

English:
1. Client calls `POST /cart/{userId}/items`.
2. ApiGateway transforms the path to `/basket/{userId}/items`.
3. BasketService validates the product by calling CatalogService.
4. BasketService stores the cart in Redis.

Tiếng Việt:
1. Client gọi `POST /cart/{userId}/items`.
2. ApiGateway đổi path thành `/basket/{userId}/items`.
3. BasketService validate sản phẩm bằng cách gọi CatalogService.
4. BasketService lưu giỏ hàng vào Redis.

### Checkout Flow / Luồng Checkout

English:
1. Client adds items to BasketService.
2. Client calls `POST /orders/checkout` with `Idempotency-Key`.
3. OrderingService loads the basket from BasketService.
4. OrderingService validates the basket owner and item snapshot.
5. OrderingService creates a pending order in SQLite.
6. OrderingService clears the basket.
7. Retrying with the same idempotency key returns the same order instead of creating a duplicate.

Tiếng Việt:
1. Client thêm item vào BasketService.
2. Client gọi `POST /orders/checkout` kèm `Idempotency-Key`.
3. OrderingService lấy basket từ BasketService.
4. OrderingService validate owner và snapshot item trong basket.
5. OrderingService tạo order trạng thái `Pending` trong SQLite.
6. OrderingService clear basket.
7. Retry cùng idempotency key sẽ trả lại order cũ, không tạo duplicate.

### Discount Flow / Luồng Discount

English:
1. Client calls `GET /discounts/{code}` to inspect a coupon.
2. Client calls `POST /discounts/apply` with coupon code and order amount.
3. DiscountService loads the coupon from SQLite.
4. Domain strategy calculates percentage or fixed amount discount.

Tiếng Việt:
1. Client gọi `GET /discounts/{code}` để xem coupon.
2. Client gọi `POST /discounts/apply` với coupon code và order amount.
3. DiscountService đọc coupon từ SQLite.
4. Domain strategy tính giảm giá theo phần trăm hoặc số tiền cố định.

## 8. Data Ownership / Quyền Sở Hữu Dữ Liệu

Each service owns its own storage. Other services should not read that storage directly.

Mỗi service sở hữu storage riêng. Service khác không được đọc trực tiếp storage đó.

| Service | Owns / Sở hữu |
|---|---|
| CatalogService | Product data in `catalog.db` |
| BasketService | Basket data in Redis |
| OrderingService | Order data in `ordering.db` |
| DiscountService | Coupon data in `discount.db` |

SQLite database files are local runtime artifacts and should not be committed.

Các file SQLite là artifact runtime local và không nên commit.

## 9. Clean Architecture Notes / Ghi Chú Clean Architecture

Typical dependency direction:

Chiều dependency tiêu chuẩn:

```text
API -> Application -> Domain
Infrastructure -> Application abstractions
Program.cs wires everything together
```

Rules used in this project:

Quy tắc đang dùng trong project:

- `API` maps HTTP requests and responses only.
- `Application` contains commands, queries, handlers, DTOs, and interfaces.
- `Domain` contains entities, enums, and business rules.
- `Infrastructure` implements repositories, SQLite initialization, Redis access, HTTP/gRPC clients.
- `Program.cs` only composes layers and maps endpoints.

- `API` chỉ map HTTP request/response.
- `Application` chứa command, query, handler, DTO và interface.
- `Domain` chứa entity, enum và business rule.
- `Infrastructure` implement repository, init SQLite, Redis, HTTP/gRPC client.
- `Program.cs` chỉ compose layer và map endpoint.

## 10. Local Development / Chạy Local

Prerequisites:

Yêu cầu:

- .NET 10 SDK
- Docker Desktop, for Aspire-managed Redis and RabbitMQ
- Postman, optional but useful for testing flows

Build the solution:

Build toàn solution:

```powershell
dotnet build MicroShop.sln --no-restore --nologo -v minimal
```

Run with Aspire AppHost:

Chạy bằng Aspire AppHost:

```powershell
dotnet run --project MicroShop.AppHost\MicroShop.AppHost.csproj
```

Then call the gateway:

Sau đó gọi qua gateway:

```text
https://localhost:7001
```

If HTTPS local certificates cause Postman issues, turn off SSL certificate verification in Postman for local testing only.

Nếu Postman lỗi certificate HTTPS local, có thể tắt SSL certificate verification trong Postman khi test local.

## 11. Postman Collections / Bộ Test Postman

Import these files into Postman:

Import các file này vào Postman:

- `postman/MicroShop.OrderFlow.postman_collection.json`
- `postman/MicroShop.DiscountFlow.postman_collection.json`

Order flow covers:

Luồng Order test:

- Get product from Catalog.
- Add product to Basket.
- Checkout with `Idempotency-Key`.
- Retry checkout with the same key.
- Get orders and get order by id.

Discount flow covers:

Luồng Discount test:

- Get `SAVE10`.
- Apply percentage discount.
- Apply fixed discount.
- Test expired, disabled, and missing coupon behavior.

## 12. Debugging Guide / Hướng Dẫn Debug

Recommended debugging order:

Thứ tự debug nên dùng:

1. Check the Postman response status and `traceId`.
2. Check ApiGateway logs to see which downstream service received the request.
3. Check the target service logs.
4. Test the target service directly, bypassing the gateway.
5. Check external dependencies: Redis, SQLite file path, downstream service URL, gRPC endpoint.
6. Read the first stack trace line that points to project code.

Useful commands:

Các lệnh hữu ích:

```powershell
dotnet build MicroShop.sln --no-restore --nologo -v minimal
netstat -ano | Select-String ':7001|:7002|:7003|:7005|:7006|:6379'
```

Common cases:

Các lỗi hay gặp:

- `500` from BasketService: check Redis connection first.
- `404` through Gateway but direct service works: check YARP route or path transform.
- Checkout duplicate risk: always send `Idempotency-Key`.
- Postman HTTPS error: check local dev certificate or disable SSL verification for local testing.

## 13. Current Limitations / Giới Hạn Hiện Tại

This project is still a learning system, not a full production e-commerce platform.

Project này vẫn là hệ thống học tập, chưa phải e-commerce production hoàn chỉnh.

Current limitations:

Giới hạn hiện tại:

- No full payment workflow.
- No inventory reservation.
- No distributed transaction or saga yet.
- RabbitMQ is available but not fully used for domain events yet.
- Authentication/authorization is not applied consistently to every service.
- SQLite is used for simplicity; production would usually use a managed database.

## 14. Next Improvements / Hướng Phát Triển Tiếp

- Add `OrderCreated` event and process it asynchronously with RabbitMQ.
- Add PaymentService and InventoryService.
- Add saga/outbox pattern for checkout reliability.
- Add integration tests for Gateway + service flows.
- Add centralized logging and trace correlation.
- Harden authentication and authorization across services.

- Thêm event `OrderCreated` và xử lý async bằng RabbitMQ.
- Thêm PaymentService và InventoryService.
- Thêm saga/outbox pattern để checkout chắc hơn.
- Thêm integration test cho Gateway + service flow.
- Thêm centralized logging và trace correlation.
- Siết authentication/authorization cho toàn hệ thống.
