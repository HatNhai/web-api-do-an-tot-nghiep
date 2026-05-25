// "Một sản phẩm từ đồ án tốt nghiệp"

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Service.Application.Interfaces;
using Service.Core.Api.Controllers;
using Service.Share.Contract.Queries;
using Service.Shared.Commons.Interfaces.Extentions;
using Service.Shared.Commons.Model.Commons;
using System.Diagnostics;

namespace Service.Api.Controllers.v1
{
    /// <summary>
    /// Controller xử lý chẩn đoán bệnh từ ảnh bằng mô hình ML (MLP + RF)
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    //[Authorize]
    public class PredictController : BaseController
    {
        private readonly IPredictService _predictService;
        private readonly ILogger<PredictController> _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        public PredictController(
            IRequestContext requestContext,
            IPredictService predictService,
            ILogger<PredictController> logger)
            : base(requestContext)
        {
            _predictService = predictService;
            _logger = logger;
        }

        /// <summary>
        /// Chẩn đoán bệnh từ ảnh upload
        /// </summary>
        /// <param name="imageFile">File ảnh cần chẩn đoán (jpg, png, ...)</param>
        /// <param name="notes">Ghi chú thêm (tùy chọn)</param>
        /// <returns>Kết quả chẩn đoán: mức độ, tỉ lệ, độ tin cậy</returns>
        /// <response code="200">Chẩn đoán thành công</response>
        /// <response code="400">File ảnh không hợp lệ</response>
        [HttpPost("Predict")]
        public async Task<IActionResult> Predict(IFormFile imageFile, [FromForm] string? notes = null)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                _logger.LogWarning("Predict bị từ chối: không có file ảnh trong request");
                throw new ArgumentNullException(nameof(imageFile), "Vui lòng chọn file ảnh.");
            }

            _logger.LogInformation(
                "Nhận yêu cầu Predict: file='{FileName}', size={SizeKb:0.0} KB, contentType={ContentType}",
                imageFile.FileName, imageFile.Length / 1024.0, imageFile.ContentType);

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await _predictService.PredictAsync(imageFile, notes);
                sw.Stop();

                _logger.LogInformation(
                    "Predict THÀNH CÔNG sau {Elapsed} ms: Id={Id}, Severity={Severity} ({Ratio:P1}), Confidence={Confidence:P1}",
                    sw.ElapsedMilliseconds, result.Id, result.SeverityLevelName, result.SeverityRatio, result.Confidence);

                return Ok(result);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Predict THẤT BẠI sau {Elapsed} ms cho file '{FileName}'",
                    sw.ElapsedMilliseconds, imageFile.FileName);
                throw;
            }
        }

        /// <summary>
        /// Lấy danh sách phân trang lịch sử chẩn đoán
        /// </summary>
        /// <param name="searchOptions">Điều kiện tìm kiếm</param>
        /// <returns>Danh sách chẩn đoán phân trang</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [HttpPost("GetPaged")]
        public async Task<IActionResult> GetPaged([FromBody] DiagnosisQuery searchOptions)
        {
            if (searchOptions == null)
            {
                _logger.LogWarning("GetPaged bị từ chối: searchOptions null");
                throw new ArgumentNullException(nameof(searchOptions), "Đầu vào không hợp lệ");
            }

            _logger.LogInformation("GetPaged: draw={Draw}", searchOptions.draw);

            var sw = Stopwatch.StartNew();
            try
            {
                DataTableJson data = await _predictService.GetPaged(searchOptions);
                sw.Stop();
                _logger.LogInformation(
                    "GetPaged THÀNH CÔNG sau {Elapsed} ms", sw.ElapsedMilliseconds);
                return Ok(data);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "GetPaged THẤT BẠI sau {Elapsed} ms", sw.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
