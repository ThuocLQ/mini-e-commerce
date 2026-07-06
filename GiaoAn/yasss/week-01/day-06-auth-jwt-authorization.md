# Day 06 - Authentication, Authorization, JWT

## 1. Câu chuyện đời thường

Tưởng tượng API là một **tòa nhà**.

```text
Authentication = bảo vệ hỏi "bạn là ai?"
Authorization = bạn được vào phòng nào?
JWT = thẻ ra vào có chữ ký chống giả.
Claim = thông tin ghi trên thẻ.
401 = chưa có thẻ hoặc thẻ giả.
403 = có thẻ nhưng không đủ quyền.
```

---

## 2. Bảng thuật ngữ dễ hiểu

| Thuật ngữ chuẩn | Dịch dễ hiểu | Ví dụ |
|---|---|---|
| Authentication | Xác minh bạn là ai | Login/JWT hợp lệ |
| Authorization | Kiểm tra bạn được làm gì | Role Admin |
| JWT | Thẻ ra vào dạng token | Bearer token |
| Claim | Thông tin trên thẻ | userId, role |
| Signature | Chữ ký chống giả | Token không bị sửa |
| Issuer | Nơi cấp thẻ | IdentityService |
| Audience | Nơi thẻ được dùng | MicroShop API |
| Expiration | Hạn dùng | token hết hạn |
| 401 | Chưa xác thực/token sai | Missing/invalid token |
| 403 | Đã xác thực nhưng thiếu quyền | User không phải Admin |

---

## 3. Authentication

Authentication trả lời:

```text
Bạn là ai?
```

Trong API:

```text
Client gửi JWT.
Server validate token.
Nếu hợp lệ, server biết userId/claims.
```

---

## 4. Authorization

Authorization trả lời:

```text
Bạn được phép làm gì?
```

Ví dụ:

```text
User thường được xem order của mình.
Admin được xem dashboard/recovery endpoint.
```

Một user có thể authenticated nhưng vẫn bị forbidden.

---

## 5. JWT

JWT giống thẻ ra vào.

Nó chứa thông tin như:

```text
userId
email
role
expiration
```

Nhưng không được chỉ decode rồi tin. Phải validate.

### Signature là gì?

Signature = chữ ký chống giả.

Nói dễ hiểu:

```text
Nếu ai đó tự sửa role từ User thành Admin, chữ ký sẽ không còn hợp lệ.
```

### Issuer là gì?

Issuer = nơi cấp token.

Ví dụ:

```text
IdentityService cấp JWT.
```

API phải kiểm tra token có đúng nơi cấp không.

### Audience là gì?

Audience = hệ thống mà token được phép dùng.

Ví dụ:

```text
Token cấp cho MicroShop API thì không nên dùng cho hệ khác.
```

### Expiration là gì?

Expiration = thời điểm hết hạn.

Token hết hạn thì không được chấp nhận.

---

## 6. 401 vs 403

### 401 Unauthorized

Dễ hiểu:

```text
Bạn chưa chứng minh được bạn là ai.
```

Ví dụ:

```text
Không gửi token.
Token hết hạn.
Token giả.
```

### 403 Forbidden

Dễ hiểu:

```text
Bạn là người thật, nhưng không được vào phòng này.
```

Ví dụ:

```text
User thường gọi admin endpoint.
```

---

## 7. CORS

CORS là rule của browser kiểm soát website nào được gọi API từ frontend.

Nói dễ hiểu:

```text
Tòa nhà chỉ cho một số website gửi request từ trình duyệt vào.
```

Nhưng CORS không thay thế authentication/authorization.

---

## 8. MicroShop connection

```text
IdentityService phát JWT.
Gateway/API validate JWT.
Order endpoint cần biết user nào.
Admin/recovery endpoint cần quyền cao.
Webhook payment nên verify signature/HMAC từ provider, không dựa vào user JWT.
```

### HMAC là gì?

HMAC là chữ ký dùng secret chung để kiểm tra webhook có thật từ provider không.

Nói dễ hiểu:

```text
Provider gửi thư kèm dấu niêm phong.
PaymentService kiểm tra dấu niêm phong có đúng không.
```

---

## 9. Interview answer mẫu

```text
Authentication xác định user là ai, Authorization xác định user được phép làm gì. JWT cần validate signature, issuer, audience, lifetime/expiration và signing key. 401 là chưa xác thực hoặc token không hợp lệ; 403 là đã xác thực nhưng không đủ quyền.
```

## 10. Checkpoint

```text
1. Authentication khác Authorization thế nào?
2. Claim là gì?
3. Signature trong JWT để làm gì?
4. Issuer là gì?
5. Audience là gì?
6. 401 khác 403 thế nào?
7. CORS có thay thế auth không?
```

## 11. Flashcards

```text
Authentication = bạn là ai.
Authorization = bạn được làm gì.
JWT = thẻ ra vào.
Claim = thông tin trên thẻ.
Signature = chống giả.
Issuer = nơi cấp token.
Audience = nơi token được dùng.
401 = token thiếu/sai.
403 = thiếu quyền.
```
