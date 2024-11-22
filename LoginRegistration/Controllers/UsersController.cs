using LoginRegistration.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LoginRegistration.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly LoginDbContext _context;
        private readonly IConfiguration _configuration;
        public UsersController(LoginDbContext dbcontext, IConfiguration configuration)
        {
            _context = dbcontext;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] UserDTO userDto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == userDto.Email))
                return BadRequest("Email already in use.");

            // Create a new User entity
            var user = new User
            {
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Email = userDto.Email,
                CreatedAt = DateTime.UtcNow // Automatically set creation timestamp
            };

            // Hash password
            using var hmac = new HMACSHA512();
            user.Password = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(userDto.Password)));

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok("Registration successful!");
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login(LoginDTO loginDto)
        {
            // Find the user by email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email && u.Password==loginDto.Password);
            if(user != null)
            {
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, _configuration["Jwt:Subject"]),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("UserId", user.UserId.ToString()),
                    new Claim("Email", user.Email.ToString())
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
                var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var token = new JwtSecurityToken(
                    _configuration["Jwt: Issuer"],
                    _configuration["Jwt:Audience"],
                    claims,
                    expires: DateTime.UtcNow.AddMinutes(60),
                    signingCredentials: signIn
                    );
                string tokenValue = new JwtSecurityTokenHandler().WriteToken(token);

                user.Password = null; 
                return Ok(new {Token = tokenValue, User = user});
                //return Ok(user);
            }
            if (user == null)
                return Unauthorized("Invalid email or password.");

            // Verify the password
            using var hmac = new HMACSHA512();
            var computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password)));

            if (computedHash != user.Password)
                return Unauthorized("Invalid email or password.");
            if(user == null)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 3)
                {
                    user.IsLocked = true;
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                }
                _context.SaveChanges();
                return Unauthorized("Invalid credentials or account locked.");
            }
            if (user.IsLocked && user.LockoutEnd > DateTime.Now)
                return Unauthorized("Account is locked. Try again later.");

            user.FailedLoginAttempts = 0;
            _context.SaveChanges();
            // Return a success response
            return Ok("Login successful!");
        }

        [HttpGet]
        [Route("GetUsers")]
        public async Task<IActionResult>GetUsers()
        {
            var users = await _context.Users
                .Select(user => new UserResponseDto
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    CreatedAt = user.CreatedAt,
                    IsLocked = user.IsLocked
                }).ToListAsync();

                return Ok(users);
        }

        [HttpGet]
        [Route("GetUserById")]
        public async Task<IActionResult>GetUserById(int Id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == Id);

            if (user == null)
                return NoContent();

            var userResponse = new UserResponseDto
            {
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                IsLocked = user.IsLocked
            };

            return Ok(userResponse);
        }


    }
}
