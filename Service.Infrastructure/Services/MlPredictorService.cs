// "Đồ án tốt nghiệp - ML Predictor: OpenCV + ONNX Runtime"

using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Service.Application.Interfaces;
using Service.Infrastructure.MlServices;
using Service.Infrastructure.MlServices.TrichChonDacTrung;
using System.Diagnostics;

namespace Service.Infrastructure.Services
{
    /// <summary>
    /// Implementation của IMlPredictor — chạy ML pipeline đầy đủ:
    /// 1. Decode bytes → Mat (OpenCV) + resize 256x256
    /// 2. Extract F2 (24 features) + F4 (87 features)
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
        private readonly ILogger<MlPredictorService> _logger;

        private const string ModelVersion = "v1.0-MLP+RF";
        private const int ImageSize = 256;

        public MlPredictorService(ILogger<MlPredictorService> logger)
        {
            _logger = logger;

            var modelsDir = Path.Combine(
                AppContext.BaseDirectory,
                "MlServices", "Models");

            if (!Directory.Exists(modelsDir))
            {
                _logger.LogCritical("Không tìm thấy folder models: {ModelsDir}", modelsDir);
                throw new DirectoryNotFoundException(
                    $"Không tìm thấy folder models: {modelsDir}. " +
                    "Hãy chắc chắn các file .onnx được set 'Copy to Output Directory = Copy if newer'.");
            }

            _logger.LogInformation("Đang load các model ONNX từ {ModelsDir}...", modelsDir);
            var sw = Stopwatch.StartNew();

            // Load 3 ONNX models + scaler (1 lần duy nhất khi DI khởi tạo Singleton)
            _mlpSession = new InferenceSession(
                Path.Combine(modelsDir, "mlp_f2_classification.onnx"));
            _rfRegressionSession = new InferenceSession(
                Path.Combine(modelsDir, "rf_f4_regression.onnx"));
            _rfSeveritySession = new InferenceSession(
                Path.Combine(modelsDir, "rf_f4_temporal_severity.onnx"));

            _f2Scaler = StandardScaler.LoadFromFile(
                Path.Combine(modelsDir, "mlp_f2_scaler.json"));

            sw.Stop();
            _logger.LogInformation("Load model xong trong {Elapsed} ms (version={Version})",
                sw.ElapsedMilliseconds, ModelVersion);

            LogInputs("MLP Classification", _mlpSession);
            LogInputs("RF Regression", _rfRegressionSession);
            LogInputs("RF Severity", _rfSeveritySession);
            _logger.LogInformation("StandardScaler: NumberOfFeatures = {Count}", _f2Scaler.NumberOfFeatures);
        }

        public MlPredictionResult Predict(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes rỗng.", nameof(imageBytes));

            // 1. Decode + resize ảnh
            var swStep = Stopwatch.StartNew();
            using var imageRgb = DecodeAndResize(imageBytes);
            _logger.LogDebug("  - Decode + resize {Size}x{Size}: {Elapsed} ms",
                ImageSize, ImageSize, swStep.ElapsedMilliseconds);

            // 2. Extract features
            swStep.Restart();
            var f2 = TrichChonDacTrungF2.Extract(imageRgb);   // 24 features
            _logger.LogDebug("  - Trích F2 ({Count} đặc trưng): {Elapsed} ms", f2.Length, swStep.ElapsedMilliseconds);

            swStep.Restart();
            var f4 = TrichChonDacTrungF4.Extract(imageRgb);   // 87 features
            _logger.LogDebug("  - Trích F4 ({Count} đặc trưng): {Elapsed} ms", f4.Length, swStep.ElapsedMilliseconds);

            // 3. MLP Classification (F2 → cần scale trước)
            swStep.Restart();
            var mlpInput = _f2Scaler.TransformToFloat(f2);
            var mlpProbs = RunOnnxProbabilities(_mlpSession, mlpInput);
            double confidence = mlpProbs.Length > 0 ? mlpProbs.Max() : 0;
            _logger.LogDebug("  - MLP classification → confidence={Conf:F3} ({Elapsed} ms)",
                confidence, swStep.ElapsedMilliseconds);

            // 4. RF Regression → SeverityRatio (0.0 - 1.0)
            swStep.Restart();
            var rfInput = f4.Select(x => (float)x).ToArray();
            var regOutput = RunOnnx(_rfRegressionSession, rfInput);
            double severityRatio = Math.Clamp((double)regOutput[0], 0.0, 1.0);
            _logger.LogDebug("  - RF regression → severityRatio={Ratio:F3} ({Elapsed} ms)",
                severityRatio, swStep.ElapsedMilliseconds);

            // 5. RF Temporal Severity (F4 + time_index, 88 features) → SeverityLevel (0..3)
            swStep.Restart();
            float timeIndex = (float)Math.Clamp(Math.Round(severityRatio * 3.0), 0, 3);
            var rfTempInput = new float[rfInput.Length + 1];
            Array.Copy(rfInput, rfTempInput, rfInput.Length);
            rfTempInput[rfInput.Length] = timeIndex;
            var sevOutput = RunOnnx(_rfSeveritySession, rfTempInput);
            int severityLevel = Math.Clamp((int)Math.Round(sevOutput[0]), 0, 3);
            _logger.LogDebug("  - RF severity → level={Level} ({Elapsed} ms)",
                severityLevel, swStep.ElapsedMilliseconds);

            return new MlPredictionResult
            {
                SeverityLevel = severityLevel,
                SeverityRatio = severityRatio,
                Confidence = confidence,
                ModelVersion = ModelVersion
            };
        }


        /// <summary>
        /// Chạy thử pipeline với 1 ảnh giả 256x256 để ép load model + JIT toàn bộ
        /// pipeline ngay khi khởi động (gọi từ Program.cs), tránh cold-start rơi vào
        /// request dự đoán đầu tiên gây timeout.
        /// </summary>
        public void Warmup()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var dummy = new Mat(ImageSize, ImageSize, MatType.CV_8UC3, Scalar.All(127));
                Cv2.ImEncode(".jpg", dummy, out byte[] bytes);
                Predict(bytes);
                sw.Stop();
                _logger.LogInformation("Warm-up ML pipeline hoàn tất trong {Elapsed} ms.", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogWarning(ex, "Warm-up ML pipeline thất bại sau {Elapsed} ms (sẽ load lazy ở request đầu).", sw.ElapsedMilliseconds);
            }
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

        /// <summary>
        /// Chạy 1 classifier ONNX, trả về vector probabilities (output thứ 2 trong sklearn-onnx convention).
        /// Output[0] = label (int64), Output[1] = probabilities (float[1, n_classes]).
        /// </summary>
        private static float[] RunOnnxProbabilities(InferenceSession session, float[] features)
        {
            var tensor = new DenseTensor<float>(features, new[] { 1, features.Length });
            var inputName = session.InputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var results = session.Run(inputs);
            var resultList = results.ToList();

            // Tìm output là tensor float (probabilities)
            foreach (var r in resultList)
            {
                try { return r.AsEnumerable<float>().ToArray(); }
                catch { /* skip non-float outputs */ }
            }

            // Fallback: chỉ có label → trả vector toàn 1.0 cho confidence
            return new[] { 1.0f };
        }

        private void LogInputs(string name, InferenceSession session)
        {
            foreach (var input in session.InputMetadata)
            {
                _logger.LogDebug(
                    "[{Name}] Input '{Key}': shape=[{Shape}], type={Type}",
                    name, input.Key,
                    string.Join(",", input.Value.Dimensions),
                    input.Value.ElementType.Name);
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
