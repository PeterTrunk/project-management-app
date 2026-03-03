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

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName
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

            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            //A felvétel DB-be + mentés
            await _context.AddAsync(user);
            await _context.SaveChangesAsync();

            //Token előállítás
            var token = CreateToken(user);

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName
            };
        }
    }
}
