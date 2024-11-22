using Microsoft.EntityFrameworkCore;

namespace LoginRegistration.Models
{
    public class LoginDbContext : DbContext
    {
        public LoginDbContext(DbContextOptions<LoginDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
    }
}
