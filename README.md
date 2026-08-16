# Notes 笔记系统

一个移动端优先、支持桌面端适配的笔记应用，支持 Markdown 编辑预览、LaTeX 数学公式、笔记版本历史、时间线、自定义背景色、置顶等功能。

## 技术栈

| 层 | 技术 |
|---|---|
| 后端 | ASP.NET Core 8 Web API、EF Core、ASP.NET Identity、JWT 认证 |
| 数据库 | MySQL 8（Pomelo.EntityFrameworkCore.MySql） |
| 前端 | Vue 3、Vite、Vue Router、Pinia、Vant 4 |
| 渲染 | Marked + DOMPurify（Markdown）、KaTeX（数学公式） |
| 部署 | Nginx 反向代理 + systemd 守护进程 |

## 项目结构

```
Notes/
├── backend/Notes.Api/          # ASP.NET Core 后端
│   ├── Controllers/            # API 控制器（Auth、Notes、Categories、Tags、Attachments）
│   ├── DTOs/                   # 数据传输对象
│   ├── Models/                 # 实体模型（Note、ApplicationUser、NoteVersion 等）
│   ├── Data/                   # AppDbContext + EF Core 迁移
│   ├── Services/               # 业务服务（TokenService、FileStorageService）
│   ├── Infrastructure/         # 基础设施（JSON DateTime UTC 转换器）
│   └── Program.cs              # 应用入口（DI 配置、中间件、自动迁移）
├── frontend/                   # Vue 3 前端
│   ├── src/
│   │   ├── views/              # 页面组件
│   │   ├── components/         # 通用组件（MarkdownBody、ColorPicker）
│   │   ├── composables/        # 组合式函数（useResponsive 响应式布局）
│   │   ├── stores/             # Pinia 状态管理（auth）
│   │   ├── api/                # Axios HTTP 封装
│   │   ├── utils/              # 工具函数（颜色、时间格式化）
│   │   └── router/             # Vue Router 路由配置
│   └── package.json
├── deploy/                     # 部署脚本与配置
│   ├── 01-init-server.sh       # 服务器初始化
│   ├── 02-deploy-app.sh        # 应用部署
│   ├── deploy.ps1              # 本地构建上传
│   ├── notes-api.service       # systemd 服务单元
│   ├── nginx-notes.conf        # Nginx 反代配置
│   └── README.md               # 部署详细文档
└── Notes.sln                   # .NET 解决方案文件
```

## 功能列表

### 用户系统
- 注册 / 登录（JWT 认证）
- 修改昵称和头像
- 个人设置页

### 笔记管理
- 创建、编辑、删除笔记
- Markdown 编辑 + 实时预览（移动端分屏切换，桌面端左右并排）
- LaTeX 数学公式渲染（行内 `$...$`、块级 `$$...$$`）
- 图片附件上传与内嵌引用
- 笔记置顶（置顶笔记优先显示，支持在列表、详情、编辑页切换）
- 自定义笔记背景色（用户级默认色 + 笔记级独立色）
- 可自定义色板（添加 / 删除颜色，跨设备同步）

### 组织与检索
- 分类管理（笔记归类）
- 标签管理（多标签关联）
- 全文搜索（标题 + 正文，中文兼容）
- 时间线视图（按年/月/日分组，支持分类、标签、关键词、时间范围筛选）

### 版本历史
- 自动记录笔记每次修改的版本快照
- SHA-256 内容去重（内容未变不生成新版本）
- 查看任意历史版本内容
- 一键恢复到指定版本（恢复操作本身也会生成新版本）

### 界面适配
- 移动端优先设计（Vant 4 组件库）
- 桌面端响应式适配（1024px+ 双列网格、1440px+ 三列网格）
- 深色背景卡片自动切换文字与标签配色

## 本地开发

### 前置要求

- [.NET SDK 8.0](https://dotnet.microsoft.com/download)
- [Node.js >= 18](https://nodejs.org/)
- MySQL 8（本地或远程实例）

### 数据库准备

创建一个 MySQL 数据库和用户：

```sql
CREATE DATABASE notes_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'notes'@'localhost' IDENTIFIED BY '你的密码';
GRANT ALL PRIVILEGES ON notes_db.* TO 'notes'@'localhost';
FLUSH PRIVILEGES;
```

### 后端配置

在 `backend/Notes.Api/` 下创建 `appsettings.Development.json`（此文件已被 gitignore）：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=notes_db;User=notes;Password=你的密码;"
  },
  "Jwt": {
    "Key": "至少16位的随机字符串作为开发密钥",
    "Issuer": "NotesApi",
    "Audience": "NotesClient"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

启动后端：

```bash
cd backend/Notes.Api
dotnet restore
dotnet run
```

后端启动在 `http://localhost:5000`，Swagger 文档在 `http://localhost:5000/swagger`（仅开发环境）。
首次启动会自动执行 EF Core 迁移建表。

### 前端配置

前端默认连接 `http://localhost:5000`，如需修改请编辑 `frontend/src/api/http.js`。

```bash
cd frontend
npm install
npm run dev
```

前端开发服务器启动在 `http://localhost:5173`。

## 部署

详细部署流程请参阅 [deploy/README.md](deploy/README.md)。

### 快速部署（阿里云 Linux）

1. **服务器初始化**（仅首次）：执行 `deploy/01-init-server.sh`
2. **本地构建上传**：运行 `deploy/deploy.ps1 -ServerIP <你的公网IP>`
3. **服务器端部署**：执行 `bash /opt/notes/_deploy_tmp/02-deploy-app.sh`

部署架构：

```
浏览器 → Nginx (80) → 前端静态文件 + 反代 /api/* → ASP.NET Core (5000) → MySQL (3306)
```

## 关键设计说明

- **自动迁移**：`Program.cs` 中 `db.Database.Migrate()` 在所有环境执行，无需手动运行迁移命令
- **时间处理**：自定义 `JsonDateTimeUtcConverter` 确保 DateTime 序列化时带 `Z` 后缀（UTC），前端正确转换为本地时间
- **图片鉴权**：JWT Bearer 支持从 query 参数 `access_token` 读取令牌，解决 `<img>` 标签无法设置请求头的问题
- **中文搜索**：通过 `EF.Functions.Collate(..., "utf8mb4_general_ci")` 实现中英文兼容搜索
- **版本去重**：笔记保存时计算内容快照的 SHA-256 Hash，与上一个版本相同则不生成新版本

## License

MIT
