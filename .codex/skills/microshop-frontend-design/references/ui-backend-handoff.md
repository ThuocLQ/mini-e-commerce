# Handoff UI – Backend cho MicroShop

Dùng khi UI slice đọc/ghi dữ liệu qua Gateway, hoặc khi thiết kế phát hiện contract chưa đủ để hiển thị nghiệp vụ một cách trung thực. Handoff là một yêu cầu ngắn, có thể kiểm chứng; không thiết kế database, event hay endpoint nội bộ từ phía frontend.

## Nội dung tối thiểu

Nêu bằng ngôn ngữ nghiệp vụ:

1. **Actor và mục tiêu:** customer hay vai trò operations nào, dữ liệu trong phạm vi/ownership nào, quyết định nào họ cần đưa ra.
2. **Gateway contract:** method/path, query/path/body, response view model, field bắt buộc/nullability, pagination/sort/filter và version/cursor nếu có. Frontend không phụ thuộc DTO service nội bộ.
3. **Authorization:** authenticated hay role/claim nào; hành vi mong đợi cho 401, 403 và ownership mismatch.
4. **State machine:** state được phép nhìn thấy, transition nào user được yêu cầu, trạng thái pending/eventual consistency và điều kiện server-confirmed success.
5. **Tính đúng đắn khi mutation:** idempotency key, duplicate behavior, optimistic concurrency/version, validation/business conflict, retry safety và bản ghi nào là source of truth sau khi timeout.
6. **Failure model:** status/code ổn định hoặc error shape an toàn, câu chữ/action UX mong muốn, support trace/correlation ID nếu backend cung cấp. Không chuyển raw exception hay downstream detail cho người dùng.
7. **Observability:** audit fields hoặc timestamp cần trả về để nhân viên giải quyết case; không yêu cầu frontend đọc log hay event broker.

## Quy tắc thiết kế hợp đồng

- Contract public ở Gateway là anti-corruption boundary: Application/Domain sở hữu rule, service nội bộ sở hữu implementation; frontend chỉ tiêu thụ capability đã được phê duyệt.
- Với thao tác tiền, tồn kho, đơn, fulfillment hoặc dữ liệu nhạy cảm, contract phải phân biệt `accepted/requested`, `processing/pending`, `completed`, `failed` và `rejected` khi các trạng thái này có ý nghĩa. Đừng gộp chúng thành boolean `success`.
- Endpoint liệt kê có filter/sort phải trả về đủ ngữ cảnh để Operations hiểu kết quả; endpoint chi tiết phải trả điều kiện và thông tin audit thực sự được phép xem.
- Không buộc frontend poll để che thiếu contract. Nếu eventual consistency là chủ ý, xác định tín hiệu refresh/retry, thời gian hiển thị và copy trạng thái trung thực.

## Mẫu handoff ngắn

```
Case: [vai trò] cần [mục tiêu] cho [đối tượng/phạm vi].
Gateway: METHOD /path; input ...; output view model ...
Quyền: ...; 401/403/ownership: ...
States: ...; success được xác nhận khi ...; pending hiển thị ...
Mutation safety: idempotency/version/retry ...
Errors UX: validation ...; conflict ...; unavailable ...; trace ...
Audit/refresh: updatedAt/actor/correlation và cách UI làm mới ...
```

Nếu một mục chưa được backend xác định, thể hiện nó là câu hỏi/constraint mở trong handoff; đừng giả lập hành vi bằng UI.
