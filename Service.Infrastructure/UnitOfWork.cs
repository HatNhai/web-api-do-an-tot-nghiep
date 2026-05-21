using Service.Domain;
using Service.Domain.IRepositories;
using Service.Infrastructure.Persistence;
using Service.Infrastructure.Repositories;
using Service.Shared.Commons.Interfaces.Extentions;
using Service.Shared.Infrastructures.MSSQL.Repositories.Bases;

namespace Service.Infrastructure
{
    public class UnitOfWork : BaseUnitOfWork<AppDbContext>, IUnitOfWork
    {
        private AppDbContext _context;
        private IDiagnosisRepository? _diagnosisRepository;
        public UnitOfWork(AppDbContext context, IRequestContext requestContext) : base(context, requestContext)
        {
            _context = context;
        }

        public IDiagnosisRepository DiagnosisRepository => _diagnosisRepository ??= new DiagnosisRepository(_context);
    }
}
