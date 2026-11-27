using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.LotDetails;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller quản lý danh mục & cấu hình tiện ích (Amenities), bộ ảnh (Photos), ảnh bìa và thuộc tính bãi xe (US-064, EP09).
/// Tuyến đường: /api/v1/parking-lots/...
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
public sealed class LotDetailsController(
    IGetStandardAmenitiesCatalogUseCase getStandardAmenitiesCatalog,
    IGetLotAmenitiesUseCase getLotAmenities,
    IUpdateLotAmenitiesUseCase updateLotAmenities,
    IAddLotAmenityUseCase addLotAmenity,
    IDeleteLotAmenityUseCase deleteLotAmenity,
    IGetLotPhotosUseCase getLotPhotos,
    IAddLotPhotoUseCase addLotPhoto,
    IUpdateLotPhotoUseCase updateLotPhoto,
    ISetCoverPhotoUseCase setCoverPhoto,
    IReorderLotPhotosUseCase reorderLotPhotos,
    IDeleteLotPhotoUseCase deleteLotPhoto,
    IGetLotAttributesUseCase getLotAttributes,
    IUpdateLotAttributesUseCase updateLotAttributes,
    ICurrentUser currentUser) : ControllerBase
{
    // ==================== AMENITIES ====================

    /// <summary>GET /api/v1/parking-lots/amenities/catalog - Danh mục tiện ích chuẩn hệ thống.</summary>
    [HttpGet("amenities/catalog")]
    [ProducesResponseType<IReadOnlyList<AmenityCatalogItemDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<AmenityCatalogItemDto>> GetCatalog()
        => Ok(getStandardAmenitiesCatalog.Execute());

    /// <summary>GET /api/v1/parking-lots/{lotId}/amenities - Danh sách tiện ích hiện có của bãi.</summary>
    [HttpGet("{lotId:int}/amenities")]
    [ProducesResponseType<IReadOnlyList<LotAmenityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LotAmenityDto>>> GetAmenities(int lotId, CancellationToken cancellationToken)
        => Ok(await getLotAmenities.ExecuteAsync(lotId, cancellationToken));

    /// <summary>PUT /api/v1/parking-lots/{lotId}/amenities - Cập nhật toàn bộ tiện ích của bãi (Batch replace).</summary>
    [HttpPut("{lotId:int}/amenities")]
    [ProducesResponseType<IReadOnlyList<LotAmenityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<LotAmenityDto>>> UpdateAmenities(
        int lotId,
        [FromBody] UpdateLotAmenitiesRequest request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await updateLotAmenities.ExecuteAsync(lotId, request, effectiveOwnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/amenities - Thêm một tiện ích vào bãi xe.</summary>
    [HttpPost("{lotId:int}/amenities")]
    [ProducesResponseType<LotAmenityDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotAmenityDto>> AddAmenity(
        int lotId,
        [FromBody] LotAmenityInputDto request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await addLotAmenity.ExecuteAsync(lotId, request, effectiveOwnerId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>DELETE /api/v1/parking-lots/{lotId}/amenities/{code} - Xóa tiện ích khỏi bãi xe.</summary>
    [HttpDelete("{lotId:int}/amenities/{code}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAmenity(
        int lotId,
        string code,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        await deleteLotAmenity.ExecuteAsync(lotId, code, effectiveOwnerId, cancellationToken);
        return NoContent();
    }

    // ==================== PHOTOS ====================

    /// <summary>GET /api/v1/parking-lots/{lotId}/photos - Lấy danh sách ảnh bãi xe theo SortOrder.</summary>
    [HttpGet("{lotId:int}/photos")]
    [ProducesResponseType<IReadOnlyList<LotPhotoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LotPhotoDto>>> GetPhotos(int lotId, CancellationToken cancellationToken)
        => Ok(await getLotPhotos.ExecuteAsync(lotId, cancellationToken));

    /// <summary>POST /api/v1/parking-lots/{lotId}/photos - Thêm ảnh mới vào bộ sưu tập của bãi.</summary>
    [HttpPost("{lotId:int}/photos")]
    [ProducesResponseType<LotPhotoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotPhotoDto>> AddPhoto(
        int lotId,
        [FromBody] AddLotPhotoRequest request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await addLotPhoto.ExecuteAsync(lotId, request, effectiveOwnerId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>PUT /api/v1/parking-lots/{lotId}/photos/{photoId} - Cập nhật chú thích hoặc thứ tự ảnh.</summary>
    [HttpPut("{lotId:int}/photos/{photoId:int}")]
    [ProducesResponseType<LotPhotoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotPhotoDto>> UpdatePhoto(
        int lotId,
        int photoId,
        [FromBody] UpdateLotPhotoRequest request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await updateLotPhoto.ExecuteAsync(lotId, photoId, request, effectiveOwnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/photos/{photoId}/set-cover - Đặt ảnh làm ảnh bìa đại diện.</summary>
    [HttpPost("{lotId:int}/photos/{photoId:int}/set-cover")]
    [ProducesResponseType<LotPhotoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotPhotoDto>> SetCover(
        int lotId,
        int photoId,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await setCoverPhoto.ExecuteAsync(lotId, photoId, effectiveOwnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>PUT /api/v1/parking-lots/{lotId}/photos/reorder - Sắp xếp lại thứ tự hiển thị của các ảnh.</summary>
    [HttpPut("{lotId:int}/photos/reorder")]
    [ProducesResponseType<IReadOnlyList<LotPhotoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LotPhotoDto>>> ReorderPhotos(
        int lotId,
        [FromBody] ReorderLotPhotosRequest request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await reorderLotPhotos.ExecuteAsync(lotId, request, effectiveOwnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>DELETE /api/v1/parking-lots/{lotId}/photos/{photoId} - Xóa ảnh khỏi bãi xe.</summary>
    [HttpDelete("{lotId:int}/photos/{photoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(
        int lotId,
        int photoId,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        await deleteLotPhoto.ExecuteAsync(lotId, photoId, effectiveOwnerId, cancellationToken);
        return NoContent();
    }

    // ==================== LOT ATTRIBUTES ====================

    /// <summary>GET /api/v1/parking-lots/{lotId}/attributes - Lấy thuộc tính tổng hợp của bãi xe.</summary>
    [HttpGet("{lotId:int}/attributes")]
    [ProducesResponseType<LotAttributesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotAttributesDto>> GetAttributes(int lotId, CancellationToken cancellationToken)
        => Ok(await getLotAttributes.ExecuteAsync(lotId, cancellationToken));

    /// <summary>PUT /api/v1/parking-lots/{lotId}/attributes - Cập nhật thuộc tính mô tả, hotline, chiều cao bãi.</summary>
    [HttpPut("{lotId:int}/attributes")]
    [ProducesResponseType<LotAttributesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotAttributesDto>> UpdateAttributes(
        int lotId,
        [FromBody] UpdateLotAttributesRequest request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await updateLotAttributes.ExecuteAsync(lotId, request, effectiveOwnerId, cancellationToken);
        return Ok(result);
    }
}
