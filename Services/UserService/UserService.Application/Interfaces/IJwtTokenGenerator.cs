using UserService.Domain.Entities;

namespace UserService.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, IEnumerable<string> roles, string? sessionHash = null);
}
