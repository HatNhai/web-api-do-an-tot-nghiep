// "Một sản phẩm từ đồ án tốt nghiệp"

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Service.Application.Interfaces;
using Service.Domain;
using Service.Domain.Entities;
using Service.Share.Contract.Dtos;
using Service.Share.Contract.Queries;
using Service.Shared.Commons.Model.Commons;
using System.Diagnostics;

namespace Service.Application.Services
{
    public class PredictService : IPredictService
    {
        private readonly IUnitOfWork _UnitOfWork;
        private readonly IMlPredictorService _mlPredictor;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PredictService> _logger;

        public PredictService(
            IUnitOfWork unitOfWork,
            IMlPredictorService mlPredictor,
            IWebHostEnvironment env,
            ILogger<PredictService> logger)
        {
            _UnitOfWork = unitOfWork;
            _mlPredictor = mlPredictor;
            _env = env;
            _logger = logger;
        }

        public async Task<DataTableJson> GetPaged(DiagnosisQuery searchOption)
        {
            try
            {
                _logger.LogDebug("GetPaged → truy vấn DB...");
                var (items, total) = await _UnitOfWork.DiagnosisRepository.GetPagedDtoAsync(searchOption);
                _logger.LogDebug("GetPaged ← DB trả về {Count}/{Total} bản ghi", items.Count, total);

                return new DataTableJson(items,
                                             searchOption.draw,
                                             total,
                                             items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi GetPaged");
                throw new Exception(ex.Message);
            }
        }


        public async Task<DiagnosisDto> PredictAsync(IFormFile imageFile, string? notes = null)
        {
            if (imageFile == null || imageFile.Length == 0)
                throw new ArgumentException("Vui lòng chọn file ảnh.");

            var swTotal = Stopwatch.StartNew();

            // 1. Đọc ảnh vào bộ nhớ
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await imageFile.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }
            _logger.LogDebug("[1/4] Đã đọc {Bytes} byte ảnh vào memory", imageBytes.Length);

            // 2. Gọi ML pipeline
            var swMl = Stopwatch.StartNew();
            var mlResult = _mlPredictor.Predict(imageBytes);
            swMl.Stop();
            _logger.LogInformation(
                "[2/4] ML pipeline xong trong {Elapsed} ms → SeverityLevel={Level}, Ratio={Ratio:F3}, Confidence={Conf:F3}, Model={Model}",
                swMl.ElapsedMilliseconds, mlResult.SeverityLevel, mlResult.SeverityRatio,
                mlResult.Confidence, mlResult.ModelVersion);

            // 3. Lưu ảnh xuống đĩa
            var swSave = Stopwatch.StartNew();
            var savedFileName = await SaveImageToDisk(imageBytes, imageFile.FileName);
            swSave.Stop();
            _logger.LogDebug("[3/4] Đã lưu ảnh '{SavedName}' ({Elapsed} ms)", savedFileName, swSave.ElapsedMilliseconds);

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

            // 4. Lưu kết quả vào DB
            var swDb = Stopwatch.StartNew();
            await _UnitOfWork.DiagnosisRepository.AddAsync(diagnosis);
            swDb.Stop();
            _logger.LogDebug("[4/4] Đã lưu vào DB ({Elapsed} ms), Id={Id}", swDb.ElapsedMilliseconds, diagnosis.Id);

            swTotal.Stop();
            _logger.LogInformation(
                "PredictAsync xong: tổng {Total} ms (ML={Ml} ms, lưu ảnh={Save} ms, lưu DB={Db} ms)",
                swTotal.ElapsedMilliseconds, swMl.ElapsedMilliseconds, swSave.ElapsedMilliseconds, swDb.ElapsedMilliseconds);

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
