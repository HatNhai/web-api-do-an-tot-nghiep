using OpenCvSharp;
using Service.Infrastructure.Helper;

namespace Service.Infrastructure.MlServices.TrichChonDacTrung
{
    public static class TrichChonDacTrungF4
    {
        public static double[] Extract(Mat imageRgb)
        {
            if (imageRgb == null || imageRgb.Empty())
                throw new ArgumentException("Ảnh đầu vào không hợp lệ", nameof(imageRgb));

            var f1 = TrichChonDacTrungF1.Extract(imageRgb);                  // 12
            var vegetation = TrichChonDacTrungF2.ExtractVegetationOnly(imageRgb);  // 7
            var texture = TrichChonDacTrungF3.ExtractTextureOnly(imageRgb);  // 63

            return Concat.Arrays(f1, vegetation, texture);                    // 82
        }
    }
}
