using BookingService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace BookingService.Application.Features.MonthlyPasses;

/// <summary>
/// US-066 (luật 4): đăng ký thuê bao MỚI chỉ được khi A &lt; Q
/// (A = số thuê bao đang hiệu lực của bãi, đọc local; Q = hạn mức từ cấu hình GateService).
/// Chống race: Infrastructure chèn trong transaction có pg_advisory_xact_lock(lotId) +
/// đếm lại A — 2 request đồng thời khi còn đúng 1 hạn mức thì đúng 1 thành công.
/// </summary>
public sealed record RegisterMonthlyPassCommand(
    int ParkingLotId,
    int UserId,
    int VehicleId,
    string PlateNumber,
    int Months = 1,
    int? OwnerProfileId = null,
    int? SlotId = null,
    string? SlotCode = null);

public sealed record MonthlyPassDto(
    int Id, string Code, int ParkingLotId, int UserId, string PlateNumber,
    DateOnly ValidFrom, DateOnly ValidTo, string Status);

public interface IMonthlyPassRegistrationStore
{
    Task<int> CountActiveAsync(int parkingLotId, CancellationToken cancellationToken = default);

    /// <summary>Chèn thuê bao Active trong transaction có advisory lock + kiểm tra count &lt; quota. False = hết hạn mức.</summary>
    Task<bool> TryAddActiveWithinQuotaAsync(MonthlyPass pass, int subscriberQuota, CancellationToken cancellationToken = default);
}

/// <summary>Đọc Q từ cấu hình GateService. null = bãi chưa cấu hình US-066 → không giới hạn.</summary>
public interface ILotSubscriberQuotaProvider
{
    Task<int?> GetSubscriberQuotaAsync(int parkingLotId, CancellationToken cancellationToken = default);
}

public interface IRegisterMonthlyPassUseCase
{
    Task<MonthlyPassDto> ExecuteAsync(RegisterMonthlyPassCommand command, CancellationToken cancellationToken = default);
}

public sealed class RegisterMonthlyPassUseCase(
    IMonthlyPassRegistrationStore store,
    ILotSubscriberQuotaProvider quotaProvider,
    TimeProvider timeProvider) : IRegisterMonthlyPassUseCase
{
    public const string CodeQuotaFull = "SUBSCRIBER_QUOTA_FULL";

    public async Task<MonthlyPassDto> ExecuteAsync(RegisterMonthlyPassCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");
        if (command.UserId <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        if (command.VehicleId <= 0) throw new ValidationException("vehicleId phải là số nguyên dương.");
        if (string.IsNullOrWhiteSpace(command.PlateNumber)) throw new ValidationException("Biển số không được để trống.");
        if (command.Months is < 1 or > 24) throw new ValidationException("Số tháng đăng ký phải từ 1 đến 24.");

        var quota = await quotaProvider.GetSubscriberQuotaAsync(command.ParkingLotId, cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var pass = new MonthlyPass
        {
            Code = $"MP-{command.ParkingLotId:D3}-{nowUtc:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}",
            ParkingLotId = command.ParkingLotId,
            UserId = command.UserId,
            VehicleId = command.VehicleId,
            OwnerProfileId = command.OwnerProfileId ?? 0,
            PlateNumber = command.PlateNumber.Trim().ToUpperInvariant(),
            SlotId = command.SlotId,
            SlotCode = command.SlotCode,
            ValidFrom = today,
            ValidTo = today.AddMonths(command.Months),
            Status = MonthlyPassStatus.Active,
        };

        if (quota.HasValue)
        {
            // US-066 (luật 4): A < Q mới được đăng ký. A đọc local; chốt lại dưới advisory lock trong store.
            var activeCount = await store.CountActiveAsync(command.ParkingLotId, cancellationToken);
            if (activeCount >= quota.Value)
                throw new ValidationException(
                    $"{CodeQuotaFull}: Bãi đã hết hạn mức thuê bao (A = {activeCount} >= Q = {quota.Value}). Vui lòng thử lại sau khi có thuê bao hết hạn.");
        }

        // Chèn thuê bao trong transaction có advisory lock (Q = int.MaxValue khi bãi chưa cấu hình → luôn qua).
        if (!await store.TryAddActiveWithinQuotaAsync(pass, quota ?? int.MaxValue, cancellationToken))
            throw new ValidationException(
                $"{CodeQuotaFull}: Bãi vừa hết hạn mức thuê bao (Q = {quota}) do có đăng ký đồng thời. Vui lòng thử lại.");

        return new MonthlyPassDto(pass.Id, pass.Code, pass.ParkingLotId, pass.UserId, pass.PlateNumber,
            pass.ValidFrom, pass.ValidTo, pass.Status.ToString());
    }
}