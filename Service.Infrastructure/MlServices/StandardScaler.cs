using System.Text.Json;

namespace Service.Infrastructure.MlServices
{
    public class StandardScaler
    {
        private readonly double[] _mean;
        private readonly double[] _scale;
        private readonly int _nFeatures;
        public int NumberOfFeatures => _nFeatures;
        private StandardScaler(double[] mean, double[] scale, int nFeatures)
        {
            _mean = mean;
            _scale = scale;
            _nFeatures = nFeatures;
        }
        public static StandardScaler LoadFromFile(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
                throw new ArgumentException("Đường dẫn file không được để trống", nameof(jsonPath));

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"Không tìm thấy file scaler: {jsonPath}");

            string jsonContent = File.ReadAllText(jsonPath);

            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("mean", out var meanElement))
                throw new InvalidOperationException("File JSON thiếu trường 'mean'");

            var mean = new double[meanElement.GetArrayLength()];
            int i = 0;
            foreach (var item in meanElement.EnumerateArray())
                mean[i++] = item.GetDouble();

            if (!root.TryGetProperty("scale", out var scaleElement))
                throw new InvalidOperationException("File JSON thiếu trường 'scale'");

            var scale = new double[scaleElement.GetArrayLength()];
            i = 0;
            foreach (var item in scaleElement.EnumerateArray())
                scale[i++] = item.GetDouble();

            if (!root.TryGetProperty("n_features", out var nFeaturesElement))
                throw new InvalidOperationException("File JSON thiếu trường 'n_features'");

            int nFeatures = nFeaturesElement.GetInt32();

            if (mean.Length != nFeatures)
                throw new InvalidOperationException($"Số chiều 'mean' ({mean.Length}) không khớp 'n_features' ({nFeatures})");

            if (scale.Length != nFeatures)
                throw new InvalidOperationException($"Số chiều 'scale' ({scale.Length}) không khớp 'n_features' ({nFeatures})");

            return new StandardScaler(mean, scale, nFeatures);
        }

        /// <param name="features">Mảng features đầu vào (số chiều phải khớp NumberOfFeatures).</param>
        /// <returns>Mảng features đã chuẩn hóa (cùng số chiều).</returns>
        /// <exception cref="ArgumentNullException">Khi features null.</exception>
        /// <exception cref="ArgumentException">Khi số chiều không khớp.</exception>
        public double[] Transform(double[] features)
        {
            if (features == null)
                throw new ArgumentNullException(nameof(features));

            if (features.Length != _nFeatures)
                throw new ArgumentException($"Số features không khớp. Mong đợi {_nFeatures}, nhận {features.Length}",
                    nameof(features));

            var scaled = new double[_nFeatures];
            for (int i = 0; i < _nFeatures; i++)
            {
                if (Math.Abs(_scale[i]) < 1e-10)
                    scaled[i] = 0;
                else
                    scaled[i] = (features[i] - _mean[i]) / _scale[i];
            }

            return scaled;
        }

        /// <summary>
        /// Chuẩn hóa và trả về float[] (dùng cho ONNX input thường là float32).
        /// </summary>
        /// <param name="features">Mảng features đầu vào (double).</param>
        /// <returns>Mảng features đã scaled, ở dạng float[].</returns>
        public float[] TransformToFloat(double[] features)
        {
            var scaled = Transform(features);
            var result = new float[scaled.Length];
            for (int i = 0; i < scaled.Length; i++)
                result[i] = (float)scaled[i];
            return result;
        }
    }
}