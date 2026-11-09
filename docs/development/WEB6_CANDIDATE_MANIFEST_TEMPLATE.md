# WEB6 B05 — Candidate manifest

Manifest này là bản ghi điều khiển riêng của test harness B05. [Runbook](WEB6_CANDIDATE_RUNBOOK.md) quy định trình tự nghiệm thu. [Kế hoạch B05](../superpowers/plans/2026-11-10-web6-b05-paired-candidate.md) quy định các task và bằng chứng cần thu.

## Bản ghi tạo mới

| Trường | Giá trị cần pin |
| --- | --- |
| `version`, `mode`, `candidateId` | Phiên bản bản ghi `1`, `web6-candidate`, ID ứng viên khớp với ID build của frontend và cấu hình host |
| `backendSha`, `backendTree`, `frontendSha`, `frontendTree` | Bốn Git ID lowercase, mỗi ID 40 ký tự hex |
| `backendLocksSha256`, `frontendLockSha256` | Digest danh sách lock file của graph build backend và digest của frontend package lock |
| `web4ContractSha256`, `web5ContractSha256`, `web6ContractSha256` | Digest ba cây contract được pin |
| `backendDllSha256`, `webConfigSha256`, `iisConfigSha256` | Digest của DLL Release, Web.config và cấu hình IIS Express dùng cho lần chạy |
| `distRootSha256`, `publishedRootSha256` | Digest của hai cây frontend riêng biệt, phải bằng nhau |
| `schema13`, `sqlReceiptSha256` | Fingerprint schema13 và digest của biên nhận SQL ownership đã kiểm chứng độc lập |
| `origin`, `certificateSha256` | HTTPS loopback origin và SHA-256 của certificate DER |
| `browsers`, `selector`, `browserSelector` | `chromium,edge`, test selector backend và tên spec Playwright |
| `runSeconds`, `shutdownSeconds`, `workers`, `stopRule` | Budget hữu hạn, đúng một worker và `fail-closed` |
| `sha256` | SHA-256 của tất cả byte UTF-8 trước dòng seal, với LF và thứ tự field do writer cố định |

Digest cây dùng danh sách đường dẫn tương đối phân biệt hoa thường, sắp xếp ordinal. Mỗi dòng chứa Base64 của đường dẫn UTF-8 dùng dấu `/`, dấu `=`, SHA-256 của file và LF. Tên đường dẫn trùng khi so sánh không phân biệt hoa thường bị từ chối. Nội dung và đường dẫn của từng file chỉ dùng để đo hash trong harness. Bản ghi chỉ lưu digest tổng.

## Vòng đời

1. Gọi `CreateNew(root, inputs)` với đường dẫn tuyệt đối đến một thư mục chưa tồn tại trong vùng test đã được cấp quyền. Root chỉ có ACL của người chạy test. Chuẩn bị `security-audit.log` dưới root với cùng chính sách owner, rồi giữ artifact và biên nhận SQL ở các vùng riêng đã được harness sở hữu.
2. Gọi `SealArtifacts(files, sqlOwnershipReceiptHash)` sau khi xác minh nguồn build, schema và SQL ownership. Harness đo byte thực của từng artifact, so sánh `dist` và bản publish, kiểm tra audit file có thể mở và flush, rồi tạo mới và đọc lại `candidate.manifest`. Giữ cùng đối tượng manifest suốt run.
3. Gọi `VerifyBeforeAdmission` trước traffic với process ID và tên queue đã được harness pin độc lập. Observer phải đo lại Git HEAD/tree và trạng thái clean của cả hai repo, fingerprint schema, certificate thực, SQL ownership và topology HTTP.sys hiện tại. Dùng bộ xác minh process/config/command của WEB5 để chứng minh cùng IIS Express process. Để SQL receipt hash trống nếu ownership chưa được chứng minh. Gate kiểm lại artifact, owner/ACL, đúng tập file, audit destination và topology một controller/worker trên đúng origin. Thất bại dừng admission.
4. Dùng `RecordDisposition(stage, outcome)` trên cùng đối tượng để tạo riêng một `disposition-<stage>.record` cho mỗi stage Chromium, Edge, Cutover, Workload, Recovery hoặc Cleanup. Outcome nằm trong tập Passed, Failed, Blocked và Quarantined. Record chứa candidate ID, hash của nguyên manifest đã seal, stage, outcome và seal riêng. Giữ bytes manifest nguyên vẹn. Nếu root hoặc file record không còn xác minh được, lưu disposition qua kênh bằng chứng của controller và giữ tài nguyên cần quarantine.
5. Dispose chỉ đóng handle. Controller thực hiện drain và cleanup theo runbook, đồng thời lưu bản tóm tắt đã khử thông tin nhạy cảm. Receipt hash tham chiếu một authority SQL riêng. Quyền admission và quyền dọn SQL thuộc các bước xác minh của harness.

Giữ local record và audit file trong private root. Evidence đưa vào Git chỉ gồm bản tóm tắt đã duyệt, digest, ID đã khử thông tin nhạy cảm và kết quả thực tế. Đo một worker chỉ hỗ trợ đánh giá limiter trong topology này.
