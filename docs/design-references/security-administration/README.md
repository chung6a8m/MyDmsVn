# Security administration — visual references

These three images record the latest applicable mockups from the Owner discussion on 2026-10-25. They are composition references, not screenshots of an implemented candidate or evidence of UI Labs acceptance. The image explicitly removed during that discussion is excluded; older variants with superseded header elements are not included.

Read the [UI and behavior specification](../../design/SECURITY_ADMINISTRATION_UI.md), [implementation plan](../../plans/20261025-002-security-administration-ui-lab-first.md) and [UI Labs review guide](../../development/SECURITY_UI_LAB_REVIEW.md). The specification and the Owner's explicit corrections are normative.

## Included views

| File | Applies to | Image dimensions |
|---|---|---|
| [01-user-effective-permissions.png](01-user-effective-permissions.png) | Selected-user effective-permission view after removal of header role chips/manage link. | 1586 × 992 |
| [02-role-permissions.png](02-role-permissions.png) | Role-grant view after removal of the duplicate header member icon/count. | 1586 × 992 |
| [04-permission-catalog.png](04-permission-catalog.png) | Read-only permission-catalog view requested for the third main tab. | 1586 × 992 |

The catalog retains the original mockup sequence label `04`; no earlier direct-editor variant is included. The direct Allow/Deny/Inherit editor, account maintenance, membership and import behavior are specified in text and must be demonstrated in the actual Lab candidate.

## Required interpretation

- User detail headers have no role chips, role list or separate manage-role link on any subtab. Membership detail/actions belong to **Vai trò (n)**.
- Role detail headers have no separate people icon/member-count text beside the active badge. **Thành viên (n)** retains the detail count; a count in the role master list is permitted.
- **Danh mục quyền** is read-only, with module/lifecycle filters, search, selected-definition details and copy-key action. It has no user/role assignment controls.
- Use actual canonical catalog keys. Shortened examples such as `Products.Read`, `Customers.Write` and `Inventory.Read` in the first two images are illustrative and must not become keys or aliases in code.
- Use existing active/inactive and protected-account semantics. A generated label such as **Tạm khóa** does not introduce a new account-state enum or workflow. Sample user/role/module totals are dynamic data, not constants.
- The effective view is labelled **Theo dữ liệu phân quyền đã tải** and explains missing/stale sources as specified. Its generated headline does not establish a live authorization guarantee or prove protected-account bypass.
- Role checkboxes stage a draft, and **Lưu quyền vai trò** dispatches ordered single-key commands with explicit partial-success recovery. The header checkbox in the image does not require a select-all feature.
- If the optional role-members footer link is retained, use **Xem thành viên của vai trò** and open the existing child tab. Do not imply every member has identical effective rights.
- Direct decisions use **Theo vai trò / Cho phép / Từ chối** and one-key **Lưu quyền này**. Apply the latest user-header correction there even though that separate older image is excluded.
- Generated shell/navigation decorations, repeated create affordances and icon details do not select unrelated shell features. Reuse the existing shell, one clear create action per object context, pinned theme/control primitives and the specification's compact layout.
- These PNG pixel dimensions do not prescribe a WinForms client size or physical monitor scale. The running UI must fit the specified existing shell bounds and pass actual Light/Dark/native100/native125 review.

## Reference integrity

Copied byte-for-byte from the retained generated variants; no crop, retouch or image rewrite was performed for this PR.

| File | Bytes | SHA-256 |
|---|---|---|
| `01-user-effective-permissions.png` | 1,630,373 | `4b3093b4bbade5dc6bb209fd9315dd3bb834474edf5be30cfa38adfc1f0d9df5` |
| `02-role-permissions.png` | 1,627,979 | `7595bb429f480e17ca451502f63fd1a75919fca2cac890f5d164146c7a784513` |
| `04-permission-catalog.png` | 1,582,300 | `2b78dbb9e82aa7e62b021172395d474d7b59a203f02f8c5c4aa1ee781ace4c73` |

## Preview

### User — effective permissions

![User effective-permission view with role-header chips removed](01-user-effective-permissions.png)

### Role — granted permissions

![Role-grant view without a duplicate member count in its detail header](02-role-permissions.png)

### Permission catalog

![Read-only permission catalog with search, module filters and selected-definition details](04-permission-catalog.png)
