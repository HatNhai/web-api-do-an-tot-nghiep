using Service.Shared.Commons.Querys.ModalQuery;
namespace Service.Share.Contract.Queries
{
    /// <summary>
    /// Query phân trang + filter cho danh sách chẩn đoán.
    /// </summary>
    public class DiagnosisQuery : BaseQuery
    {
        /// <summary>
        /// Trang hiện tại (bắt đầu từ 1).
        /// </summary>
        public int PageIndex { get; set; } = 1;

        /// <summary>
        /// Số lượng bản ghi mỗi trang.
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// Lọc theo mức độ bệnh (null = tất cả).
        /// </summary>
        public int? SeverityLevel { get; set; }

        /// <summary>
        /// Lọc từ ngày (null = không giới hạn).
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Lọc đến ngày (null = không giới hạn).
        /// </summary>
        public DateTime? ToDate { get; set; }
    }
}