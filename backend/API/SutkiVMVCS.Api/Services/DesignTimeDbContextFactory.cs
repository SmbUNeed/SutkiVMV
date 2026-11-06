using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SutkiVMV.Data;
using SutkiVMVCS.Api.Services.Security;

namespace SutkiVMVCS.Services
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(config.GetConnectionString("DefaultConnection"))
                .Options;

            var dpProvider = DataProtectionProvider.Create("DesignTime");
            var encryptor = new FieldEncryptor(dpProvider);

            return new AppDbContext(options, encryptor);
        }
    }
}
