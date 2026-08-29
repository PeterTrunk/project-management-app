using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Model;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.EmailService;
using ProjectManager.API.Services.RateLimit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ProjectManager.API.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IEmailService _emailService;
        private readonly IRateLimitService _rateLimitService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext context, 
            ICurrentUserService currentUserService, 
            IEmailService emailService,
            IRateLimitService rateLimitService,
            ILogger<AuthService> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _emailService = emailService;
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            //Rate limiting
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"login:{dto.Email}", 5, TimeSpan.FromMinutes(15));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve bejelentkezésnél | Email: {Email}", dto.Email);
                throw new RateLimitException($"Meghaladtad a maximális bejelentkezési kísérletek számát! Próbáld újra {retryAfter} másodperc múlva!");
            }
                

            //Db ellenörzése
            var user = await _context.Users.FirstOrDefaultAsync(user => user.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                _logger.LogWarning("Sikertelen bejelentkezés | Email: {Email}", dto.Email);
                throw new Exception("Hibás email vagy jelszó!");
            }

            //Ha TOTP aktív akkor ne adjunk JWT-t, csak jelezzük hogy kell a TOTP token
            if (user.IsTotpEnabled)
            {
                _logger.LogInformation("TOTP szükséges | Email: {Email}", user.Email);
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

            var refreshMinutes = int.Parse(
                Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_LIFETIME")!);

            //Refresh Token
            var refreshTokenEntry = new RefreshToken
            {
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(refreshMinutes)
            };

            await _context.RefreshTokens.AddAsync(refreshTokenEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sikeres bejelentkezés | Email: {Email}", user.Email);

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

            var expiryMinutes = int.Parse(
                Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES")!);

            //Token összerakása
            var token = new JwtSecurityToken(
                issuer: Environment.GetEnvironmentVariable("JWT_ISSUER"),
                audience: Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, string ipAddress)
        {
            if(await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                _logger.LogWarning("Regisztrációs kísérlet foglalt email-lel | Email: {Email}", dto.Email);
                throw new Exception("Ez az email már foglalt!");
            }

            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"register:{ipAddress}", 5, TimeSpan.FromHours(1));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve regisztrációnál | IP: {IpAddress}", ipAddress);
                throw new RateLimitException($"Túl sok regisztrációs kísérlet. Próbáld újra {retryAfter} másodperc múlva!");
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

            var verificationToken = Guid.NewGuid().ToString("N");
            user.EmailVerificationToken = verificationToken;
            await _context.SaveChangesAsync();

            await _emailService.SendEmailVerificationAsync(user.Email, user.DisplayName, verificationToken);

            _logger.LogInformation("Sikeres regisztráció | Email: {Email}", user.Email);

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
            {
                _logger.LogWarning("Érvénytelen refresh token használata - token nem található");
                throw new Exception("Token nem található!");
            }
                
            if (refreshTokenEntry.IsRevoked)
            {
                _logger.LogWarning("Visszavont refresh token használata | UserId: {UserId}", refreshTokenEntry.UserId);
                throw new Exception("Token felfüggesztve!");
            }
                
            if (refreshTokenEntry.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Lejárt refresh token használata | UserId: {UserId}", refreshTokenEntry.UserId);
                throw new Exception("Token lejárt!");
            }
               

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

            _logger.LogInformation("Token megújítva | UserId: {UserId}", user.Id);

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
            {
                _logger.LogWarning("Kijelentkezési kísérlet érvénytelen tokennel");
                throw new Exception("Token nem található!");
            }

            refreshTokenEntry.IsRevoked = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sikeres kijelentkezés | UserId: {UserId}", refreshTokenEntry.UserId);
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
                UserId = user.Id,
                IsTotpEnabled = user.IsTotpEnabled,
                IsEmailVerified = user.IsEmailVerified
            };
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(user => user.Id == userId);

            if (user == null)
                throw new Exception("Felhasználó nem található");
            
            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Sikertelen jelszóváltoztatás - hibás jelenlegi jelszó | UserId: {UserId}", userId);
                throw new Exception("Hibás jelenlegi jelszó!");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Jelszó sikeresen megváltoztatva | UserId: {UserId}", userId);
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
            {
                _logger.LogWarning("Sikertelen TOTP aktiválás - érvénytelen token | UserId: {UserId}", userId);
                return false;
            }
                

            user.IsTotpEnabled = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("TOTP sikeresen aktiválva | UserId: {UserId}", userId);
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

            _logger.LogInformation("TOTP kikapcsolva | UserId: {UserId}", userId);
        }

        public async Task<AuthResponseDto> LoginWithTotpAsync(LoginWithTotpDto dto)
        {
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"login:{dto.Email}", 5, TimeSpan.FromMinutes(15));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve TOTP bejelentkezésnél | Email: {Email}", dto.Email);
                throw new RateLimitException($"Meghaladtad a maximális bejelentkezési kísérletek számát! Próbáld újra {retryAfter} másodperc múlva!");
            }   

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                _logger.LogWarning("Sikertelen TOTP bejelentkezés - hibás jelszó | Email: {Email}", dto.Email);
                throw new Exception("Hibás email vagy jelszó!");
            }

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
            {
                _logger.LogWarning("Sikertelen TOTP bejelentkezés - érvénytelen token | Email: {Email}", dto.Email);
                throw new Exception("Érvénytelen TOTP token!");
            } 

            var token = CreateToken(user);

            var refreshTokenEntry = new RefreshToken
            {
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            await _context.RefreshTokens.AddAsync(refreshTokenEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sikeres TOTP bejelentkezés | Email: {Email}", user.Email);

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
        
        public async Task VerifyEmailAsync(string token)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
            if (user == null)
                throw new Exception("Érvénytelen vagy lejárt token!");

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            await _context.SaveChangesAsync();
        }

        public async Task ResendVerificationEmailAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                throw new Exception("Felhasználó nem található!");
            if (user.IsEmailVerified)
                throw new Exception("Az email cím már van megerősítve!");

            var verificationToken = Guid.NewGuid().ToString("N");
            user.EmailVerificationToken = verificationToken;
            await _context.SaveChangesAsync();

            await _emailService.SendEmailVerificationAsync(user.Email, user.DisplayName, verificationToken);
        }

        public async Task ForgotPasswordAsync(string email)
        {
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"forgot_password:{email}", 3, TimeSpan.FromHours(1));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve jelszó visszaállításnál | Email: {Email}", email);
                throw new RateLimitException($"Meghaladtad a maximális jelszó változtatási kisérletek számát!. Próbáld újra {retryAfter} másodperc múlva!");
            }
                

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            //Biztonsági okokból ne jelezzük ha nem létezik a user
            if (user == null)
            {
                _logger.LogInformation("Jelszó visszaállítás nem létező email-re | Email: {Email}", email);
                return;
            }
            //Régi tokenek érvénytelenítése
            var oldTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed)
                .ToListAsync();
            foreach (var oldToken in oldTokens)
                oldToken.IsUsed = true;

            var token = Guid.NewGuid().ToString("N");
            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };

            await _context.PasswordResetTokens.AddAsync(resetToken);
            await _context.SaveChangesAsync();

            await _emailService.SendPasswordResetAsync(user.Email, user.DisplayName, token);

            _logger.LogInformation("Jelszó visszaállítási email elküldve | UserId: {UserId}", user.Id);
        }

        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed);

            if (resetToken == null)
            {
                _logger.LogWarning("Érvénytelen jelszó visszaállítási token használata");
                throw new Exception("Érvénytelen vagy lejárt token!");
            }
                

            if (resetToken.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Lejárt jelszó visszaállítási token használata | UserId: {UserId}", resetToken.UserId);
                resetToken.IsUsed = true;
                await _context.SaveChangesAsync();
                throw new Exception("A token lejárt!");
            }

            resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            resetToken.IsUsed = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Jelszó sikeresen visszaállítva | UserId: {UserId}", resetToken.UserId);
        }
    }
}
