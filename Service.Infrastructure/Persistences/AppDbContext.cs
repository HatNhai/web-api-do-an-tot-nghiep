using Microsoft.EntityFrameworkCore;
using Service.Domain.Entities;
using Service.Shared.Infrastructures.MSSQL.Repositories.Bases;

namespace Service.Infrastructure.Persistence
{
    public class AppDbContext : AuditableDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Bảng lưu các lần dự đoán bệnh lá cây.
        /// </summary>
        public DbSet<Diagnosis> Diagnoses { set; get; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            base.OnModelCreating(builder);
        }
    }
}