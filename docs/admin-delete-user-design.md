# 用户管理页「删除用户」功能设计文档

> 状态：待审批
> 日期：2026-08-25
> 范围：`backend/Notes.Api`、`frontend-admin`
> 关联：阶段五已完成（用户管理页已有 重置密码 / 禁用·启用，缺「删除」）

---

## 1. 背景与目标

管理后台用户管理页 `frontend-admin/src/views/UserManage.vue` 目前仅有「重置密码」「禁用/启用」两个操作，缺少「删除用户」。目标：在现有 Admin 控制器与用户管理页上，补齐**管理员删除任意普通用户**的能力，并妥善处理被删用户的数据与物理文件。

设计原则：
- 与现有 `AdminUsersController` 的「不能操作自己账号」守卫保持一致；
- 与现有删除类接口（分类/标签/笔记/附件均为**硬删除**）保持一致——本功能同样为**硬删除，不可恢复**；
- 删除前由前端弹窗明示影响范围，二次确认。

---

## 2. 已确认的数据库行为（写代码时无需再查）

| 操作 | FK 行为 | 结论 |
|---|---|---|
| 删除 User | `Note.UserId` 必填 → EF 约定 **Cascade** | 用户的笔记被级联删除 |
| 删除 User | `Category.UserId` 必填 → **Cascade** | 用户的分类被级联删除 |
| 删除 User | `Tag.UserId` 必填 → **Cascade** | 用户的标签被级联删除 |
| 删除 Note | `Attachment.NoteId` → **Cascade**（已显式配置） | 笔记附件记录被级联删除 |
| 删除 Note | `NoteTag.NoteId`/`TagId` → **Cascade**（已显式配置） | 笔记-标签关联被级联删除 |
| 删除 Note | `NoteVersion.NoteId` → **无级联** | 必须显式先删 NoteVersion（同 AdminNotesController 做法） |
| 删除 User | `AspNetUserRoles` 等 Identity 行 → **Cascade**（Identity 配置） | 用户角色等随用户删除 |
| 物理文件 | EF **不会**删除磁盘文件 | 附件 `Uploads/note_*` 与头像 `Uploads/Avatars/*` 必须显式删 |

> 注：User→Note/Category/Tag 的级联依赖「`UserId` 为必填 → EF 默认 Cascade」这一约定。若后续迁移发现该约定未生效（运行时报 FK 约束错误），则改为在 `DeleteAsync` 之前显式 `RemoveRange(notes/categories/tags)`。本设计以「级联兜底 + 仅显式处理无级联项与物理文件」为最小实现。

---

## 3. 后端设计

### 3.1 新增端点 `DELETE /api/admin/users/{id}`

在 `Controllers/Admin/AdminUsersController.cs` 追加（沿用既有 `CurrentUserId` 私有属性与返回风格）：

| 步骤 | 动作 |
|---|---|
| 1. 自操作守卫 | `if (id == CurrentUserId) return BadRequest(new { message = "不能删除自己的账号" })` |
| 2. 取用户 | `var user = await _userManager.FindByIdAsync(id);` 不存在返回 `404` |
| 3. 管理员守卫 | `if (await _userManager.IsInRoleAsync(user, "Admin")) return BadRequest(new { message = "不能删除管理员账号" })` |
| 4. 载入笔记+附件 | `var notes = await _db.Notes.Where(n => n.UserId == id).Include(n => n.Attachments).ToListAsync();` 取 `noteIds` |
| 5. 删附件物理文件 | 遍历 `notes` 下每个 `Attachment` 调 `_files.Delete(att.FilePath)`（异常吞掉，避免单文件失败阻断） |
| 6. 删头像物理文件 | 解析 `user.AvatarUrl`（形如 `/api/auth/avatar/{name}`），取 `{name}` 拼 `Path.Combine(_files.GetUploadsRoot(), "Avatars", name)` 后 `_files.Delete(...)`（无头像则跳过） |
| 7. 显式删 NoteVersion | `_db.NoteVersions.RemoveRange(_db.NoteVersions.Where(v => noteIds.Contains(v.NoteId))); await _db.SaveChangesAsync();` |
| 8. 删用户 | `var r = await _userManager.DeleteAsync(user); if (!r.Succeeded) return BadRequest(new { message = 错误汇总 });` |
| 9. 返回 | `return NoContent();` |

注入：控制器已注入 `AppDbContext _db` 与 `UserManager<ApplicationUser> _userManager`；`IFileStorageService` 需新增注入（用于第 5/6 步）。

### 3.2 DTO 扩展

`AdminUserDto` 增加 `IsAdmin` 字段，供前端隐藏「删除管理员」按钮：

```csharp
public record AdminUserDto(
    string Id,
    string Email,
    string? DisplayName,
    string? AvatarUrl,
    DateTime CreatedAt,
    bool LockedOut,
    bool IsAdmin,        // 新增：是否 Admin 角色
    int NoteCount);
```

在 `List` 方法中一次性批量计算（避免 N+1）：

```csharp
var adminRole = await _roleManager.FindByNameAsync("Admin");
var adminUserIds = adminRole != null
    ? await _db.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync()
    : new List<string>();
// 投影时：IsAdmin = adminUserIds.Contains(u.Id)
```

需为控制器注入 `RoleManager<IdentityRole>`。

---

## 4. 前端设计（UserManage.vue）

参照既有「禁用/启用」的 `ElMessageBox.confirm` + `http` 调用模式，**不新增依赖**。

### 4.1 操作列新增删除按钮

```html
<el-button v-if="!row.isAdmin && row.email !== authStore.email"
  size="small" type="danger" plain @click="handleDelete(row)">删除</el-button>
```

- 对**自己**（`row.email === authStore.email`）隐藏——`auth.js` 已存 `email`，无需额外改动 store；
- 对 **Admin 角色**（`row.isAdmin`）隐藏——由后端 `IsAdmin` 字段驱动；即便误触，后端也会 400 拦截。

### 4.2 删除逻辑

```js
async function handleDelete(row) {
  const name = row.displayName || row.email
  try {
    await ElMessageBox.confirm(
      `确定要删除用户「${name}」吗？该用户的 ${row.noteCount} 篇笔记、附件、分类、标签及头像将一并删除，操作不可恢复。`,
      '删除确认', { type: 'error', confirmButtonText: '删除', cancelButtonText: '取消' })
  } catch { return }
  try {
    await http.delete(`/admin/users/${row.id}`)
    ElMessage.success('已删除')
    load()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '删除失败')
  }
}
```

需引入 `useAuthStore` 取 `email`（与 `isAdminToken` 同文件已导出）。

---

## 5. 通用边界规则

- 自操作 → `400 { message: "不能删除自己的账号" }`
- 删除管理员 → `400 { message: "不能删除管理员账号" }`
- 目标不存在 → `404`
- 删除失败（Identity 错误汇总）→ `400 { message }`
- 成功 → `204 No Content`
- **无数据库迁移**：仅利用既有 FK 与级联，不改 schema。

---

## 6. 文件级改动清单

| # | 文件 | 类型 | 内容 |
|---|---|---|---|
| 1 | `backend/Notes.Api/Controllers/Admin/AdminUsersController.cs` | 修改 | 新增 `DELETE /{id}`；注入 `IFileStorageService` + `RoleManager<IdentityRole>` |
| 2 | `backend/Notes.Api/DTOs/Admin/AdminDtos.cs` | 修改 | `AdminUserDto` 增加 `IsAdmin` |
| 3 | `frontend-admin/src/views/UserManage.vue` | 修改 | 操作列新增删除按钮 + `handleDelete` + 引入 `useAuthStore` |

仅 3 个文件，改动面小。

---

## 7. 实施批次（对应增量 commit）

| 批次 | 内容 | 验证 | commit 信息 |
|---|---|---|---|
| B1 | 后端：文件 1~2 | `dotnet build` 0 警告 0 错误；Swagger 手测删除（含自删/删管理员/删含附件用户 + 确认磁盘文件消失 + 确认 NoteVersion 清理） | `feat(admin): 后台支持删除用户（含数据与物理文件清理）` |
| B2 | 前端：文件 3 | `npx vite build` 通过；dev server 联调删除全链路 | `feat(admin): 用户管理页支持删除用户` |

每批完成后增量 commit（不 push，待你确认）。

---

## 8. 风险与注意事项

1. **硬删除不可恢复**：前端弹窗必须明示「操作不可恢复」，与附件删除的文案风格一致。
2. **NoteVersion 无级联**：必须显式先删（第 7 步），否则 `DeleteAsync` 可能因 FK 约束失败或残留孤儿记录。
3. **物理文件必须显式删**：附件在 `Uploads/note_*`，头像在 `Uploads/Avatars/*`；不删会积累孤立文件（阶段五的 `/admin/attachments/orphans` 扫描可事后兜底清理，但应前置删除）。
4. **管理员锁定风险**：禁止删除 Admin 角色账号，避免管理后台被锁死；若未来需要删管理员，应先「取消角色」再删。
5. **级联约定依赖**：User→Note/Category/Tag 的 Cascade 依赖 `UserId` 必填约定；上线前在测试库实删一次验证级联生效，必要时退化为显式 `RemoveRange`。
6. **头像路径解析**：`AvatarUrl` 形如 `/api/auth/avatar/{name}`，取末段文件名拼 `Uploads/Avatars/` 即可；空值时跳过。

---

## 9. 后续可选（本次不做）

- 软删除（标记 `IsDeleted` 而非物理删除），保留数据可恢复；
- 删除前二次输入用户名确认（防误删）；
- 批量删除 / 勾选多用户删除。
