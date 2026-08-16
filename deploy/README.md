# Notes 笔记系统 · 阿里云部署指南（无 Docker · Alibaba Cloud Linux 4 / RHEL 9）

> 针对你选的环境：**Alibaba Cloud Linux 4 + root 用户登录 + 公网 IP 直接访问 HTTP + 服务器已安装 MySQL 8**。
> 部署后访问：`http://<阿里云公网IP>`。
> 全程**不使用 Docker**，采用业界主流组合：**ASP.NET Core Runtime 8 + systemd 守护 + Nginx 反向代理 + MySQL 8**。

---

## 一、部署架构总览

```
浏览器 http://<公网IP>/
        │
        ▼
   ┌──────────────┐   80 (Nginx)
   │    Nginx     │─────────┐
   └──────────────┘         │
     │            │         │
     ▼静态        ▼/api      │ 127.0.0.1:5000 仅本机回环
 /usr/share/    反代到 ─────┘
 nginx/notes/   ASP.NET Core
 (前端 dist)    Kestrel
                     │
                     ▼ 127.0.0.1:3306
                   MySQL 8 (notes_db)
```

- 前端：Vite build 后的纯静态文件放在 Nginx 目录，直接对外提供。
- 后端：ASP.NET Core 只监听 **127.0.0.1:5000**（不直接对外），由 Nginx 反代所有 `/api/*`。
- 数据库：只有本机 root/notes 账号能连 MySQL，更安全。
- 进程守护：`systemd` 重启 ASP.NET Core 进程（崩溃、服务器重启均自动恢复）。
- 阿里云安全组：只需放行 **TCP 80**；22 建议仅对自己的 IP 白名单；3306、5000 不需要对公网开放。

---

## 二、第一阶段 · 服务器初始化（只执行一次）

**本地 Windows 打开 PowerShell，SSH 连进去：**
```powershell
ssh -p 22 root@<你的公网IP>
```

进入后，把本仓库 `deploy/01-init-server.sh` 传上去执行（或者直接复制下面等价命令执行）。

### 方法 A：用脚本（推荐）

先上传脚本（另开一个本地 PowerShell）：
```powershell
scp -P 22 deploy\01-init-server.sh root@<公网IP>:/root/
```
回到服务器终端：
```bash
chmod +x /root/01-init-server.sh
bash /root/01-init-server.sh
```
它会依次：
1. `dnf upgrade` 系统；
2. `dnf install nginx`，开机自启；`firewalld` 若在运行则放行 80/443；
3. 接入微软 RPM 源，`dnf install aspnetcore-runtime-8.0`；
4. 向你询问 MySQL root 密码 → 创建数据库 `notes_db`、专用账号 `notes`，并保存密码到 `/root/.notes.env`；
5. 确认 `nginx` 用户存在（装 Nginx 时自动创建），并建立 `/opt/notes/backend`、`/usr/share/nginx/notes` 等目录。

### 方法 B：一行行手工敲（嫌脚本啰嗦时）

```bash
# 1) 升级系统 + 装 Nginx
dnf -y upgrade --allowerasing
dnf -y install nginx
systemctl enable --now nginx
# 放行端口（如果 firewalld 运行中）
systemctl is-active --quiet firewalld && {
  firewall-cmd --permanent --add-service=http
  firewall-cmd --permanent --add-service=https
  firewall-cmd --reload
}

# 2) 装 ASP.NET Core 8 Runtime
rpm --import https://packages.microsoft.com/keys/microsoft.asc
curl -sSLo /etc/yum.repos.d/microsoft-prod.repo \
  https://packages.microsoft.com/config/rhel/9/prod.repo
dnf -y makecache
dnf -y install aspnetcore-runtime-8.0
dotnet --list-runtimes   # 应看到 Microsoft.AspNetCore.App 8.*

# 3) 创建 MySQL 数据库与账号（用你现有的 MySQL root 密码）
mysql -uroot -p   # 输入密码后进入
# 在 MySQL 内执行：
#   CREATE DATABASE notes_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
#   CREATE USER 'notes'@'localhost' IDENTIFIED BY '这里改个强密码';
#   GRANT ALL PRIVILEGES ON notes_db.* TO 'notes'@'localhost';
#   FLUSH PRIVILEGES;
#   \q

# 4) 建目录（nginx 用户随 Nginx 包自动创建，无需手工建）
id -u nginx >/dev/null 2>&1 || useradd -r -s /sbin/nologin nginx
mkdir -p /opt/notes/backend /usr/share/nginx/notes /var/log/notes-api
chown -R nginx:nginx /opt/notes /var/log/notes-api
```

### 阿里云控制台别忘开安全组！
登录阿里云 → ECS 实例 → 安全组 → 入方向，至少放行：
| 协议 | 端口 | 源 | 说明 |
|---|---|---|---|
| TCP | 22  | 你的办公公网 IP（别填 0.0.0.0/0） | SSH |
| TCP | 80  | 0.0.0.0/0 | 网站 HTTP（必须） |

3306/5000 严禁对外放行，只用本地回环。

---

## 三、第二阶段 · 本地构建并上传（每次发版本都做）

前提：**本地 Windows** 已装好：
- [.NET SDK 8.0](https://dotnet.microsoft.com/zh-cn/download)（发布时要 SDK，服务器只装 Runtime）
- [Node.js ≥ 18](https://nodejs.org/zh-cn)
- Win10/11 自带的 `ssh`、`scp`（在 PowerShell 里打 `ssh -V` 能看到版本号即可）

### 一键上传脚本：

在项目根目录 `Notes\` 开一个 PowerShell：
```powershell
powershell -ExecutionPolicy Bypass -File deploy\deploy.ps1 -ServerIP <你的公网IP>
# 例：powershell -ExecutionPolicy Bypass -File deploy\deploy.ps1 -ServerIP 120.xx.xx.xx
```

脚本会做：**`dotnet publish linux-x64` → `npm run build` → scp 上传到服务器的 `/opt/notes/_deploy_tmp/` 和 `/opt/notes/backend/`**。

> 若 SSH 首次连接提示 `Are you sure you want to continue connecting?` 输入 `yes` 回车。
> 若使用密钥登录：设置系统环境变量 `GIT_SSH_COMMAND=ssh -i C:\Users\你\.ssh\id_rsa` 后再执行即可。

---

## 四、第三阶段 · 服务器端落地 + 启动

回到服务器终端执行：

```bash
bash /opt/notes/_deploy_tmp/02-deploy-app.sh
```

脚本交互过程：
1. 问你 **MySQL 账号 `notes` 的密码**（就是第一阶段你设置的那个强密码，若你执行了 01-init 会自动读 `/root/.notes.env`）。
2. 首次部署自动生成随机 64 字节 **JWT Secret Key** 写入 `/opt/notes/backend/appsettings.Production.json`。
3. 停止旧服务 → 赋权 `nginx` → 复制前端 dist 到 Nginx 目录。
4. 安装 `notes-api.service`（systemd）、`nginx-notes.conf`（Nginx）。
5. `systemctl start notes-api`：**ASP.NET Core 启动时会自动执行 EF Core Migrations**（Program.cs 里已写好 `db.Database.Migrate()`），`notes_db` 会自动建表，包括本次新增的 AspNetUsers.AvatarUrl 列。

执行完，你会看到部署完成提示。再跑两个验证：

```bash
# 1) API 进程是否健康（鉴权端点应返回 401，这是正常）
curl -i http://127.0.0.1:5000/api/auth/me
# 期望：HTTP/1.1 401 Unauthorized，说明后端活着、JWT 中间件正常

# 2) Nginx 能反代
curl -i http://127.0.0.1/api/auth/me
# 期望同上

# 3) 前端静态文件
curl -I http://127.0.0.1/
# 期望：HTTP/1.1 200 OK，Content-Type: text/html
```

然后浏览器打开 `http://<公网IP>`，看到登录页即为成功！**先去「注册」创建第一个账号，就可以使用啦。**

---

## 五、日常运维速查

| 需求 | 命令 |
|---|---|
| 查看 API 实时日志 | `journalctl -u notes-api -f` |
| 查看最近 100 行 API 日志 | `journalctl -u notes-api -n 100 --no-pager` |
| 重启 API | `systemctl restart notes-api` |
| 停止 API | `systemctl stop notes-api` |
| API 当前状态 | `systemctl status notes-api` |
| 重载 Nginx 配置 | `nginx -t && systemctl reload nginx` |
| Nginx 访问/错误日志 | `tail -f /var/log/nginx/access.log` / `tail -f /var/log/nginx/error.log` |
| 附件/头像文件存放路径 | `/opt/notes/backend/Uploads/` |
| 修改数据库连接/JWT 密钥 | 编辑 `/opt/notes/backend/appsettings.Production.json` → `systemctl restart notes-api` |
| 修改后再次发布新版本 | 本地再跑一次 `deploy.ps1` → 服务器再跑 `02-deploy-app.sh` |

---

## 六、常见问题

**Q1. curl 后端 401，但前端打开注册后点「注册」报网络错误？**
检查：1) 阿里云安全组 TCP 80 是否放行；2) Nginx 是否在跑 `systemctl status nginx`；3) `nginx -t` 配置检查；4) API 是否挂了 `systemctl status notes-api`。

**Q2. 页面能打开但前端 404 刷新白屏？**
这是 SPA fallback 没生效。确认 `/etc/nginx/conf.d/notes.conf` 中的 `location / { try_files $uri $uri/ /index.html; }`，然后 `nginx -t && systemctl reload nginx`。

**Q3. 中文搜索不到内容？**
迁移建表已用 `utf8mb4_0900_ai_ci`，后端搜索时会通过 `EF.Functions.Collate(..., "utf8mb4_general_ci")` 强制兼容中英文。如仍异常请 `journalctl -u notes-api` 看具体 SQL 错误。

**Q4. 图片/头像加载 401？**
前端 Settings 和 MarkdownBody 已自动把访问图片的 URL 拼接 `?access_token=xxx`。如果你发现某张图片没有带上 → 请确认该 URL 是不是后端返回的 `/api/attachments/{id}` 或 `/api/auth/avatar/{name}` 路径，JWT Bearer 的 OnMessageReceived 会识别 query 参数里的 token。

**Q5. 以后想绑定域名 + 开启 HTTPS？**
把 A 记录指向公网 IP（需要备案），然后 `dnf -y install certbot python3-certbot-nginx`，再 `certbot --nginx -d 你的域名` 一键自动配证书和 301 跳转。本项目 Nginx 配置的 server_name 是 `_`（默认接管 80），Certbot 一般能正确识别。

---

## 七、本次新增的文件清单（都在仓库 deploy/ 目录）

| 文件 | 用途 |
|---|---|
| [01-init-server.sh](file:///d:/CSharp/Notes/deploy/01-init-server.sh) | 新服务器初始化：装 Nginx / ASP.NET Runtime / 建库建账号 / 建目录 |
| [02-deploy-app.sh](file:///d:/CSharp/Notes/deploy/02-deploy-app.sh) | 每版本部署：填密码、生成 JWT Key、停服务、赋权、systemd + Nginx 落盘、启动、迁移 |
| [deploy.ps1](file:///d:/CSharp/Notes/deploy/deploy.ps1) | Windows 本地：publish 后端 + build 前端 + scp 全量上传 |
| [notes-api.service](file:///d:/CSharp/Notes/deploy/notes-api.service) | systemd 服务单元，守护 ASP.NET Core 进程、崩了自动重启 |
| [nginx-notes.conf](file:///d:/CSharp/Notes/deploy/nginx-notes.conf) | Nginx 反代配置：前端静态 / api 反代 / SPA fallback / 上传 20MB 限制 / 静态缓存 |
| [appsettings.Production.json](file:///d:/CSharp/Notes/deploy/appsettings.Production.json) | 生产配置模板，`02-deploy-app.sh` 会把密码和 JWT Key 替换进去 |

完成部署后，建议把 `deploy/` 目录加入 Git 提交（因为里面是部署文档和配置，不属于任何忽略规则）。
