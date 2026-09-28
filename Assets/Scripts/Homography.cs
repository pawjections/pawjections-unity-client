using UnityEngine;

/// <summary>
/// A perspective transform between two planes, such as the camera image and
/// the projected screen, fitted from four corresponding points.
/// </summary>
public readonly struct Homography
{
    // Row-major 3x3 matrix with the last element fixed at 1
    readonly double[] _m;

    Homography(double[] m) => _m = m;

    /// <summary>
    /// Fits the transform taking each point in <paramref name="from"/> to the matching
    /// point in <paramref name="to"/>. Fails when three of the points are in a line.
    /// </summary>
    public static bool TryFit(Vector2[] from, Vector2[] to, out Homography homography)
    {
        // Each pair gives two linear equations in the eight unknown matrix entries:
        //   x*m0 + y*m1 + m2 - u*x*m6 - u*y*m7 = u
        //   x*m3 + y*m4 + m5 - v*x*m6 - v*y*m7 = v
        var a = new double[8, 9];
        for (int i = 0; i < 4; i++)
        {
            double x = from[i].x, y = from[i].y, u = to[i].x, v = to[i].y;
            double[] rowU = { x, y, 1, 0, 0, 0, -u * x, -u * y, u };
            double[] rowV = { 0, 0, 0, x, y, 1, -v * x, -v * y, v };
            for (int j = 0; j < 9; j++)
            {
                a[2 * i, j] = rowU[j];
                a[2 * i + 1, j] = rowV[j];
            }
        }

        homography = default;
        var m = SolveAugmented(a);
        if (m == null)
            return false;

        homography = new Homography(new[] { m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], 1.0 });
        return true;
    }

    public Vector2 Map(Vector2 p)
    {
        double w = _m[6] * p.x + _m[7] * p.y + _m[8];
        return new Vector2(
            (float)((_m[0] * p.x + _m[1] * p.y + _m[2]) / w),
            (float)((_m[3] * p.x + _m[4] * p.y + _m[5]) / w));
    }

    /// <summary>Gaussian elimination with partial pivoting on an n x (n+1) augmented matrix.</summary>
    static double[] SolveAugmented(double[,] a)
    {
        int n = a.GetLength(0);
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < n; row++)
                if (System.Math.Abs(a[row, col]) > System.Math.Abs(a[pivot, col]))
                    pivot = row;
            if (System.Math.Abs(a[pivot, col]) < 1e-12)
                return null;

            for (int j = col; j <= n; j++)
                (a[col, j], a[pivot, j]) = (a[pivot, j], a[col, j]);

            for (int row = 0; row < n; row++)
            {
                if (row == col)
                    continue;
                double factor = a[row, col] / a[col, col];
                for (int j = col; j <= n; j++)
                    a[row, j] -= factor * a[col, j];
            }
        }

        var x = new double[n];
        for (int i = 0; i < n; i++)
            x[i] = a[i, n] / a[i, i];
        return x;
    }
}
