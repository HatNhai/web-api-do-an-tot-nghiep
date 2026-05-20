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

            var f1 = TrichChonDacTrungF1.Extract(imageRgb);   // 12 color features
            var f2 = TrichChonDacTrungF2.Extract(imageRgb);   // 7 vegetation features
            var f3 = TrichChonDacTrungF3.Extract(imageRgb);   // 63 texture features

            return Concat.Arrays(f1, f2, f3);
        }
    }
}
