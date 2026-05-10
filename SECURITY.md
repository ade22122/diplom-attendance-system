# Безопасность проекта

## Что уже включено

- Запросы к базе выполняются через Django ORM, без ручного `cursor.execute()` и без сборки SQL-строк из пользовательского ввода.
- Включены CSRF-защита, экранирование шаблонов Django и защита от clickjacking.
- Добавлены security headers:
  - `Content-Security-Policy`;
  - `Permissions-Policy`;
  - `X-Frame-Options`;
  - `X-Content-Type-Options`;
  - `Referrer-Policy`.
- Cookie настроены как `HttpOnly` и `SameSite=Lax`.
- Вход ограничен: после 5 неверных попыток для одного логина/IP действует блокировка на 10 минут.
- Роли синхронизируются с группами Django: администраторы, преподаватели, студенты.

## Настройки для локального запуска

Для локальной разработки можно оставить:

```env
DEBUG=True
SESSION_COOKIE_SECURE=False
CSRF_COOKIE_SECURE=False
SECURE_SSL_REDIRECT=False
SECURE_HSTS_SECONDS=0
```

## Настройки для публикации сайта

Перед размещением на сервере:

```env
DEBUG=False
SECRET_KEY=сложный-длинный-секретный-ключ
ALLOWED_HOSTS=ваш-домен.ru,www.ваш-домен.ru
CSRF_TRUSTED_ORIGINS=https://ваш-домен.ru,https://www.ваш-домен.ru

SESSION_COOKIE_SECURE=True
CSRF_COOKIE_SECURE=True
SECURE_SSL_REDIRECT=True
SECURE_HSTS_SECONDS=31536000
SECURE_HSTS_INCLUDE_SUBDOMAINS=True
SECURE_HSTS_PRELOAD=True
```

PostgreSQL должен быть закрыт от внешнего интернета. Порт `5432` лучше оставить доступным только с сервера приложения или из локальной сети.

## Правила для кода

- Не использовать `raw()`, `cursor.execute()` и ручную склейку SQL-строк с данными пользователя.
- Не выключать `csrf_token` в формах.
- Не ставить `ALLOWED_HOSTS=*`.
- Не хранить реальные пароли в репозитории.
- Не показывать `.env` и резервные копии базы через веб-сервер.
