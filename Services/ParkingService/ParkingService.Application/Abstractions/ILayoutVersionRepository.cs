using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu Phiên bản sơ đồ lưới tầng (LayoutVersion - SRS v2 F1.4, T-502, US-059).
/// </summary>
public interface ILayoutVersionRepository
{
    /// <summary>Lấy phiên bản layout đã xuất bản (IsPublished = true) mới nhất của tầng.</summary>
    Task<LayoutVersion?> GetLatestPublishedByFloorIdAsync(int floorId, CancellationToken cancellationToken = default);

    /// <summary>Lấy bản vẽ nháp (IsPublished = false) mới nhất của tầng nếu có.</summary>
    Task<LayoutVersion?> GetLatestDraftByFloorIdAsync(int floorId, CancellationToken cancellationToken = default);

    /// <summary>Lấy phiên bản layout theo số phiên bản cụ thể của tầng.</summary>
    Task<LayoutVersion?> GetByFloorIdAndVersionAsync(int floorId, int versionNo, CancellationToken cancellationToken = default);

    /// <summary>Lấy toàn bộ lịch sử các phiên bản layout của tầng (sắp xếp giảm dần theo VersionNo).</summary>
    Task<IReadOnlyList<LayoutVersion>> GetAllVersionsByFloorIdAsync(int floorId, CancellationToken cancellationToken = default);

    /// <summary>Lấy số phiên bản tiếp theo cho tầng (max VersionNo + 1, bắt đầu từ 1).</summary>
    Task<int> GetNextVersionNoAsync(int floorId, CancellationToken cancellationToken = default);

    Task AddAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default);

    Task UpdateAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default);

    Task DeleteAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
