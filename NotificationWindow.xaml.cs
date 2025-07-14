using System;
using System.Windows;
using System.Windows.Threading;

namespace BetterNotifications
{
    public partial class NotificationWindow : Window
    {
        public Action<NotificationWindow> OnClosedExternally;

        public NotificationWindow(string title, string message)
        {
            InitializeComponent();
            TitleText.Text = title;
            ContentText.Text = message;
        }

        public void ShowAt(Point position)
        {
            Left = position.X;
            Top = position.Y;
            Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Close();
            };
            timer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            OnClosedExternally?.Invoke(this);
        }

        public void XPressed(object sender, EventArgs args)
        {
            Close();
        }
    }
}
