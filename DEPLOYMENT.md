# Публикация сайта в интернете

Проект подготовлен для публикации без платной базы Render: Django запускается на бесплатном Render Web Service, а PostgreSQL подключается отдельно через бесплатный тариф Neon или Supabase. База берется из переменной `DATABASE_URL`, статические файлы собираются командой `collectstatic`, а секреты задаются через переменные окружения.

## Бесплатный вариант

Схема:

```text
Render Free Web Service + Neon Free PostgreSQL
```

или:

```text
Render Free Web Service + Supabase Free PostgreSQL
```

Render Postgres лучше не выбирать для бесплатного дипломного демо, потому что бесплатные базы Render истекают через 30 дней.

## 1. Создайте бесплатную PostgreSQL-базу

### Вариант A: Neon

1. Откройте https://neon.com/
2. Создайте бесплатный проект.
3. В разделе `Connection string` скопируйте строку подключения PostgreSQL.
4. Формат будет примерно такой:

```text
postgresql://USER:PASSWORD@HOST.neon.tech/DBNAME?sslmode=require
```

### Вариант B: Supabase

1. Откройте https://supabase.com/
2. Создайте бесплатный проект.
3. Откройте `Project Settings` -> `Database`.
4. Скопируйте connection string для PostgreSQL.

## 2. Опубликуйте сайт на Render

1. Залейте актуальный код на GitHub.
2. Откройте Render: https://render.com/
3. Выберите `Blueprints` -> `New Blueprint Instance`.
4. Подключите репозиторий `ade22122/diplom-attendance-system`.
5. Render прочитает `render.yaml` и создаст только Web Service.
6. При создании Render попросит значение `DATABASE_URL`.
7. Вставьте туда connection string из Neon или Supabase.
8. После успешной сборки сайт будет доступен по адресу вида:

```text
https://diplom-attendance-system.onrender.com/
```

## Что делает Render

- `buildCommand`: `bash build.sh`
- `startCommand`: `python -m gunicorn config.wsgi:application --bind 0.0.0.0:$PORT`
- база данных: внешний PostgreSQL через `DATABASE_URL`
- статика: WhiteNoise + `python manage.py collectstatic --no-input`
- миграции: `python manage.py migrate`

## Важное про аватарки

Загруженные пользователями аватарки хранятся в `media/`. На бесплатных web-сервисах Render файловая система временная, поэтому загруженные файлы могут пропасть после redeploy/restart. Для постоянного хранения аватарок нужен платный Persistent Disk на Render или облачное хранилище вроде S3.

Если подключаете Render Persistent Disk, используйте mount path:

```text
/opt/render/project/src/media
```

и задайте переменную окружения:

```text
MEDIA_ROOT=/opt/render/project/src/media
```

## Команды для локальной проверки

```powershell
.\.venv\Scripts\python.exe manage.py check --deploy
.\.venv\Scripts\python.exe manage.py collectstatic --no-input
.\.venv\Scripts\python.exe manage.py test
```
