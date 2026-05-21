using OpenCvSharp;
using Service.Infrastructure.Helper;
using System.Threading.Channels;

namespace Service.Infrastructure.MlServices.TrichChonDacTrung
{
    public static class TrichChonDacTrungF3
    {
        private const int ReduceLevels = 16;

        private const int LbpP = 8;          // 8 điểm xung quanh
        private const double LbpR = 1.0;     // Bán kính 1
        private const int LbpNumBins = 59;   // P*(P-1)+3 = 59 (theo Python code)

        //public static double[] Extract(Mat imageRgb)
        //{
        //    var glcmFeatures = ExtractGLCM(imageRgb);

        //    var lbpHistogram = ExtractLBP(imageRgb);

        //    return Helper.Concat.Arrays(glcmFeatures, lbpHistogram);
        //}
        public static double[] Extract(Mat imageRgb)
        {
            if (imageRgb == null || imageRgb.Empty())
                throw new ArgumentException("Ảnh đầu vào không hợp lệ", nameof(imageRgb));

            var f1 = TrichChonDacTrungF1.Extract(imageRgb);   
            var texture = ExtractTextureOnly(imageRgb);        
            return Concat.Arrays(f1, texture); 
            
        }

        public static double[] ExtractTextureOnly(Mat imageRgb)
        {
            var glcmFeatures = ExtractGLCM(imageRgb);
            var lbpHistogram = ExtractLBP(imageRgb);
            return Concat.Arrays(glcmFeatures, lbpHistogram);  
        }

        /// <summary>
        /// Tính 4 GLCM features: contrast, homogeneity, energy, correlation.
        /// Trung bình của 4 hướng: 0°, 45°, 90°, 135° với distance=1.
        /// </summary>
        public static double[] ExtractGLCM(Mat imageRgb)
        {

            using var gray = new Mat();
            Cv2.CvtColor(imageRgb, gray, ColorConversionCodes.RGB2GRAY);

            using var grayReduced = new Mat();
            gray.ConvertTo(grayReduced, MatType.CV_8U, 1.0 / 16.0);

            var directions = new (int dy, int dx)[]
            {
                (0, 1),
                (-1, 1),
                (-1, 0),
                (-1, -1)
            };

            double sumContrast = 0;
            double sumHomogeneity = 0;
            double sumEnergy = 0;
            double sumCorrelation = 0;

            foreach (var (dy, dx) in directions)
            {
                var glcm = BuildGLCM(grayReduced, dy, dx, ReduceLevels);

                var props = ComputeGLCMProperties(glcm, ReduceLevels);
                sumContrast += props.contrast;
                sumHomogeneity += props.homogeneity;
                sumEnergy += props.energy;
                sumCorrelation += props.correlation;
            }

            return new[]
            {
                sumContrast / 4.0,
                sumHomogeneity / 4.0,
                sumEnergy / 4.0,
                sumCorrelation / 4.0
            };
        }

        /// <summary>
        /// Build ma trận GLCM theo 1 hướng cụ thể.
        /// Output: ma trận double[levels, levels] đã normalize (tổng = 1).
        /// 
        /// Tham số:
        ///   gray: Ảnh grayscale đã reduce levels (0 → levels-1)
        ///   dy, dx: Hướng (offset từ pixel hiện tại)
        ///   levels: Số mức xám (16)
        /// </summary>
        /// 
        private static double[,] BuildGLCM(Mat gray, int dy, int dx, int levels)
        {
            var glcm = new double[levels, levels];
            var indexer = gray.GetGenericIndexer<byte>();

            int rows = gray.Rows;
            int cols = gray.Cols;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int y2 = y + dy;
                    int x2 = x + dx;

                    if (y2 < 0 || y2 >= rows || x2 < 0 || x2 >= cols)
                        continue;

                    int i = indexer[y, x];
                    int j = indexer[y2, x2];

                    if (i < levels && j < levels)
                    {
                        glcm[i, j] += 1;
                    }
                }
            }

            for (int i = 0; i < levels; i++)
            {
                for (int j = i + 1; j < levels; j++)
                {
                    double sum = glcm[i, j] + glcm[j, i];
                    glcm[i, j] = sum;
                    glcm[j, i] = sum;
                }
            }

            double total = 0;
            for (int i = 0; i < levels; i++)
                for (int j = 0; j < levels; j++)
                    total += glcm[i, j];

            if (total > 0)
            {
                for (int i = 0; i < levels; i++)
                    for (int j = 0; j < levels; j++)
                        glcm[i, j] /= total;
            }

            return glcm;
        }

        /// <summary>
        /// Tính 4 properties của ma trận GLCM.
        /// </summary>
        private static (double contrast, double homogeneity, double energy, double correlation)
            ComputeGLCMProperties(double[,] glcm, int levels)
        {
            double contrast = 0;
            double homogeneity = 0;
            double energySquare = 0;

            for (int i = 0; i < levels; i++)
            {
                for (int j = 0; j < levels; j++)
                {
                    double p = glcm[i, j];
                    int diff = i - j;

                    contrast += diff * diff * p;
                    homogeneity += p / (1.0 + Math.Abs(diff));
                    energySquare += p * p;
                }
            }

            double energy = Math.Sqrt(energySquare);

            double meanI = 0;
            double meanJ = 0;
            for (int i = 0; i < levels; i++)
            {
                for (int j = 0; j < levels; j++)
                {
                    meanI += i * glcm[i, j];
                    meanJ += j * glcm[i, j];
                }
            }

            // σ_i² = Σ Σ (i - μ_i)² × GLCM[i, j]
            // σ_j² = Σ Σ (j - μ_j)² × GLCM[i, j]
            double varI = 0;
            double varJ = 0;
            for (int i = 0; i < levels; i++)
            {
                for (int j = 0; j < levels; j++)
                {
                    double di = i - meanI;
                    double dj = j - meanJ;
                    varI += di * di * glcm[i, j];
                    varJ += dj * dj * glcm[i, j];
                }
            }

            double stdI = Math.Sqrt(varI);
            double stdJ = Math.Sqrt(varJ);

            // Correlation = Σ Σ ((i - μ_i)(j - μ_j) × GLCM[i, j]) / (σ_i × σ_j)
            double correlation = 0;
            if (stdI > 0 && stdJ > 0)
            {
                for (int i = 0; i < levels; i++)
                {
                    for (int j = 0; j < levels; j++)
                    {
                        correlation += ((i - meanI) * (j - meanJ) * glcm[i, j]) / (stdI * stdJ);
                    }
                }
            }
            else
            {
                // Trường hợp ảnh đồng nhất, stdI hoặc stdJ = 0
                // skimage trả về 1.0 trong case này
                correlation = 1.0;
            }

            return (contrast, homogeneity, energy, correlation);
        }

        /// <summary>
        /// Hàm trích chọn đặc trưng LBP (Local Binary Pattern) từ ảnh RGB.
        /// </summary>
        /// <param name="imageRgb"></param>
        /// <returns></returns>
        public static double[] ExtractLBP(Mat imageRgb)
        {
            using var gray = new Mat();
            Cv2.CvtColor(imageRgb, gray, ColorConversionCodes.RGB2GRAY);
            return ExtractLBPFromGray(gray);
        }

        private static double[] ExtractLBPFromGray(Mat gray)
        {
            int rows = gray.Rows;
            int cols = gray.Cols;
            var indexer = gray.GetGenericIndexer<byte>();

            // Step 1: Tính sẵn vị trí 8 điểm xung quanh
            // angle = 2π × p / P
            // dx = R × cos(angle), dy = -R × sin(angle)
            var dx = new double[LbpP];
            var dy = new double[LbpP];
            for (int p = 0; p < LbpP; p++)
            {
                double angle = 2.0 * Math.PI * p / LbpP;
                dx[p] = LbpR * Math.Cos(angle);
                dy[p] = -LbpR * Math.Sin(angle);

                // Round nhỏ để xử lý floating point error
                if (Math.Abs(dx[p]) < 1e-10) dx[p] = 0;
                if (Math.Abs(dy[p]) < 1e-10) dy[p] = 0;
            }

            // Step 2: Tính LBP label cho mỗi pixel
            // Mảng labels chứa giá trị 0..9 cho uniform, 9 cho non-uniform (skimage convention)
            var labels = new double[rows * cols];
            int labelIdx = 0;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    double centerValue = indexer[y, x];

                    // Lấy giá trị 8 điểm (bilinear interpolation)
                   
                    int numTransitions = 0;
                    int prevBit = -1;
                    int firstBit = -1;
                    int countOnes = 0;

                    for (int p = 0; p < LbpP; p++)
                    {
                        double py = y + dy[p];
                        double px = x + dx[p];

                        double pointValue = BilinearInterpolate(indexer, py, px, rows, cols);

                        // So sánh với center
                        // skimage: pointValue >= centerValue → bit 1
                        int bit = pointValue >= centerValue ? 1 : 0;

                        if (bit == 1) countOnes++;

                        // Đếm transitions
                        if (p == 0)
                        {
                            firstBit = bit;
                        }
                        else
                        {
                            if (bit != prevBit) numTransitions++;
                        }
                        prevBit = bit;
                    }

                    // Cyclic transition: bit cuối → bit đầu
                    if (prevBit != firstBit) numTransitions++;

                    // Determine label
                    double label;
                    if (numTransitions <= 2)
                    {
                        // Uniform: label = số bit 1 (0..8)
                        label = countOnes;
                    }
                    else
                    {
                        // Non-uniform: label = P+1 = 9 (skimage convention)
                        label = LbpP + 1;
                    }

                    labels[labelIdx++] = label;
                }
            }

            // Step 3: Build histogram 59 bins với range (0, 59), density=True
            // Python: np.histogram(lbp.ravel(), bins=59, range=(0, 59), density=True)
            return BuildHistogram(labels, LbpNumBins, 0, LbpNumBins);
        }

        private static double BilinearInterpolate(
            OpenCvSharp.MatIndexer<byte> indexer,
            double y, double x,
            int rows, int cols)
        {
            // Round nếu rất gần số nguyên (tránh floating point error)
            double yRounded = Math.Round(y, 5);
            double xRounded = Math.Round(x, 5);

            // Nếu là pixel nguyên, trả trực tiếp
            if (yRounded == Math.Floor(yRounded) && xRounded == Math.Floor(xRounded))
            {
                int yi = (int)yRounded;
                int xi = (int)xRounded;
                if (yi < 0 || yi >= rows || xi < 0 || xi >= cols)
                    return 0;
                return indexer[yi, xi];
            }

            // Bilinear interpolation
            int y0 = (int)Math.Floor(y);
            int y1 = y0 + 1;
            int x0 = (int)Math.Floor(x);
            int x1 = x0 + 1;

            double wy = y - y0;
            double wx = x - x0;

            // Lấy 4 pixel xung quanh, clip vào trong ảnh
            double p00 = (y0 >= 0 && y0 < rows && x0 >= 0 && x0 < cols) ? indexer[y0, x0] : 0;
            double p01 = (y0 >= 0 && y0 < rows && x1 >= 0 && x1 < cols) ? indexer[y0, x1] : 0;
            double p10 = (y1 >= 0 && y1 < rows && x0 >= 0 && x0 < cols) ? indexer[y1, x0] : 0;
            double p11 = (y1 >= 0 && y1 < rows && x1 >= 0 && x1 < cols) ? indexer[y1, x1] : 0;

            double result =
                p00 * (1 - wy) * (1 - wx) +
                p01 * (1 - wy) * wx +
                p10 * wy * (1 - wx) +
                p11 * wy * wx;

            return result;
        }
        private static double[] BuildHistogram(double[] data, int numBins, double rangeMin, double rangeMax)
        {
            var counts = new int[numBins];
            int totalCount = 0;
            double binWidth = (rangeMax - rangeMin) / numBins;

            foreach (var val in data)
            {
                if (val < rangeMin || val > rangeMax) continue;

                int binIdx;
                if (val == rangeMax)
                {
                    // numpy: last bin includes right edge
                    binIdx = numBins - 1;
                }
                else
                {
                    binIdx = (int)Math.Floor((val - rangeMin) / binWidth);
                }

                if (binIdx >= 0 && binIdx < numBins)
                {
                    counts[binIdx]++;
                    totalCount++;
                }
            }

            // Normalize: density = count / (total × bin_width)
            var hist = new double[numBins];
            if (totalCount > 0)
            {
                double divisor = totalCount * binWidth;
                for (int i = 0; i < numBins; i++)
                    hist[i] = counts[i] / divisor;
            }

            return hist;
        }
    }
}
