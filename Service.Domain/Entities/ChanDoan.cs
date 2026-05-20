namespace Service.Domain.Entities;

/// <summary>
/// Một lần dự đoán bệnh lá cây.
/// </summary>
public class Diagnosis
{
    /// <summary>
    /// Khóa chính.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Mức độ bệnh: Healthy (0), Mild (1), Moderate (2), Severe (3).
    /// </summary>
    public SeverityLevel SeverityLevel { get; set; }

    /// <summary>
    /// Tỉ lệ vùng bệnh (0.0 - 1.0).
    /// </summary>
    public double SeverityRatio { get; set; }

    /// <summary>
    /// Độ tin cậy (0.0 - 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Tên model AI được sử dụng.
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Tên file ảnh gốc.
    /// </summary>
    public string? ImageFileName { get; set; }

    /// <summary>
    /// Đường dẫn ảnh trên server.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Thời điểm chẩn đoán.
    /// </summary>
    public DateTime DiagnosedAt { get; set; }

    /// <summary>
    /// Ghi chú thêm.
    /// </summary>
    public string? Notes { get; set; }
}