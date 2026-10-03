using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.ParkingLots;

/// <summary>Input của Use Case (UC-09 Xem chi tiết bãi).</summary>
public sealed record GetParkingLotByIdQuery(int Id);

public interface IGetParkingLotByIdUseCase
{
    Task<ParkingLotDto> ExecuteAsync(GetParkingLotByIdQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy thông tin bãi đỗ theo Id.
/// Luồng: Validate input → gọi Repository (Infrastructure) → áp business rule → map sang DTO.
/// </summary>
public sealed class GetParkingLotByIdUseCase(
    IParkingLotRepository repository,
    TimeProvider timeProvider,
    ILogger<GetParkingLotByIdUseCase> logger) : IGetParkingLotByIdUseCase
{
    public async Task<ParkingLotDto> ExecuteAsync(GetParkingLotByIdQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Id <= 0)
            throw new ValidationException("Id bãi đỗ phải là số nguyên dương.");

        var lot = await repository.GetByIdAsync(query.Id, cancellationToken);

        // Business rule: bãi không tồn tại HOẶC chưa Active (Pending KYB / Suspended) thì tài xế không được xem
        if (lot is null || !lot.IsPubliclyVisible)
        {
            logger.LogWarning("Bãi Id={Id} không tồn tại hoặc chưa công khai (Status={Status})",
                query.Id, lot?.Status.ToString() ?? "null");
            throw new NotFoundException("Bãi đỗ", query.Id);
        }

        var nowVn = TimeOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.AddHours(7));
        return lot.ToDto(nowVn);
    }
}
