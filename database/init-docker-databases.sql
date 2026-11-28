-- Tự động khởi tạo 9 database cho 9 Microservices khi container Postgres chạy lần đầu
SELECT 'CREATE DATABASE pm_user' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_user')\gexec
SELECT 'CREATE DATABASE pm_vehicle' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_vehicle')\gexec
SELECT 'CREATE DATABASE pm_parking' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_parking')\gexec
SELECT 'CREATE DATABASE pm_booking' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_booking')\gexec
SELECT 'CREATE DATABASE pm_payment' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_payment')\gexec
SELECT 'CREATE DATABASE pm_notification' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_notification')\gexec
SELECT 'CREATE DATABASE pm_gate' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_gate')\gexec
SELECT 'CREATE DATABASE pm_admin' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_admin')\gexec
SELECT 'CREATE DATABASE pm_support' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pm_support')\gexec
