namespace Service.Application.Interfaces
{
    public class MlPredictionResult
    {
        /// <summary>Mức độ bệnh: 0=Healthy, 1=Mild, 2=Moderate, 3=Severe.</summary>
        public int SeverityLevel { get; set; }

        /// <summary>Tỉ lệ vùng bệnh (0.0 - 1.0).</summary>
        public double SeverityRatio { get; set; }

        /// <summary>Độ tin cậy của dự đoán (0.0 - 1.0).</summary>
        public double Confidence { get; set; }

        /// <summary>Version của model dùng để predict.</summary>
        public string ModelVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Abstraction cho ML pipeline dự đoán bệnh lá cây.
    /// Implementation ở Infrastructure (dùng OpenCV + ONNX Runtime).
    /// </summary>
    public interface IMlPredictorService
    {
        /// <summary>
        /// Nhận bytes ảnh, chạy full pipeline (extract features + 3 ONNX models)
        /// và trả về kết quả dự đoán.
        /// </summary>
        /// <param name="imageBytes">Bytes của ảnh (jpg/png).</param>
        /// <returns>Kết quả dự đoán gồm severity level, ratio, confidence.</returns>
        MlPredictionResult Predict(byte[] imageBytes);

        /// <summary>
        /// Chạy thử pipeline với 1 ảnh giả để ép load model + JIT ngay khi khởi động,
        /// tránh để cold-start rơi vào request dự đoán đầu tiên (gây timeout).
        /// </summary>
        void Warmup();
    }
}