namespace Service.Api
{
    /// <summary>
    /// Trạng thái sẵn sàng của model ML, để UI biết khi nào ẩn màn loading khởi tạo.
    /// Cố tình tách riêng (không giữ tham chiếu tới model nặng) nên endpoint kiểm tra
    /// trạng thái KHÔNG bị block bởi quá trình load model.
    /// </summary>
    public class ModelReadinessState
    {
        private volatile bool _isReady;

        /// <summary>Model đã load + warm-up xong và sẵn sàng phục vụ dự đoán hay chưa.</summary>
        public bool IsReady => _isReady;

        /// <summary>Thông điệp lỗi nếu warm-up model thất bại (null nếu không lỗi).</summary>
        public string? Error { get; private set; }

        public void MarkReady() => _isReady = true;

        public void MarkFailed(string error) => Error = error;
    }
}
