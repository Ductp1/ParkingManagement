using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Kyb;

public interface IGetKybDocumentFileUseCase
{
    Task<KybDocumentFileContent> ExecuteAsync(
        int lotId,
        string fileId,
        int? ownerProfileId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tải / Xem nội dung file tài liệu KYB của bãi đỗ xe (US-054).
/// Kiểm soát quyền truy cập chặt chẽ (chỉ Chủ bãi sở hữu bãi hoặc Quản trị viên mới được xem).
/// </summary>
public sealed class GetKybDocumentFileUseCase(
    IParkingLotManagementRepository lotRepository,
    IKybDocumentStorage storage) : IGetKybDocumentFileUseCase
{
    public async Task<KybDocumentFileContent> ExecuteAsync(
        int lotId,
        string fileId,
        int? ownerProfileId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(fileId))
            throw new ValidationException("Mã file không hợp lệ.");

        // Chống Path Traversal trong fileId
        if (fileId.Contains("..") || fileId.Contains('/') || fileId.Contains('\\'))
            throw new ValidationException("Mã file không hợp lệ.");

        var lot = await lotRepository.GetByIdAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ", lotId);

        if (!isAdmin)
        {
            if (ownerProfileId is null or <= 0)
                throw new ValidationException("OwnerProfileId không hợp lệ.");

            if (lot.OwnerProfileId != ownerProfileId.Value)
                throw new ConflictException("Bạn không có quyền truy cập tài liệu của bãi đỗ này.");
        }

        var content = await storage.ReadAsync(lotId, fileId, cancellationToken);
        if (content is null)
            throw new NotFoundException("Tài liệu KYB", fileId);

        return content;
    }
}
