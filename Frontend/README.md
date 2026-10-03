# Frontend – ParkingManagement.Web

Chỗ đặt ứng dụng React + TypeScript (Vite) theo bản thiết kế: 1 app, 4 khu vực `driver/`, `owner/`, `gate/`, `admin/`.

Tạo project:

```powershell
cd Frontend
npm create vite@latest ParkingManagement.Web -- --template react-ts
```

Frontend **chỉ gọi API Gateway** `http://localhost:5000`, không gọi trực tiếp port của từng service.
CORS đã mở sẵn cho `http://localhost:5173` (port mặc định của Vite) ở Gateway và cả 9 service.
