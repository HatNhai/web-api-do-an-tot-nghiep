namespace Service.Infrastructure.Helper
{
    /// <summary>
    /// Helper class - cung cấp method nối nhiều mảng double thành 1.
    /// </summary>
    internal static class Concat
    {
        public static double[] Arrays(params double[][] arrays)
        {
            if (arrays == null || arrays.Length == 0)
                return Array.Empty<double>();

            int totalLength = 0;
            foreach (var arr in arrays)
            {
                if (arr != null)
                    totalLength += arr.Length;
            }
            var result = new double[totalLength];
            int offset = 0;
            foreach (var arr in arrays)
            {
                if (arr == null) continue;
                Array.Copy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }
            return result;
        }
    }
}