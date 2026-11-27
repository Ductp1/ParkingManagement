using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.LotDetails;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class LotDetailsTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    private static ParkingLot CreateTestLot(int id = 1, int ownerProfileId = 1, int totalSlots = 20, int availableSlots = 20)
    {
        return new ParkingLot(
            id: id,
            name: "Bãi xe Test",
            address: "123 Test Street",
            latitude: 10.77,
            longitude: 106.70,
            totalSlots: totalSlots,
            availableSlots: availableSlots,
            maxHeightCm: 220,
            openTime: new TimeOnly(6, 0),
            closeTime: new TimeOnly(22, 0),
            status: ParkingLotStatus.Active,
            ownerProfileId: ownerProfileId);
    }

    private sealed class FakeLotDetailsRepo : ILotDetailsRepository
    {
        public List<ParkingLot> Lots { get; } = [];
        public List<LotAmenity> Amenities { get; } = [];
        public List<LotPhoto> Photos { get; } = [];
        private int _amenityIdCounter = 1;
        private int _photoIdCounter = 1;

        public Task<IReadOnlyList<LotAmenity>> GetAmenitiesByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        {
            var list = Amenities.Where(a => a.ParkingLotId == parkingLotId).OrderBy(a => a.Code).ToList();
            return Task.FromResult<IReadOnlyList<LotAmenity>>(list);
        }

        public Task<LotAmenity?> GetAmenityByCodeAsync(int parkingLotId, string code, CancellationToken cancellationToken = default)
        {
            var item = Amenities.FirstOrDefault(a => a.ParkingLotId == parkingLotId && string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(item);
        }

        public Task AddAmenityAsync(LotAmenity amenity, CancellationToken cancellationToken = default)
        {
            if (amenity.Id == 0)
            {
                _amenityIdCounter = Math.Max(_amenityIdCounter, (Amenities.Count > 0 ? Amenities.Max(a => a.Id) : 0) + 1);
                WithId(amenity, _amenityIdCounter++);
            }
            Amenities.Add(amenity);
            return Task.CompletedTask;
        }

        public Task DeleteAmenityAsync(LotAmenity amenity, CancellationToken cancellationToken = default)
        {
            Amenities.Remove(amenity);
            return Task.CompletedTask;
        }

        public Task ReplaceAmenitiesAsync(int parkingLotId, IEnumerable<LotAmenity> newAmenities, CancellationToken cancellationToken = default)
        {
            Amenities.RemoveAll(a => a.ParkingLotId == parkingLotId);
            foreach (var a in newAmenities)
            {
                if (a.Id == 0) WithId(a, _amenityIdCounter++);
                Amenities.Add(a);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LotPhoto>> GetPhotosByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        {
            var list = Photos.Where(p => p.ParkingLotId == parkingLotId).OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToList();
            return Task.FromResult<IReadOnlyList<LotPhoto>>(list);
        }

        public Task<LotPhoto?> GetPhotoByIdAsync(int photoId, CancellationToken cancellationToken = default)
        {
            var item = Photos.FirstOrDefault(p => p.Id == photoId);
            return Task.FromResult(item);
        }

        public Task AddPhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default)
        {
            if (photo.Id == 0)
            {
                _photoIdCounter = Math.Max(_photoIdCounter, (Photos.Count > 0 ? Photos.Max(p => p.Id) : 0) + 1);
                WithId(photo, _photoIdCounter++);
            }
            Photos.Add(photo);
            return Task.CompletedTask;
        }

        public Task UpdatePhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default)
        {
            var index = Photos.FindIndex(p => p.Id == photo.Id);
            if (index >= 0) Photos[index] = photo;
            return Task.CompletedTask;
        }

        public Task DeletePhotoAsync(LotPhoto photo, CancellationToken cancellationToken = default)
        {
            Photos.Remove(photo);
            return Task.CompletedTask;
        }

        public Task SetCoverPhotoAsync(int parkingLotId, int photoId, CancellationToken cancellationToken = default)
        {
            string? coverUrl = null;
            foreach (var p in Photos.Where(x => x.ParkingLotId == parkingLotId))
            {
                if (p.Id == photoId)
                {
                    p.IsCover = true;
                    coverUrl = p.Url;
                }
                else
                {
                    p.IsCover = false;
                }
            }

            var lot = Lots.FirstOrDefault(l => l.Id == parkingLotId);
            if (lot != null && coverUrl != null)
            {
                lot.SetCoverImageUrl(coverUrl);
            }

            return Task.CompletedTask;
        }

        public Task ReorderPhotosAsync(int parkingLotId, IReadOnlyDictionary<int, int> photoSortOrders, CancellationToken cancellationToken = default)
        {
            foreach (var p in Photos.Where(x => x.ParkingLotId == parkingLotId))
            {
                if (photoSortOrders.TryGetValue(p.Id, out var newSort))
                {
                    p.SortOrder = newSort;
                }
            }
            return Task.CompletedTask;
        }

        public Task<ParkingLot?> GetLotByIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        {
            var lot = Lots.FirstOrDefault(l => l.Id == parkingLotId && !l.IsDeleted);
            return Task.FromResult(lot);
        }

        public Task<ParkingLot?> GetLotWithDetailsAsync(int parkingLotId, CancellationToken cancellationToken = default)
        {
            var lot = Lots.FirstOrDefault(l => l.Id == parkingLotId && !l.IsDeleted);
            if (lot != null)
            {
                lot.Amenities.Clear();
                foreach (var a in Amenities.Where(x => x.ParkingLotId == parkingLotId))
                    lot.Amenities.Add(a);

                lot.Photos.Clear();
                foreach (var p in Photos.Where(x => x.ParkingLotId == parkingLotId))
                    lot.Photos.Add(p);
            }
            return Task.FromResult(lot);
        }

        public Task UpdateLotAsync(ParkingLot lot, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    // ==================== CATALOG TESTS ====================

    [Fact]
    public void Catalog_ReturnsExpectedAmenities()
    {
        var useCase = new GetStandardAmenitiesCatalogUseCase();
        var items = useCase.Execute();

        Assert.NotNull(items);
        Assert.True(items.Count >= 12);
        Assert.Contains(items, i => i.Code == "COVERED");
        Assert.Contains(items, i => i.Code == "CCTV");
        Assert.Contains(items, i => i.Code == "EV_CHARGER");
        Assert.Contains(items, i => i.Code == "SECURITY_24H");
        Assert.All(items, i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.Code));
            Assert.False(string.IsNullOrWhiteSpace(i.Name));
            Assert.False(string.IsNullOrWhiteSpace(i.Description));
        });
    }

    // ==================== AMENITIES TESTS ====================

    [Fact]
    public async Task GetLotAmenities_ReturnsList_WhenLotExists()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 10);
        repo.Lots.Add(lot);
        repo.Amenities.Add(WithId(new LotAmenity { ParkingLotId = 10, Code = "CCTV", Name = "Camera", Note = "24/7" }, 1));
        repo.Amenities.Add(WithId(new LotAmenity { ParkingLotId = 10, Code = "COVERED", Name = "Có mái che" }, 2));

        var useCase = new GetLotAmenitiesUseCase(repo);
        var result = await useCase.ExecuteAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Code == "CCTV" && a.Note == "24/7");
        Assert.Contains(result, a => a.Code == "COVERED");
    }

    [Fact]
    public async Task GetLotAmenities_ThrowsNotFound_WhenLotNotFound()
    {
        var repo = new FakeLotDetailsRepo();
        var useCase = new GetLotAmenitiesUseCase(repo);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(999));
    }

    [Fact]
    public async Task AddLotAmenity_SuccessfullyAdds_WithDefaultName()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new AddLotAmenityUseCase(repo);
        var dto = await useCase.ExecuteAsync(1, new LotAmenityInputDto("EV_CHARGER", null, "Trạm sạc nhanh"), ownerProfileId: 5);

        Assert.NotNull(dto);
        Assert.Equal("EV_CHARGER", dto.Code);
        Assert.Equal("Trụ sạc xe điện", dto.Name);
        Assert.Equal("Trạm sạc nhanh", dto.Note);
        Assert.Single(repo.Amenities);
    }

    [Fact]
    public async Task AddLotAmenity_ThrowsForbidden_WhenNotOwner()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new AddLotAmenityUseCase(repo);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(1, new LotAmenityInputDto("CCTV"), ownerProfileId: 99));
    }

    [Fact]
    public async Task AddLotAmenity_ThrowsConflict_WhenCodeExists()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Amenities.Add(WithId(new LotAmenity { ParkingLotId = 1, Code = "CCTV", Name = "Camera" }, 1));

        var useCase = new AddLotAmenityUseCase(repo);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(1, new LotAmenityInputDto("cctv"), ownerProfileId: 5));
    }

    [Fact]
    public async Task AddLotAmenity_ThrowsValidation_WhenCodeEmpty()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new AddLotAmenityUseCase(repo);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(1, new LotAmenityInputDto("   "), ownerProfileId: 5));
    }

    [Fact]
    public async Task UpdateLotAmenities_BatchReplacesAmenities()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Amenities.Add(WithId(new LotAmenity { ParkingLotId = 1, Code = "OLD_AMENITY", Name = "Cũ" }, 1));

        var useCase = new UpdateLotAmenitiesUseCase(repo);
        var request = new UpdateLotAmenitiesRequest([
            new LotAmenityInputDto("COVERED", "Mái che"),
            new LotAmenityInputDto("EV_CHARGER", null, "2 cổng 60kW")
        ]);

        var result = await useCase.ExecuteAsync(1, request, ownerProfileId: 5);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, a => a.Code == "OLD_AMENITY");
        Assert.Contains(result, a => a.Code == "COVERED");
        Assert.Contains(result, a => a.Code == "EV_CHARGER" && a.Note == "2 cổng 60kW");
    }

    [Fact]
    public async Task UpdateLotAmenities_ThrowsConflict_OnDuplicateCodesInRequest()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new UpdateLotAmenitiesUseCase(repo);
        var request = new UpdateLotAmenitiesRequest([
            new LotAmenityInputDto("CCTV"),
            new LotAmenityInputDto("cctv", "Camera 2")
        ]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(1, request, ownerProfileId: 5));
    }

    [Fact]
    public async Task DeleteLotAmenity_DeletesSuccessfully()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Amenities.Add(WithId(new LotAmenity { ParkingLotId = 1, Code = "CCTV", Name = "Camera" }, 1));

        var useCase = new DeleteLotAmenityUseCase(repo);
        await useCase.ExecuteAsync(1, "cctv", ownerProfileId: 5);

        Assert.Empty(repo.Amenities);
    }

    [Fact]
    public async Task DeleteLotAmenity_ThrowsNotFound_WhenAmenityDoesNotExist()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new DeleteLotAmenityUseCase(repo);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(1, "NON_EXISTENT", ownerProfileId: 5));
    }

    // ==================== PHOTOS TESTS ====================

    [Fact]
    public async Task GetLotPhotos_ReturnsSortedPhotos()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "url2.jpg", SortOrder = 2 }, 2));
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "url1.jpg", SortOrder = 1, IsCover = true }, 1));

        var useCase = new GetLotPhotosUseCase(repo);
        var result = await useCase.ExecuteAsync(1);

        Assert.Equal(2, result.Count);
        Assert.Equal("url1.jpg", result[0].Url);
        Assert.Equal("url2.jpg", result[1].Url);
    }

    [Fact]
    public async Task AddLotPhoto_FirstPhoto_AutomaticallyBecomesCover()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new AddLotPhotoUseCase(repo);
        var result = await useCase.ExecuteAsync(1, new AddLotPhotoRequest("https://cdn/p1.jpg", "Lối vào"), ownerProfileId: 5);

        Assert.True(result.IsCover);
        Assert.Equal("https://cdn/p1.jpg", result.Url);
        Assert.Equal("https://cdn/p1.jpg", lot.CoverImageUrl);
    }

    [Fact]
    public async Task AddLotPhoto_SecondPhoto_NotCoverByDefault()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "p1.jpg", IsCover = true }, 1));
        lot.SetCoverImageUrl("p1.jpg");

        var useCase = new AddLotPhotoUseCase(repo);
        var result = await useCase.ExecuteAsync(1, new AddLotPhotoRequest("p2.jpg", "Tầng 1", SortOrder: 2), ownerProfileId: 5);

        Assert.False(result.IsCover);
        Assert.Equal("p1.jpg", lot.CoverImageUrl);
        Assert.Equal(2, repo.Photos.Count);
    }

    [Fact]
    public async Task AddLotPhoto_WithIsCoverTrue_SetsCover()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "p1.jpg", IsCover = true }, 1));
        lot.SetCoverImageUrl("p1.jpg");

        var useCase = new AddLotPhotoUseCase(repo);
        var result = await useCase.ExecuteAsync(1, new AddLotPhotoRequest("p2.jpg", "Ảnh bìa mới", IsCover: true), ownerProfileId: 5);

        Assert.True(result.IsCover);
        Assert.Equal("p2.jpg", lot.CoverImageUrl);
        Assert.False(repo.Photos.First(p => p.Id == 1).IsCover);
    }

    [Fact]
    public async Task AddLotPhoto_ThrowsValidation_WhenUrlEmpty()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new AddLotPhotoUseCase(repo);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(1, new AddLotPhotoRequest("   "), ownerProfileId: 5));
    }

    [Fact]
    public async Task UpdateLotPhoto_UpdatesCaptionAndSortOrder()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "p1.jpg", Caption = "Old", SortOrder = 1 }, 1));

        var useCase = new UpdateLotPhotoUseCase(repo);
        var result = await useCase.ExecuteAsync(1, 1, new UpdateLotPhotoRequest("New Caption", 5), ownerProfileId: 5);

        Assert.Equal("New Caption", result.Caption);
        Assert.Equal(5, result.SortOrder);
    }

    [Fact]
    public async Task SetCoverPhoto_SwitchesCover_AndUpdatesLotCoverImageUrl()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "p1.jpg", IsCover = true }, 1));
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "p2.jpg", IsCover = false }, 2));
        lot.SetCoverImageUrl("p1.jpg");

        var useCase = new SetCoverPhotoUseCase(repo);
        var result = await useCase.ExecuteAsync(1, 2, ownerProfileId: 5);

        Assert.True(result.IsCover);
        Assert.Equal("p2.jpg", lot.CoverImageUrl);
        Assert.False(repo.Photos.First(p => p.Id == 1).IsCover);
        Assert.True(repo.Photos.First(p => p.Id == 2).IsCover);
    }

    [Fact]
    public async Task ReorderLotPhotos_UpdatesSortOrders()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "p1.jpg", SortOrder = 1 }, 10));
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "p2.jpg", SortOrder = 2 }, 20));

        var useCase = new ReorderLotPhotosUseCase(repo);
        var result = await useCase.ExecuteAsync(1, new ReorderLotPhotosRequest([
            new PhotoOrderItemDto(10, 5),
            new PhotoOrderItemDto(20, 1)
        ]), ownerProfileId: 5);

        Assert.Equal(20, result[0].Id);
        Assert.Equal(1, result[0].SortOrder);
        Assert.Equal(10, result[1].Id);
        Assert.Equal(5, result[1].SortOrder);
    }

    [Fact]
    public async Task DeleteLotPhoto_NonCover_DeletesWithoutChangingCover()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "cover.jpg", IsCover = true, SortOrder = 1 }, 1));
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "extra.jpg", IsCover = false, SortOrder = 2 }, 2));
        lot.SetCoverImageUrl("cover.jpg");

        var useCase = new DeleteLotPhotoUseCase(repo);
        await useCase.ExecuteAsync(1, 2, ownerProfileId: 5);

        Assert.Single(repo.Photos);
        Assert.Equal("cover.jpg", lot.CoverImageUrl);
        Assert.True(repo.Photos[0].IsCover);
    }

    [Fact]
    public async Task DeleteLotPhoto_Cover_PromotesNextPhotoToCover()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "old_cover.jpg", IsCover = true, SortOrder = 1 }, 1));
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "next_cover.jpg", IsCover = false, SortOrder = 2 }, 2));
        lot.SetCoverImageUrl("old_cover.jpg");

        var useCase = new DeleteLotPhotoUseCase(repo);
        await useCase.ExecuteAsync(1, 1, ownerProfileId: 5);

        Assert.Single(repo.Photos);
        Assert.Equal("next_cover.jpg", lot.CoverImageUrl);
        Assert.True(repo.Photos[0].IsCover);
    }

    [Fact]
    public async Task DeleteLotPhoto_LastPhoto_ClearsLotCoverImageUrl()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "only_one.jpg", IsCover = true, SortOrder = 1 }, 1));
        lot.SetCoverImageUrl("only_one.jpg");

        var useCase = new DeleteLotPhotoUseCase(repo);
        await useCase.ExecuteAsync(1, 1, ownerProfileId: 5);

        Assert.Empty(repo.Photos);
        Assert.Null(lot.CoverImageUrl);
    }

    // ==================== ATTRIBUTES TESTS ====================

    [Fact]
    public async Task GetLotAttributes_ReturnsCombinedDetails()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1);
        lot.UpdateAttributes("Bãi đỗ trung tâm", "0901234567", 240);
        lot.SetCoverImageUrl("cover.jpg");
        repo.Lots.Add(lot);
        repo.Amenities.Add(WithId(new LotAmenity { ParkingLotId = 1, Code = "CCTV", Name = "Camera" }, 1));
        repo.Photos.Add(WithId(new LotPhoto { ParkingLotId = 1, Url = "cover.jpg", IsCover = true, SortOrder = 1 }, 1));

        var useCase = new GetLotAttributesUseCase(repo);
        var result = await useCase.ExecuteAsync(1);

        Assert.Equal(1, result.ParkingLotId);
        Assert.Equal("Bãi xe Test", result.Name);
        Assert.Equal("Bãi đỗ trung tâm", result.Description);
        Assert.Equal("0901234567", result.HotlinePhone);
        Assert.Equal(240, result.MaxHeightCm);
        Assert.Equal("cover.jpg", result.CoverImageUrl);
        Assert.Single(result.Amenities);
        Assert.Single(result.Photos);
    }

    [Fact]
    public async Task UpdateLotAttributes_UpdatesValuesSuccessfully()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new UpdateLotAttributesUseCase(repo);
        var request = new UpdateLotAttributesRequest("Mô tả mới", "0911223344", 250);
        var result = await useCase.ExecuteAsync(1, request, ownerProfileId: 5);

        Assert.Equal("Mô tả mới", result.Description);
        Assert.Equal("0911223344", result.HotlinePhone);
        Assert.Equal(250, result.MaxHeightCm);
        Assert.Equal("Mô tả mới", lot.Description);
        Assert.Equal("0911223344", lot.HotlinePhone);
        Assert.Equal(250, lot.MaxHeightCm);
    }

    [Fact]
    public async Task UpdateLotAttributes_ThrowsValidationException_WhenHeightIsInvalid()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new UpdateLotAttributesUseCase(repo);
        var request = new UpdateLotAttributesRequest("Mô tả", "0911223344", 0);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(1, request, ownerProfileId: 5));
    }

    [Fact]
    public async Task UpdateLotAttributes_ThrowsForbidden_WhenNotOwner()
    {
        var repo = new FakeLotDetailsRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new UpdateLotAttributesUseCase(repo);
        var request = new UpdateLotAttributesRequest("Mô tả", "0911223344", 250);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(1, request, ownerProfileId: 99));
    }
}
