#!/bin/bash
set -e

echo "⬇️  Получаем изменения из GitHub..."
cd /var/www/sutki
git pull origin Temporary

echo "⏹️  Останавливаем сервис..."
systemctl stop sutki

echo "🔨 Собираем API..."
dotnet publish backend/API/SutkiVMVCS.Api/SutkiVMVCS.csproj \
  -c Release \
  -o /var/www/sutki/publish \
  --self-contained true \
  -r linux-x64

echo "🔑 Права на файлы..."
chmod +x /var/www/sutki/publish/SutkiVMVCS
chown -R www-data:www-data /var/www/sutki/publish

echo "🗄️  Применяем миграции..."
CONN=$(grep "^ConnectionStrings__DefaultConnection=" /etc/sutki/env | cut -d'=' -f2-)
cd /var/www/sutki/backend/API/SutkiVMVCS.Api
dotnet ef database update --connection "$CONN"
cd /var/www/sutki

echo "▶️  Запускаем сервис..."
systemctl start sutki

echo "✅ Готово!"
