#!/usr/bin/env bash
# ============================================================================
# Notes 笔记系统 - 服务器端部署脚本（上传完文件后，在服务器执行它）
# 使用：bash 02-deploy-app.sh <SERVER_PUBLIC_IP>
#   SERVER_PUBLIC_IP 仅用于生成 JWT 示例密钥提示，不影响运行。
# ============================================================================
set -euo pipefail

echo -e "\e[32m==== 0. 检查目录 ====\e[0m"
[ -d /opt/notes/backend ] || { echo "/opt/notes/backend 不存在，请先执行 01-init-server.sh"; exit 1; }
[ -d /usr/share/nginx/notes ] || { echo "/usr/share/nginx/notes 不存在"; exit 1; }

echo -e "\e[32m==== 1. 准备 appsettings.Production.json ====\e[0m"
APPSETTINGS=/opt/notes/backend/appsettings.Production.json
if [ -f "$APPSETTINGS" ]; then
  echo "已存在 $APPSETTINGS，保留原有配置（删除后重新部署可覆盖）。"
else
  # 从模板生成（要求用户填入密码与密钥）
  if [ -f /root/.notes.env ]; then
    # shellcheck disable=SC1091
    . /root/.notes.env
  fi

  read -r -s -p "请输入 MySQL 专用账号 notes 的密码 [从 01-init 记住的 NOTES_DB_PW]: " NOTES_DB_PW_USER
  if [ -n "$NOTES_DB_PW_USER" ]; then NOTES_DB_PW="$NOTES_DB_PW_USER"; fi
  while [ -z "${NOTES_DB_PW:-}" ]; do read -r -s -p "密码不能为空，重新输入: " NOTES_DB_PW; done
  echo

  # 管理员账号（可留空跳过种子，之后可用后台「重置密码」创建/改密）
  read -r -p "请输入管理员邮箱 [默认 admin@notes.local, 直接回车使用]: " NOTES_ADMIN_EMAIL_IN
  if [ -z "$NOTES_ADMIN_EMAIL_IN" ]; then NOTES_ADMIN_EMAIL="admin@notes.local"; else NOTES_ADMIN_EMAIL="$NOTES_ADMIN_EMAIL_IN"; fi
  read -r -s -p "请输入管理员密码 [至少 6 位，留空则不创建管理员]: " NOTES_ADMIN_PW_IN
  echo
  NOTES_ADMIN_PASSWORD="${NOTES_ADMIN_PW_IN:-}"

  # 生成随机 64 字节 JWT Key
  JWT_KEY="$(head -c 64 /dev/urandom | base64 -w0)"

  cp /opt/notes/_deploy_tmp/appsettings.Production.json "$APPSETTINGS" 2>/dev/null || {
    echo "请先上传 deploy/appsettings.Production.json 模板到 /opt/notes/_deploy_tmp/";
    exit 1;
  }
  sed -i "s|__DB_PASSWORD__|${NOTES_DB_PW}|g"       "$APPSETTINGS"
  sed -i "s|__JWT_SECRET_KEY__|${JWT_KEY}|g"        "$APPSETTINGS"
  sed -i "s|__ADMIN_EMAIL__|${NOTES_ADMIN_EMAIL}|g" "$APPSETTINGS"
  sed -i "s|__ADMIN_PASSWORD__|${NOTES_ADMIN_PASSWORD}|g" "$APPSETTINGS"
  echo "JWT Key 已随机生成并写入配置。如需备份请查看: $APPSETTINGS"
  if [ -n "$NOTES_ADMIN_PASSWORD" ]; then
    echo "管理员账号已写入配置（$NOTES_ADMIN_EMAIL），服务启动时会自动创建/授权。"
  else
    echo "未设置管理员密码，跳过管理员种子（如需创建，请重跑部署或直接编辑 $APPSETTINGS）。"
  fi
fi

echo -e "\e[32m==== 2. 停止旧服务 → 复制新文件 → 赋权 ====\e[0m"
systemctl stop notes-api 2>/dev/null || true

# backend 目录内容已在 /opt/notes/backend，只做赋权
chown -R nginx:nginx /opt/notes/backend
chmod -R u=rwX,g=rX,o=rX /opt/notes/backend
chown -R nginx:nginx /opt/notes/backend/Uploads 2>/dev/null || mkdir -p /opt/notes/backend/Uploads && chown -R nginx:nginx /opt/notes/backend/Uploads

# 前端 dist
if [ -d /opt/notes/_deploy_tmp/dist ]; then
  rm -rf /usr/share/nginx/notes/*
  cp -a /opt/notes/_deploy_tmp/dist/. /usr/share/nginx/notes/
  chown -R nginx:nginx /usr/share/nginx/notes
  chmod -R u=rwX,g=rX,o=rX /usr/share/nginx/notes
else
  echo -e "\e[33m未检测到 /opt/notes/_deploy_tmp/dist，跳过前端更新。\e[0m"
fi

# 管理后台 dist
if [ -d /opt/notes/_deploy_tmp/dist-admin ]; then
  mkdir -p /usr/share/nginx/notes-admin
  rm -rf /usr/share/nginx/notes-admin/*
  cp -a /opt/notes/_deploy_tmp/dist-admin/. /usr/share/nginx/notes-admin/
  chown -R nginx:nginx /usr/share/nginx/notes-admin
  chmod -R u=rwX,g=rX,o=rX /usr/share/nginx/notes-admin
else
  echo -e "\e[33m未检测到 /opt/notes/_deploy_tmp/dist-admin，跳过管理后台更新。\e[0m"
fi

echo -e "\e[32m==== 3. 复制 systemd 与 Nginx 配置 ====\e[0m"
if [ -f /opt/notes/_deploy_tmp/notes-api.service ]; then
  cp /opt/notes/_deploy_tmp/notes-api.service /etc/systemd/system/notes-api.service
  systemctl daemon-reload
  systemctl enable notes-api
fi
if [ -f /opt/notes/_deploy_tmp/nginx-notes.conf ]; then
  cp /opt/notes/_deploy_tmp/nginx-notes.conf /etc/nginx/conf.d/notes.conf
  # 默认移除 default.conf，避免端口冲突
  if [ -f /etc/nginx/conf.d/default.conf ]; then
    mv /etc/nginx/conf.d/default.conf /etc/nginx/conf.d/default.conf.disabled 2>/dev/null || true
  fi
  nginx -t && systemctl reload nginx
fi

echo -e "\e[32m==== 4. 启动 API 服务（自动执行 EF Core Migrations）====\e[0m"
systemctl start notes-api
sleep 3
systemctl status notes-api --no-pager || true

echo
echo -e "\e[32m==== 部署完成！健康检查 ====\e[0m"
echo "  后端：curl -s http://127.0.0.1:5000/api/auth/me  (应返回 401 Unauthorized)"
echo "  前端：浏览器打开 http://<服务器公网IP>"
echo "  日志：journalctl -u notes-api -f"
