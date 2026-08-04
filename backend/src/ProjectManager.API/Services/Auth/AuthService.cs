using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Model;
using ProjectManager.API.Services.CurrentUserService;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ProjectManager.API.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;


        public AuthService(AppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {   
            //Db ellenörzése
            var user = await _context.Users.FirstOrDefaultAsync(user => user.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                throw new Exception("Hibás email vagy jelszó!");
            }

            //Ha TOTP aktív akkor ne adjunk JWT-t, csak jelezzük hogy kell a TOTP token
            if (user.IsTotpEnabled)
            {
                return new AuthResponseDto
                {
                    RequiresTotp = true,
                    Email = user.Email,
                    UserId = user.Id,
                    DisplayName = user.DisplayName
                };
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
            Encoding.UTF8.GetBytes(
                Environment.GetEnvironmentVariable("JWT_SECRET")!));
            //Token összerakása
            var token = new JwtSecurityToken(
                issuer: Environment.GetEnvironmentVariable("JWT_ISSUER"),
                audience: Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
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

        public async Task<TotpSetupResponseDto> SetupTotpAsync()
        {
            var userId = _currentUserService.UserId;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

            //Random 20 byte-os secret generálás
            var secretKey = KeyGeneration.GenerateRandomKey(20);
            var base32Secret = Base32Encoding.ToString(secretKey);

            //Ideiglenesen tároljuk (még nincs aktiválva)
            user.TotpSecret = base32Secret;
            await _context.SaveChangesAsync();

            //otpauth:// URI generálás Google Authenticator számára
            var otpAuthUri = $"otpauth://totp/ProjectManager:{Uri.EscapeDataString(user.Email)}" +
                             $"?secret={base32Secret}&issuer=ProjectManager&algorithm=SHA1&digits=6&period=30";

            return new TotpSetupResponseDto
            {
                SecretKey = base32Secret,
                OtpAuthUri = otpAuthUri
            };
        }

        public async Task<bool> VerifyAndEnableTotpAsync(string token)
        {
            var userId = _currentUserService.UserId;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található!");
            if (string.IsNullOrEmpty(user.TotpSecret))
                throw new Exception("TOTP nincs beállítva!");

            var secretKey = Base32Encoding.ToBytes(user.TotpSecret);
            var totp = new Totp(secretKey);

            var isValid = totp.VerifyTotp(
                token,
                out _,
                VerificationWindow.RfcSpecifiedNetworkDelay
            );

            if (!isValid)
                return false;

            user.IsTotpEnabled = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task DisableTotpAsync()
        {
            var userId = _currentUserService.UserId;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

            user.TotpSecret = null;
            user.IsTotpEnabled = false;
            await _context.SaveChangesAsync();
        }

        public async Task<AuthResponseDto> LoginWithTotpAsync(LoginWithTotpDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new Exception("Hibás email vagy jelszó!");

            if (!user.IsTotpEnabled || string.IsNullOrEmpty(user.TotpSecret))
                throw new Exception("TOTP nincs bekapcsolva ennél a felhasználónál!");

            var secretKey = Base32Encoding.ToBytes(user.TotpSecret);
            var totp = new Totp(secretKey);

            var isValid = totp.VerifyTotp(
                dto.TotpToken,
                out _,
                VerificationWindow.RfcSpecifiedNetworkDelay
            );

            if (!isValid)
                throw new Exception("Érvénytelen TOTP token!");

            var token = CreateToken(user);

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
                RefreshToken = refreshTokenEntry.Token,
                IsTotpEnabled = true
            };
        }

        public async Task<bool> IsTotpRequiredAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            return user?.IsTotpEnabled ?? false;
        }
    }
}
