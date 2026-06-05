# Публикация сайта в интернете

Проект подготовлен для публикации на Render: Django запускается через Gunicorn, PostgreSQL берется из `DATABASE_URL`, статические файлы собираются командой `collectstatic`, а секреты задаются через переменные окружения.

## Быстрый деплой на Render

1. Залейте актуальный код на GitHub.
2. Откройте Render: https://render.com/
3. Выберите `Blueprints` -> `New Blueprint Instance`.
4. Подключите репозиторий `ade22122/diplom-attendance-system`.
5. Render прочитает `render.yaml`, создаст Web Service и PostgreSQL.
6. После успешной сборки сайт будет доступен по адресу вида:

```text
https://diplom-attendance-system.onrender.com/
```

## Что делает Render

- `buildCommand`: `bash build.sh`
- `startCommand`: `python -m gunicorn config.wsgi:application --bind 0.0.0.0:$PORT`
- база данных: Render PostgreSQL через `DATABASE_URL`
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
