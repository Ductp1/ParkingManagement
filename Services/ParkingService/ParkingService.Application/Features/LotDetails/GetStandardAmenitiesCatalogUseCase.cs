namespace ParkingService.Application.Features.LotDetails;

public interface IGetStandardAmenitiesCatalogUseCase
{
    IReadOnlyList<AmenityCatalogItemDto> Execute();
}

public sealed class GetStandardAmenitiesCatalogUseCase : IGetStandardAmenitiesCatalogUseCase
{
    public IReadOnlyList<AmenityCatalogItemDto> Execute()
        => StandardAmenitiesCatalog.Items;
}
