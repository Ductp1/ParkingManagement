using GateService.Application.Features.LotCapacity;
using GateService.Domain.Entities;
using GateService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GateService.Infrastructure.Persistence;

/// <summary>
/// Cổng vào/ra NGUYÊN TỬ chống race (US-066): pre-check bằng LotAdmissionPolicy chỉ để trả
/// lý do từ chối thân thiện; authority là UPDATE/INSERT ... WHERE có điều kiện — rowsAffected = 0
/// nghĩa là request khác chiếm chỗ trước → từ chối. Không dùng mô hình "đọc rồi kiểm tra rồi ghi".
/// </summary>
public sealed class LotAdmissionGate(GateDbContext db) : ILotAdmissionGate
{
    public async Task<(bool Admitted, string? RejectReason)> TryAdmitAsync(
        int parkingLotId, AdmissionType type, CancellationToken cancellationToken = default)
    {
        var policy = await db.LotCapacityConfigs.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ParkingLotId == parkingLotId, cancellationToken);
        if (policy is null)
            return (true, null); // chưa cấu hình US-066 → không giới hạn (tương thích ngược).

        var counter = await db.LotAdmissionCounters.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ParkingLotId == parkingLotId, cancellationToken);
        var (allowed, reason) = LotAdmissionPolicy.Decide(
            counter?.Total ?? 0, counter?.Transient ?? 0, policy.Capacity, policy.TransientBuffer, type);
        if (!allowed)
            return (false, reason);

        var isTransient = type == AdmissionType.Transient ? 1 : 0;
        var isSubscriber = type == AdmissionType.Subscriber ? 1 : 0;

        // Nhận chỗ nguyên tử: INSERT dòng đầu tiên hoặc UPDATE có điều kiện (C trần tổng, B trần vãng lai).
        var rows = await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "LotAdmissionCounters" ("ParkingLotId", "Total", "Transient", "Subscriber", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({parkingLotId}, 1, {isTransient}, {isSubscriber}, NOW(), NOW())
            ON CONFLICT ("ParkingLotId") DO UPDATE SET
                "Total" = "LotAdmissionCounters"."Total" + 1,
                "Transient" = "LotAdmissionCounters"."Transient" + {isTransient},
                "Subscriber" = "LotAdmissionCounters"."Subscriber" + {isSubscriber},
                "UpdatedAtUtc" = NOW()
            WHERE "LotAdmissionCounters"."Total" < {policy.Capacity}
              AND ({isSubscriber} = 1 OR "LotAdmissionCounters"."Transient" < {policy.TransientBuffer})
            """, cancellationToken);

        if (rows > 0)
            return (true, null);

        // Race: pre-check còn chỗ nhưng atomic update không nhận được dòng nào → request khác nhanh hơn.
        var fresh = await db.LotAdmissionCounters.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ParkingLotId == parkingLotId, cancellationToken);
        var (_, fallbackReason) = LotAdmissionPolicy.Decide(
            fresh?.Total ?? 0, fresh?.Transient ?? 0, policy.Capacity, policy.TransientBuffer, type);
        return (false, fallbackReason ?? AdmissionRejectReasons.LotFull);
    }

    public async Task ReleaseAsync(
        int parkingLotId, AdmissionType type, CancellationToken cancellationToken = default)
    {
        var isTransient = type == AdmissionType.Transient ? 1 : 0;
        var isSubscriber = type == AdmissionType.Subscriber ? 1 : 0;

        // Luật 6: trừ đúng bộ đếm theo loại đã ghi; điều kiện bảo vệ không âm.
        await db.Database.ExecuteSqlAsync($"""
            UPDATE "LotAdmissionCounters"
            SET "Total" = "Total" - 1,
                "Transient" = "Transient" - {isTransient},
                "Subscriber" = "Subscriber" - {isSubscriber}
            WHERE "ParkingLotId" = {parkingLotId}
              AND "Total" > 0
              AND ({isTransient} = 0 OR "Transient" > 0)
              AND ({isSubscriber} = 0 OR "Subscriber" > 0)
            """, cancellationToken);
    }
}