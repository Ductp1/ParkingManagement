using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Enums;

namespace ParkingService.Application.Features.KybMisdeclaration;

/// <summary>Row → DTO cho màn xử lý khai báo sai (US-056); tính IsOverdue theo đồng hồ hiện tại.</summary>
public static class MisdeclarationMapper
{
    public static LotMisdeclarationDto ToDto(MisdeclarationLotRow lot, IReadOnlyList<MisdeclarationCaseRow> cases,
        KybMisdeclarationSettings settings, DateTime nowUtc)
    {
        var items = cases.Select(c => ToDto(c, nowUtc)).ToList();
        return new LotMisdeclarationDto(
            new MisdeclarationLotDto(lot.Id, lot.Name, lot.Address, lot.OwnerProfileId, lot.Status.ToString(),
                lot.Status == ParkingLotStatus.Active),
            items.FirstOrDefault(c => IsOpen(c.Status)),
            items,
            settings.MinorCorrectionDays,
            (int)LotMisdeclarationCase.ResponseWindow.TotalHours);
    }

    public static LotMisdeclarationCaseDto ToDto(MisdeclarationCaseRow c, DateTime nowUtc)
    {
        var open = c.Status is MisdeclarationCaseStatus.Warned or MisdeclarationCaseStatus.Suspended;
        return new LotMisdeclarationCaseDto(c.Id, c.Severity.ToString(), c.Category.ToString(), c.Status.ToString(), c.Finding,
            c.DueAtUtc, open && c.DueAtUtc < nowUtc, c.OpenedByUserId, c.OpenedAtUtc, c.EscalationNote, c.EscalatedAtUtc,
            c.ClosedByUserId, c.ClosedAtUtc, c.ResolutionNote);
    }

    private static bool IsOpen(string status)
        => status is nameof(MisdeclarationCaseStatus.Warned) or nameof(MisdeclarationCaseStatus.Suspended);
}
