---
name: microshop-frontend-design
description: Thiết kế hoặc triển khai giao diện Storefront, Operations Portal và UI dùng chung của MicroShop. Dùng cho luồng ecommerce B2C hay vận hành cần phản ánh quy trình, contract Gateway và trạng thái nghiệp vụ thật; không dùng cho trang marketing độc lập.
---

# Thiết kế Frontend MicroShop

Tạo giao diện thương mại điện tử đáng tin cậy, có thể vận hành: người dùng hiểu việc đang diễn ra, có thể hoàn tất tác vụ hoặc phục hồi khi lỗi. Giao diện là biểu hiện của nghiệp vụ đã được backend xác nhận, không phải dashboard hay bản mock đẹp mắt.

## Bắt đầu từ ngữ cảnh thật

Trước khi đổi UI, rà soát phần đang có ở `Frontend/apps/storefront` hoặc `Frontend/apps/operations`, route cùng adapter Gateway, request/response, phân quyền và trạng thái backend liên quan. Giữ nguyên các quyết định đã có hiệu lực nếu yêu cầu không đụng tới chúng.

- Frontend chỉ gọi `ApiGateway` qua proxy/adapter đã có; không gọi service container, broker, database hoặc route nội bộ/debug trực tiếp.
- Thiết kế từ action, điều kiện hợp lệ, kết quả server xác nhận, lỗi phục hồi được và đường quay lại; sau đó mới chọn bố cục.
- Không hiển thị “thành công”, tồn kho, thanh toán hay thay đổi trạng thái trước khi server xác nhận. Với read model bất đồng bộ, nói rõ đang chờ đồng bộ và cho phép làm mới an toàn.
- Không đưa secret, webhook key, API key nội bộ hay điều khiển giả lập đặc quyền vào trình duyệt. Token ngắn hạn chỉ được giữ trong bộ nhớ khi adapter hiện hữu cho phép; production dùng cookie/session Gateway đã phê duyệt.

Khi một slice thay đổi dữ liệu, đọc [handoff contract UI–backend](references/ui-backend-handoff.md) trước khi thiết kế hoặc yêu cầu backend. Đây là cách chuyển quyết định UX thành contract, không phải lý do để frontend tự suy đoán luật nghiệp vụ.

## Kiến trúc thông tin theo sản phẩm

Chọn IA theo vai trò và công việc, không theo component có sẵn.

- **Storefront B2C:** đặt catalog và khám phá sản phẩm ở trung tâm; product detail giúp ra quyết định; basket và checkout là một hành trình liên tục; tài khoản dẫn tới đơn của chính khách. Điều hướng phải cho thấy vị trí và số lượng giỏ hàng, không chôn tác vụ mua hàng trong modal hoặc menu mơ hồ.
- **Operations Portal:** tổ chức theo workstream mà nhân viên thực hiện lặp lại: đơn hàng/thanh toán, tồn kho/đối soát, catalog, mua hàng/nhà cung cấp, khuyến mại hoặc fulfillment khi backend hỗ trợ. Bên trong mỗi workstream, ưu tiên danh sách có lọc, hàng/case detail, hành động đúng quyền và lịch sử/audit. Không biến portal thành trang tổng quan trang trí.
- **UI dùng chung:** chỉ chuẩn hoá pattern khi cùng ý nghĩa nghiệp vụ: trạng thái, tiền tệ, thời gian, lỗi, bảng, form, xác nhận và quyền. Không ép Storefront và Operations dùng cùng mật độ hoặc cùng bố cục.

Mỗi trang cần trả lời nhanh: đây là phạm vi dữ liệu nào, trạng thái mới nhất là khi nào, người dùng có thể làm gì, và hậu quả của hành động là gì. Metric chỉ xuất hiện khi có nguồn dữ liệu, phạm vi thời gian/bộ lọc, thời điểm cập nhật và hành động theo sau; không tạo số liệu, biểu đồ, hoạt động gần đây hay cảnh báo để lấp chỗ trống.

## Luồng và UI states

Với từng feature, định nghĩa state machine có thể nhìn thấy thay vì chỉ một happy path: initial/loading, ready, empty hoặc no-result, validation, working/duplicate-submit lock, unauthorized/forbidden, unavailable/retryable, conflict/stale data, và server-confirmed success. Giữ dữ liệu ổn định khi đang refresh nếu điều đó giúp người dùng tiếp tục công việc; dùng skeleton/progress chỉ khi nó phản ánh phần đang tải.

### Storefront

Thiết kế và kiểm tra đầy đủ các tình huống sau khi feature liên quan xuất hiện:

1. Catalog: tìm/lọc, loading, không có kết quả, sản phẩm không còn bán, giá và khả dụng tồn kho, product detail với thông tin đủ để mua.
2. Basket: thêm, tăng/giảm, xoá, giỏ trống; cập nhật giá/tồn kho và version conflict phải giải thích rõ lựa chọn làm mới hoặc sửa lại.
3. Xác thực: nhãn và validation rõ; sai thông tin đăng nhập, session hết hạn, quyền/ownership bị từ chối đều có đường tiếp tục an toàn.
4. Checkout: khoá gửi lặp, dùng idempotency theo contract, hiển thị lỗi coupon/tồn kho/basket version có thể xử lý; sau khi tạo đơn, hiển thị mã tham chiếu và trạng thái chờ thanh toán thay vì coi là đã thanh toán.
5. Đơn và thanh toán: hiển thị snapshot item, tổng tiền/tiền tệ, thời gian, trạng thái dễ hiểu và bản chất pending/asynchronous. Chỉ mời thao tác thanh toán khi state machine cho phép.

### Operations Portal

Thiết kế cho người dùng có trách nhiệm vận hành, không cho người xem báo cáo chung chung.

- Danh sách phải có cột ổn định, trạng thái, bộ lọc có ý nghĩa, tìm kiếm, empty state theo filter và row action. Table có thể cuộn ngang trên màn hình nhỏ thay vì làm mất dữ liệu quan trọng.
- Case detail phải cho biết đối tượng, dữ liệu tác động, ràng buộc, quyền thao tác, trạng thái trước/sau, thời điểm cập nhật và trace/audit ID nếu contract cấp.
- Hành động tài chính, điều chỉnh tồn, huỷ/void, nhận hàng hay thay đổi không đảo ngược phải xác nhận explicit: nói rõ phạm vi và hậu quả, chống gửi lặp, rồi phản hồi kết quả đã được server xác nhận. Không suy diễn quyền từ UI; 403 là state riêng với hướng dẫn hợp lệ.
- Luồng đối soát phải nêu dữ liệu nào khớp/lệch/thiếu và cho biết thao tác tiếp theo có thật. Không dùng “health score” hoặc màu trạng thái mơ hồ thay cho nguyên nhân nghiệp vụ.

## Ngôn ngữ giao diện

- Storefront thân thiện và định hướng sản phẩm: dùng media sản phẩm thật khi có; nếu chưa có, dùng placeholder trung tính, trung thực. Ưu tiên thứ bậc giá, tồn kho và CTA mua.
- Operations gọn, dày thông tin và dễ quét: điều hướng bền vững, bảng, filter, badge trạng thái, chi tiết theo ngữ cảnh và action ở nơi quyết định được đưa ra. Bề mặt chỉ có viền/card khi nó tạo ranh giới nội dung thật.
- Dùng typography hệ thống, palette trung tính nhỏ với accent có chủ đích. Tránh gradient trang trí, blob/floating decoration, stock imagery mơ hồ, card tròn đồng loạt và dashboard analytics giả.
- Chọn control theo nhiệm vụ: input/stepper cho số lượng, menu cho tập lựa chọn, segmented control cho mode, icon button cho công cụ gọn, và button cho command rõ ràng. Icon-only control luôn có accessible name và tooltip.

## Khả năng tiếp cận và responsive

- Ưu tiên semantic HTML; mọi field có label nhìn thấy được, validation gắn với field, thứ tự tab hợp lý và trạng thái đang gửi. Dùng ARIA để bổ sung, không thay thế semantics.
- Focus phải đi vào dialog khi mở, trở về trigger khi đóng, và không mất sau lỗi hoặc chuyển route. Cập nhật trạng thái quan trọng dùng `role=status`/`alert` phù hợp nhưng không lặp thông báo.
- Không mã hoá trạng thái đơn, thanh toán, tồn kho hay validation chỉ bằng màu. Bảo đảm tương phản, kích thước target chạm/nhấp hợp lý và nội dung có thể zoom.
- Kiểm tra ít nhất desktop rộng, tablet và mobile hẹp: không viewport-scaled type, chữ bị cắt, control chồng nhau hoặc hành động bị ẩn. Trên mobile, ưu tiên tác vụ chính; navigation có thể thu gọn/scroll ngang có nhãn, form một cột, table giữ cấu trúc bằng overflow hoặc pattern detail đã chủ ý.

## Kiến trúc frontend và ranh giới hệ thống

- Đặt Gateway calls sau typed client/adapter; component nhận view model nghiệp vụ, không tự dựng URL hay parse raw HTTP payload. Validate runtime payload ở boundary.
- Giữ client component hẹp; để read screen render phía server khi phù hợp, chỉ đưa state tương tác/form vào client.
- UI có thể map status backend sang câu chữ người dùng, nhưng không tạo enum, transition hay business rule cạnh tranh với Domain/Application. Hành vi server là nguồn sự thật theo Clean Architecture.
- Lỗi cho người dùng phải an toàn, có action hoặc support trace ID nếu có. Không lộ stack trace, payload downstream, token hay nội bộ hạ tầng.

## Visual QA và điều kiện hoàn thành

Sau khi triển khai UI slice, xác minh bằng API controlled/live phù hợp, rồi chạy browser ở các viewport cần thiết. Không chấp nhận chỉ có mocked happy path.

1. Thực hiện primary workflow cùng các state lỗi/empty/unauthorized phù hợp; thử duplicate submit và stale/concurrent update nếu mutation có rủi ro đó.
2. Chụp screenshot browser cho bước chính và ít nhất một state rủi ro có ý nghĩa (ví dụ empty, unavailable, conflict hoặc pending). Rà soát ảnh theo hierarchy, overflow, focus, contrast, copy, trạng thái và responsive—không chỉ kiểm tra ảnh đã tạo ra.
3. Xác nhận route thật, phân quyền, payload và state machine với Gateway; payment/order/inventory không được “xanh” chỉ vì UI vừa gửi request.
4. Ghi nhận contract cần thiết hoặc thiếu theo reference; chỉ coi slice hoàn thành khi user có đường hồi phục và UI không tiết lộ hạ tầng.
