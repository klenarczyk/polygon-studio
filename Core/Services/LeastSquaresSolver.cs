namespace PolygonStudio.Core.Services;

/// <summary>
/// Minimizes the sum of squared residuals using
/// Levenberg-Marquardt (damped Gauss-Newton) iteration.
/// </summary>
public sealed class LeastSquaresSolver
{
    private readonly LeastSquaresOptions _options;

    public LeastSquaresSolver(LeastSquaresOptions? options = null)
    {
        _options = options ?? new LeastSquaresOptions();

        if (_options.MaxIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxIterations must be positive.");

        if (_options.GradientTolerance <= 0 ||
            _options.StepTolerance <= 0 ||
            _options.InitialDamping <= 0 ||
            _options.MaximumDamping <= _options.InitialDamping ||
            _options.FiniteDifferenceStep <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), "Solver tolerances and damping values are invalid."
            );
        }
    }

    public LeastSquaresResult Solve(double[] initialVariables, Func<double[], double[]> residualFunction)
    {
        var x = (double[])initialVariables.Clone();
        double[] residuals = Evaluate(residualFunction, x);
        double cost = Cost(residuals);
        double damping = _options.InitialDamping;

        for (var iteration = 0; iteration < _options.MaxIterations; iteration++)
        {
            double[,] jacobian = BuildJacobian(x, residuals, residualFunction);
            (double[,] normalMatrix, double[] gradient) = BuildNormalMatrix(jacobian, residuals);

            if (InfinityNorm(gradient) <= _options.GradientTolerance)
                return LeastSquaresResult.Success(x, "Gradient tolerance reached.");

            var system = (double[,])normalMatrix.Clone();

            for (var i = 0; i < x.Length; i++)
            {
                // Scale damping by the curvature of each variable.
                system[i, i] += damping * Math.Max(normalMatrix[i, i], 1e-12);
            }

            double[] rhs = gradient.Select(g => -g).ToArray();

            if (!TrySolveLinearSystem(system, rhs, out double[] delta))
            {
                damping *= 10;

                if (damping > _options.MaximumDamping)
                {
                    return LeastSquaresResult.Failure(x, "Could not solve the damped linear system.");
                }

                continue;
            }

            var candidate = new double[x.Length];

            for (var i = 0; i < x.Length; i++)
                candidate[i] = x[i] + delta[i];

            if (candidate.Any(v => !double.IsFinite(v)))
            {
                damping *= 10;
                continue;
            }

            double[] candidateResiduals = Evaluate(residualFunction, candidate);
            double candidateCost = Cost(candidateResiduals);

            if (candidateCost < cost) // Making progress
            {
                double stepNorm = Norm(delta);
                double variableNorm = Norm(x);

                x = candidate;
                residuals = candidateResiduals;
                cost = candidateCost;
                damping = Math.Max(damping / 3, 1e-15);

                if (stepNorm <= _options.StepTolerance * (variableNorm + _options.StepTolerance))
                {
                    return LeastSquaresResult.Success(x, "Step tolerance reached.");
                }
            }
            else // Not making progress
            {
                damping *= 10;

                if (damping > _options.MaximumDamping)
                {
                    return LeastSquaresResult.Failure(x, "No improving step found; the solver stalled.");
                }
            }
        }

        return LeastSquaresResult.Failure(x, "Maximum iteration count reached.");
    }

    private double[,] BuildJacobian(double[] x, double[] baseResiduals, Func<double[], double[]> residualFunction)
    {
        var jacobian = new double[baseResiduals.Length, x.Length];

        for (var col = 0; col < x.Length; col++)
        {
            var perturbed = (double[])x.Clone();
            double step = _options.FiniteDifferenceStep *
                          Math.Max(1.0, Math.Abs(x[col]));

            perturbed[col] += step;

            double[] perturbedResiduals = Evaluate(residualFunction, perturbed);

            if (perturbedResiduals.Length != baseResiduals.Length)
            {
                throw new InvalidOperationException(
                    "The residual function must return the same number of residuals for every variable vector."
                );
            }

            for (var row = 0; row < baseResiduals.Length; row++)
            {
                jacobian[row, col] = (perturbedResiduals[row] - baseResiduals[row]) / step;
            }
        }

        return jacobian;
    }

    private static (double[,] Matrix, double[] Gradient) BuildNormalMatrix(double[,] jacobian, double[] residuals)
    {
        int rows = jacobian.GetLength(0);
        int columns = jacobian.GetLength(1);

        var matrix = new double[columns, columns];
        var gradient = new double[columns];

        for (var i = 0; i < columns; i++)
        {
            for (var row = 0; row < rows; row++)
                gradient[i] += jacobian[row, i] * residuals[row];

            for (int j = i; j < columns; j++)
            {
                var sum = 0.0;

                for (var row = 0; row < rows; row++)
                    sum += jacobian[row, i] * jacobian[row, j];

                matrix[i, j] = sum;
                matrix[j, i] = sum;
            }
        }

        return (matrix, gradient);
    }

    private static bool TrySolveLinearSystem(double[,] matrix, double[] rhs, out double[] solution)
    {
        int n = rhs.Length;
        solution = new double[n];

        // Gaussian elimination with partial pivoting.
        for (var column = 0; column < n; column++)
        {
            var pivot = column;

            for (var row = column + 1; row < n; row++)
            {
                if (Math.Abs(matrix[row, column]) >
                    Math.Abs(matrix[pivot, column]))
                {
                    pivot = row;
                }
            }

            if (Math.Abs(matrix[pivot, column]) < 1e-20)
                return false;

            if (pivot != column)
            {
                for (int j = column; j < n; j++)
                    (matrix[column, j], matrix[pivot, j]) =
                        (matrix[pivot, j], matrix[column, j]);

                (rhs[column], rhs[pivot]) =
                    (rhs[pivot], rhs[column]);
            }

            double divisor = matrix[column, column];

            for (int j = column; j < n; j++)
                matrix[column, j] /= divisor;

            rhs[column] /= divisor;

            for (var row = 0; row < n; row++)
            {
                if (row == column)
                    continue;

                double factor = matrix[row, column];

                for (int j = column; j < n; j++)
                    matrix[row, j] -= factor * matrix[column, j];

                rhs[row] -= factor * rhs[column];
            }
        }

        Array.Copy(rhs, solution, n);
        return solution.All(double.IsFinite);
    }

    // ---
    
    private static double[] Evaluate(Func<double[], double[]> residualFunction, double[] variables) =>
        residualFunction((double[])variables.Clone());

    private static double Cost(double[] residuals) => residuals.Sum(r => r * r) * 0.5;
    
    private static double Norm(double[] values) => Math.Sqrt(values.Sum(v => v * v));

    private static double InfinityNorm(double[] values) => values.Length == 0 ? 0 : values.Max(Math.Abs);
}

public sealed class LeastSquaresOptions
{
    public int MaxIterations { get; init; } = 1000;
    public double GradientTolerance { get; init; } = 1e-8;
    public double StepTolerance { get; init; } = 1e-9;
    public double InitialDamping { get; init; } = 1e-3;
    public double MaximumDamping { get; init; } = 1e15;
    public double FiniteDifferenceStep { get; init; } = 1e-6;
}

public sealed record LeastSquaresResult(double[] Variables, bool Converged, string? Message = null)
{
    public static LeastSquaresResult Success(double[] variables, string? message = null) =>
        new((double[])variables.Clone(), true, message);
    
    public static LeastSquaresResult Failure(double[] variables, string message) =>
        new((double[])variables.Clone(), false, message);
}