using System.Net.Http.Json;
using System.Text.Json;
using GateService.Application.Features.Shifts;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.HttpClients;

/// <summary>IStaffDirectory qua UserService: GET /api/v1/users/me/staff?lotId=&amp;staffUserId= (JWT chủ bãi do handler chuyển tiếp).</summary>
public sealed class StaffDirectoryClient(HttpClient http) : IStaffDirectory
{
    public async Task<bool> IsAssignedAsync(int parkingLotId, int staffUserId, CancellationToken ct)
    {
        const string unavailable = "Chưa xác minh được phân công nhân viên, thử lại sau ít phút.";
        try
        {
            using var response = await http.GetAsync($"api/v1/users/me/staff?lotId={parkingLotId}&staffUserId={staffUserId}", ct);
            if (!response.IsSuccessStatusCode) throw new DependencyUnavailableException(unavailable);
            return (await response.Content.ReadFromJsonAsync<JsonElement[]>(ct))?.Length > 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException(unavailable);
        }
    }
}
