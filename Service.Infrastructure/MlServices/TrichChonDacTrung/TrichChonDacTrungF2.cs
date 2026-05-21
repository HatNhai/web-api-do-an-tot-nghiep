using OpenCvSharp;
using Service.Infrastructure.Helper;

namespace Service.Infrastructure.MlServices.TrichChonDacTrung
{
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

                using var numerator = new Mat();
                Cv2.Subtract(G, R, numerator);

                using var denominator = new Mat();
                Cv2.Add(G, R, denominator);
                Cv2.Subtract(denominator, B, denominator);
                Cv2.Add(denominator, new Scalar(Epsilon), denominator);

                using var vari = new Mat();
                Cv2.Divide(numerator, denominator, vari);

                Cv2.MeanStdDev(vari, out Scalar variMean, out Scalar variStd);

                var variArray = MatToDoubleArray(vari);
                double variP10 = Percentile(variArray, 10);
                double variP90 = Percentile(variArray, 90);

                using var sum = new Mat();
                Cv2.Add(R, G, sum);
                Cv2.Add(sum, B, sum);
                Cv2.Add(sum, new Scalar(Epsilon), sum);

                using var Rnorm = new Mat();
                using var Gnorm = new Mat();
                using var Bnorm = new Mat();
                Cv2.Divide(R, sum, Rnorm);
                Cv2.Divide(G, sum, Gnorm);
                Cv2.Divide(B, sum, Bnorm);

                double rNormMean = Cv2.Mean(Rnorm).Val0;
                double gNormMean = Cv2.Mean(Gnorm).Val0;
                double bNormMean = Cv2.Mean(Bnorm).Val0;

                return new[]
                {
                    variMean.Val0,
                    variStd.Val0,
                    variP10,
                    variP90,
                    rNormMean,
                    gNormMean,
                    bNormMean
                };
            }
            finally
            {
                foreach (var ch in channels) ch.Dispose();
            }
        }

        //public static double[] Extract(Mat imageRgb)
        //{
        //    if (imageRgb == null || imageRgb.Empty())
        //        throw new ArgumentException("Ảnh đầu vào không hợp lệ", nameof(imageRgb));

        //    var channels = Cv2.Split(imageRgb);
        //    try
        //    {
        //        using var R = new Mat();
        //        using var G = new Mat();
        //        using var B = new Mat();
        //        channels[0].ConvertTo(R, MatType.CV_32F);
        //        channels[1].ConvertTo(G, MatType.CV_32F);
        //        channels[2].ConvertTo(B, MatType.CV_32F);


        //        using var numerator = new Mat();
        //        Cv2.Subtract(G, R, numerator);

        //        using var denominator = new Mat();
        //        Cv2.Add(G, R, denominator);                              
        //        Cv2.Subtract(denominator, B, denominator);               
        //        Cv2.Add(denominator, new Scalar(Epsilon), denominator);  

        //        using var vari = new Mat();
        //        Cv2.Divide(numerator, denominator, vari);


        //        Cv2.MeanStdDev(vari, out Scalar variMean, out Scalar variStd);


        //        var variArray = MatToDoubleArray(vari);
        //        double variP10 = Percentile(variArray, 10);
        //        double variP90 = Percentile(variArray, 90);


        //        using var sum = new Mat();
        //        Cv2.Add(R, G, sum);
        //        Cv2.Add(sum, B, sum);
        //        Cv2.Add(sum, new Scalar(Epsilon), sum);

        //        using var Rnorm = new Mat();
        //        using var Gnorm = new Mat();
        //        using var Bnorm = new Mat();
        //        Cv2.Divide(R, sum, Rnorm);
        //        Cv2.Divide(G, sum, Gnorm);
        //        Cv2.Divide(B, sum, Bnorm);

        //        double rNormMean = Cv2.Mean(Rnorm).Val0;
        //        double gNormMean = Cv2.Mean(Gnorm).Val0;
        //        double bNormMean = Cv2.Mean(Bnorm).Val0;

        //        return new[]
        //        {
        //            variMean.Val0,   
        //            variStd.Val0,    
        //            variP10,         
        //            variP90,         
        //            rNormMean,       
        //            gNormMean,       
        //            bNormMean        
        //        };
        //    }
        //    finally
        //    {
        //        foreach (var ch in channels) ch.Dispose();
        //    }
        //}

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
