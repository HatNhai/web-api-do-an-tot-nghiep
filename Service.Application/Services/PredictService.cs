// "Một sản phẩm từ đồ án tốt nghiệp"

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Service.Application.Interfaces;
using Service.Domain;
using Service.Domain.Entities;
using Service.Domain.IRepositories;
using Service.Share.Contract.Dtos;
using Service.Share.Contract.Queries;
using Service.Shared.Commons.Model.Commons;

namespace Service.Application.Services
{
    public class PredictService : IPredictService
    {
        private readonly IUnitOfWork _UnitOfWork;
        private readonly IMlPredictorService _mlPredictor;
        private readonly IWebHostEnvironment _env;

        public PredictService(IUnitOfWork unitOfWork, IMlPredictorService mlPredictor, IWebHostEnvironment env)
        {
            _UnitOfWork = unitOfWork;
            _mlPredictor = mlPredictor;
            _env = env;
        }

        public async Task<DataTableJson> GetPaged(DiagnosisQuery searchOption)
        {
            try
            {
                var (items, total) = await _UnitOfWork.DiagnosisRepository.GetPagedDtoAsync(searchOption);


                return new DataTableJson(items,
                                             searchOption.draw,
                                             total,
                                             items.Count);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        public async Task<DiagnosisDto> PredictAsync(IFormFile imageFile, string? notes = null)
        {
            if (imageFile == null || imageFile.Length == 0)
                throw new ArgumentException("Vui lòng chọn file ảnh.");

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await imageFile.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            var mlResult = _mlPredictor.Predict(imageBytes);

            var savedFileName = await SaveImageToDisk(imageBytes, imageFile.FileName);

            var diagnosis = new Diagnosis
            {
                Id = Guid.NewGuid(),
                SeverityLevel = (SeverityLevel)mlResult.SeverityLevel,
                SeverityRatio = mlResult.SeverityRatio,
                Confidence = mlResult.Confidence,
                ModelUsed = mlResult.ModelVersion,
                ImageFileName = imageFile.FileName,
                ImagePath = $"/uploads/diagnoses/{savedFileName}",
                DiagnosedAt = DateTime.UtcNow,
                Notes = notes
            };

            await _UnitOfWork.DiagnosisRepository.AddAsync(diagnosis);

            return new DiagnosisDto
            {
                Id = diagnosis.Id,
                SeverityLevel = (int)diagnosis.SeverityLevel,
                SeverityLevelName = diagnosis.SeverityLevel.ToString(),
                SeverityRatio = diagnosis.SeverityRatio,
                Confidence = diagnosis.Confidence,
                ModelUsed = diagnosis.ModelUsed,
                ImageFileName = diagnosis.ImageFileName,
                ImagePath = diagnosis.ImagePath,
                DiagnosedAt = diagnosis.DiagnosedAt,
                Notes = diagnosis.Notes
            };
        }

        private async Task<string> SaveImageToDisk(byte[] bytes, string originalFileName)
        {
            var uploadsFolder = Path.Combine(
                _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"),
                "uploads", "diagnoses");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var savedFileName = $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";
            await File.WriteAllBytesAsync(Path.Combine(uploadsFolder, savedFileName), bytes);
            return savedFileName;
        }

    }
}