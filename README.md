# DaLat Audio Guide – Backend (C#, .NET 10)

Kiến trúc 3 lớp: `DaLat.DAL` (EF Core + Pomelo MySQL) → `DaLat.BLL` (nghiệp vụ) → `DaLat.API` (ASP.NET Core).

## Chạy
1. `cp .env.example .env` rồi sửa mật khẩu và JWT_KEY.
2. (Dịch tự động) `./scripts/start-ollama.ps1 -DockerAccess` ở terminal riêng.
3. `docker compose up -d --build` (thêm `--profile tunnel` để lấy link Cloudflare).
4. API: http://localhost:8080 · phpMyAdmin: http://localhost:8081

## Test
`dotnet test`

## Endpoint chính
- Công khai: `GET /api/languages`, `GET /api/places?lang=`, `GET /api/places/nearby?lat&lng&lang`, `POST /api/playlogs`, `POST /api/visits/ping`
- Admin (JWT): `POST /api/admin/auth/login`, `/api/admin/places` (CRUD, `PUT {id}/translations`, `POST auto-translate`), `/api/admin/languages`, `/api/admin/uploads`, `GET /api/admin/visitors/online`, `GET /api/admin/stats/visits|plays`

Giao diện web (HTML/JS) đặt trong `DaLat.API/wwwroot` (chưa có trong gói này).
