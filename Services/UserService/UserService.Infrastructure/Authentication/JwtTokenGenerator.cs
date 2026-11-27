using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ParkingManagement.SharedKernel.Enums;

namespace UserService.Infrastructure.Authentication;

public class JwtTokenGenerator(IConfiguration configuration, IHostEnvironment environment, TimeProvider timeProvider) : IJwtTokenGenerator
{
    private const string Issuer = "ParkingManagement";
    private const string Audience = "ParkingManagement.Clients";

    public string GenerateToken(User user, IEnumerable<string> roles, string? sessionHash = null)
    {
        var activeAssignments = user.StaffAssignments.Where(s => s.IsActive && !s.OwnerProfile.IsLocked
            && s.OwnerProfile.Status == OwnerStatus.Active).ToArray();
        var effectiveRoles = roles.Where(role => role switch
        {
            "Staff" => activeAssignments.Select(s => s.OwnerProfileId).Distinct().Count() == 1,
            "LotOwner" => user.OwnerProfile is { IsLocked: false, Status: OwnerStatus.Active },
            _ => true
        }).ToArray();
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (sessionHash is not null) claims.Add(new Claim("sid", sessionHash));

        if (user.OwnerProfile != null && effectiveRoles.Contains("LotOwner"))
        {
            claims.Add(new Claim("OwnerProfileId", user.OwnerProfile.Id.ToString()));
        }
        else if (effectiveRoles.Contains("Staff"))
        {
            var tenants = activeAssignments
                .Select(s => s.OwnerProfileId).Distinct().ToArray();
            if (tenants.Length == 1) claims.Add(new Claim("OwnerProfileId", tenants[0].ToString()));
            foreach (var lot in activeAssignments.Select(s => s.ParkingLotId).Distinct())
                claims.Add(new Claim("ParkingLotId", lot.ToString()));
        }

        foreach (var role in effectiveRoles)
        {
            claims.Add(new Claim("role", role));
        }

        using var rsa = RSA.Create();
        var privateKeyPath = Path.GetFullPath(configuration["Jwt:PrivateKeyPath"] ?? "Keys/private.key", environment.ContentRootPath);
        if (!File.Exists(privateKeyPath))
            throw new InvalidOperationException($"Thiếu private key JWT: {privateKeyPath}.");
        rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
        if (rsa.KeySize < 2048) throw new InvalidOperationException("Khóa RSA phải có ít nhất 2048 bit.");
        // Không chấp nhận file chỉ chứa public key để ký token.
        rsa.ExportParameters(true);
        var publicKeyPath = Path.GetFullPath(configuration["Jwt:PublicKeyPath"] ?? "Keys/public.key", environment.ContentRootPath);
        if (!File.Exists(publicKeyPath))
            throw new InvalidOperationException($"Thiếu public key JWT: {publicKeyPath}.");
        using var verifier = RSA.Create();
        verifier.ImportFromPem(File.ReadAllText(publicKeyPath));
        if (!rsa.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(verifier.ExportSubjectPublicKeyInfo()))
            throw new InvalidOperationException("Public key và private key JWT không thuộc cùng một cặp khóa.");
        
        // RSA chỉ sống trong lần gọi này; không cache signer giữ tham chiếu sau khi RSA bị dispose.
        var key = new RsaSecurityKey(rsa) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddHours(24), // JWT RS256 24h
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
