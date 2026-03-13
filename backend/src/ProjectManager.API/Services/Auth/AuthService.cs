using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Model;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ProjectManager.API.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {   
            //Db ellenörzése
            var user = await _context.Users.FirstOrDefaultAsync(user => user.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                throw new Exception("Hibás email vagy jelszó!");
            }

            //Token előállítás
            var token = CreateToken(user);

            //Refresh Token
            var refreshTokenEntry = new RefreshToken
            {
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            await _context.RefreshTokens.AddAsync(refreshTokenEntry);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                RefreshToken = refreshTokenEntry.Token
            };
        }
        private string CreateToken(User user)
        {
            //Claims: A token tartalma
            var claims = new[]
            {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.DisplayName)
            };
            //Aláíró kulcs
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            //Token összerakása
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            if(await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                throw new Exception("Ez az email már foglalt!");
            }

            User user = new User();
            user.Email = dto.Email;
            user.DisplayName = dto.DisplayName;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            //A felvétel DB-be + mentés
            await _context.AddAsync(user);
            await _context.SaveChangesAsync();

            //Token előállítás
            var token = CreateToken(user);

            //Refresh Token
            var refreshTokenEntry = new RefreshToken
            {
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            await _context.RefreshTokens.AddAsync(refreshTokenEntry);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                RefreshToken = refreshTokenEntry.Token
            };
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var refreshTokenEntry = await _context.RefreshTokens.FirstOrDefaultAsync(rf =>
                rf.Token == refreshToken);

            if (refreshTokenEntry == null)
                throw new Exception("Token nem található!");
            if (refreshTokenEntry.IsRevoked)
                throw new Exception("Token felfüggesztve!");
            if (refreshTokenEntry.ExpiresAt < DateTime.UtcNow)
                throw new Exception("Token lejárt!");

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Id == refreshTokenEntry.UserId);

            if (user == null)
                throw new Exception("Felhasználó nem található");

            var accessToken = CreateToken(user);

            RefreshToken newRefreshTokenEntry = new RefreshToken
            {
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
            };

            //Egy Transaction: - Token rotation
            refreshTokenEntry.IsRevoked = true;
            await _context.RefreshTokens.AddAsync(newRefreshTokenEntry);
            await _context.SaveChangesAsync();
            
            return new AuthResponseDto
            {
                DisplayName = user.DisplayName,
                Token = accessToken,
                Email = user.Email,
                UserId = user.Id,
                RefreshToken = newRefreshTokenEntry.Token
            };
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var refreshTokenEntry = await _context.RefreshTokens.FirstOrDefaultAsync(rf =>
                rf.Token == refreshToken);

            if (refreshTokenEntry == null)
                throw new Exception("Token nem található!");

            refreshTokenEntry.IsRevoked = true;
            await _context.SaveChangesAsync();
        }

        public async Task<UserProfileDto> MeAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(user => user.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található");

            return new UserProfileDto
            {
                DisplayName = user.DisplayName,
                Email = user.Email,
                UserId = user.Id
            };
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(user => user.Id == userId);

            if (user == null)
                throw new Exception("Felhasználó nem található");
            
            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                throw new Exception("Hibás jelenlegi jelszó!");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();
        }

        public async Task<UserProfileDto> ChangeUserProfileAsync(Guid userId, UpdateProfileDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(user => user.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található");

            user.DisplayName = dto.DisplayName;
            await _context.SaveChangesAsync();
            return new UserProfileDto
            {
                DisplayName = user.DisplayName,
                Email = user.Email,
                UserId = user.Id
            };
        }
    }
}
