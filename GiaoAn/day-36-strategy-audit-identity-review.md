---
day: 36
title: "Advanced Strategy Pattern + Audit Log + Advanced Identity Review"
duration: "90-120 phút"
project: "MicroShop"
type: "lesson"
repo_aware: true
source_of_truth: true
language: "vi"
encoding_note: "UTF-8 Markdown tiếng Việt chuẩn"
style: "learning-practice"
---

# Day 36: Advanced Strategy Pattern + Audit Log + Advanced Identity Review

## 0. Hôm nay học gì?

Hôm nay mình review 3 mảng: Strategy Pattern nâng cao, Audit Log policy và Identity hardening.

Bài này không rewrite IdentityService, không build RBAC full, không tích hợp OIDC ngay.

## 1. Vì sao cần bài này?

Strategy giúp code dễ mở rộng khi có nhiều behavior. Audit log giúp truy vết hành động quan trọng. Identity hardening giúp biết auth hiện tại đang ở mức học hay đã gần production.

Đây là các chủ đề hay gặp khi hệ thống bắt đầu nghiêm túc hơn.

## 2. Khái niệm cốt lõi

### Strategy Pattern

Strategy dùng khi có nhiều cách xử lý cùng một interface.

### Audit Log

Audit log không giống application log.

```text
Application log: debug hệ thống.
Audit log: truy vết hành động quan trọng.
```

### Identity hardening

Auth không chỉ là login ra token. Cần nghĩ tới:

```text
Signing key
Token lifetime
Claims
Refresh token
RBAC
OIDC/SSO
```

## 3. Nhìn vào repo hiện tại

Các service chính:

```text
Services/ApiGateway
Services/CatalogService
Services/BasketService
Services/OrderingService
Services/DiscountService
Services/IdentityService
Services/PaymentService
Services/OrderQueryService
```

Các worker:

```text
Services/NotificationWorker
Workers/ProjectionWorker
```

Các project dùng chung:

```text
BuildingBlocks.Contracts
MicroShop.AppHost
MicroShop.ServiceDefaults
```

Hạ tầng local:

```text
PostgreSQL: lưu dữ liệu write-side
Redis: lưu basket/cache
RabbitMQ: workflow/task messaging
Kafka: event stream/projection learning
MongoDB: read model và projection failure
Docker Compose: chạy local runtime
Aspire AppHost: orchestration local .NET
```

Route/ID cũ không dùng lại:

```text
/orders/read-model
ORD-900
CUST-900
```


Repo-aware:

```text
DiscountService đã có Strategy Pattern thật:
- IDiscountStrategy
- DiscountStrategyFactory
- FixedAmountDiscountStrategy
- PercentageDiscountStrategy

DiscountStrategyFactory dùng dictionary/fail-fast duplicate type.
Đây không phải demo switch-case đơn giản.
```

## 4. Thực hành từng bước

### Bước 1: Inspect strategy/auth/audit

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "Strategy|Provider|Payment|Discount|Audit|Jwt|Token|Role|Claim|Authorize|Authentication|Policy"
```

### Bước 2: Review DiscountService

Kiểm tra:

```text
Thêm discount type mới có dễ không?
Factory có fail-fast duplicate type không?
Strategy có test chưa?
Validation invalid discount ra sao?
```

### Bước 3: Review PaymentService

Ghi future direction:

```text
Payment provider strategy vẫn là candidate/future.
Không ép abstraction nếu chỉ có một provider/behavior.
```

### Bước 4: Viết audit log policy

Tạo:

```text
docs/security/audit-log-policy.md
```

Event nên audit:

```text
Login success/failure
Order checkout
Payment created
Payment webhook received
Payment status changed
Catalog/Discount mutation nếu có admin flow
```

### Bước 5: Review Identity

Tạo:

```text
docs/security/identity-hardening-review-day-36.md
docs/security/sso-oidc-decision-note.md
docs/backlog/day-36-audit-identity-hardening-backlog.md
```

## 5. Kết quả kỳ vọng

Kỳ vọng:

```text
DiscountService được ghi đúng là đã có Strategy Pattern.
Audit policy rõ field và rule không log secret/JWT.
Identity limitation được document trung thực.
SSO/OIDC là future direction, chưa implement.
```

## 6. Lỗi hay gặp

```text
Lỗi 1: Mô tả DiscountService như candidate dù đã implement.
Lỗi 2: Bỏ qua dictionary/fail-fast duplicate type.
Lỗi 3: Claim auth production-ready.
Lỗi 4: Cố tích hợp OIDC/RBAC quá sớm.
```

## 7. Tổng kết bài học

Day 36 giúp mình nhìn hệ thống ở góc security/extension. Không chỉ code chạy, mà còn phải biết audit, auth và strategy sẽ lớn lên thế nào.

## 8. Checklist trước khi commit

```text
[ ] Hiểu được mục tiêu chính của bài.
[ ] Đã chạy các lệnh kiểm tra chính.
[ ] Đã tạo/cập nhật đúng docs hoặc code trong scope.
[ ] Đã test phần cần test.
[ ] Không dùng endpoint/id cũ.
[ ] Không claim production-ready.
[ ] Không làm lệch RabbitMQ/Kafka responsibility.
```

## 9. Commit/tag gợi ý

```text
Commit: Day 36: Strategy Audit Identity Review
Tag: day-36-strategy-audit-identity-review
```
