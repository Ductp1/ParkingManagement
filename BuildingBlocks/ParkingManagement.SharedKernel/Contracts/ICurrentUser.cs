namespace ParkingManagement.SharedKernel.Contracts;

public interface ICurrentUser
{
    int? UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    int? OwnerProfileId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<int> ParkingLotIds => [];
}
