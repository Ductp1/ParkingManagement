# 📚 HƯỚNG DẪN CHI TIẾT: TỪ DATABASE ĐẾN C# MODEL

## 🎯 MỤC ĐÍCH CỦA BÀI HỌC
Bạn sẽ hiểu:
1. **Tại sao** phải align entity model với database schema
2. **Cách thức** EF Core mapping hoạt động
3. **Những lỗi phổ biến** khi mismatch giữa DB và Code
4. **Cách fix** từng loại lỗi

---

## 📊 BẮT ĐẦU: TỪ DATABASE SCHEMA

### Step 1: Xem SQL Schema Ban Đầu
```sql
-- Database: PM_NotificationDb
CREATE TABLE [DeviceTokens] (
	[Id] int NOT NULL IDENTITY,
	[UserId] int NOT NULL,
	[Token] nvarchar(512) NOT NULL,
	[Platform] nvarchar(40) NOT NULL,      -- 🔴 THIS IS STRING, NOT ENUM!
	[DeviceName] nvarchar(100) NULL,
	[LastUsedAtUtc] datetime2 NOT NULL,
	[IsRevoked] bit NOT NULL,
	[CreatedAtUtc] datetime2 NOT NULL,
	[UpdatedAtUtc] datetime2 NULL,
	CONSTRAINT [PK_DeviceTokens] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationPreferences] (
	[Id] int NOT NULL IDENTITY,
	[UserId] int NOT NULL,
	[TemplateKey] nvarchar(50) NOT NULL,
	[Channel] nvarchar(40) NOT NULL,
	[IsEnabled] bit NOT NULL,
	[QuietFrom] time NULL,                -- 🔴 THIS IS SQL TIME, NOT TIMEONLY!
	[QuietTo] time NULL,                  -- 🔴 EF MAPS TIME → TimeSpan, NOT TimeOnly!
	[CreatedAtUtc] datetime2 NOT NULL,
	[UpdatedAtUtc] datetime2 NULL,
	CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([Id])
);
```

### 🔍 **3 Điểm Quan Trọng Từ Schema:**

| SQL Type | Ý nghĩa | ❌ Sai cách | ✅ Đúng cách (C#) |
|----------|---------|-----------|-----------------|
| `nvarchar(40)` | Chuỗi Variable Length | `enum DevicePlatform` | `string` |
| `time` | Chỉ giờ:phút:giây | `TimeOnly?` | `TimeSpan?` |
| `nvarchar(max)` | Text lớn | `StringBuilder` | `string?` |

---

## 🏗️ PHẦN 1: ENTITY MODELS - BRIDGE GIỮA DB VÀ CODE

### **File 1: DeviceToken.cs** (Domain Layer)

#### 📌 **Khái niệm:**
Entity model là **class/record đại diện cho một bản ghi trong database**.

#### ❌ **LỖI CŨ - Mismatch với Database:**
```csharp
using ParkingManagement.SharedKernel.Enums;

public class DeviceToken : BaseEntity
{
	public int UserId { get; set; }
	public string Token { get; set; } = string.Empty;

	// ❌ SAI: Database column là nvarchar(40), nhưng dùng ENUM
	public DevicePlatform Platform { get; set; }
	//       ↑ Đây là enum với giá trị: Desktop=0, Mobile=1, Web=2
	//         Khi save DB, EF sẽ lưu số (0/1/2) thay vì "Android"/"Web"

	public string? DeviceName { get; set; }
	public DateTime LastUsedAtUtc { get; set; }
	public bool IsRevoked { get; set; }
}
```

**Tại sao sai?**
```
Database yêu cầu: Platform = "Android", "iOS", "Web", etc.
Code dùng enum:   Platform = 0, 1, 2, 3 (số nguyên)
				  ↓
				  Mismatch! EF Core sẽ chuyển enum → số
				  Database sẽ lưu: 0, 1 thay vì "Android", "Web"
```

#### ✅ **LỒI MỚI - Khớp với Database:**
```csharp
public class DeviceToken : BaseEntity
{
	public int UserId { get; set; }
	public string Token { get; set; } = string.Empty;

	// ✅ ĐÚNG: Database column là nvarchar(40) → Dùng STRING
	public string Platform { get; set; } = string.Empty;
	//       ↑ Giờ có thể lưu: "Android", "iOS", "Web", "Desktop"
	//         Đây là convention: convention-based types (không cần enum)

	public string? DeviceName { get; set; }
	public DateTime LastUsedAtUtc { get; set; }
	public bool IsRevoked { get; set; }
}
```

---

### **File 2: NotificationPreference.cs** (Domain Layer)

#### 📌 **Khái niệm:**
`TimeSpan` vs `TimeOnly`:
- **TimeSpan**: Khoảng thời gian (có thể tính giờ:phút:giây)
- **TimeOnly**: Chỉ là thời gian trong ngày (23:30 chẳng hạn)

Trong EF Core, SQL `TIME` kiểu gì mapping sang C#?

```
SQL Server                    EF Core 10
==========================================
datetime2                  → DateTime
date                       → DateOnly
time                       → TimeSpan  ⚠️ NOT TimeOnly!
```

#### ❌ **LỖI CŨ:**
```csharp
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

public class NotificationPreference : BaseEntity
{
	public int UserId { get; set; }
	public string TemplateKey { get; set; } = string.Empty;
	public NotificationChannel Channel { get; set; }
	public bool IsEnabled { get; set; } = true;

	// ❌ SAI: Database là TIME, nhưng dùng TimeOnly
	public TimeOnly? QuietFrom { get; set; }
	//      ↑ TimeOnly là .NET 6+ type chỉ cho TIME của một ngày
	//        Nhưng EF Core 10 vận chuyển TIME → TimeSpan, không phải TimeOnly!

	public TimeOnly? QuietTo { get; set; }
}
```

**Tại sao sai?**
```
EF Core 10 Mapping:  TIME (SQL) ↔ TimeSpan (C#)
Code dùng:          TimeOnly (C#)
					↓
					Incompatible! EF không biết convert TimeSpan → TimeOnly
					Compile Error: Cannot convert TimeSpan to TimeOnly
```

#### ✅ **LỒI MỚI:**
```csharp
public class NotificationPreference : BaseEntity
{
	public int UserId { get; set; }
	public string TemplateKey { get; set; } = string.Empty;
	public NotificationChannel Channel { get; set; }
	public bool IsEnabled { get; set; } = true;

	// ✅ ĐÚNG: Database TIME → C# TimeSpan
	public TimeSpan? QuietFrom { get; set; }
	//       ↑ TimeSpan có method: TotalHours, TotalMinutes, etc.
	//         Ví dụ: new TimeSpan(23, 0, 0) = 23:00 (11 PM)

	public TimeSpan? QuietTo { get; set; }
}
```

---

## 🔐 PHẦN 2: EF CORE CONFIGURATION - MAPPING CHI TIẾT

### **File: NotificationConfiguration.cs** (Fluent API)

#### 📌 **Khái niệm:**
EF Core Fluent API cho phép **cấu hình chi tiết** cách entity map với database:

```csharp
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
	public void Configure(EntityTypeBuilder<Notification> e)
	{
		e.ToTable("Notifications");  // Tên bảng trong DB

		// Property Configuration
		e.Property(x => x.TemplateKey)
			.HasMaxLength(50)        // SQL: nvarchar(50)
			.IsRequired();           // SQL: NOT NULL

		e.Property(x => x.Title)
			.HasMaxLength(200)       // SQL: nvarchar(200)
			.IsRequired();

		e.Property(x => x.Body)
			.HasMaxLength(2000)      // SQL: nvarchar(2000)
			.IsRequired();

		e.Property(x => x.DataJson)
			.IsMaxText();            // SQL: nvarchar(max)

		e.Property(x => x.LastError)
			.HasMaxLength(1000);     // SQL: nvarchar(1000), nullable

		// Indexes - Cho query performance
		e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });
		//        ↑ INDEX cho "list notification của user"

		e.HasIndex(x => new { x.Status, x.RetryCount });
		//        ↑ INDEX cho "lấy thông báo failed để retry"
	}
}
```

**Tại sao cần configuration này?**
```
Nếu không có:
- EF sẽ guess: Column length? NOT NULL hay NULL? Index có cần không?
- Có thể generate sai schema → Mismatch với DB thực
- Query chậm nếu thiếu index

Có configuration:
- EF biết chính xác cấu trúc
- Schema luôn khớp
- Query nhanh với index
```

---

## 🌱 PHẦN 3: SEEDING DATA - DỠ LỖI TỪNG BƯỚC

### **File: NotificationDataSeeder.cs** (Infrastructure Layer)

#### 📌 **Khái niệm:**
Seeding = **Chèn dữ liệu demo vào database** khi app khởi động lần đầu.

#### ❌ **LỖI CŨ - DeviceToken Seeding:**
```csharp
private static async Task SeedExtrasAsync(NotificationDbContext db, ...)
{
	if (!await db.DeviceTokens.AnyAsync(cancellationToken))
	{
		db.DeviceTokens.AddRange(
			new DeviceToken { 
				UserId = DemoIds.Driver1User, 
				Token = "demo-fcm-token-driver1-android", 

				// ❌ SAI: DevicePlatform ENUM không tồn tại!
				Platform = DevicePlatform.Android,
				//         ↑ Code cũ lấy enum từ SharedKernel.Enums
				//           Nhưng đó là namespace lỗi - DevicePlatform không định nghĩa
				//           Hoặc nếu có, nó là số (0, 1, 2)
				//           Nhưng database cần "Android", "Web"

				DeviceName = "Samsung Galaxy A55", 
				LastUsedAtUtc = DateTime.UtcNow 
			},
			...
		);
	}
}
```

#### ✅ **LỒI MỚI - Dùng String:**
```csharp
private static async Task SeedExtrasAsync(NotificationDbContext db, ...)
{
	if (!await db.DeviceTokens.AnyAsync(cancellationToken))
	{
		db.DeviceTokens.AddRange(
			new DeviceToken { 
				UserId = DemoIds.Driver1User, 
				Token = "demo-fcm-token-driver1-android", 

				// ✅ ĐÚNG: Platform giờ là string
				Platform = "Android",  // Lưu trực tiếp vào DB dưới dạng string
				//       ↑ Không qua enum, không qua số
				//         EF Core: "Android" → nvarchar(40) trong DB

				DeviceName = "Samsung Galaxy A55", 
				LastUsedAtUtc = DateTime.UtcNow 
			},
			new DeviceToken { 
				UserId = DemoIds.OwnerVincomUser, 
				Token = "demo-fcm-token-owner-vincom-web", 
				Platform = "Web",      // Cấu hình 2: Web
				DeviceName = "Chrome – Owner portal", 
				LastUsedAtUtc = DateTime.UtcNow 
			}
		);
	}
}
```

#### ❌ **LỖI CŨ - NotificationPreference Seeding:**
```csharp
// ❌ SAI: TimeOnly(23, 0) nhưng EF map TIME → TimeSpan
new NotificationPreference { 
	UserId = DemoIds.Driver1User, 
	TemplateKey = "BOOKING_REMINDER", 
	Channel = NotificationChannel.Push, 
	IsEnabled = true,
	QuietFrom = new TimeOnly(23, 0),   // ❌ TimeOnly, nhưng property là TimeSpan!
	QuietTo = new TimeOnly(6, 0)
}
```

**Lỗi gì?**
```
QuietFrom được khai báo là TimeSpan?
Nhưng seeding code gán TimeOnly
↓
Compile Error: Cannot convert TimeOnly to TimeSpan?
```

#### ✅ **LỒI MỚI - Dùng TimeSpan:**
```csharp
// ✅ ĐÚNG: QuietFrom/To là TimeSpan, seed TimeSpan
new NotificationPreference { 
	UserId = DemoIds.Driver1User, 
	TemplateKey = "BOOKING_REMINDER", 
	Channel = NotificationChannel.Push, 
	IsEnabled = true,

	// ✅ TimeSpan(hours, minutes, seconds)
	QuietFrom = new TimeSpan(23, 0, 0),  // 23:00 - 11 PM
	//                       ↑    ↑   ↑
	//                       h    m   s
	//         Vào database dưới dạng TIME: '23:00:00'

	QuietTo = new TimeSpan(6, 0, 0)      // 06:00 - 6 AM
}
```

**Tại sao TimeSpan?**
```
TimeSpan có 3 thành phần: Hours, Minutes, Seconds
new TimeSpan(23, 0, 0) = 23h 0m 0s

SQL TIME columns lưu: hh:mm:ss
TimesSpan là cách C# represent điều này
```

---

## 🎨 PHẦN 4: DTO & APPLICATION LAYER - NAMING CONFLICTS

### **File: NotificationFeatures.cs** (Application)
```csharp
namespace NotificationService.Application.Features;

// DTO ĐẦU TIÊN - "Inbox" version
public sealed record NotificationDto(
	int Id,              // ID thôi
	string Channel,      // String (from DB)
	string TemplateKey,  // String
	string Title,        // String
	string Body,         // String
	string? DataJson,    // String
	string Status,       // String (from DB)
	bool IsRead,         // Bit
	DateTime CreatedAtUtc // DateTime2
);

// Use case #1: Inbox queries
public interface INotificationQueries
{
	Task<PagedResult<NotificationDto>> ListByUserAsync(
		int UserId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken
	);
	Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken);
}
```

### **File: INotificationUseCases.cs** (Application)
```csharp
namespace NotificationService.Application.Features.Notifications;

// ❌ ĐÃ CÓ NotificationDto MỚI - DUPLICATE!
public sealed record NotificationDto(
	int Id,
	int UserId,                          // Thêm UserId
	NotificationChannel Channel,         // ENUM (!= string)
	string TemplateKey,
	string Title,
	string Body,
	string? DataJson,
	NotificationStatus Status,           // ENUM (NOT string)
	int RetryCount,                      // Thông tin thêm
	string? LastError,                   // Thông tin thêm
	DateTime? SentAtUtc,                 // Thông tin thêm
	bool IsRead,
	DateTime? ReadAtUtc,                 // Thông tin thêm
	DateTime CreatedAtUtc
);

public interface IGetNotificationsUseCase
{
	Task<(int UnreadCount, List<NotificationDto> Items)> ExecuteAsync(
		GetNotificationsRequest request,
		CancellationToken cancellationToken = default
	);
}
```

#### 🔴 **VẤNĐỀ:**
```
2 DTOs cùng tên: NotificationDto
Namespace khác nhau → Compiler bối rối

File 1: NotificationService.Application.Features.NotificationDto
File 2: NotificationService.Application.Features.Notifications.NotificationDto

Khi compile, nó không biết:
- IGetNotificationsUseCase return cái nào?
- ListByUserAsync return cái nào?
↓
Compile Error: Ambiguous type name 'NotificationDto'
```

#### ✅ **GIẢI PHÁP:**

**Step 1: Xóa duplicate**
```csharp
// File: INotificationUseCases.cs
// XÓA record NotificationDto định nghĩa ở đây
```

**Step 2: Import từ Features**
```csharp
// File: INotificationUseCases.cs
using NotificationService.Application.Features;  // ← Import
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Notifications;

// Không định nghĩa NotificationDto ở đây!
// Dùng NotificationDto từ Features namespace

public interface IGetNotificationsUseCase
{
	Task<(int UnreadCount, List<NotificationDto> Items)> ExecuteAsync(
		//                          ↑
		//                 Tham chiếu tới Features.NotificationDto
		GetNotificationsRequest request,
		CancellationToken cancellationToken = default
	);
}
```

**Step 3: Compile thành công**
```
Giờ chỉ có 1 NotificationDto:
NotificationService.Application.Features.NotificationDto

Tất cả use cases dùng cùng loại
Không có conflict
```

---

## 📈 PHẦN 5: BUILD PROCESS - TỪNG BƯỚC KIỂM CHỨNG

### **Step 1: Build Infrastructure**
```bash
dotnet build Services/NotificationService/NotificationService.Infrastructure/...
```
**Kết quả:**
- ❌ FAILED: DeviceToken.cs compile error
  - Lý do: `DevicePlatform` enum không import hoặc không tồn tại
  - Fix: Thay `DevicePlatform Platform` → `string Platform`

### **Step 2: Build Application**
```bash
dotnet build Services/NotificationService/NotificationService.Application/...
```
**Kết quả:**
- ❌ FAILED: INotificationUseCases.cs compile error
  - Lý do: Duplicate `NotificationDto` definition
  - Fix: Xóa duplicate, import từ Features namespace

### **Step 3: Build API**
```bash
dotnet build Services/NotificationService/NotificationService.API/...
```
**Kết quả:**
- ❌ FAILED: Seeder compile error
  - Lý do: `Platform = DevicePlatform.Android` (enum không tồn tại)
  - Fix: `Platform = "Android"` (string literal)

- ❌ FAILED: Seeder compile error
  - Lý do: `QuietFrom = new TimeOnly(23, 0)` (property là TimeSpan, gán TimeOnly)
  - Fix: `QuietFrom = new TimeSpan(23, 0, 0)` (TimeSpan)

### **Step 4: Rebuild All**
```bash
dotnet build ParkingManagement.sln
```
**Kết quả:**
- ✅ SUCCESS: Tất cả projects compile

---

## 🧠 PHẦN 6: LÝ THUYẾT - ENTITY FRAMEWORK CORE MAPPING

### **Bảng Mapping Đầy Đủ:**

| SQL Server | EF Core Type | C# Type | Ví dụ |
|------------|-------------|--------|--------|
| `int` | mapped | `int` | 42 |
| `bigint` | mapped | `long` | 9223372036854775807 |
| `bit` | mapped | `bool` | true/false |
| `nvarchar(50)` | mapped | `string` | "Hello" |
| `nvarchar(max)` | mapped | `string?` | null hoặc dài bất kỳ |
| `datetime2` | mapped | `DateTime` | 2026-10-02 15:30:00 |
| `date` | mapped | `DateOnly` | 2026-10-02 |
| `time` | mapped | `TimeSpan` | 15:30:00 |
| `uuid/uniqueidentifier` | mapped | `Guid` | {guid-value} |

### **Convention vs Explicit Configuration:**

```csharp
// CONVENTION (Implicit) - EF tự đoán
public class User
{
	public int Id { get; set; }           // ← EF biết: PK, auto-increment
	public string Email { get; set; }     // ← EF biết: nvarchar(max)
}

// EXPLICIT (Fluent API) - Bạn chỉ rõ
public class UserConfiguration : IEntityTypeConfiguration<User>
{
	public void Configure(EntityTypeBuilder<User> e)
	{
		e.Property(x => x.Email).HasMaxLength(255).IsRequired(); // Chỉ rõ: 255 ký tự, NOT NULL
	}
}
```

---

## 🐛 PHẦN 7: COMMON MISTAKES & HOW TO DEBUG

### **Mistake #1: String vs Enum**
```csharp
// ❌ Database column là nvarchar, nhưng code dùng enum
public Status Status { get; set; }  // enum Status { Pending=0, Sent=1 }

// Khi save: "Pending" → 0 (mismatch!)
// ✅ Fix: Dùng string hoặc map enum → string
public string Status { get; set; }  // Hoặc dùng ValueConverter
```

### **Mistake #2: TimeOnly vs TimeSpan**
```csharp
// ❌ Database TIME, nhưng dùng TimeOnly
public TimeOnly QuietHour { get; set; }

// ✅ Fix: Dùng TimeSpan (EF Core → SQL TIME)
public TimeSpan QuietHour { get; set; }
```

### **Mistake #3: Duplicate Type Names**
```csharp
// File 1: Features/UserDto.cs
namespace App.Features;
public record UserDto(int Id, string Name);

// File 2: Features.Admin/UserDto.cs
namespace App.Features.Admin;
public record UserDto(int Id, string Name, string Role);  // ❌ Duplicate!

// ✅ Fix: Rename hoặc consolidate
public record AdminUserDto(int Id, string Name, string Role);
```

---

## 🎓 PHẦN 8: BEST PRACTICES

### **1. Always Match Database Types**
```csharp
// Kiểm tra database schema TRƯỚC khi code
-- SQL:
CREATE TABLE [Orders] (
	[Id] int NOT NULL IDENTITY,
	[CreatedAt] datetime2 NOT NULL,
	[Status] nvarchar(50) NOT NULL,
	[Total] decimal(18,2) NOT NULL
);

// C#:
public class Order : BaseEntity
{
	// ✅ Match chính xác
	public DateTime CreatedAt { get; set; }      // datetime2 → DateTime
	public string Status { get; set; }           // nvarchar(50) → string
	public decimal Total { get; set; }           // decimal(18,2) → decimal
}
```

### **2. Use Fluent API for Complex Mappings**
```csharp
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
	public void Configure(EntityTypeBuilder<Order> e)
	{
		e.Property(x => x.Status).HasMaxLength(50).IsRequired();
		e.Property(x => x.Total).HasPrecision(18, 2);
		e.HasIndex(x => new { x.UserId, x.CreatedAt });  // Index giúp query nhanh
	}
}
```

### **3. Use Single Source of Truth for DTOs**
```csharp
// ✅ GOOD: 1 DTO shared across use cases
public sealed record NotificationDto(int Id, string Channel, ...);

// ❌ BAD: Multiple DTOs cùng tên, khác namespace
public record NotificationDto(...) { }  // Features namespace
public record NotificationDto(...) { }  // Features.Admin namespace
```

### **4. Use ValueConverters for Enum Mapping**
```csharp
// Nếu cần lưu enum dưới dạng string trong DB:
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
	public void Configure(EntityTypeBuilder<Order> e)
	{
		e.Property(x => x.Status)
			.HasConversion<string>()  // Lưu enum dưới dạng string
			.HasMaxLength(50);
	}
}

// Database sẽ lưu: "Pending", "Processing", "Completed"
// Code dùng: Status.Pending, Status.Processing
```

---

## ✅ CHECKLIST - VERIFY ALIGNMENT

Trước khi commit code, check danh sách này:

- [ ] **Database Schema** reviewed từ SQL script hoặc SSMS
- [ ] **All SQL types mapped** đúng sang C# (datetime2→DateTime, nvarchar→string, etc)
- [ ] **No enum conflicts** (SharedKernel enum vs local enum)
- [ ] **Duplicate DTOs removed** (check naming)
- [ ] **Seeding data matches entity types** (Platform: "Android" không phải enum)
- [ ] **Fluent API configured** với MaxLength, Required, Indexes
- [ ] **Build succeeded** (Infrastructure → Application → API)
- [ ] **No compiler errors** (check Error List)
- [ ] **Entity relationships correct** (FK, navigation properties)

---

## 📚 THAM KHẢO THÊM

### **EF Core Official Docs:**
- Data Types: https://learn.microsoft.com/en-us/ef/core/modeling/shadow-properties
- Fluent API: https://learn.microsoft.com/en-us/ef/core/modeling/relationships
- Value Converters: https://learn.microsoft.com/en-us/ef/core/modeling/value-converters

### **SQL Server Data Types to .NET:**
- https://learn.microsoft.com/en-us/sql/t-sql/data-types/data-types-transact-sql

### **TimeSpan vs TimeOnly:**
- TimeSpan docs: https://learn.microsoft.com/en-us/dotnet/api/system.timespan
- TimeOnly docs: https://learn.microsoft.com/en-us/dotnet/api/system.timeonly

---

## 🎯 BƯỚC TIẾP THEO

1. **Tìm hiểu thêm:** Mở PM_NotificationDb trong SSMS, so sánh với entity models
2. **Thực hành:** Thêm column mới vào database, update entity, run migration
3. **Challenge:** Tạo entity mới (Complaint, Payment) từ SQL schema
4. **Deep Dive:** Học về Value Converters, Shadow Properties, Query Filters

---

**Bạn đã hiểu chi tiết hành trình từ Database Schema → Compile Error → Fix → Success! 🚀**
