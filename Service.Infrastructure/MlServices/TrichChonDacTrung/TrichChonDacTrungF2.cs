using OpenCvSharp;
using Service.Infrastructure.Helper;

namespace Service.Infrastructure.MlServices.TrichChonDacTrung
{
    /// <summary>
    /// F2 - Vegetation indices.
    ///
    /// Extract(): 24 features = F1 (12) + Vegetation (12)
    /// ExtractVegetationOnly(): 12 features = 3 indices x 4 stats (mean, std, p10, p90)
    ///   VARI  = (G - R) / (G + R - B + e)
    ///   NormG = G / (R + G + B + e)
    ///   ExG   = 2G - R - B
    /// </summary>
    public static class TrichChonDacTrungF2
    {
        private const double Epsilon = 1e-6;

        public static double[] Extract(Mat imageRgb)
        {
            if (imageRgb == null || imageRgb.Empty())
                throw new ArgumentException("Ảnh đầu vào không hợp lệ", nameof(imageRgb));

            var f1 = TrichChonDacTrungF1.Extract(imageRgb);
            var vegetation = ExtractVegetationOnly(imageRgb);
            return Concat.Arrays(f1, vegetation);
        }

        public static double[] ExtractVegetationOnly(Mat imageRgb)
        {
            var channels = Cv2.Split(imageRgb);
            try
            {
                using var R = new Mat();
                using var G = new Mat();
                using var B = new Mat();
                channels[0].ConvertTo(R, MatType.CV_32F);
                channels[1].ConvertTo(G, MatType.CV_32F);
                channels[2].ConvertTo(B, MatType.CV_32F);

                // VARI = (G - R) / (G + R - B + e)
                using var variNum = new Mat();
                Cv2.Subtract(G, R, variNum);

                using var variDen = new Mat();
                Cv2.Add(G, R, variDen);
                Cv2.Subtract(variDen, B, variDen);
                Cv2.Add(variDen, new Scalar(Epsilon), variDen);

                using var vari = new Mat();
                Cv2.Divide(variNum, variDen, vari);

                // NormG = G / (R + G + B + e)
                using var sum = new Mat();
                Cv2.Add(R, G, sum);
                Cv2.Add(sum, B, sum);
                Cv2.Add(sum, new Scalar(Epsilon), sum);

                using var normG = new Mat();
                Cv2.Divide(G, sum, normG);

                // ExG = 2G - R - B
                using var exg = new Mat();
                using var twoG = new Mat();
                Cv2.Multiply(G, new Scalar(2.0), twoG);
                Cv2.Subtract(twoG, R, exg);
                Cv2.Subtract(exg, B, exg);

                var variStats = ComputeStats(vari);
                var normGStats = ComputeStats(normG);
                var exgStats = ComputeStats(exg);

                return Concat.Arrays(variStats, normGStats, exgStats);
            }
            finally
            {
                foreach (var ch in channels) ch.Dispose();
            }
        }

        /// <summary>
        /// 4 stats cho một index: mean, std, p10, p90.
        /// </summary>
        private static double[] ComputeStats(Mat index)
        {
            Cv2.MeanStdDev(index, out Scalar mean, out Scalar std);
            var arr = MatToDoubleArray(index);
            return new[]
            {
                mean.Val0,
                std.Val0,
                Percentile(arr, 10),
                Percentile(arr, 90)
            };
        }

        private static double[] MatToDoubleArray(Mat mat)
        {
            int total = mat.Rows * mat.Cols;
            var result = new double[total];
            var indexer = mat.GetGenericIndexer<float>();
            int idx = 0;
            for (int y = 0; y < mat.Rows; y++)
                for (int x = 0; x < mat.Cols; x++)
                    result[idx++] = indexer[y, x];
            return result;
        }

        private static double Percentile(double[] data, double percentile)
        {
            if (data == null || data.Length == 0)
                return 0;

            var sorted = (double[])data.Clone();
            Array.Sort(sorted);

            double rank = (percentile / 100.0) * (sorted.Length - 1);
            int lowerIdx = (int)Math.Floor(rank);
            int upperIdx = (int)Math.Ceiling(rank);

            if (lowerIdx == upperIdx)
                return sorted[lowerIdx];

            double weight = rank - lowerIdx;
            return sorted[lowerIdx] * (1 - weight) + sorted[upperIdx] * weight;
        }
    }
}
