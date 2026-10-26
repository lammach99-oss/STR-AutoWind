using System.Collections.Immutable;
using StructuralLoadAssistant.Domain;
namespace StructuralLoadAssistant.Engineering;

/// <summary>Explicit-input review arithmetic; contains no normative coefficient tables.</summary>
public static class AsNz2021MatrixEngine
{
    public static double DesignSpeed(IReadOnlyList<double> cardinalSpeeds, double bearing, LimitState state)
    {
        if (cardinalSpeeds.Count != 8 || cardinalSpeeds.Any(v => !double.IsFinite(v) || v <= 0)
            || !double.IsFinite(bearing) || !Enum.IsDefined(state)) throw new ArgumentException("Eight positive cardinal speeds, finite bearing and valid limit state required.");
        double Normalize(double a) => (a % 360 + 360) % 360;
        double At(double a) { double index = Normalize(a) / 45; int i = (int)Math.Floor(index); return cardinalSpeeds[i] + (cardinalSpeeds[(i + 1) % 8] - cardinalSpeeds[i]) * (index - i); }
        double maximum = Math.Max(At(bearing - 45), At(bearing + 45));
        for (int i = 0; i < 8; i++)
            if (Math.Abs(Normalize(i * 45 - bearing + 180) - 180) <= 45) maximum = Math.Max(maximum, cardinalSpeeds[i]);
        return state == LimitState.Uls ? Math.Max(30, maximum) : maximum;
    }

    public static (double ExternalPa, double InternalPa, double NetPa) Pressure(double speed, double density,
        double cdyn, double cpe, double ka, double kce, double kl, double kp, double cpi, double kci, double kv)
    {
        double[] positive = [speed, density, cdyn, ka, kce, kl, kp, kci, kv];
        if (positive.Any(v => !double.IsFinite(v) || v <= 0) || !double.IsFinite(cpe) || !double.IsFinite(cpi))
            throw new ArgumentException("Pressure inputs must be finite; physical multipliers must be positive.");
        double q = 0.5 * density * speed * speed * cdyn;
        double external = q * cpe * ka * kce * kl * kp; // Eq. 5.2(2)
        double internalPressure = q * cpi * kci * kv; // Eq. 5.2(1)
        if (!double.IsFinite(external) || !double.IsFinite(internalPressure) || !double.IsFinite(external - internalPressure))
            throw new ArgumentOutOfRangeException(nameof(speed), "Pressure overflow; check supplied SI values.");
        return (external, internalPressure, external - internalPressure);
    }
}
