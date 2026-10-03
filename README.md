# ParkingManagement – Smart Parking Marketplace (Microservice + Clean Architecture)

.NET 10 · ASP.NET Core Web API · EF Core + LINQ · SQL Server · YARP Gateway · xUnit

## 1. Cấu trúc

```
ParkingManagement/
├── ParkingManagement.slnx
├── Directory.Build.props              ← net10.0, Nullable, ImplicitUsings cho mọi project
├── run-all.ps1                        ← chạy 9 service + Gateway
│
├── BuildingBlocks/                    ← code dùng chung, KHÔNG chứa nghiệp vụ của service nào
│   ├── ParkingManagement.SharedKernel     BaseEntity, enum, exception, PlateNormalizer, hợp đồng sự kiện, DemoIds
│   └── ParkingManagement.ServiceDefaults  DbContext gốc, middleware lỗi, khởi tạo DB, OpenAPI, /health, CORS
│
├── Gateway/
│   └── ParkingManagement.Gateway      ← YARP, cổng vào duy nhất http://localhost:5000
│
├── Services/
│   └── <Tên>Service/
│       ├── <Tên>Service.API              Presentation: Controller, Program.cs, appsettings.json
│       ├── <Tên>Service.Application      Use case, DTO, interface (port)
│       ├── <Tên>Service.Domain           Entity, quy tắc nghiệp vụ thuần
│       ├── <Tên>Service.Infrastructure   DbContext RIÊNG, migration, repository/query (LINQ), seed
│       └── <Tên>Service.Test             Unit test Domain + Application (không cần DB)
│
├── Tests/
│   └── ParkingManagement.IntegrationTests  ← chạy service thật trong bộ nhớ + SQL Server
├── database/                          ← script SQL từng database + tài liệu bảng
└── Frontend/                          ← chỗ đặt app React (chưa tạo)
```

**Quy tắc phụ thuộc trong mỗi service:**

```
API ──► Application ──► Domain ──► SharedKernel
 │           ▲
 └──► Infrastructure (cài đặt interface của Application) ──► ServiceDefaults
```

- Domain không biết EF Core, HTTP hay service khác.
- Controller chỉ gọi Use Case, không gọi DbContext.
- **Service không tham chiếu project của service khác.** Muốn dữ liệu service khác thì gọi API của nó (qua Gateway) hoặc nghe sự kiện trong `SharedKernel/Contracts/IntegrationEvents.cs`.

## 2. 9 service

| Service | Phụ trách | Port | Database | API mẫu (gọi qua Gateway :5000) |
|---|---|---|---|---|
| UserService | TV1 | 5101 | PM_UserDb | `GET /api/users/5`, `GET /api/users?role=Driver` |
| VehicleService | TV2 | 5102 | PM_VehicleDb | `GET /api/vehicles?userId=5`, `GET /api/vehicles/by-plate/51F-123.45` |
| ParkingService | TV5 | 5103 | PM_ParkingDb | `GET /api/parking-lots/1`, `GET /api/parking-lots/search?lat=10.777&lng=106.701&radiusKm=5` |
| BookingService | TV3 | 5104 | PM_BookingDb | `GET /api/bookings/BK-0002`, `GET /api/bookings/BK-0002/cancellation-preview` |
| PaymentService | TV4 | 5105 | PM_PaymentDb | `GET /api/pricing/quote?parkingLotId=1&vehicleType=Sedan&startAtUtc=...&endAtUtc=...`, `GET /api/payments?bookingId=2` |
| NotificationService | TV6 | 5106 | PM_NotificationDb | `GET /api/notifications?userId=5` |
| GateService | TV7 | 5107 | PM_GateDb | `GET /api/parking-sessions?parkingLotId=2`, `GET /api/parking-sessions/lookup?parkingLotId=2&plate=51A-999.99` |
| AdminService | TV8 | 5108 | PM_AdminDb | `GET /api/admin/settings` |
| SupportService | TV9 | 5109 | PM_SupportDb | `GET /api/complaints?ownerProfileId=1`, `GET /api/reviews?parkingLotId=1` |

Mỗi service còn có `/health` và `/openapi/v1.json`. Gateway có `GET /health/services` để xem service nào đang chạy.

## 3. Chạy

Yêu cầu: .NET SDK 10, SQL Server (instance mặc định `.`). Máy dùng SQL Express thì đổi `Server=.` thành
`Server=.\\SQLEXPRESS` trong `appsettings.json` của 9 service.

```powershell
cd ParkingManagement
.\run-all.ps1
```

Script mở 10 cửa sổ (9 service + Gateway). Lần chạy đầu mỗi service tự tạo database của mình và nạp dữ liệu demo
(mật khẩu chung `Demo@123`, danh sách tài khoản trong `database/README.md`). Sau khoảng 20 giây, mở
http://localhost:5000/health/services – cả 9 service phải là `Healthy`.

Chỉ cần làm 1 service: chạy riêng project đó (`dotnet run --project Services/BookingService/BookingService.API`)
hoặc chọn nó làm Startup Project trong Visual Studio. Service khác không chạy cũng không sao vì không có phụ thuộc lúc khởi động.

## 4. Test

```powershell
dotnet test ParkingManagement.slnx
```

- 49 unit test trong 9 project `*.Test`, bám theo Test Plan v3: TC-BOOK-07/08 (mốc hủy 59/60/61 phút), TC-PARK-07 (ân hạn 15 phút),
  TC-SEARCH-02 (chặn xe cao), TC-REG-04/05 (biển số)...
- 5 integration test (ParkingService, BookingService) cần SQL Server đang chạy.

## 5. Quy ước làm việc

- Mỗi người chỉ sửa trong `Services/<service của mình>`. Sửa `BuildingBlocks` phải báo cả nhóm vì ảnh hưởng 9 service.
- Thêm bảng / cột: sửa Entity (Domain) + Configuration (Infrastructure), rồi tạo migration cho đúng service:
  ```powershell
  dotnet ef migrations add <TenMigration> --project Services/BookingService/BookingService.Infrastructure --startup-project Services/BookingService/BookingService.API --output-dir Persistence/Migrations
  ```
- Truy vấn dữ liệu bằng LINQ (EF Core), `AsNoTracking()` + `Select` sang DTO, phân trang bằng `Skip/Take`.
- Mẫu cho 1 use case mới: xem `Services/BookingService` (Domain rule → Use case → Query LINQ → Controller → Unit test).
