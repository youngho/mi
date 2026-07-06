namespace PinkSoft.BDS
{
    /// <summary>
    /// 4점 Homography: 라이다 평면 좌표 → 정규화 스크린 좌표 (0~1).
    /// Direct Linear Transform (DLT) 8x8 풀이.
    /// </summary>
    public static class HomographyCalculator
    {
        public static double[] Compute(double[,] srcPoints, double[,] dstPoints)
        {
            if (srcPoints.GetLength(0) != 4 || dstPoints.GetLength(0) != 4)
                throw new ArgumentException("Exactly 4 point pairs required");

            var a = new double[8, 8];
            var b = new double[8];

            for (int i = 0; i < 4; i++)
            {
                double x = srcPoints[i, 0], y = srcPoints[i, 1];
                double u = dstPoints[i, 0], v = dstPoints[i, 1];
                int r = i * 2;
                a[r, 0] = x; a[r, 1] = y; a[r, 2] = 1;
                a[r, 6] = -u * x; a[r, 7] = -u * y;
                b[r] = u;
                a[r + 1, 3] = x; a[r + 1, 4] = y; a[r + 1, 5] = 1;
                a[r + 1, 6] = -v * x; a[r + 1, 7] = -v * y;
                b[r + 1] = v;
            }

            var h = SolveLinear8(a, b);
            return new[] { h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], 1.0 };
        }

        public static (float u, float v) Transform(double[] h, float x, float y)
        {
            double w = h[6] * x + h[7] * y + h[8];
            if (Math.Abs(w) < 1e-9)
                return (0, 0);
            return ((float)((h[0] * x + h[1] * y + h[2]) / w),
                    (float)((h[3] * x + h[4] * y + h[5]) / w));
        }

        static double[] SolveLinear8(double[,] a, double[] b)
        {
            int n = 8;
            var m = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    m[i, j] = a[i, j];
                m[i, n] = b[i];
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int row = col + 1; row < n; row++)
                    if (Math.Abs(m[row, col]) > Math.Abs(m[pivot, col]))
                        pivot = row;

                for (int j = 0; j <= n; j++)
                    (m[col, j], m[pivot, j]) = (m[pivot, j], m[col, j]);

                var div = m[col, col];
                if (Math.Abs(div) < 1e-12)
                    throw new InvalidOperationException("Degenerate homography");

                for (int j = col; j <= n; j++)
                    m[col, j] /= div;

                for (int row = 0; row < n; row++)
                {
                    if (row == col) continue;
                    var factor = m[row, col];
                    for (int j = col; j <= n; j++)
                        m[row, j] -= factor * m[col, j];
                }
            }

            var x = new double[n];
            for (int i = 0; i < n; i++)
                x[i] = m[i, n];
            return x;
        }
    }
}
