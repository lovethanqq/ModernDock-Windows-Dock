using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MyCustomDock
{
    // Small non-modal volume surface. It owns only presentation and pointer
    // lifetime; the DockWindow callback remains responsible for Core Audio.
    public sealed class VolumeFlyout : Popup
    {
        private readonly FrameworkElement placementTarget;
        private readonly Slider slider;
        private readonly TextBlock percentage;
        private readonly Path speakerGlyph;
        private readonly Path waveGlyph;
        private readonly Path muteGlyph;
        private readonly DispatcherTimer closeTimer;
        private bool updatingFromSystem;
        private bool pointerOverPopup;
        private bool pointerOverTarget;

        public VolumeFlyout(FrameworkElement target)
        {
            placementTarget = target;
            PlacementTarget = target;
            Placement = PlacementMode.Top;
            AllowsTransparency = true;
            StaysOpen = true;
            PopupAnimation = PopupAnimation.Fade;
            Focusable = false;

            var root = new Border
            {
                Width = 150,
                Height = 44,
                Padding = new Thickness(10, 4, 10, 4),
                Background = new SolidColorBrush(Color.FromArgb(238, 26, 24, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 5,
                    Opacity = 0.48
                }
            };
            AutomationProperties.SetName(root, "ModernDock.VolumeFlyout");
            AutomationProperties.SetAutomationId(root, "ModernDock.VolumeFlyout");

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

            var iconGrid = new Grid { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
            speakerGlyph = new Path
            {
                Data = Geometry.Parse("M1,7 L5,7 L10,3 L10,15 L5,11 L1,11 Z"),
                Fill = new SolidColorBrush(Color.FromArgb(232, 255, 255, 255)),
                Stretch = Stretch.Uniform,
                Width = 18,
                Height = 18
            };
            waveGlyph = new Path
            {
                Data = Geometry.Parse("M12,6 C14,8 14,10 12,12 M14,4 C18,8 18,10 14,14"),
                Stroke = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                StrokeThickness = 1.25,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent,
                Stretch = Stretch.Uniform,
                Width = 18,
                Height = 18
            };
            muteGlyph = new Path
            {
                Data = Geometry.Parse("M12,5 L18,13 M18,5 L12,13"),
                Stroke = new SolidColorBrush(Color.FromArgb(232, 255, 255, 255)),
                StrokeThickness = 1.35,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent,
                Stretch = Stretch.Uniform,
                Width = 18,
                Height = 18,
                Visibility = Visibility.Collapsed
            };
            iconGrid.Children.Add(speakerGlyph);
            iconGrid.Children.Add(waveGlyph);
            iconGrid.Children.Add(muteGlyph);
            AutomationProperties.SetName(iconGrid, "音量图标");
            Grid.SetColumn(iconGrid, 0);
            grid.Children.Add(iconGrid);

            slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Height = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Focusable = false,
                IsTabStop = false,
                Margin = new Thickness(2, 0, 4, 0)
            };
            AutomationProperties.SetName(slider, "音量");
            AutomationProperties.SetAutomationId(slider, "ModernDock.VolumeSlider");
            KeyboardNavigation.SetIsTabStop(slider, false);
            slider.ValueChanged += HandleSliderValueChanged;
            Grid.SetColumn(slider, 1);
            grid.Children.Add(slider);

            percentage = new TextBlock
            {
                Text = "--",
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            AutomationProperties.SetName(percentage, "音量百分比");
            AutomationProperties.SetAutomationId(percentage, "ModernDock.VolumePercentage");
            Grid.SetColumn(percentage, 2);
            grid.Children.Add(percentage);

            root.Child = grid;
            Child = root;

            closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
            closeTimer.Tick += (s, e) =>
            {
                if (pointerOverPopup || pointerOverTarget)
                {
                    closeTimer.Stop();
                }
                else
                {
                    Close();
                }
            };

            root.MouseEnter += (s, e) =>
            {
                pointerOverPopup = true;
                closeTimer.Stop();
            };
            root.MouseLeave += (s, e) =>
            {
                pointerOverPopup = false;
                RestartCloseTimer();
            };
            if (placementTarget != null)
            {
                placementTarget.MouseEnter += (s, e) =>
                {
                    pointerOverTarget = true;
                    closeTimer.Stop();
                };
                placementTarget.MouseLeave += (s, e) =>
                {
                    pointerOverTarget = false;
                    RestartCloseTimer();
                };
            }

            Opened += (s, e) =>
            {
                UpdatePlacementOffset();
                RestartCloseTimer();
            };
            Closed += (s, e) => closeTimer.Stop();
        }

        public event Action<float> VolumeChanged;

        public void Show()
        {
            UpdatePlacementOffset();
            IsOpen = true;
            RestartCloseTimer();
        }

        public void Close()
        {
            closeTimer.Stop();
            IsOpen = false;
        }

        public void SetTargetPointerOver(bool isOver)
        {
            pointerOverTarget = isOver;
            if (isOver) closeTimer.Stop();
            else RestartCloseTimer();
        }

        public void Refresh(float scalar, bool muted, bool stateKnown)
        {
            Refresh(scalar, muted, stateKnown, stateKnown);
        }

        public void Refresh(float scalar, bool muted, bool volumeKnown, bool muteKnown)
        {
            updatingFromSystem = true;
            try
            {
                slider.Value = volumeKnown ? Math.Max(0.0, Math.Min(100.0, scalar * 100.0f)) : 0.0;
                percentage.Text = volumeKnown ? Math.Round(scalar * 100.0f).ToString("0") + "%" : "--";
                speakerGlyph.Visibility = Visibility.Visible;
                waveGlyph.Visibility = muteKnown && !muted ? Visibility.Visible : Visibility.Collapsed;
                muteGlyph.Visibility = muteKnown && muted ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                updatingFromSystem = false;
            }
        }

        private void HandleSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updatingFromSystem || VolumeChanged == null) return;
            VolumeChanged((float)(e.NewValue / 100.0));
        }

        private void RestartCloseTimer()
        {
            if (!IsOpen || pointerOverPopup || pointerOverTarget) return;
            closeTimer.Stop();
            closeTimer.Start();
        }

        private void UpdatePlacementOffset()
        {
            double targetWidth = placementTarget == null || placementTarget.ActualWidth <= 0 ? 36 : placementTarget.ActualWidth;
            HorizontalOffset = targetWidth - 150;
            VerticalOffset = -6;
        }
    }
}
