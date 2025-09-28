using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace YouTube
{
	public class CustomMediaTransportControls : MediaTransportControls
	{
		private Button _settingsButton;
		private Border _settingsBorder;
		private bool _isFullscreen;
		private Windows.UI.Xaml.DispatcherTimer _hideTimer;
		public event EventHandler SettingsClicked;

		public bool IsFullscreen
		{
			get { return _isFullscreen; }
			set
			{
				_isFullscreen = value;
				ApplyButtonStyle();
			}
		}

		public CustomMediaTransportControls()
		{
			this.DefaultStyleKey = typeof(MediaTransportControls);
		}

		protected override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			// Create a small settings button and place it inside the control's root grid top-right overlay area
			var root = GetTemplateChild("RootGrid") as Grid;
			if (root != null)
			{
				// Create a border container for rounded corners (UWP Button doesn't support CornerRadius)
				_settingsBorder = new Border
				{
					Width = 36,
					Height = 36,
					HorizontalAlignment = HorizontalAlignment.Right,
					VerticalAlignment = VerticalAlignment.Top,
					Margin = new Thickness(10),
					Background = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x00, 0x00)),
					CornerRadius = new CornerRadius(18)
				};
							
				_settingsButton = new Button
				{
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Stretch,
					Background = new SolidColorBrush(Colors.Transparent),
					BorderBrush = null,
					BorderThickness = new Thickness(0)
				};
				var icon = new SymbolIcon(Symbol.Setting) { Foreground = new SolidColorBrush(Colors.White) };
				_settingsButton.Content = icon;
				_settingsButton.Click += SettingsButton_Click;
				_settingsBorder.Child = _settingsButton;
				root.Children.Add(_settingsBorder);

				// Setup hide timer for fullscreen auto-hide behavior matching player
				_hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
				_hideTimer.Tick += (s, e) =>
				{
					_hideTimer.Stop();
					if (_isFullscreen && _settingsBorder != null)
					{
						_settingsBorder.Visibility = Visibility.Collapsed;
					}
				};

				// React to user interaction to show temporarily in fullscreen
				this.PointerMoved += (s, e) => ShowAndScheduleHide();
				this.Tapped += (s, e) => ShowAndScheduleHide();

				ApplyButtonStyle();
			}
		}

		private void ShowAndScheduleHide()
		{
			if (_settingsButton == null || _settingsBorder == null) return;
			if (_isFullscreen)
			{
				_settingsBorder.Visibility = Visibility.Visible;
				_settingsBorder.Opacity = 1;
				_hideTimer?.Stop();
				_hideTimer?.Start();
			}
		}

		private void ApplyButtonStyle()
		{
			if (_settingsButton == null || _settingsBorder == null) return;
			// In fullscreen: fully transparent background, still clickable; otherwise subtle dim background
			_settingsBorder.Background = _isFullscreen
				? new SolidColorBrush(Colors.Transparent)
				: new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x00, 0x00));
			
			if (_isFullscreen)
			{
				_settingsBorder.Visibility = Visibility.Collapsed; // hidden until interaction
				_settingsBorder.Opacity = 1;
			}
			else
			{
				_settingsBorder.Visibility = Visibility.Visible;
				_settingsBorder.Opacity = 1;
			}
		}

		private void SettingsButton_Click(object sender, RoutedEventArgs e)
		{
			// Raise an event; use the button as sender so host can anchor flyout
			if (SettingsClicked != null)
			{
				SettingsClicked(_settingsButton, EventArgs.Empty);
			}
		}
	}
}

