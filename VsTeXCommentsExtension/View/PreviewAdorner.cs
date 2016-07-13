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

        public PreviewAdorner(UIElement adornedElement, IResourcesManager resourcesManager, IVsSettings vsSettings)
            : base(adornedElement)
        {
            //content
            var panel = new StackPanel() { Orientation = Orientation.Vertical, Margin = new Thickness(6, 6, 8, 8), UseLayoutRounding = true, SnapsToDevicePixels = true };

            var image = new Image() { SnapsToDevicePixels = true, UseLayoutRounding = true };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            image.SetBinding(Image.SourceProperty, new Binding(nameof(ImageSource)) { Source = this });
            image.SetBinding(Image.WidthProperty, new Binding(nameof(TeXCommentAdornment.RenderedImageWidth)) { Source = adornedElement });
            image.SetBinding(Image.HeightProperty, new Binding(nameof(TeXCommentAdornment.RenderedImageHeight)) { Source = adornedElement });

            var textBlock = new TextBlock() { Text = "Preview:", Margin = new Thickness(0, 0, 0, 2) };
            textBlock.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(IResourcesManager.ForegroundUI)) { Source = resourcesManager });
            panel.Children.Add(textBlock);
            panel.Children.Add(image);

            var border = new Border() { BorderThickness = new Thickness(1), Child = panel };
            border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(IResourcesManager.ForegroundUI)) { Source = resourcesManager });
            border.SetBinding(Border.BackgroundProperty, new Binding(nameof(IVsSettings.CommentsBackground)) { Source = vsSettings });
            Child = border;

            //visibility setup
            var style = new Style(typeof(PreviewAdorner));
            style.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed));
            var isCarretInsideTeXBlockBinding = new Binding(nameof(TeXCommentAdornment.IsCaretInsideTeXBlock)) { Source = adornedElement };
            var visibilityTrigger = new MultiDataTrigger();
            visibilityTrigger.Conditions.Add(new Condition(new Binding(nameof(TeXCommentAdornment.CurrentState)) { Source = adornedElement }, TeXCommentAdornmentState.EditingWithPreview));
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