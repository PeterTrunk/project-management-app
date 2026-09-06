using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using ProjectManager.API.Common.Exceptions;
using ProjectManager.API.Common.Options;
using ProjectManager.API.Common.Security;
using ProjectManager.API.Data;
using ProjectManager.API.DTOs.Auth;
using ProjectManager.API.Model;
using ProjectManager.API.Services.CurrentUserService;
using ProjectManager.API.Services.EmailService;
using ProjectManager.API.Services.RateLimit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
        private readonly JwtOptions _jwtOptions;
        private readonly IHttpContextAccessor _httpContextAccessor;

        //Explicit work factor: enélkül a könyvtár alapértelmezettjét használnánk, amely egy csomagfrissítéssel csendben megváltozhat.
        //A Verify a hash-ből olvassa ki a faktort, ezért a korábban mentett jelszavak továbbra is érvényesek maradnak.
        private const int BcryptWorkFactor = 12;

        //A nem létező email ágán is le kell futnia egy BCrypt ellenőrzésnek,
        //különben a válaszidő elárulja, létezik-e a fiók. Egyszer számoljuk ki, induláskor.
        private static readonly string DummyPasswordHash =
            BCrypt.Net.BCrypt.HashPassword("timing-equalization-placeholder", BcryptWorkFactor);

        public AuthService(
            AppDbContext context, 
            ICurrentUserService currentUserService, 
            IEmailService emailService,
            IRateLimitService rateLimitService,
            ILogger<AuthService> logger,
            IOptions<JwtOptions> jwtOptions,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _currentUserService = currentUserService;
            _emailService = emailService;
            _rateLimitService = rateLimitService;
            _logger = logger;
            _jwtOptions = jwtOptions.Value;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetIpAddress() =>
            _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        //Az email cím nem kerülhet nyersen a naplóba. A JWT titokkal kulcsolt HMAC determinisztikus,
        //tehát a bejegyzések továbbra is összefűzhetők egy fiókra, de a cím a napló birtokában sem állítható vissza.
        private string EmailRef(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "unknown";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
            return Convert.ToHexString(hash)[..12].ToLowerInvariant();
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            //Rate limiting
            var ipAddress = GetIpAddress();
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"login:{ipAddress}:{dto.Email}", 5, TimeSpan.FromMinutes(15));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve bejelentkezésnél | EmailRef: {EmailRef}", EmailRef(dto.Email));
                throw new RateLimitException($"Meghaladtad a maximális bejelentkezési kísérletek számát! Próbáld újra {retryAfter} másodperc múlva!");
            }

            //DB ellenörzése
            var user = await _context.Users.FirstOrDefaultAsync(user => user.Email == dto.Email);

            //A Verify akkor is lefut, ha nincs ilyen felhasználó: így a válaszidő nem árulja el a fiók létezését
            var isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user?.PasswordHash ?? DummyPasswordHash);

            if (user == null || !isPasswordValid)
            {
                _logger.LogWarning("Sikertelen bejelentkezés | EmailRef: {EmailRef}", EmailRef(dto.Email));
                throw new Exception("Hibás email vagy jelszó!");
            }

            //Letiltott fiók nem léphet be - a hibaüzenet szándékosan általános
            if (!user.IsActive)
            {
                _logger.LogWarning("Bejelentkezési kísérlet letiltott fiókkal | UserId: {UserId}", user.Id);
                throw new Exception("Hibás email vagy jelszó!");
            }

            //Ha TOTP aktív akkor ne adjunk JWT-t, csak jelezzük hogy kell a TOTP token
            if (user.IsTotpEnabled)
            {
                _logger.LogInformation("TOTP szükséges | UserId: {UserId}", user.Id);
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
                Token = SecureTokenGenerator.Generate(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.RefreshTokenLifetimeMinutes),
                RememberMe = dto.RememberMe
            };

            await _context.RefreshTokens.AddAsync(refreshTokenEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sikeres bejelentkezés | UserId: {UserId}", user.Id);

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                RefreshToken = refreshTokenEntry.Token,
                RememberMe = refreshTokenEntry.RememberMe
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
                Encoding.UTF8.GetBytes(_jwtOptions.Secret));

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, string ipAddress)
        {
            //A rate limit MINDEN ág előtt fut: ha a foglaltság-ellenőrzés előzné meg,
            //a támadó a számláló növelése nélkül tesztelhetne tetszőleges címeket
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"register:{ipAddress}", 5, TimeSpan.FromHours(1));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve regisztrációnál | IP: {IpAddress}", ipAddress);
                throw new RateLimitException($"Túl sok regisztrációs kísérlet. Próbáld újra {retryAfter} másodperc múlva!");
            }

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                _logger.LogWarning("Regisztrációs kísérlet foglalt email-lel | EmailRef: {EmailRef}", EmailRef(dto.Email));
                throw new Exception("Ez az email már foglalt!");
            }


            User user = new User();
            user.Email = dto.Email;
            user.DisplayName = dto.DisplayName;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, BcryptWorkFactor);

            //A felvétel DB-be + mentés
            await _context.AddAsync(user);
            await _context.SaveChangesAsync();

            //Token előállítás
            var token = CreateToken(user);

            //Refresh Token
            var refreshTokenEntry = new RefreshToken
            {
                Token = SecureTokenGenerator.Generate(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
            };

            await _context.RefreshTokens.AddAsync(refreshTokenEntry);

            var verificationToken = SecureTokenGenerator.Generate();
            user.EmailVerificationToken = verificationToken;
            await _context.SaveChangesAsync();

            await _emailService.SendEmailVerificationAsync(user.Email, user.DisplayName, verificationToken);

            _logger.LogInformation("Sikeres regisztráció | UserId: {UserId}", user.Id);

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
            //Feltételes UPDATE, csak akkor állítja is_revoked = true-ra
            //ha is_revoked még false, ettől atomikus: nem lehet race condition!
            var rowsAffected = await _context.RefreshTokens
                .Where(rf => rf.Token == refreshToken && !rf.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(rf => rf.IsRevoked, true));

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Visszavont vagy nem létező refresh token használata");
                throw new Exception("Token nem található vagy már felhasználva!");
            }

            //Token adatainak lekérése
            var refreshTokenEntry = await _context.RefreshTokens
                .FirstOrDefaultAsync(rf => rf.Token == refreshToken);

            if (refreshTokenEntry!.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Lejárt refresh token használata | UserId: {UserId}", refreshTokenEntry.UserId);
                throw new Exception("Token lejárt!");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == refreshTokenEntry.UserId);

            if (user == null)
                throw new Exception("Felhasználó nem található!");

            //Letiltott fiók a meglévő refresh tokenjével sem újíthat meg munkamenetet
            if (!user.IsActive)
            {
                _logger.LogWarning("Token megújítási kísérlet letiltott fiókkal | UserId: {UserId}", user.Id);
                throw new Exception("Token nem található vagy már felhasználva!");
            }

            var accessToken = CreateToken(user);

            var newRefreshTokenEntry = new RefreshToken
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.RefreshTokenLifetimeMinutes),
                Token = SecureTokenGenerator.Generate(),
                UserId = user.Id,
                RememberMe = refreshTokenEntry.RememberMe
            };

            await _context.RefreshTokens.AddAsync(newRefreshTokenEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Token megújítva | UserId: {UserId}", user.Id);

            return new AuthResponseDto
            {
                DisplayName = user.DisplayName,
                Token = accessToken,
                Email = user.Email,
                UserId = user.Id,
                RefreshToken = newRefreshTokenEntry.Token,
                RememberMe = refreshTokenEntry.RememberMe
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

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, BcryptWorkFactor);
            await _context.SaveChangesAsync();

            //Minden meglévő munkamenet érvénytelenítése - a jelszóváltás célja pont az,
            //hogy egy esetleges támadó hozzáférése megszűnjön
            var revokedCount = await RevokeAllRefreshTokensAsync(userId);

            _logger.LogInformation(
                "Jelszó sikeresen megváltoztatva | UserId: {UserId} | Visszavont refresh tokenek: {RevokedCount}", userId, revokedCount);
        }

        //Egy felhasználó összes nem revoked refresh tokenjének revoke-olja.
        //Jelszóváltás, jelszó-visszaállítás és 2FA módosítás után kinyirja a munkameneteket.
        private async Task<int> RevokeAllRefreshTokensAsync(Guid userId)
        {
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.IsRevoked, true));
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

            //Aktív 2FA mellett a secret nem írható felül: különben egy eltulajdonított
            //munkamenettel kizárható a jogos tulajdonos a saját fiókjából új secret felülírással
            if (user.IsTotpEnabled)
            {
                _logger.LogWarning("2FA újrabeállítási kísérlet aktív 2FA mellett | UserId: {UserId}", userId);
                throw new InvalidOperationException(
                    "A kétfaktoros hitelesítés már aktív. Előbb kapcsold ki, majd állítsd be újra!");
            }

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

            //A 2FA bekapcsolása előtt nyitott munkamenetek még 2FA nélkül jöhettek létre,
            //ezeket is érvényteleníteni kell, különben a védelem megkerülhető marad egy régi kapcsolaton keresztül
            var revokedCount = await RevokeAllRefreshTokensAsync(userId);

            _logger.LogInformation(
                "TOTP sikeresen aktiválva | UserId: {UserId} | Visszavont refresh tokenek: {RevokedCount}", userId, revokedCount);
            return true;
        }

        public async Task DisableTotpAsync(DisableTotpDto dto)
        {
            var userId = _currentUserService.UserId;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new Exception("Felhasználó nem található!");

            //Ismételt hitelesítés: egy ellopott access token önmagában ne tudja
            //leszedni a fiókról a második faktort
            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Sikertelen 2FA kikapcsolás - hibás jelszó | UserId: {UserId}", userId);
                throw new UnauthorizedAccessException("Hibás jelszó!");
            }

            user.TotpSecret = null;
            user.IsTotpEnabled = false;
            await _context.SaveChangesAsync();

            //A 2FA szintjének csökkentése után minden korábbi munkamenet érvénytelen
            var revokedCount = await RevokeAllRefreshTokensAsync(userId);

            _logger.LogInformation(
                "TOTP kikapcsolva | UserId: {UserId} | Visszavont refresh tokenek: {RevokedCount}", userId, revokedCount);
        }

        public async Task<AuthResponseDto> LoginWithTotpAsync(LoginWithTotpDto dto)
        {
            var ipAddress = GetIpAddress();
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"login:{ipAddress}:{dto.Email}", 5, TimeSpan.FromMinutes(15));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve TOTP bejelentkezésnél | EmailRef: {EmailRef}", EmailRef(dto.Email));
                throw new RateLimitException($"Meghaladtad a maximális bejelentkezési kísérletek számát! Próbáld újra {retryAfter} másodperc múlva!");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user?.PasswordHash ?? DummyPasswordHash);

            if (user == null || !isPasswordValid)
            {
                _logger.LogWarning("Sikertelen TOTP bejelentkezés - hibás jelszó | EmailRef: {EmailRef}", EmailRef(dto.Email));
                throw new Exception("Hibás email vagy jelszó!");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("TOTP bejelentkezési kísérlet letiltott fiókkal | UserId: {UserId}", user.Id);
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
                _logger.LogWarning("Sikertelen TOTP bejelentkezés - érvénytelen token | UserId: {UserId}", user.Id);
                throw new Exception("Érvénytelen TOTP token!");
            } 

            var token = CreateToken(user);

            var refreshTokenEntry = new RefreshToken
            {
                Token = SecureTokenGenerator.Generate(),
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.RefreshTokenLifetimeMinutes),
                RememberMe = dto.RememberMe
            };

            await _context.RefreshTokens.AddAsync(refreshTokenEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sikeres TOTP bejelentkezés | UserId: {UserId}", user.Id);

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                RefreshToken = refreshTokenEntry.Token,
                IsTotpEnabled = true,
                RememberMe = refreshTokenEntry.RememberMe
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
            //Enélkül a végpont email-bombázásra és a levélküldő kvóta kimerítésére használható,
            //utóbbi a jelszó-visszaállító leveleket is megbénítaná
            var ipAddress = GetIpAddress();
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"resend_verification:{ipAddress}:{email}", 3, TimeSpan.FromHours(1));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve verifikációs email újraküldésnél | EmailRef: {EmailRef}", EmailRef(email));
                throw new RateLimitException($"Túl sok kérés. Próbáld újra {retryAfter} másodperc múlva!");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                throw new Exception("Felhasználó nem található!");
            if (user.IsEmailVerified)
                throw new Exception("Az email cím már van megerősítve!");

            var verificationToken = SecureTokenGenerator.Generate();
            user.EmailVerificationToken = verificationToken;
            await _context.SaveChangesAsync();

            await _emailService.SendEmailVerificationAsync(user.Email, user.DisplayName, verificationToken);
        }

        public async Task ForgotPasswordAsync(string email)
        {
            //A kulcs IP-t is tartalmaz: csak email alapján bárki kizárhatna bárkit a jelszó-visszaállításból
            var ipAddress = GetIpAddress();
            var (isLimited, retryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"forgot_password:{ipAddress}:{email}", 3, TimeSpan.FromHours(1));
            if (isLimited)
            {
                _logger.LogWarning("Rate limit elérve jelszó visszaállításnál | EmailRef: {EmailRef}", EmailRef(email));
                throw new RateLimitException($"Meghaladtad a maximális jelszó változtatási kisérletek számát!. Próbáld újra {retryAfter} másodperc múlva!");
            }

            //Tágabb, csak IP-alapú limit a sok címre szórt támadás ellen
            var (isIpLimited, ipRetryAfter) = await _rateLimitService
                .IsRateLimitedAsync($"forgot_password_ip:{ipAddress}", 15, TimeSpan.FromHours(1));
            if (isIpLimited)
            {
                _logger.LogWarning("IP szintű rate limit elérve jelszó visszaállításnál | IP: {IpAddress}", ipAddress);
                throw new RateLimitException($"Túl sok kérés. Próbáld újra {ipRetryAfter} másodperc múlva!");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            //Biztonsági okokból ne jelezzük ha nem létezik a user
            if (user == null)
            {
                _logger.LogInformation("Jelszó visszaállítás nem létező email-re | EmailRef: {EmailRef}", EmailRef(email));
                return;
            }
            //Régi tokenek érvénytelenítése
            var oldTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed)
                .ToListAsync();
            foreach (var oldToken in oldTokens)
                oldToken.IsUsed = true;

            var token = SecureTokenGenerator.Generate();
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

            resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, BcryptWorkFactor);
            resetToken.IsUsed = true;
            await _context.SaveChangesAsync();

            //A visszaállítás gyakran épp fiókátvétel utáni történhet - a támadó
            //munkamenetét revoke-oljuk
            var revokedCount = await RevokeAllRefreshTokensAsync(resetToken.UserId);

            _logger.LogInformation(
                "Jelszó sikeresen visszaállítva | UserId: {UserId} | Visszavont refresh tokenek: {RevokedCount}",
                resetToken.UserId, revokedCount);
        }
    }
}
