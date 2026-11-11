using ParkingManagement.SharedKernel.Domain;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Bộ đếm runtime US-066 của 1 bãi: Total (T = tổng xe đang đỗ),
/// Transient (V = xe vãng lai đang đỗ), Subscriber (xe thuê bao đang đỗ; T = V + Subscriber).
/// Chỉ cập nhật bằng thao tác nguyên tử có điều kiện (LotAdmissionGate) — KHÔNG sửa tay.
/// </summary>
public class LotAdmissionCounter : BaseEntity
{
    public int ParkingLotId { get; set; }
    public int Total { get; set; }
    public int Transient { get; set; }
    public int Subscriber { get; set; }
}