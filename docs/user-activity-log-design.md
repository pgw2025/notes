# 用户行为日志（浏览 / 登录 / 操作记录）设计方案

> 状态：**待审批**（本文档仅设计，未改任何代码）
> 范围：后端 Notes.Api + 管理后台 frontend-admin
> 日期：2026-09-14

---

## 一、需求分析

用户要求在管理后台能看到：

| 需求 | 对应行为 | 说明 |
|---|---|---|
| 谁什么时间登录了 | 登录记录 | 登录成功 / 登录失败（可选）/ 登出 |
| 什么时间修改了什么 | 操作记录 | 笔记增删改、版本恢复、置顶、分类/标签管理、资料修改、附件上传删除、管理员操作 |
| 浏览了哪个笔记 | 浏览记录 | 打开笔记详情（GET /notes/{id}），需防刷去重 |

**明确不记录的**（避免日志噪音，见决策点 3）：
- token 自动续期（refresh，每次页面停留 15 分钟必然触发一次）
- 列表 / 搜索 / 时间线等只读聚合接口
- /auth/me、头像读取

---

## 二、总体架构

```
请求 → JWT 认证 → ActivityLogFilter（全局 Action Filter）
                      │  按「路由映射表」识别行为类型
                      │  采集 UserId / IP / UA / 实体快照
                      ▼
              Channel<ActivityLog>（内存队列，写入不阻塞请求）
                      ▼
              BackgroundService 消费 → AppDbContext 批量落库
                      ▼
              MySQL ActivityLogs 表
                      ▼
              管理后台 ActivityLog.vue（筛选 + 分页 + 单用户视图）
```

---

## 三、决策点（请逐项确认）

### 决策点 1：行为采集方式 —— 推荐 A

| 方案 | 做法 | 优点 | 缺点 |
|---|---|---|---|
| **A. 全局 Action Filter + 路由映射表（推荐）** | 一个 `ActivityLogFilter` 全局注册，内部用 `Controller+Action → (行为类型, 实体类型)` 静态映射表判定要不要记、记什么 | 零侵入业务代码；新增行为只改映射表一行；集中可维护 | 需要维护映射表（约 20 行配置） |
| B. 特性标注埋点 | 自定义 `[Activity(ActionType.NoteUpdate)]` 标到各 Action 上 | 语义在代码现场 | 要改每一个控制器（约 15 处），侵入大 |
| C. Controller 内手动调用日志服务 | 每个 Action 里写 `await _log.RecordAsync(...)` | 能拿到最精确的业务上下文（如笔记标题） | 侵入最大、容易漏记，改一处业务就要记得改日志 |

**推荐 A**，实体名称快照（笔记标题等）通过在 Filter 中按需查一次库补全；查不到（已删除）就显示「已删除」。

### 决策点 2：存储结构 —— 推荐 A（单表）

| 方案 | 说明 |
|---|---|
| **A. 单表 ActivityLogs（推荐）** | 登录/操作/浏览统一一张表，用 `ActionType` 区分。查询灵活（一条时间线看全量），表结构简单 |
| B. 三张表（LoginLogs / OperationLogs / ViewLogs） | 各表字段更贴合，但管理页要 UNION 查询，且"浏览笔记"和"修改笔记"本质都是访问实体，拆开过度设计 |

**推荐 A**。表结构：

```csharp
public class ActivityLog
{
    public long Id { get; set; }                      // bigint 自增
    public string UserId { get; set; }                // Identity 用户 Id
    public ActivityAction Action { get; set; }        // 行为类型（int 枚举存储）
    public ActivityEntity EntityType { get; set; }    // None/Note/Category/Tag/Attachment/User/Profile
    public string? EntityId { get; set; }             // 实体 Id（字符串兼容 Identity string Id）
    public string? EntityTitle { get; set; }          // 实体名称快照（删除后仍可读）
    public string? Detail { get; set; }               // JSON 扩展字段（如旧标题→新标题、搜索词）
    public string? Ip { get; set; }                   // 客户端 IP（X-Forwarded-For 优先）
    public string? UserAgent { get; set; }            // 截断至 300 字符
    public DateTime CreatedAt { get; set; }           // UTC
}
```

索引：`(UserId, CreatedAt)`、`(CreatedAt)`、`(Action)`。
关联：UserId 不设外键强删联动（用户被删后日志保留，显示「已注销用户」），**见决策点 6**。

### 决策点 3：记录范围清单

**记录**（✅ 默认开启 / ⭕ 可选 / ❌ 不记）：

| 行为 | Action 枚举 | 默认 |
|---|---|---|
| 登录成功 | `Login` | ✅ |
| 登录失败（含账号被禁用被拒） | `LoginFailed` | ⭕ 有安全排查价值，建议开 |
| 登出 | `Logout` | ✅ |
| 笔记：新建 / 修改 / 删除 | `NoteCreate` / `NoteUpdate` / `NoteDelete` | ✅ |
| 笔记：版本恢复 | `NoteRestore` | ✅ |
| 笔记：置顶 / 取消置顶 | `NotePin` / `NoteUnpin` | ✅ |
| 笔记：浏览详情 | `NoteView` | ✅（带去重，见下） |
| 附件：上传 / 删除 | `AttachmentUpload` / `AttachmentDelete` | ✅ |
| 分类：建 / 改 / 删 | `CategoryCreate` / `CategoryUpdate` / `CategoryDelete` | ✅ |
| 标签：建 / 删 | `TagCreate` / `TagDelete` | ✅ |
| 资料修改（昵称/头像/默认色） | `ProfileUpdate` | ✅ |
| 管理员：禁用/解封/重置密码/删用户/删笔记 | `AdminSetStatus` / `AdminResetPassword` / `AdminDeleteUser` / `AdminDeleteNote` | ✅（操作者记管理员自己） |
| 搜索关键词 | `Search` | ⭕ 默认关（可能高频且含隐私） |
| token 自动续期 refresh | — | ❌ 噪音 |
| 列表/时间线等只读接口 | — | ❌ 噪音 |

**浏览去重规则（防刷）**：同一用户 + 同一笔记，**10 分钟内**重复打开只记一条（内存 `ConcurrentDictionary` 时间戳，进程级即可，重启丢失可接受）。⭕ 去重窗口默认 10 分钟，可改。

### 决策点 4：写入方式与性能 —— 推荐 B（异步队列）

| 方案 | 说明 |
|---|---|
| A. Filter 内同步写库 | 实现最简单，但日志写失败/变慢会拖累业务请求 |
| **B. Channel 内存队列 + BackgroundService 批量落库（推荐）** | 请求内只入队（微秒级），后台攒批（每 20 条或每 3 秒）批量 Insert；日志异常不影响业务 |
| C. 写文件再解析 | 运维复杂，个人项目没必要 |

个人项目流量小，A 也完全够用；**推荐 B** 因为实现代价仅多约 40 行，且彻底隔离故障域。若选 B，停机时队列中未落库的少量日志会丢（秒级窗口内），可接受。

### 决策点 5：数据保留策略

| 选项 | 说明 |
|---|---|
| **A. 保留 90 天 + 启动时自动清理（推荐）** | 每次应用启动删 `CreatedAt < now-90d` 的记录；后台日志页同时提供「手动清空」按钮 |
| B. 永久保留 | 表会无限增长（浏览记录是大头，估算：日均 200 次浏览 → 一年 7 万行，MySQL 完全无压力，其实也可接受） |
| C. 保留 30 天 | 更激进 |

个人项目数据量小，**A、B 都合理**，默认建议 A。

### 决策点 6：用户删除后日志怎么办

| 选项 | 说明 |
|---|---|
| **A. 日志保留，不设外键（推荐）** | ActivityLogs.UserId 存 Id 快照，另在 Detail 或冗余列存用户名快照；显示时查不到用户就显示「已注销」 |
| B. 级联删除日志 | 用户删了历史也消失，审计价值归零，不推荐 |

选 A 时在写入日志时冗余一列 `UserName`（快照），避免删除用户后日志完全无法读。**此列建议加上**（成本极低）。

### 决策点 7：查询 API 与前端入口

**后端新增** `AdminActivitiesController`（Admin 策略）：

```
GET /api/admin/activities
    ?userId=      可选，按用户过滤
    &action=      可选，按行为类型过滤
    &entityType=  可选
    &start= &end= 可选，时间范围
    &q=           可选，搜实体名称/用户名
    &page= &pageSize=
→ { items, total }
（返回项附带用户昵称/头像，联 Users 表，查不到显示「已注销」）

GET /api/admin/actions    → 行为类型枚举字典（供前端下拉）
DELETE /api/admin/activities  → 手动清空（可选）
```

**前端 frontend-admin 新增**「操作日志」页（`ActivityLog.vue`）：
- 筛选栏：用户搜索框、行为类型下拉、时间范围（el-date-picker）、关键词
- 表格列：时间 / 用户（头像+昵称）/ 行为（带颜色 tag）/ 对象（类型+名称）/ IP / 浏览器
- 行为展示示例：`浏览笔记《xxx》`、`修改笔记《yyy》`、`登录成功`、`管理员重置了 zzz 的密码`
- **UserManage.vue 联动**：每行操作区加「记录」按钮 → 跳转日志页并预置 userId 过滤（决策点 7 内含）

---

## 四、技术细节与风险

1. **IP 获取（重要）**：生产环境走 Nginx 反代，需在 `Program.cs` 启用 `ForwardedHeaders` 中间件，否则记到的 IP 全是 `127.0.0.1`。当前项目未配置，本次需一并加上（属于本功能必要配套，改动约 3 行）。
2. **UserAgent**：取 `Request.Headers.UserAgent`，截断 300 字符。
3. **Migration**：新增实体 + `dotnet ef migrations add AddActivityLogs`；项目启动自动 Migrate，部署即建表。
4. **登录失败记录**：`LoginFailed` 时 UserId 可能为空（账号不存在），此时 UserId 存空串、Detail 存尝试的账号名（脱敏只存账号本身，不存密码）。
5. **浏览记录的位置**：在 Filter 记 `NoteView` 时需查一次笔记标题（Filter 内 scoped 注入 DbContext），注意用 `AsNoTracking`。
6. **日志表不参与业务事务**：独立落库，绝不影响业务请求结果。

## 五、改动清单预估

| 层 | 文件 | 性质 |
|---|---|---|
| 后端-模型 | `Models/ActivityLog.cs`、`Models/ActivityEnums.cs`（新） | 新增 |
| 后端-数据 | `AppDbContext` 注册 DbSet + 索引配置 | 小改 |
| 后端-服务 | `Services/ActivityLogger.cs`（Channel + BackgroundService）（新） | 新增 |
| 后端-过滤 | `Infrastructure/ActivityLogFilter.cs`（含路由映射表）（新）+ `Program.cs` 注册 filter 与 ForwardedHeaders | 新增+小改 |
| 后端-接口 | `Controllers/Admin/AdminActivitiesController.cs`（新） | 新增 |
| 后端-迁移 | `Migrations/AddActivityLogs`（生成） | 生成 |
| 前端-admin | `views/ActivityLog.vue`（新）、`router` 加路由、`AdminLayout` 侧边栏加菜单、`UserManage.vue` 加「记录」入口、`api/http.js` 不变 | 新增+小改 |

实施拆分（审批通过后按 Phase 提交）：
- **Phase 1**：后端全部（实体+队列+Filter+接口+迁移）—— 一个 commit
- **Phase 2**：管理后台日志页 + 用户管理页入口 —— 一个 commit

---

## 六、需要你确认的决策点汇总

1. 采集方式：**A 全局 Filter+映射表**（推荐）？
2. 存储：**A 单表 ActivityLogs**（推荐）？
3. 记录范围：默认清单 OK？登录失败 ⭕ 和搜索词 ⭕ 开不开？浏览去重窗口 10 分钟 OK？
4. 写入方式：**B 异步队列**（推荐）还是求简单用 A 同步？
5. 保留策略：90 天自动清理（A）还是永久保留（B）？
6. 删除用户后日志保留 + 冗余 UserName 快照列（推荐）？
7. 前端入口：新增独立「操作日志」页 + 用户管理页「记录」按钮跳转，OK？
