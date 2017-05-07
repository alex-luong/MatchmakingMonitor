﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using MatchingMakingMonitor.Models;
using Newtonsoft.Json;
using RestSharp;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MatchingMakingMonitor.SocketIO;
using System.Reactive.Linq;

namespace MatchingMakingMonitor
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		public MainWindow(SocketIOService socketIOService)
		{
			InitializeComponent();
		}

		private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
		{
			Scroller.Height = e.NewSize.Height - Header.ActualHeight;
		}
	}
}