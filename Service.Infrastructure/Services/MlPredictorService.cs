// "Đồ án tốt nghiệp - ML Predictor: OpenCV + ONNX Runtime"

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Service.Application.Interfaces;
using Service.Infrastructure.MlServices;
using Service.Infrastructure.MlServices.TrichChonDacTrung;

namespace Service.Infrastructure.Services
{
    /// <summary>
    /// Implementation của IMlPredictor — chạy ML pipeline đầy đủ:
    /// 1. Decode bytes → Mat (OpenCV) + resize 256x256
    /// 2. Extract F2 (19 features) + F4 (82 features)
    /// 3. MLP Classification (F2 scaled) → confidence
    /// 4. RF Regression (F4) → severity ratio
    /// 5. RF Temporal Severity (F4) → severity level
    /// </summary>
    public class MlPredictorService : IMlPredictorService, IDisposable
    {
        private readonly InferenceSession _mlpSession;
        private readonly InferenceSession _rfRegressionSession;
        private readonly InferenceSession _rfSeveritySession;
        private readonly StandardScaler _f2Scaler;

        private const string ModelVersion = "v1.0-MLP+RF";
        private const int ImageSize = 256;

        public MlPredictorService()
        {
            var modelsDir = Path.Combine(
                AppContext.BaseDirectory,
                "MlServices", "Models", "models_onnx");

            if (!Directory.Exists(modelsDir))
                throw new DirectoryNotFoundException(
                    $"Không tìm thấy folder models: {modelsDir}. " +
                    "Hãy chắc chắn các file .onnx được set 'Copy to Output Directory = Copy if newer'.");

            // Load 3 ONNX models + scaler (1 lần duy nhất khi DI khởi tạo Singleton)
            _mlpSession = new InferenceSession(
                Path.Combine(modelsDir, "mlp_f2_classification.onnx"));
            _rfRegressionSession = new InferenceSession(
                Path.Combine(modelsDir, "rf_f4_regression.onnx"));
            _rfSeveritySession = new InferenceSession(
                Path.Combine(modelsDir, "rf_f4_temporal_severity.onnx"));

            // ⬇️ Đổi LoadFromJson → LoadFromFile cho khớp StandardScaler thực tế
            _f2Scaler = StandardScaler.LoadFromFile(
                Path.Combine(modelsDir, "mlp_f2_scaler.json"));

            // Log để debug (xem trong Output window khi chạy)
            LogInputs("MLP Classification", _mlpSession);
            LogInputs("RF Regression", _rfRegressionSession);
            LogInputs("RF Severity", _rfSeveritySession);
            Console.WriteLine($"[Scaler] NumberOfFeatures = {_f2Scaler.NumberOfFeatures}");
        }

        public MlPredictionResult Predict(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes rỗng.", nameof(imageBytes));

            // 1. Decode + resize ảnh
            using var imageRgb = DecodeAndResize(imageBytes);

            // 2. Extract features
            var f2 = TrichChonDacTrungF2.Extract(imageRgb);   // 19 features
            var f4 = TrichChonDacTrungF4.Extract(imageRgb);   // 82 features

            // 3. MLP Classification (F2 → cần scale trước)
            // ⬇️ Đổi sang TransformToFloat (vì ONNX cần float[])
            var mlpInput = _f2Scaler.TransformToFloat(f2);
            var mlpOutput = RunOnnx(_mlpSession, mlpInput);
            double confidence = mlpOutput.Length > 0 ? mlpOutput.Max() : 0;

            // 4. RF Regression → SeverityRatio (0.0 - 1.0)
            var rfInput = f4.Select(x => (float)x).ToArray();
            var regOutput = RunOnnx(_rfRegressionSession, rfInput);
            double severityRatio = Math.Clamp((double)regOutput[0], 0.0, 1.0);

            // 5. RF Temporal Severity → SeverityLevel (0..3)
            var sevOutput = RunOnnx(_rfSeveritySession, rfInput);
            int severityLevel = Math.Clamp((int)Math.Round(sevOutput[0]), 0, 3);

            return new MlPredictionResult
            {
                SeverityLevel = severityLevel,
                SeverityRatio = severityRatio,
                Confidence = confidence,
                ModelVersion = ModelVersion
            };
        }


        /// <summary>
        /// Decode byte[] thành Mat RGB và resize về 256x256.
        /// </summary>
        private static Mat DecodeAndResize(byte[] imageBytes)
        {
            var matBgr = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            if (matBgr.Empty())
                throw new Exception("Không decode được ảnh. File có thể bị hỏng hoặc sai định dạng.");

            try
            {
                using var matRgb = new Mat();
                Cv2.CvtColor(matBgr, matRgb, ColorConversionCodes.BGR2RGB);

                var resized = new Mat();
                Cv2.Resize(matRgb, resized, new Size(ImageSize, ImageSize));
                return resized;
            }
            finally
            {
                matBgr.Dispose();
            }
        }

        /// <summary>
        /// Chạy 1 model ONNX với input vector 1D, trả về output 1D.
        /// Tự fallback giữa float (regression/probs) và long (classification label).
        /// </summary>
        private static float[] RunOnnx(InferenceSession session, float[] features)
        {
            var tensor = new DenseTensor<float>(features, new[] { 1, features.Length });
            var inputName = session.InputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var results = session.Run(inputs);
            var first = results.First();

            try
            {
                return first.AsEnumerable<float>().ToArray();
            }
            catch
            {
                // Classification model có thể trả long (label index) thay vì float
                return first.AsEnumerable<long>().Select(x => (float)x).ToArray();
            }
        }

        private static void LogInputs(string name, InferenceSession session)
        {
            foreach (var input in session.InputMetadata)
            {
                Console.WriteLine(
                    $"[{name}] Input '{input.Key}': " +
                    $"shape=[{string.Join(",", input.Value.Dimensions)}], " +
                    $"type={input.Value.ElementType.Name}");
            }
        }

        public void Dispose()
        {
            _mlpSession?.Dispose();
            _rfRegressionSession?.Dispose();
            _rfSeveritySession?.Dispose();
        }
    }
}