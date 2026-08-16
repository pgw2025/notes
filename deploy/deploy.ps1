# ============================================================================
# Notes 笔记系统 - Windows 本地构建 + 上传脚本 (PowerShell 5.1+)
# 功能：
#   1. 发布后端 (dotnet publish linux-x64)
#   2. 构建前端 (npm run build)
#   3. 把构建产物 + 部署配置通过 scp 上传到服务器
#   4. 提醒你下一步在服务器执行 02-deploy-app.sh
#
# 使用 (从 Notes 项目根目录运行)：
#   powershell -ExecutionPolicy Bypass -File deploy\deploy.ps1 -ServerIP 1.2.3.4 [-SshPort 22] [-SshUser root]
#
# 说明：会使用系统已有的 ssh/scp（Win10+ 自带 OpenSSH）。首次使用要求确认主机指纹。
# ============================================================================

param(
  [Parameter(Mandatory = $true)]  [string]$ServerIP,
  [Parameter(Mandatory = $false)] [int]   $SshPort = 22,
  [Parameter(Mandatory = $false)] [string]$SshUser = "root"
)
$ErrorActionPreference = "Stop"

# --------- 路径 ---------
$RepoRoot       = Split-Path -Parent $PSScriptRoot
$BackendProj    = Join-Path $RepoRoot "backend\Notes.Api\Notes.Api.csproj"
$BackendPublish = Join-Path $RepoRoot "publish\backend"
$FrontendDir    = Join-Path $RepoRoot "frontend"
$FrontendDist   = Join-Path $FrontendDir "dist"
$DeployDir      = Join-Path $RepoRoot "deploy"
$RemoteTmp      = "/opt/notes/_deploy_tmp"
$RemoteBackend  = "/opt/notes/backend"

Write-Host "==> 1. 清理旧的发布目录" -ForegroundColor Cyan
if (Test-Path $BackendPublish) { Remove-Item $BackendPublish -Recurse -Force }
New-Item -ItemType Directory -Force -Path $BackendPublish | Out-Null

Write-Host "==> 2. 发布后端 (linux-x64 / Framework-Dependent) <== 需要本机安装 .NET SDK 8" -ForegroundColor Cyan
dotnet publish $BackendProj `
  -c Release `
  -r linux-x64 `
  --self-contained false `
  -o $BackendPublish `
  /p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }
Write-Host "   后端发布目录大小：" -NoNewline
Get-ChildItem $BackendPublish -Recurse | Measure-Object -Property Length -Sum |
  ForEach-Object { Write-Host ("{0:N2} MB" -f ($_.Sum / 1MB)) }

Write-Host "==> 3. 构建前端 <== 需要本机安装 Node >=18" -ForegroundColor Cyan
Push-Location $FrontendDir
try {
  if (!(Test-Path "node_modules")) {
    npm ci
  }
  npm run build
  if ($LASTEXITCODE -ne 0) { throw "npm run build 失败" }
} finally {
  Pop-Location
}

Write-Host "==> 4. 上传到服务器 ${SshUser}@${ServerIP}:${SshPort}" -ForegroundColor Cyan
Write-Host "   4.1 准备 _deploy_tmp 目录（部署脚本依赖它）"
ssh -p $SshPort ${SshUser}@${ServerIP} "mkdir -p ${RemoteTmp} ${RemoteBackend} && rm -rf ${RemoteTmp}/*"
if ($LASTEXITCODE -ne 0) { throw "无法 SSH 到服务器，请检查 IP/端口/密码/密钥。" }

Write-Host "   4.2 上传后端发布产物 (较慢，耐心等待)"
scp -P $SshPort -r "${BackendPublish}\*" "${SshUser}@${ServerIP}:${RemoteBackend}/"
if ($LASTEXITCODE -ne 0) { throw "后端 scp 上传失败" }

Write-Host "   4.3 上传前端 dist"
scp -P $SshPort -r "${FrontendDist}" "${SshUser}@${ServerIP}:${RemoteTmp}/"
if ($LASTEXITCODE -ne 0) { throw "前端 dist scp 上传失败" }

Write-Host "   4.4 上传 deploy/ 下的模板与脚本"
scp -P $SshPort `
  "${DeployDir}\appsettings.Production.json" `
  "${DeployDir}\notes-api.service" `
  "${DeployDir}\nginx-notes.conf" `
  "${DeployDir}\02-deploy-app.sh" `
  "${SshUser}@${ServerIP}:${RemoteTmp}/"
if ($LASTEXITCODE -ne 0) { throw "部署模板 scp 上传失败" }

# 让脚本可执行
ssh -p $SshPort ${SshUser}@${ServerIP} "chmod +x ${RemoteTmp}/02-deploy-app.sh && ls -la ${RemoteTmp}"

Write-Host ""
Write-Host "==> 上传全部完成！请 SSH 登录服务器执行：" -ForegroundColor Green
Write-Host "    bash /opt/notes/_deploy_tmp/02-deploy-app.sh"
Write-Host "    （它会：生成 appsettings.Production.json / 停服务 / 赋权 / 迁移 / 启动 systemd / 重载 Nginx）"
Write-Host ""
Write-Host "如果是第一台全新服务器，先执行过 deploy/01-init-server.sh"
