using Microsoft.AspNetCore.Http;
using Service.Domain.Entities;
using Service.Share.Contract.Dtos;
using Service.Share.Contract.Queries;
using Service.Shared.Commons.Interfaces.SQL;
namespace Service.Domain.IRepositories
{
    public interface IDiagnosisRepository : IRepository<Diagnosis>
    {
        Task<(List<DiagnosisDto> Items, int Total)> GetPagedDtoAsync(DiagnosisQuery query);

    }
}
