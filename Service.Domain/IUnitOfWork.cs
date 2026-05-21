using Service.Domain.IRepositories;
using Service.Shared.Commons.Interfaces.SQL;

namespace Service.Domain
{
    public interface IUnitOfWork : IBaseUnitOfWork
    {
        public IDiagnosisRepository DiagnosisRepository { get; }


    }
}
