using AdminService.Infrastructure.Persistence;
using AdminService.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace AdminService.Test;

// US-079: truy vấn hợp đồng đã ký dịch được sang SQL của Npgsql (ToQueryString – không mở kết nối database).
public class CommissionRateSqlTests
{
    private static AdminDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AdminDbContext>().UseNpgsql("Host=unused").Options);

    [Fact]
    public void SignedContracts_ForLots_FiltersSignedContractAndSignedVersion_InDatabase()
    {
        using var db = CreateContext();

        var sql = CommissionRateQueries.SignedContracts(db, [7, 9]).ToQueryString();

        Assert.Contains("\"LotContractVersions\"", sql);
        Assert.Contains("\"Status\" = 'Signed'", sql);
        Assert.Contains("\"SignedAtUtc\" IS NOT NULL", sql);
        Assert.Contains("\"ParkingLotId\" = ANY (", sql);                    // lọc bãi ở database, không kéo cả bảng về
        Assert.DoesNotContain("\"FileKey\"", sql);                           // Select đúng cột cần, không đọc file / thông tin ngân hàng
        Assert.DoesNotContain("\"BankAccountNumber\"", sql);
    }

    [Fact]
    public void SignedContracts_AllLots_HasNoLotFilter()
    {
        using var db = CreateContext();
        Assert.DoesNotContain("ANY (", CommissionRateQueries.SignedContracts(db, null).ToQueryString());
    }
}
