# 管理后台阶段五设计文档（分类 / 标签 / 附件管理 + 部署打通）

> 状态：待审批
> 日期：2026-08-25
> 范围：`backend/Notes.Api`、`frontend-admin`、`deploy/`
> 关联现状：阶段一~四已完成（登录/仪表盘/用户管理/笔记管理），三个占位页标注「阶段五实现」

---

## 1. 背景与目标

管理后台已完成用户管理与笔记管理，但：

1. **后端缺三个 Admin 控制器**：`api/admin/categories`、`api/admin/tags`、`api/admin/attachments` 均不存在；
2. **前端三个占位页**：`CategoryManage.vue` / `TagManage.vue` / `AttachmentManage.vue` 仅渲染「阶段五实现」占位文案，路由已注册；
3. **部署链路缺口**：`nginx-notes.conf` 无 `/admin/` location；`deploy.ps1` 与 `02-deploy-app.sh` 均不构建/上传 `frontend-admin`（其 `vite.config.js` 已配 `base: '/admin/'`）；生产环境无管理员种子账号。

目标：补齐三个管理模块的前后端实现，并让管理后台可以在生产环境通过 `https://<ip>:2129/admin/` 访问。

### 已确认的数据库行为（写代码时无需再查）

| 操作 | FK 行为 | 结论 |
|---|---|---|
| 删除 Category | `Note.CategoryId` → `SetNull` | 直接删，笔记自动变「未分类」 |
| 删除 Tag | `NoteTag` → `Cascade` | 直接删，关联关系自动清理 |
| 删除 Note | `Attachment` → `Cascade`；`NoteVersion` **无级联** | AdminNotesController 已显式先删 NoteVersion |
| 删除 Attachment | 记录删除，物理文件需手动删 | 复用 `IFileStorageService.Delete()` |

---

## 2. 后端设计

### 2.1 分类管理 `AdminCategoriesController`

新建 `backend/Notes.Api/Controllers/Admin/AdminCategoriesController.cs`，`[Route("api/admin/categories")]` + `[Authorize(Policy = "Admin")]`。

| 端点 | 参数 | 说明 |
|---|---|---|
| `GET /` | `q`（分类名或用户邮箱模糊）、`page`、`pageSize` | 跨用户分页列表。默认按 `CreatedAt` 倒序，附带 `noteCount` 与所属用户（Email + DisplayName）。pageSize 钳制 1~100，page < 1 重置为 1（与 AdminUsersController 一致） |
| `PUT /{id}` | Body: `{ name: string }` | 管理员重命名。校验：非空、去首尾空白；**同用户内唯一**（`c.UserId == cat.UserId && c.Name == name && c.Id != id` 冲突返回 409） |
| `DELETE /{id}` | — | 直接删除（SetNull 已兜底）。不存在返回 404 |

实现要点（对齐 AdminNotesController 的既有模式）：

- 列表用一次查询取分页数据，再按 `userIds` 批量查用户信息拼 `Dictionary`，避免 N+1；
- `noteCount` 用 `Select(c => new { c, NoteCount = c.Notes.Count })` 投影；
- 中文搜索沿用 `EF.Functions.Collate(field, "utf8mb4_general_ci").Contains(term)` 模式。

### 2.2 标签管理 `AdminTagsController`

新建 `Controllers/Admin/AdminTagsController.cs`，结构与分类管理完全对称：

| 端点 | 参数 | 说明 |
|---|---|---|
| `GET /` | `q`（标签名或用户邮箱）、`page`、`pageSize` | 跨用户分页列表，附带 `noteCount`（`t.NoteTags.Count`）与所属用户 |
| `PUT /{id}` | Body: `{ name: string }` | 重命名，同用户内唯一校验，冲突 409 |
| `DELETE /{id}` | — | 直接删除（NoteTag 级联清理） |

> 用户端 `TagsController` 目前没有重命名接口（Categories 有 PUT），本模块的管理端重命名不依赖它，互不影响。

### 2.3 附件管理 `AdminAttachmentsController`

新建 `Controllers/Admin/AdminAttachmentsController.cs`，注入 `AppDbContext` + `IFileStorageService`：

| 端点 | 参数 | 说明 |
|---|---|---|
| `GET /` | `q`（文件名）、`userId`、`noteId`、`contentType`、`from`/`to`、`page`、`pageSize` | 跨用户分页列表。每行返回：Id、FileName、Size、ContentType、CreatedAt、所属笔记 Id+标题、所属用户 Email+DisplayName。按 `CreatedAt` 倒序 |
| `GET /{id}/download` | — | 管理员下载任意附件。查 Attachment（`Include(a => a.Note)`），`_files.GetAsync(att.FilePath)` 后 `File(stream, contentType, fileName)`；文件物理丢失时捕获 `FileNotFoundException` 返回 404 |
| `DELETE /{id}` | — | 删除记录 + 物理文件（先 `_files.Delete`，再 Remove + SaveChanges） |
| `GET /orphans` | — | **孤立文件扫描**：遍历 `Uploads/note_*` 目录下所有文件，与 DB 中全部 `Attachment.FilePath` 求差集，返回孤立文件列表（相对路径、文件名、大小、最后修改时间）+ 总大小 |
| `DELETE /orphans` | — | 批量清理上述孤立文件。**二次确认由前端弹窗承担**；后端执行前重新扫描（防止误删刚上传的文件），仅删除扫描时仍为孤立的路径 |

孤立文件扫描的实现方式：

- `IFileStorageService` 增加一个方法 `string GetUploadsRoot()`（返回 `Path.Combine(_env.ContentRootPath, "Uploads")`），控制器用它拼目录做扫描，避免控制器自己依赖 `IWebHostEnvironment`；
- **只扫 `note_*` 子目录，明确排除 `Uploads/Avatars`**（头像生命周期挂在 `ApplicationUser.AvatarUrl` 上，是字符串 URL 而非 DB 记录，diff 逻辑不同，列入后续可选优化，本次不做）；
- 文件数量对单机笔记系统可控，`Directory.EnumerateFiles` 全量遍历 + 内存 diff 即可，不做增量索引。

### 2.4 DTO 扩展

在 `DTOs/Admin/AdminDtos.cs` 文件末尾追加（沿用现有 record 风格）：

```csharp
/// <summary>分类管理列表项</summary>
public record AdminCategoryDto(
    int Id,
    string Name,
    string UserId,
    string? UserEmail,
    string? UserDisplayName,
    int NoteCount,
    DateTime CreatedAt);

public record AdminCategoryListResponseDto(List<AdminCategoryDto> Items, int Total);

/// <summary>标签管理列表项</summary>
public record AdminTagDto(
    int Id,
    string Name,
    string UserId,
    string? UserEmail,
    string? UserDisplayName,
    int NoteCount,
    DateTime CreatedAt);

public record AdminTagListResponseDto(List<AdminTagDto> Items, int Total);

/// <summary>附件管理列表项</summary>
public record AdminAttachmentListItemDto(
    int Id,
    string FileName,
    long Size,
    string ContentType,
    int NoteId,
    string? NoteTitle,
    string UserId,
    string? UserEmail,
    string? UserDisplayName,
    DateTime CreatedAt);

public record AdminAttachmentListResponseDto(List<AdminAttachmentListItemDto> Items, int Total);

/// <summary>孤立文件信息</summary>
public record AdminOrphanFileDto(
    string RelativePath,
    string FileName,
    long Size,
    DateTime LastWriteTimeUtc);

public record AdminOrphanScanResponseDto(List<AdminOrphanFileDto> Items, int Count, long TotalSize);
```

请求 DTO（`RenameDto(string Name)`）直接放各自控制器文件末尾，与 `AdminUsersController` 中 `SetStatusDto` 的现有做法一致。

### 2.5 通用边界规则

- 所有列表端点：`page < 1 → 1`；`pageSize < 1 || > 100 → 20`；
- 所有写操作返回语义：400（参数校验失败，`{ message }`）/ 404（目标不存在）/ 409（命名冲突）；
- 无需新增数据库迁移——三张表（Categories/Tags/Attachments）与现有字段完全够用。

---

## 3. 前端设计（frontend-admin）

三个页面全部参照 `UserManage.vue` 的成熟模式（`page-card` 容器 + `toolbar` 搜索区 + `el-table` + `el-pagination` + `ElMessageBox.confirm`），复用 `api/http.js`，不新增依赖。

### 3.1 CategoryManage.vue

- 搜索框（分类名 / 用户邮箱）+ 搜索按钮；
- 表格列：分类名、所属用户（DisplayName / Email 双行）、笔记数、创建时间、操作；
- 操作：**重命名**（`el-dialog` 弹窗 + 单输入框，PUT `/admin/categories/{id}`，409 冲突时 `ElMessage.error` 后端 message）、**删除**（确认弹窗，提示「删除后该用户笔记将变为未分类」）。

### 3.2 TagManage.vue

与 3.1 完全对称，删除提示改为「删除后该标签将从所有关联笔记移除」。

### 3.3 AttachmentManage.vue

- 工具栏：文件名搜索框、所属用户下拉（复用 NoteManage.vue 的 `GET /admin/users?pageSize=200` 拉取方式）、时间范围（`el-date-picker` type=daterange）；
- 表格列：文件名（超长省略 + tooltip）、大小（格式化 KB/MB）、类型、所属笔记标题、所属用户、上传时间、操作；
- 操作：**下载**（`window.open` 带 token 的问题——采用 axios `responseType: 'blob'` 后创建临时 URL 触发下载，保持 Authorization 头）、**删除**（确认弹窗，提示「将同时删除服务器上的物理文件」）；
- 独立区块「孤立文件清理」：按钮触发 `GET /admin/attachments/orphans`，结果以摘要 + 列表展示（数量、总大小），一键清理按钮走确认弹窗后 `DELETE /admin/attachments/orphans`。

### 3.4 不需要改动的部分

- `router/index.js` 路由已注册三个页面；
- `AdminLayout.vue` 侧边栏菜单已包含三个入口（占位页在线上已可见）；
- `api/http.js` 无需扩展。

---

## 4. 部署链路设计

### 4.1 nginx-notes.conf

在 `location /api/` 之后新增：

```nginx
# ---- 管理后台静态文件（frontend-admin，base=/admin/）----
location /admin/ {
    alias /usr/share/nginx/notes-admin/;
    index index.html;
    try_files $uri $uri/ /admin/index.html;

    # 静态资源缓存策略与主站一致
    if ($uri ~* \.(?:js|css|woff2?|ttf|eot|svg|ico|png|jpg|jpeg|gif|webp)$) {
        expires 30d;
        add_header Cache-Control "public, max-age=2592000, immutable";
    }
    if ($uri ~* /admin/index\.html$) {
        expires -1;
        add_header Cache-Control "no-cache, no-store, must-revalidate";
    }
}
```

说明：`try_files` 的 fallback `/admin/index.html` 会重新走前缀匹配回到本 location，alias 下可正常服务 SPA 路由。该 location 必须放在正则 deny location（`~* /(Uploads|...)`）之前不影响——`/admin/` 路径不会命中该正则。

### 4.2 deploy.ps1

- 新增变量：`$AdminFrontendDir = Join-Path $RepoRoot "frontend-admin"`、`$AdminFrontendDist = ..."\dist"`；
- 新增构建步骤（与 frontend 构建同构）：Push-Location → 无 node_modules 则 `npm ci` → `npm run build`；
- 新增上传步骤：`scp -P $SshPort -r "${AdminFrontendDist}" "${SshUser}@${ServerIP}:${RemoteTmp}/dist-admin"`（注意目标目录名固定为 `dist-admin`，与 4.3 对应）。

### 4.3 02-deploy-app.sh

在第 2 步「前端 dist」之后新增对应段落：

```bash
# 管理后台 dist
if [ -d /opt/notes/_deploy_tmp/dist-admin ]; then
  mkdir -p /usr/share/nginx/notes-admin
  rm -rf /usr/share/nginx/notes-admin/*
  cp -a /opt/notes/_deploy_tmp/dist-admin/. /usr/share/nginx/notes-admin/
  chown -R nginx:nginx /usr/share/nginx/notes-admin
  chmod -R u=rwX,g=rX,o=rX /usr/share/nginx/notes-admin
else
  echo "未检测到 dist-admin，跳过管理后台更新。"
fi
```

### 4.4 生产管理员账号

`Program.cs` 的种子逻辑已支持读取 `Admin:Email` / `Admin:Password`，仅缺生产配置注入：

1. `deploy/appsettings.Production.json` 模板增加节点（占位符）：
   ```json
   "Admin": { "Email": "__ADMIN_EMAIL__", "Password": "__ADMIN_PASSWORD__" }
   ```
2. `02-deploy-app.sh` 第 1 步生成配置时：优先读 `/root/.notes.env` 的 `NOTES_ADMIN_EMAIL` / `NOTES_ADMIN_PASSWORD`，否则交互式提示输入（允许留空 = 跳过种子，与现有 DB 密码交互风格一致），然后 `sed` 替换占位符；
3. 若配置已存在则保留不覆盖（沿用现有「已存在保留」逻辑），管理员改密走后台「重置密码」。

### 4.5 01-init-server.sh

增加 `mkdir -p /usr/share/nginx/notes-admin`（全新服务器首次部署时避免目录不存在；02 脚本里也做了 mkdir 兜底，双保险）。

---

## 5. 文件级改动清单

| # | 文件 | 类型 | 内容 |
|---|---|---|---|
| 1 | `backend/Notes.Api/Controllers/Admin/AdminCategoriesController.cs` | 新增 | 分类管理 3 端点 |
| 2 | `backend/Notes.Api/Controllers/Admin/AdminTagsController.cs` | 新增 | 标签管理 3 端点 |
| 3 | `backend/Notes.Api/Controllers/Admin/AdminAttachmentsController.cs` | 新增 | 附件管理 5 端点（含孤立扫描/清理） |
| 4 | `backend/Notes.Api/DTOs/Admin/AdminDtos.cs` | 修改 | 追加 2.4 节 DTO |
| 5 | `backend/Notes.Api/Services/FileStorageService.cs` | 修改 | 接口 + 实现增加 `GetUploadsRoot()` |
| 6 | `frontend-admin/src/views/CategoryManage.vue` | 重写 | 占位页 → 完整管理页 |
| 7 | `frontend-admin/src/views/TagManage.vue` | 重写 | 占位页 → 完整管理页 |
| 8 | `frontend-admin/src/views/AttachmentManage.vue` | 重写 | 占位页 → 完整管理页（含孤立文件清理） |
| 9 | `deploy/nginx-notes.conf` | 修改 | 增加 `/admin/` location |
| 10 | `deploy/deploy.ps1` | 修改 | 增加 admin 前端构建 + 上传 |
| 11 | `deploy/02-deploy-app.sh` | 修改 | 增加 dist-admin 发布段 + 管理员账号注入 |
| 12 | `deploy/appsettings.Production.json` | 修改 | 增加 Admin 占位节点 |
| 13 | `deploy/01-init-server.sh` | 修改 | 创建 notes-admin 目录 |

不涉及数据库迁移，不修改 `Program.cs` / `AppDbContext`。

## 6. 实施批次（对应增量 commit）

| 批次 | 内容 | 验证 | commit 信息 |
|---|---|---|---|
| B1 | 后端：文件 1~5 | `dotnet build` 0 警告 0 错误；起服务后 Swagger 手测各端点（含 401/403 越权用例） | `feat(admin): 分类/标签/附件管理 API` |
| B2 | 前端：文件 6~8 | `npx vite build` 通过；本地起 dev server 联调三个页面全操作 | `feat(admin): 分类/标签/附件管理页面` |
| B3 | 部署：文件 9~13 | 本地构建脚本 dry-run；服务器部署后 `https://<ip>:2129/admin/` 全链路可用 | `feat(deploy): 管理后台部署链路` |

每批完成后增量 commit（不 push，待你确认）。

## 7. 风险与注意事项

1. **孤立文件清理的误删风险**：删除前后端各自扫描一次（扫描→展示→确认→重新扫描→只删仍孤立者），窗口期内新上传的文件不会误删；`Avatars` 目录明确排除；
2. **管理员重命名分类/标签**会即时影响对应用户的笔记归类/标签展示，属预期行为，前端确认弹窗需明示影响范围；
3. **附件删除**为硬删除（记录+物理文件），无回收站；弹窗文案必须强调不可恢复；
4. **下载鉴权**：不能简单 `window.open`（无 Authorization 头），必须走 axios blob 方案；
5. 生产首次部署管理员密码走交互输入/环境变量，不落明文模板进 Git。

## 8. 后续可选（本次不做）

- 用户端 `PUT /api/tags/{id}` 重命名接口（与分类能力对齐）+ `frontend Tags.vue` 支持重命名；
- 头像孤立文件清理（需要单独的 diff 策略）；
- Admin 附件列表按大小排序/筛超大文件；
- 操作审计日志表（谁在后台删了什么）。
