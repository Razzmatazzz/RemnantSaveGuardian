using Microsoft.Xaml.Behaviors;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RemnantSaveGuardian.Views.UserControls
{
    /// <summary>
    /// TextBlockPlus Control Interface
    /// </summary>
    public partial class TextBlockPlus : UserControl
    {
        public TextBlockPlus()
        {
            InitializeComponent();
        }
        #region DependencyProperties
        [Category("Extend Properties")]
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(TextBlockPlus), new PropertyMetadata(""));
        [Category("Extend Properties")]
        public int RollingSpeed
        {
            get { return (int)GetValue(RollingSpeedProperty); }
            set { SetValue(RollingSpeedProperty, value); }
        }
        public static readonly DependencyProperty RollingSpeedProperty = DependencyProperty.Register("RollingSpeed", typeof(int), typeof(TextBlockPlus), new PropertyMetadata(250));

        [Category("Extend Properties")]
        public int RollbackSpeed
        {
            get { return (int)GetValue(RollbackSpeedProperty); }
            set { SetValue(RollbackSpeedProperty, value); }
        }
        public static readonly DependencyProperty RollbackSpeedProperty = DependencyProperty.Register("RollbackSpeed", typeof(int), typeof(TextBlockPlus), new PropertyMetadata(1000));
        #endregion 
    }

    /// <summary>
    /// Rolling TextBlock Behavior
    /// </summary>
    public sealed class RollingTextBlockBehavior : Behavior<UIElement>
    {
        public int RollingSpeed
        {
            get { return (int)GetValue(RollingSpeedProperty); }
            set { SetValue(RollingSpeedProperty, value); }
        }
        public static readonly DependencyProperty RollingSpeedProperty = DependencyProperty.Register("RollingSpeed", typeof(int), typeof(RollingTextBlockBehavior), new PropertyMetadata(250));
        public int RollbackSpeed
        {
            get { return (int)GetValue(RollbackSpeedProperty); }
            set { SetValue(RollbackSpeedProperty, value); }
        }
        public static readonly DependencyProperty RollbackSpeedProperty = DependencyProperty.Register("RollbackSpeed", typeof(int), typeof(RollingTextBlockBehavior), new PropertyMetadata(1000));

        private TextBlock? _textBlock;
        private Storyboard _storyBoard = new Storyboard();
        private DoubleAnimation _animation = new DoubleAnimation();

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.MouseEnter += AssociatedObject_MouseEnter;
            AssociatedObject.MouseLeave += AssociatedObject_MouseLeave;
            AssociatedObject.MouseDown += AssociatedObject_MouseDown;
            AssociatedObject.MouseUp += AssociatedObject_MouseUp;
            AssociatedObject.PreviewMouseWheel += AssociatedObject_PreviewMouseWheel;

            DependencyProperty[] propertyChain = new DependencyProperty[]
            {
                ScrollViewerBehavior.HorizontalOffsetProperty
            };

            Storyboard.SetTargetProperty(_animation, new PropertyPath("(0)", propertyChain));
            _storyBoard.Children.Add(_animation);
        }
        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.MouseEnter -= AssociatedObject_MouseEnter;
            AssociatedObject.MouseLeave -= AssociatedObject_MouseLeave;
            AssociatedObject.MouseDown -= AssociatedObject_MouseDown;
            AssociatedObject.MouseUp -= AssociatedObject_MouseUp;
            AssociatedObject.PreviewMouseWheel -= AssociatedObject_PreviewMouseWheel;
        }
        private void AssociatedObject_MouseEnter(object sender, RoutedEventArgs e)
        {
            if (AssociatedObject is not null)
            {
                TextBlock? textBlock = AssociatedObject as TextBlock;
                if (textBlock != null)
                {
                    ScrollViewer? scrollViewer = textBlock.Parent as ScrollViewer;
                    double textWidth = textBlock.ActualWidth - scrollViewer.ActualWidth;
                    double scrollValue = scrollViewer.HorizontalOffset;
                    double scrollWidth = scrollViewer.ScrollableWidth;
                    if (scrollWidth > 0 && RollingSpeed > 0)
                    {
                        double time = (scrollWidth - scrollValue) / scrollWidth * (textWidth / RollingSpeed);
                        _animation.To = scrollWidth;
                        _animation.Duration = TimeSpan.FromSeconds(time);
                        _animation.BeginTime = TimeSpan.FromMilliseconds(200);
                        _storyBoard.Begin(scrollViewer, true);
                    }
                }
            }
        }
        private void AssociatedObject_MouseLeave(object sender, RoutedEventArgs e)
        {
            if (AssociatedObject is not null)
            {
                TextBlock? textBlock = AssociatedObject as TextBlock;
                if (textBlock != null)
                {
                    ScrollViewer? scrollViewer = textBlock.Parent as ScrollViewer;
                    double textWidth = textBlock.ActualWidth - scrollViewer.ActualWidth;
                    double scrollValue = scrollViewer.HorizontalOffset;
                    double scrollWidth = scrollViewer.ScrollableWidth;
                    if (scrollWidth > 0 && RollingSpeed > 0)
                    {
                        double time = scrollValue / scrollWidth * (textWidth / RollbackSpeed);
                        _animation.To = 0;
                        _animation.Duration = TimeSpan.FromSeconds(time);
                        _animation.BeginTime = TimeSpan.FromMilliseconds(200);
                        _storyBoard.Begin(scrollViewer, true);
                    }
                }
            }
        }
        private void AssociatedObject_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (AssociatedObject is not null)
            {
                TextBlock? textBlock = AssociatedObject as TextBlock;
                if (textBlock != null && e.LeftButton == MouseButtonState.Pressed)
                {
                    ScrollViewer? scrollViewer = textBlock.Parent as ScrollViewer;
                    _storyBoard.Pause(scrollViewer);
                }

                MouseButton button = MouseButton.Middle;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    button = MouseButton.Left;
                } else if (e.RightButton == MouseButtonState.Pressed) {
                    button = MouseButton.Right;
                }
                MouseButtonEventArgs eBack = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, button);
                eBack.RoutedEvent = UIElement.MouseDownEvent;

                TextBlockPlus? ui = VisualUpwardSearch<TextBlockPlus>(AssociatedObject) as TextBlockPlus;
                ui.RaiseEvent(eBack);
            }
        }
        private void AssociatedObject_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (AssociatedObject is not null)
            {
                TextBlock? textBlock = AssociatedObject as TextBlock;
                if (textBlock != null)
                {
                    ScrollViewer? scrollViewer = textBlock.Parent as ScrollViewer;
                    _storyBoard.Resume(scrollViewer);
                }

                MouseButton button = MouseButton.Middle;
                if (e.LeftButton == MouseButtonState.Released)
                {
                    button = MouseButton.Left;
                }
                else if (e.RightButton == MouseButtonState.Released)
                {
                    button = MouseButton.Right;
                }

                MouseButtonEventArgs eBack = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, button);
                eBack.RoutedEvent = UIElement.MouseUpEvent;

                TextBlockPlus? ui = VisualUpwardSearch<TextBlockPlus>(AssociatedObject) as TextBlockPlus;
                ui.RaiseEvent(eBack);
            }
        }
        private void AssociatedObject_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            MouseWheelEventArgs eBack = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
            eBack.RoutedEvent = UIElement.MouseWheelEvent;

            TextBlockPlus? ui = VisualUpwardSearch<TextBlockPlus>(AssociatedObject) as TextBlockPlus;
            ui.RaiseEvent(eBack);
        }
        private DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            while (source != null && source.GetType() != typeof(T))
            {
                source = VisualTreeHelper.GetParent(source);
            }
            return source;
        }
    }
    public static class ScrollViewerBehavior
    {
        public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.RegisterAttached("HorizontalOffset", typeof(double), typeof(ScrollViewerBehavior), new UIPropertyMetadata(0.0, OnHorizontalOffsetChanged));
        public static void SetHorizontalOffset(FrameworkElement target, double value)
        {
            target.SetValue(HorizontalOffsetProperty, value);
        }
        public static double GetHorizontalOffset(FrameworkElement target)
        {
            return (double)target.GetValue(HorizontalOffsetProperty);
        }
        private static void OnHorizontalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            ScrollViewer? view = target as ScrollViewer;
            if (view != null)
            {
                view.ScrollToHorizontalOffset((double)e.NewValue);
            }
        }
    }
}
