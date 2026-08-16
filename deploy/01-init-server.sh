#!/usr/bin/env bash
# ============================================================================
# Notes 笔记系统 - 服务器初始化 & 环境安装脚本（针对 Alibaba Cloud Linux 4 / RHEL 9 / CentOS Stream 9）
# 使用 root 用户在 /root 下直接执行即可：   bash 01-init-server.sh
# ============================================================================
set -euo pipefail

echo -e "\e[32m==== 1. 更新系统包 ====\e[0m"
dnf -y upgrade --allowerasing
dnf -y install epel-release || true

echo -e "\e[32m==== 2. 安装 Nginx ====\e[0m"
dnf -y install nginx
systemctl enable --now nginx
# 如开了 firewalld 则放行 80/443
if command -v firewall-cmd >/dev/null 2>&1; then
  if systemctl is-active --quiet firewalld; then
    firewall-cmd --permanent --add-service=http
    firewall-cmd --permanent --add-service=https
    firewall-cmd --reload || true
  fi
fi

echo -e "\e[32m==== 3. 安装 ASP.NET Core 8 Runtime ====\e[0m"
# 微软官方 RPM 仓库
rpm -q packages-microsoft-com-prod >/dev/null 2>&1 || {
  dnf -y install curl
  rpm --import https://packages.microsoft.com/keys/microsoft.asc
  curl -sSLo /etc/yum.repos.d/microsoft-prod.repo https://packages.microsoft.com/config/rhel/9/prod.repo
  dnf -y makecache
}
dnf -y install aspnetcore-runtime-8.0
dotnet --list-runtimes

echo -e "\e[32m==== 4. 确认 MySQL 可用并创建数据库与专用账号 ====\e[0m"
if command -v mysql >/dev/null 2>&1; then
  echo "检测到本地 MySQL，准备创建数据库 notes_db 与账号 notes。"
  read -r -s -p "请输入 MySQL root 密码（如果本地免密登录直接回车）: " MYSQL_ROOT_PW
  echo
  if [ -n "$MYSQL_ROOT_PW" ]; then
    MYSQL_CMD="mysql -uroot -p${MYSQL_ROOT_PW}"
  else
    MYSQL_CMD="mysql -uroot"
  fi

  read -r -s -p "请为新的 MySQL 专用账号 notes 设置一个强密码: " NOTES_DB_PW
  echo
  [ -z "$NOTES_DB_PW" ] && { echo "密码不能为空"; exit 1; }

  $MYSQL_CMD <<EOSQL
CREATE DATABASE IF NOT EXISTS notes_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'notes'@'localhost' IDENTIFIED BY '${NOTES_DB_PW}';
GRANT ALL PRIVILEGES ON notes_db.* TO 'notes'@'localhost';
FLUSH PRIVILEGES;
EOSQL
  echo "数据库 notes_db 和账号 notes 创建完成。请记住 NOTES_DB_PW = ${NOTES_DB_PW}"
  echo "将该密码写入 /root/.notes.env 备用。"
  echo "NOTES_DB_PW=${NOTES_DB_PW}" > /root/.notes.env
else
  echo -e "\e[33m未检测到 mysql 命令，请先手动安装 MySQL 8，或确认服务器确实已经装有 MySQL。\e[0m"
fi

echo -e "\e[32m==== 5. 创建运行用户和目录 ====\e[0m"
id -u www-data >/dev/null 2>&1 || useradd -r -s /sbin/nologin www-data
mkdir -p /opt/notes/backend
mkdir -p /usr/share/nginx/notes
mkdir -p /var/log/notes-api
chown -R www-data:www-data /opt/notes /var/log/notes-api

echo -e "\e[32m==== 环境初始化完成。接下来请执行:\e[0m"
echo "  A. 在你 Windows 本地运行 deploy\deploy.ps1 上传构建产物"
echo "  B. 或按照 deploy/README.md 手工方式逐个上传"
