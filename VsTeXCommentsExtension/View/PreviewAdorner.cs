using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    public class PreviewAdorner : Adorner
    {
        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(PreviewAdorner), new PropertyMetadata(null));

        private FrameworkElement child;
        public FrameworkElement Child
        {
            get { return child; }
            set
            {
                if (child != null) RemoveVisualChild(child);
                child = value;
                if (child != null) AddVisualChild(child);
            }
        }

        private double offsetX;
        public double OffsetX
        {
            get { return offsetX; }
            set
            {
                offsetX = value;
                InvalidateArrange();
            }
        }

        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        public PreviewAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            //content
            var panel = new StackPanel() { Orientation = Orientation.Vertical, Margin = new Thickness(6) };

            Binding binding;
            var image = new Image() { SnapsToDevicePixels = true, UseLayoutRounding = true };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            binding = new Binding(nameof(ImageSource)) { Source = this };
            image.SetBinding(Image.SourceProperty, binding);

            binding = new Binding(nameof(TeXCommentAdornment.RenderedImageWidth)) { Source = adornedElement };
            image.SetBinding(Image.WidthProperty, binding);

            binding = new Binding(nameof(TeXCommentAdornment.RenderedImageHeight)) { Source = adornedElement };
            image.SetBinding(Image.HeightProperty, binding);

            panel.Children.Add(new TextBlock() { Text = "Preview:", Margin = new Thickness(0, 0, 0, 2) });
            panel.Children.Add(image);

            Child = new Border() { Background = Brushes.White, BorderThickness = new Thickness(1), BorderBrush = Brushes.Black, Child = panel };

            //visibility setup
            var style = new Style(typeof(PreviewAdorner));
            style.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed));
            binding = new Binding(nameof(TeXCommentAdornment.CurrentState)) { Source = adornedElement };
            var isCarretInsideTeXBlockBinding = new Binding(nameof(TeXCommentAdornment.IsCaretInsideTeXBlock)) { Source = adornedElement };
            var visibilityTrigger = new MultiDataTrigger();
            visibilityTrigger.Conditions.Add(new Condition(binding, TeXCommentAdornmentState.EditingWithPreview));
            visibilityTrigger.Conditions.Add(new Condition(isCarretInsideTeXBlockBinding, true));
            visibilityTrigger.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible));
            style.Triggers.Add(visibilityTrigger);
            Style = style;
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException();
            return child;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            child.Measure(constraint);
            return child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            child.Arrange(new Rect(new Point(OffsetX, AdornedElement.DesiredSize.Height), finalSize));
            return new Size(child.ActualWidth, child.ActualHeight);
        }
    }
}