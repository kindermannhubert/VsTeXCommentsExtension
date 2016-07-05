using System;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace VsTeXCommentsExtension.View
{
    /// <summary>
    /// Interaction logic for CircleProgressBar.xaml
    /// </summary>
    public partial class CircleProgressBar : UserControl, IDisposable
    {
        private Storyboard storyboard;

        public CircleProgressBar()
        {
            this.Unloaded += CircleProgressBar_Unloaded;
            this.Loaded += CircleProgressBar_Loaded;

            InitializeComponent();
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
