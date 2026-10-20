# Owner Decision — REL-T00 — Local v1 Release Acceptance

Tôi đã review tài liệu `docs/LOCAL_V1_RELEASE_MATRIX.md` và **chính thức phê duyệt các quyết định D01–D10** dưới đây cho MyDmsVn Local v1.

Ngày phê duyệt: **2026-10-20**.

Đây là quyết định về phạm vi phát hành, trách nhiệm vận hành, chính sách và mục tiêu acceptance. Không phải xác nhận rằng các môi trường đã được kiểm thử hoặc sản phẩm đã sẵn sàng cho production.

## D01 — Release Candidate

**APPROVED**

- Version: `1.0.0-rc.1`.
- Freeze clean source commit tại REL-T01 sau khi hoàn thành các thay đổi liên quan.
- Không tự động sử dụng commit của GUI-P5 hay PR #38 làm RC chính thức.
- Package phải có source commit, version, manifest và SHA-256 cho tất cả file.
- Mọi thay đổi ảnh hưởng runtime, schema, dependencies hoặc package sau freeze phải có focused re-evidence tương ứng.

## D02 — Supported Windows

**APPROVED TARGET SCOPE**

- Windows 11 Pro 25H2 x64.
- .NET Framework 4.8.1.
- Desktop và Admin Migrator target `net48`, architecture x86.
- Hai package được phân phối riêng.
- Giao diện Light/Dark, DPI 100% và 125% nằm trong phạm vi kiểm thử.
- Clean VM không có Visual Studio hoặc .NET SDK là môi trường kiểm chứng bắt buộc.
- Exact Windows build, runtime servicing state và x86 dependencies phải được ghi nhận từ môi trường kiểm thử thực tế.

Các Windows edition/version/build khác chưa nằm trong phạm vi hỗ trợ được công bố.

Bằng chứng GUI-P5 trên Windows 11 Home Single Language chỉ được giữ theo đúng phạm vi lịch sử, không thay thế kiểm thử trên cấu hình đã chọn.

## D03 — SQL Server / Authentication / Topology

**APPROVED TARGET SCOPE**

- **SQL Server 2025 Express x64**.
- Sử dụng bản cập nhật servicing được hỗ trợ tại thời điểm kiểm thử.
- Single-PC topology: Desktop và SQL Server cùng một máy Windows.
- Một application database.
- Windows Authentication.
- Runtime principal và Maintenance principal phải là hai identity riêng biệt, với SQL permissions tách biệt.
- Runtime không có quyền migration, DDL hoặc đặc quyền DBA.
- Maintenance credential không được đóng gói cùng Desktop.
- SQL Server chỉ nhận kết nối từ endpoint được phê duyệt.
- Production connection yêu cầu `Encrypt=true`, `TrustServerCertificate=false` và xác thực certificate thực tế.
- Phải kiểm thử cả kết nối hợp lệ và các trường hợp certificate/TLS thất bại.

SQL Server 2017 Developer hiện tại chỉ là môi trường development/test lịch sử.

Chưa hỗ trợ trong RC này: multi-PC/LAN deployment, SQL Authentication, các SQL Server editions/versions khác và remote SQL topology.

Nếu chưa có SQL Server 2025 Express phù hợp cho kiểm thử, hãy giữ các scenario liên quan là BLOCKED/NOT TESTED. Không tự thay bằng SQL Server 2017 Developer rồi công bố đã VERIFIED.

## D04 — Prior Client / Schema Upgrade

**APPROVED**

- RC đầu tiên hướng đến fresh installation.
- Database ban đầu phải Empty hoặc ở trạng thái được current schema policy chấp nhận.
- Supported schema là exact scripts `001–009`, version 9, cùng các artifact/invariant checks.
- Hỗ trợ repair/reinstall cùng phiên bản mà không làm mất database, logs hoặc configuration.
- Chưa hỗ trợ upgrade từ schema prefix 001–008.
- Chưa hỗ trợ mixed-version clients, rolling upgrade, automatic migration, downgrade hoặc resume migration từ một prefix chưa hoàn chỉnh.
- Chưa claim upgrade giữa các phiên bản client khác nhau nếu chưa có cặp artifact được phê duyệt và kiểm thử.

Không tự động reset, thay đổi journal hoặc xóa database có dữ liệu cần bảo toàn.

Các trường hợp schema incompatible vẫn phải được kiểm thử để chứng minh fail-closed và không gây mutation ngoài dự kiến.

## D05 — Downtime / Maintenance

**APPROVED**

- Bắt buộc stop/drain toàn bộ Desktop và các thao tác Post đang thực hiện trước khi bảo trì schema.
- Planned maintenance window mục tiêu tối đa 2 giờ.
- Không mixed-client access trong maintenance.
- Chỉ mở lại ứng dụng sau khi schema, permissions, Login, smoke tests và reconciliation đạt yêu cầu.
- Nếu vượt maintenance window hoặc migration thất bại, tiếp tục giữ hệ thống offline và kích hoạt recovery/escalation.
- Không tự động restore, downgrade hoặc retry migration khi trạng thái dữ liệu chưa được xác minh.

Thời gian downtime thực tế phải được đo trong REL-T04, không tự đánh dấu PASS dựa trên mục tiêu 2 giờ.

## D06 — Product / Release / Security / Support Ownership

**APPROVED**

Tôi trực tiếp kiêm nhiệm các vai trò:

- Product Owner.
- Release Owner.
- Security Owner.
- Support Owner.
- Incident/Patch Decision Owner.

Không cần tạo thêm vai trò nhân sự riêng trong giai đoạn Local v1 nếu chưa có nhu cầu.

Khi ghi release sign-off, hãy sử dụng tên/định danh thực tế của tôi và ngày phê duyệt có thể truy vết. Không tự tạo tên người, chữ ký hoặc thay tôi phê duyệt các gate chưa hoàn thành.

Chính sách ban đầu:

- RC phục vụ kiểm thử/pilot có kiểm soát, chưa công bố SLA thương mại.
- Kiểm tra dependency/security advisories định kỳ hằng tháng.
- Đánh giá ngay các lỗ hổng Critical được biết đến và quyết định mitigation/hotfix theo mức độ rủi ro.
- Không công bố hỗ trợ OS/runtime/SQL đã hết vòng đời servicing.
- Support contact, escalation channel và thông tin liên hệ cụ thể phải được ghi nhận trước production GO; chưa có thông tin thực tế thì giữ PENDING.

## D07 — Signing / Distribution

**APPROVED FOR RC**

- Chấp nhận unsigned RC trong môi trường nội bộ được phê duyệt.
- Phân phối qua kênh có kiểm soát, không public download.
- Bắt buộc kiểm tra manifest, source commit, SHA-256 và quyền truy cập package.
- Checksum không được xem là bằng chứng xác thực publisher.
- Dùng versioned-directory deployment như hiện tại.
- Không bổ sung MSI hoặc auto-updater trong REL.

Production GA ưu tiên Authenticode signing và verified trust chain.

Approval unsigned RC **không đồng nghĩa** approval unsigned production. Nếu GA vẫn unsigned, phải có quyết định chấp nhận rủi ro riêng trước release.

## D08 — DBA / Backup / Restore / Recovery

**APPROVED OWNER AND TARGET POLICY**

Tôi trực tiếp kiêm nhiệm:

- DBA Owner.
- Database Maintenance Operator.
- Backup/Restore Owner.
- Recovery Decision Owner.

Các trách nhiệm này thuộc về tôi; nếu giao một người khác thực hiện thao tác cụ thể sau này, phải ghi rõ người vận hành, quyền hạn và bằng chứng thực tế.

Recovery targets:

- **RPO ≤ 1 giờ**.
- **RTO ≤ 4 giờ**.

Chính sách backup mục tiêu:

- SQL Server FULL recovery model.
- Full database backup hằng ngày.
- Transaction log backup mỗi 30 phút.
- Với SQL Server Express, dùng Windows Task Scheduler hoặc một giải pháp scheduling được quản trị và kiểm chứng tương đương; không giả định SQL Server Agent có sẵn.
- Lưu ít nhất một bản backup ngoài máy SQL.
- Backup storage phải có ACL và biện pháp bảo vệ phù hợp.
- Retention ban đầu: 14 ngày.
- Có cơ chế phát hiện backup thất bại, kiểm tra backup integrity và diễn tập restore.

REL-T04 bắt buộc:

- Restore backup thật vào database riêng được phê duyệt.
- Xác minh schema, users, business data, receipt, ledger và stock balances.
- Đo RPO/RTO thực tế.
- Diễn tập migration failure, partial commit và recovery.
- Kiểm tra reconciliation trước khi cho phép người dùng quay lại hệ thống.

RPO/RTO hiện tại là **TARGET**, không phải VERIFIED. Nếu chưa thực hiện restore drill hoặc hạ tầng chưa đáp ứng, G2 phải tiếp tục OPEN/BLOCKED.

Không chạy recovery drill trên production database.

## D09 — Legacy Authentication

**APPROVED — OPTION B**

Chọn **B: không hỗ trợ đăng nhập bằng legacy password algorithm chưa được xác minh**.

- RC đầu tiên sử dụng fresh database và identity được tạo bởi security bootstrap hiện hành.
- Không cần triển khai legacy password verifier chưa có specification/test vectors được phê duyệt.
- Legacy algorithm chưa được hỗ trợ phải fail closed.
- Không tự chuyển đổi hash hoặc reset hàng loạt tài khoản.
- Không dùng bootstrap để thay thế hoặc chiếm quyền tài khoản cũ.
- Nếu cần tiếp nhận tài khoản legacy sau này, phải có plan riêng cho controlled migration/reset, bảo toàn dữ liệu và customer impact approval.

REL-T06 phải có bằng chứng xác minh lựa chọn B và hành vi fail-closed. Fresh BCrypt tests không thay thế verification này.

## D10 — Known Limitations / Feature Exclusions / Acceptance

**APPROVED SCOPE**

Chấp nhận các giới hạn hiện tại:

- Local Mode kết nối trực tiếp SQL Server.
- Application authorization trong Desktop không tạo trusted server boundary độc lập.
- `CatalogChangedMessage` chỉ hoạt động trong cùng process.
- Không HTTP/P7, remote API hoặc AppCore/net8 package.
- Không MSI/auto-update.
- Không sales orders/invoices, outbound stock, returns/reversal, stock valuation, offline synchronization và những module chưa triển khai.
- Workstation file audit là best-effort, không phải tamper-proof audit store.

Các giới hạn này phải được ghi rõ trong release documentation và risk register.

Về GUI-P5:

- Giữ nguyên kết quả và provenance đã được acceptance.
- `Warehouse125Dark Down` vẫn là NOT TESTED trong evidence lịch sử.
- Vì RC target bao gồm DPI 125% và Dark theme, REL phải xác định và thực hiện kiểm chứng còn thiếu, hoặc trình tôi phê duyệt điều chỉnh phạm vi hỗ trợ rõ ràng.
- Không tự biến exception thành PASS và không sử dụng GUI-P5 acceptance để waive các release gate khác.

## Yêu cầu Codex cập nhật REL-T00

Hãy tiếp tục trên worktree/branch hiện tại và thực hiện:

1. Cập nhật `docs/LOCAL_V1_RELEASE_MATRIX.md` với các Owner Decisions D01–D10 ở trên.
2. Thay M02 placeholder bằng target matrix cụ thể: Windows 11 Pro 25H2 x64 + .NET Framework 4.8.1 + SQL Server 2025 Express x64 + Windows Authentication + Single-PC + Local net48/x86.
3. Giữ trạng thái của từng configuration đúng với evidence thực tế. Không đánh dấu VERIFIED trước khi có test.
4. Cập nhật ownership: tôi kiêm nhiệm Product, Release, Security, Support, DBA và Backup/Restore. Không tự tạo nhân sự hoặc giả mạo sign-off.
5. Cập nhật B01–B09: phân biệt blocker đã giải quyết bằng Owner Decision với blocker còn tồn tại do thiếu resource, implementation hoặc verification.
6. Cập nhật mapping S01–S12 và G1–G5 theo phạm vi mới.
7. Giữ nguyên evidence GUI-P5 đã được chấp nhận trong phạm vi của nó; không tự mở rộng conclusion hoặc xóa exception.
8. Cập nhật rollback boundaries, maintenance policy và recovery targets.
9. Cập nhật tài liệu plan/roadmap liên quan nếu cần để nhất quán với các quyết định trên.
10. Không thay đổi runtime/source code, SQL schema, security policy hoặc sản phẩm chỉ vì việc phê duyệt REL-T00, trừ khi có task triển khai riêng được phê duyệt.

## Quy tắc về trạng thái

Phân biệt nghiêm ngặt:

- `APPROVED`: Owner đã quyết định chính sách/phạm vi.
- `PENDING`: Thông tin hoặc phê duyệt thực tế còn thiếu.
- `NOT TESTED` / `NOT RUN`: Chưa có bằng chứng thực thi.
- `BLOCKED`: Không thể hoàn thành acceptance vì thiếu điều kiện hoặc có blocker chưa giải quyết.
- `VERIFIED` / `PASS`: Chỉ áp dụng cho scenario đã thực thi và có bằng chứng tương ứng.
- `UNSUPPORTED`: Nằm ngoài phạm vi hỗ trợ đã được phê duyệt hoặc không được implementation hỗ trợ.

Không chuyển configuration thành VERIFIED chỉ vì đã được Owner phê duyệt.

REL-T00 có thể được đánh dấu hoàn thành khi tài liệu quyết định, ownership và release/rollback boundaries đã được cập nhật và review đúng, với các thông tin triển khai cụ thể chưa có được ghi rõ là PENDING.

**REL-T00 completion không đồng nghĩa production release acceptance.**

Tất cả G1–G5 tiếp tục OPEN cho đến khi các task liên quan hoàn thành, có đủ mandatory evidence và tôi phê duyệt sign-off cuối cùng.

Sau khi cập nhật, hãy báo cáo:

- Các file đã thay đổi.
- Trạng thái từng D01–D10.
- Blocker nào đã giải quyết.
- Blocker nào còn OPEN/BLOCKED.
- Task tiếp theo có thể triển khai và task nào cần tài nguyên Windows/SQL/backup cụ thể.

Không cần hỏi lại những lựa chọn D01–D10 đã được phê duyệt, trừ khi phát hiện xung đột kỹ thuật hoặc giới hạn thực tế cần thay đổi quyết định Owner.

**Production release status: NOT READY / NO-GO cho đến khi hoàn tất REL acceptance.**
