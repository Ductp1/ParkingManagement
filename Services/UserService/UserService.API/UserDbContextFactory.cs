using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using UserService.Infrastructure.Persistence;

namespace UserService.API;

// Migration không cần khởi động API, nạp khóa JWT hay kết nối dịch vụ bên ngoài.
public sealed class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ServiceDb")
            ?? "Host=localhost;Database=pm_user;Username=postgres";
        return new UserDbContext(new DbContextOptionsBuilder<UserDbContext>().UseNpgsql(connection).Options);
    }
}
