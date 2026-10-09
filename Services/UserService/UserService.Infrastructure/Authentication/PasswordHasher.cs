using BCrypt.Net;
using UserService.Application.Interfaces;

namespace UserService.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    // Bảng công việc Jira yêu cầu cost 12
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
