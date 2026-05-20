namespace Service.Domain.Entities
{
    /// <summary>
    /// Mức độ bệnh trên lá cây.
    /// </summary>
    public enum SeverityLevel
    {
        /// <summary>
        /// Khỏe mạnh - không có bệnh.
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// Bệnh nhẹ - dưới 15% diện tích lá bị tổn thương.
        /// </summary>
        Mild = 1,

        /// <summary>
        /// Bệnh trung bình - 15-40% diện tích lá bị tổn thương.
        /// </summary>
        Moderate = 2,

        /// <summary>
        /// Bệnh nặng - trên 40% diện tích lá bị tổn thương.
        /// </summary>
        Severe = 3
    }
}