using OpenCvSharp;
namespace Service.Infrastructure.MlServices.TrichChonDacTrung
{
    /// <summary>
    /// F1 - Trích chọn đặc trưng Màu cơ bản (Color Features).
    /// 
    /// Output: 12 đặc trưng
    ///   [0]  R_mean   - Trung bình kênh Red
    ///   [1]  G_mean   - Trung bình kênh Green
    ///   [2]  B_mean   - Trung bình kênh Blue
    ///   [3]  R_std    - Độ lệch chuẩn kênh Red
    ///   [4]  G_std    - Độ lệch chuẩn kênh Green
    ///   [5]  B_std    - Độ lệch chuẩn kênh Blue
    ///   [6]  H_mean   - Trung bình kênh Hue (HSV)
    ///   [7]  S_mean   - Trung bình kênh Saturation
    ///   [8]  V_mean   - Trung bình kênh Value
    ///   [9]  H_std    - Độ lệch chuẩn kênh Hue
    ///   [10] S_std    - Độ lệch chuẩn kênh Saturation
    ///   [11] V_std    - Độ lệch chuẩn kênh Value
    /// </summary>
    public static class TrichChonDacTrungF1
    {
        public static double[] Extract(Mat imageRgb)
        {
            if (imageRgb == null || imageRgb.Empty())
                throw new ArgumentException("Ảnh đầu vào không hợp lệ", nameof(imageRgb));

            var features = new double[12];
            var rgbChannels = Cv2.Split(imageRgb);
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Cv2.MeanStdDev(rgbChannels[i], out Scalar mean, out Scalar std);
                    features[i] = mean.Val0;        
                    features[i + 3] = std.Val0;    
                }
            }
            finally
            {
                foreach (var ch in rgbChannels) ch.Dispose();
            }

            using var hsv01 = RgbToHsv01(imageRgb);
            var hsvChannels = Cv2.Split(hsv01);
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Cv2.MeanStdDev(hsvChannels[i], out Scalar mean, out Scalar std);
                    features[6 + i] = mean.Val0;        
                    features[6 + i + 3] = std.Val0;     
                }
            }
            finally
            {
                foreach (var ch in hsvChannels) ch.Dispose();
            }

            return features;
        }

        private static Mat RgbToHsv01(Mat imageRgb)
        {
            using var rgbFloat = new Mat();
            imageRgb.ConvertTo(rgbFloat, MatType.CV_32FC3, 1.0 / 255.0);

            var hsv = new Mat();
            Cv2.CvtColor(rgbFloat, hsv, ColorConversionCodes.RGB2HSV);

            var channels = Cv2.Split(hsv);
            try
            {
                using var hNormalized = new Mat();
                channels[0].ConvertTo(hNormalized, MatType.CV_32F, 1.0 / 360.0);

                var newChannels = new Mat[] { hNormalized, channels[1], channels[2] };
                Cv2.Merge(newChannels, hsv);
            }
            finally
            {
                foreach (var ch in channels) ch.Dispose();
            }
            return hsv;
        }
    }
    
}
