using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace VsTeXCommentsExtension.View
{
    /// <summary>
    /// Interaction logic for CircleProgressBar.xaml
    /// </summary>
    public partial class CircleProgressBar : UserControl, IDisposable
    {
        public static readonly DependencyProperty ResourcesManagerProperty = DependencyProperty.Register(nameof(ResourcesManager), typeof(ResourcesManager), typeof(CircleProgressBar), new PropertyMetadata(null));

        private Storyboard storyboard;

        public CircleProgressBar()
        {
            Unloaded += CircleProgressBar_Unloaded;
            Loaded += CircleProgressBar_Loaded;

            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
            {
                //this is necessary for designer when using this control from another one
                root.DataContext = new DesignTimeContexts.CircleProgressBarDesignContext();
            }
        }

        public ResourcesManager ResourcesManager
        {
            get { return (ResourcesManager)GetValue(ResourcesManagerProperty); }
            set { SetValue(ResourcesManagerProperty, value); }
        }

        private void CircleProgressBar_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (storyboard == null)
            {
                storyboard = (Storyboard)FindResource("MainStoryboard");
                BeginStoryboard(storyboard);
            }
            else
            {
                storyboard.Resume();
            }
        }

        private void CircleProgressBar_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            storyboard?.Pause();
        }

        public void Dispose()
        {
            storyboard?.Stop();
        }
    }
}
