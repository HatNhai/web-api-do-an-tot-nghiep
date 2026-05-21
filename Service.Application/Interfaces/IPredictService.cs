// "Một sản phẩm từ đồ án tốt nghiệp"

using Microsoft.AspNetCore.Http;
using Service.Shared.Commons.Model.Commons;
using Service.Share.Contract.Dtos;
using Service.Share.Contract.Queries;

namespace Service.Application.Interfaces
{
    public interface IPredictService
    {
        /// <summary>
        /// Lấy lịch sử các lần dự đoán bệnh lá cây (có phân trang).
        /// </summary>
        Task<DataTableJson> GetPaged(DiagnosisQuery searchOption);

        /// <summary>
        /// Dự đoán bệnh lá cây từ ảnh upload, lưu kết quả vào DB.
        /// </summary>
        Task<DiagnosisDto> PredictAsync(IFormFile imageFile, string? notes = null);
    }
}