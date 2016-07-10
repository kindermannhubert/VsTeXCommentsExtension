using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace VsTeXCommentsExtension.View
{
    /// <summary>
    /// Interaction logic for DropDownButton.xaml
    /// </summary>
    public partial class DropDownImageButton : UserControl
    {
        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(DropDownImageButton), new PropertyMetadata(null));

        public static readonly DependencyProperty ResourcesManagerProperty = DependencyProperty.Register(nameof(ResourcesManager), typeof(ResourcesManager), typeof(DropDownImageButton), new PropertyMetadata(null));

        private DateTime contextMenuClosed;

        public DropDownImageButton()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
            {
                //this is necessary for designer when using this control from another one
                root.DataContext = new DesignTimeContexts.DropDownImageButtonDesignContext();
            }
        }

        public event RoutedEventHandler Click;

        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        public ResourcesManager ResourcesManager
        {
            get { return (ResourcesManager)GetValue(ResourcesManagerProperty); }
            set { SetValue(ResourcesManagerProperty, value); }
        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
        }

        private void ArrowButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine((DateTime.Now - contextMenuClosed).TotalMilliseconds);
            if ((DateTime.Now - contextMenuClosed).TotalMilliseconds > 500)
            {
                var contextMenu = arrowButton.ContextMenu;
                contextMenu.Closed += ContextMenu_Closed;
                contextMenu.IsEnabled = true;
                contextMenu.PlacementTarget = arrowButton;
                contextMenu.Placement = PlacementMode.Bottom;
                contextMenu.IsOpen = true;
            }
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            arrowButton.ContextMenu.Closed -= ContextMenu_Closed;
            contextMenuClosed = DateTime.Now;
        }
    }
}
