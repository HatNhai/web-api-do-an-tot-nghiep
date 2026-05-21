namespace Service.Share.Contract.Dtos
{
    /// <summary>
    /// DTO trả về thông tin chẩn đoán bệnh lá cây.
    /// </summary>
    public class DiagnosisDto
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Mức độ bệnh: Healthy (0), Mild (1), Moderate (2), Severe (3).
        /// </summary>
        public int SeverityLevel { get; set; }

        /// <summary>
        /// Tên mức độ bệnh (cho hiển thị: "Healthy", "Mild", "Moderate", "Severe").
        /// </summary>
        public string SeverityLevelName { get; set; } = string.Empty;

        /// <summary>
        /// Tỉ lệ vùng bệnh (0.0 - 1.0).
        /// </summary>
        public double SeverityRatio { get; set; }

        /// <summary>
        /// Độ tin cậy (0.0 - 1.0).
        /// </summary>
        public double Confidence { get; set; }

        public string ModelUsed { get; set; } = string.Empty;

        public string? ImageFileName { get; set; }

        public string? ImagePath { get; set; }

        public DateTime DiagnosedAt { get; set; }

        public string? Notes { get; set; }
    }
}