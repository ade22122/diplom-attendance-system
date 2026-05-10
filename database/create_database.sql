-- База данных уже создана вручную:
-- database: diplom
-- host: localhost
-- user: postgres
-- port: 5432
-- password: задается в локальном .env и не хранится в Git

-- Этот запрос можно выполнить в pgAdmin Query Tool внутри базы diplom,
-- чтобы проверить подключение.
SELECT current_database() AS database_name, current_user AS user_name;
