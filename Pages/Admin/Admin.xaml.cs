using System;
using System.Windows;
using System.Windows.Controls;
using MonitoringApp.Services;

namespace MonitoringApp.Pages.Admin
{
	public partial class Admin : Window
	{
		private readonly SerialPortService _serialService;

		public Admin()
		{
			InitializeComponent();
			_serialService = new SerialPortService();
		}

		private void LogoutButton_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}


		private void OpenSerialMonitor_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// hide the existing right main content
				if (RightMainGrid != null)
					RightMainGrid.Visibility = Visibility.Collapsed;

				var control = new SerialMonitorControl(_serialService);
				control.RequestClose += (s, ev) =>
				{
					Application.Current.Dispatcher.BeginInvoke(new Action(() =>
					{
						if (RootGrid.Children.Contains(control)) RootGrid.Children.Remove(control);
						if (RightMainGrid != null) RightMainGrid.Visibility = Visibility.Visible;
					}));
				};

				Grid.SetColumn(control, 1);
				Grid.SetRow(control, 0);
				Grid.SetRowSpan(control, 3);
				RootGrid.Children.Add(control);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Unable to open Serial Monitor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}
}

