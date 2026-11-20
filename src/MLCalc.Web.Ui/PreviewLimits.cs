namespace MLCalc.Web.Ui;

/// <summary>
/// How far a 3D window is thinned out while the user turns or zooms it (see
/// <see cref="MLCalc.Plotting.Scene3D.Preview(MLCalc.Plotting.Axes, int, int)"/>). The web uses the plot
/// model's own limits; the app thins out further, because each drawing is carried from .NET to its
/// web view as text, which costs more than drawing it.
/// </summary>
/// <param name="GridLines">The most grid lines of a surface each way, at least 2.</param>
/// <param name="Points">About the most points of a curve or dot cloud.</param>
public sealed record PreviewLimits(int GridLines, int Points);
