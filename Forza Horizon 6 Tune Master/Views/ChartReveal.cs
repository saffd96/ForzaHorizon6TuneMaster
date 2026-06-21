using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Forza_Horizon_6_Tune_Master.Views;

/// <summary>
/// Three-phase transition for the imperatively-drawn charts (gear chart, power
/// curve), run only when the underlying data changes (not on resize/locale):
///   1. remove  — fade the current chart out,
///   2. redraw  — rebuild the chart with the new data while it's hidden,
///   3. show    — fade the new chart in.
/// Opacity is composited on the GPU, so the fade stays smooth even though the
/// chart is hundreds of individually-positioned shapes.
/// </summary>
internal static class ChartReveal
{
    private static readonly Duration FadeOutDuration = new(TimeSpan.FromMilliseconds(220));
    private static readonly Duration FadeInDuration  = new(TimeSpan.FromMilliseconds(320));

    public static void Transition(Canvas canvas, double width, double height, Action redraw, Action onCompleted)
    {
        if (width <= 0 || height <= 0) { redraw(); onCompleted(); return; }

        // Phase 1 — remove: fade the current chart out.
        var fadeOut = new DoubleAnimation(canvas.Opacity, 0.0, FadeOutDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, __) =>
        {
            // Phase 2 — redraw with the new data while fully transparent.
            redraw();

            // Phase 3 — show: fade the new chart in.
            var fadeIn = new DoubleAnimation(0.0, 1.0, FadeInDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            fadeIn.Completed += (_, __) =>
            {
                // Release the animation hold so later non-animated redraws stay visible.
                canvas.BeginAnimation(UIElement.OpacityProperty, null);
                canvas.Opacity = 1.0;
                onCompleted();
            };
            canvas.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        };
        canvas.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }
}
