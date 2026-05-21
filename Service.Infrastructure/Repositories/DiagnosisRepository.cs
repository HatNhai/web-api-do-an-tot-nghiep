using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Service.Domain.Entities;
using Service.Domain.IRepositories;
using Service.Infrastructure.Persistence;
using Service.Share.Contract.Dtos;
using Service.Share.Contract.Queries;
using Service.Shared.Infrastructures.MSSQL.Repositories.Bases;

namespace Service.Infrastructure.Repositories
{
    public class DiagnosisRepository
        : Repository<Diagnosis, AppDbContext>, Domain.IRepositories.IDiagnosisRepository
    {
        public DiagnosisRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<(List<DiagnosisDto> Items, int Total)> GetPagedDtoAsync(DiagnosisQuery query)
        {
            var queryable = _context.Set<Diagnosis>().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x =>
                    (x.ImageFileName != null && x.ImageFileName.ToLower().Contains(keyword)) ||
                    (x.Notes != null && x.Notes.ToLower().Contains(keyword)) ||
                    x.ModelUsed.ToLower().Contains(keyword));
            }

            if (query.SeverityLevel.HasValue)
            {
                queryable = queryable.Where(x => (int)x.SeverityLevel == query.SeverityLevel.Value);
            }

            if (query.FromDate.HasValue)
            {
                queryable = queryable.Where(x => x.DiagnosedAt >= query.FromDate.Value);
            }
            if (query.ToDate.HasValue)
            {
                queryable = queryable.Where(x => x.DiagnosedAt <= query.ToDate.Value);
            }

            var total = await queryable.CountAsync();

            var items = await queryable
                .OrderByDescending(x => x.DiagnosedAt)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => new DiagnosisDto
                {
                    Id = x.Id,
                    SeverityLevel = (int)x.SeverityLevel,
                    SeverityLevelName = x.SeverityLevel.ToString(),
                    SeverityRatio = x.SeverityRatio,
                    Confidence = x.Confidence,
                    ModelUsed = x.ModelUsed,
                    ImageFileName = x.ImageFileName,
                    ImagePath = x.ImagePath,
                    DiagnosedAt = x.DiagnosedAt,
                    Notes = x.Notes
                })
                .ToListAsync();

            return (items, total);
        }

        
    }



}