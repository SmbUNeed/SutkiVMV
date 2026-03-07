# Инструкция по применению миграции

## Новые сущности в базе данных

Добавлены: таблица `Users`, поле `Icon` в `Amenities`, поле `UserId` в `Clients`.

## Команды

```bash
cd API/SutkiVMVCS.Api

# Добавить миграцию (один раз)
dotnet ef migrations add AddUsersAndAmenitieIcon

# Применить к БД
dotnet ef database update
```

## Что изменится в БД

1. Создастся таблица `Users` (Id, Email unique, PasswordHash, Role, CreatedAt)
2. В таблицу `Amenities` добавится колонка `Icon` (VARCHAR, default 'fa-check')
3. В таблицу `Clients` добавится колонка `UserId` (INT nullable, FK → Users.Id)

## Первый запуск — заполните удобства

Через Admin Panel → вкладка "Добавить квартиру" → блок "Удобства" → "Добавить новое удобство".

Или напрямую через Swagger (POST /api/amenities):
```json
{ "name": "Wi-Fi", "icon": "fa-wifi" }
{ "name": "Кондиционер", "icon": "fa-snowflake" }
{ "name": "Парковка", "icon": "fa-car" }
{ "name": "Телевизор", "icon": "fa-tv" }
{ "name": "Кухня", "icon": "fa-utensils" }
```

## JWT ключ

В `appsettings.json` замените ключ на свой (минимум 32 символа):
```json
"Jwt": { "Key": "ВАШ_СЕКРЕТНЫЙ_КЛЮЧ_МИНИМУМ_32_СИМВОЛА" }
```
