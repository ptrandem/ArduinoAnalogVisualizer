using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Linq;

namespace AnalogVisualizer
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	
	public enum BlendMode
	{
		Normal,
		Short,
		Blend,
		Clear
	}

	public partial class MainWindow
	{
		private const int Speed = 1;
		private const int MaxValue = 1024;

		private double _adjustmentRatio = 0.488;
		private bool _isRunning;
		private int _xLoc;
		private double _canvasWidth;
		private double _canvasHeight = 500;

		private readonly SerialPort _comPort = new SerialPort();
		private bool[] _showChannel = new[]{ true, false, false, false, false, false};

		private int _numberActive = 1;
		private string _leftoverCharacters = "";
		private BlendMode _currentBlendMode = BlendMode.Normal;
		private string _xIncrementInput;

		public MainWindow()
		{
			InitializeComponent();

			PlotCanvas.Background = Brushes.Black;
			StartStopButton.IsEnabled = false;
			_comPort.DataReceived += ComPortDataReceived;

			var blendModes = Enum.GetNames(typeof(BlendMode));
			foreach (var mode in blendModes)
			{
				BlendModeSelector.Items.Add(mode);
			}
			BlendModeSelector.SelectedIndex = (int)_currentBlendMode;

			A0.IsChecked = true;
			A1.IsChecked = false;
			A2.IsChecked = false;
			A3.IsChecked = false;
			A4.IsChecked = false;
			A5.IsChecked = false;

			var portNames = SerialPort.GetPortNames();

			foreach (var portName in portNames)
			{
				ComPort.Items.Add(portName);
			}

			ComPort.SelectionChanged += ComPortSelectionChanged;
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			_isRunning = false;
			if (_comPort.IsOpen)
			{
				Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _comPort.Close()));
			}
			base.OnClosing(e);
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			_canvasWidth = sizeInfo.NewSize.Width - 10;
			_canvasHeight = PlotCanvas.ActualHeight;
			_adjustmentRatio = _canvasHeight / MaxValue;
			SetupCurrentMode();
			base.OnRenderSizeChanged(sizeInfo);
		}

		protected void ComPortSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			_comPort.PortName = ComPort.SelectedValue.ToString();
			_comPort.BaudRate = 9600;
			_comPort.Open();
			if (_comPort.IsOpen)
			{
				StartStopButton.IsEnabled = true;
				TranslateCheckboxes(null, null);
			}
		}

		protected void ComPortDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			RenderLine();
		}

		private void StartStop_Click(object sender, RoutedEventArgs e)
		{
			if (_isRunning)
			{
				_isRunning = false;
				StartStopButton.Content = "Start";
			}
			else
			{
				_xLoc = 0;
				Plotter0.Reset();
				Plotter1.Reset();
				Plotter2.Reset();
				Plotter3.Reset();
				Plotter4.Reset();
				Plotter5.Reset();
				_isRunning = true;
				StartStopButton.Content = "Stop";

			}
		}

		private void TranslateCheckboxes(object sender, RoutedEventArgs e)
		{
			//Set state
			_showChannel[0] = A0.IsChecked ?? false;
			_showChannel[1] = A1.IsChecked ?? false;
			_showChannel[2] = A2.IsChecked ?? false;
			_showChannel[3] = A3.IsChecked ?? false;
			_showChannel[4] = A4.IsChecked ?? false;
			_showChannel[5] = A5.IsChecked ?? false;
			
			_numberActive = 6;

			// Reset, if necessary; also set active count for blend-mode rendering calculations 
			if (!_showChannel[0]) { Plotter0.Reset(); _numberActive--; }
			if (!_showChannel[1]) { Plotter1.Reset(); _numberActive--; }
			if (!_showChannel[2]) { Plotter2.Reset(); _numberActive--; }
			if (!_showChannel[3]) { Plotter3.Reset(); _numberActive--; }
			if (!_showChannel[4]) { Plotter4.Reset(); _numberActive--; }
			if (!_showChannel[5]) { Plotter5.Reset(); _numberActive--; }

			if (_comPort.IsOpen)
			{
				//Setup configuration byte to send to device
				byte configuration = 0x00;
				byte mask = 0x01;
				_xIncrementInput = null;
				for (int i = 0; i < 6; i++)
				{
					if(_showChannel[i])
					{
						if (_xIncrementInput == null)
						{
							//Set x-increment input (basically the first input to trigger on)
							//This allows us to determine when the next "column" should be rendered
							_xIncrementInput = "A" + i;
						}
						configuration |= mask;
					}
					mask <<= 1;
				}
				
				if (Ref3.IsChecked ?? false) configuration |= 0x80;
				_comPort.Write(new [] { configuration }, 0, 1);
			}


			
			//if (_showA0) { _xIncrementInput = "A0"; }
			//else if (_showA1) { _xIncrementInput = "A1"; }
			//else if (_showA2) { _xIncrementInput = "A2"; }
			//else if (_showA3) { _xIncrementInput = "A3"; }
			//else if (_showA4) { _xIncrementInput = "A4"; }
			//else if (_showA5) { _xIncrementInput = "A5"; }

			if (_currentBlendMode == BlendMode.Blend)
			{
				// reconfigure mode if blendmode is active (to allow for recalculations)
				SetupCurrentMode();
			}
		}

		private void RenderLine()
		{
			if (!_isRunning)
			{
				// If we don't discard this data, it'll stay in the buffer
				// and be rendered very quickly when we click "start" again.
				// (Which is actually kinda cool, but makes for an 
				//  unexpected and unnatural experience.)

				_comPort.DiscardInBuffer();
				return;
			}

			// Get everything from the buffer and process it now (ideally this is 
			var rawData = _comPort.ReadExisting();
			
			// It's possible to get serial data in an "in-between" state
			// so we're going to look for the last \n as an end of valid data, and
 			// store anything after that for the next round (at which time we prepend
			// the previous run's leftover characters).
			// Seems hackey, but it works.

			rawData = string.Concat(_leftoverCharacters, rawData);

			if (!rawData.EndsWith("\n"))
			{
				var lastNewline = rawData.LastIndexOf('\n');
				if (lastNewline == -1)
				{
					_leftoverCharacters = rawData;
					return;
				}
				_leftoverCharacters = rawData.Substring(lastNewline, rawData.Length - lastNewline);
				rawData = rawData.Remove(lastNewline, rawData.Length - lastNewline);
			}
			else
			{
				//Perfect capture! Gotta clear out the leftover data from the last run.
				_leftoverCharacters = "";
			}

			var data = rawData.Split('\n');
			foreach (string dataLine in data)
			{
				//Line format: "<channel>:<value>" (example: A0:1003)
				//Token[0] is label (ex: A0)
				//Token[1] is value (ex: 1003)
				var tokens = dataLine.Split(':');
				if (tokens.Count() < 2) continue;

				// Parse and Adjust input
				int input;
				if (!Int32.TryParse(tokens[1], out input)) continue;
				var adjustedInput = _canvasHeight - (input*_adjustmentRatio);

				if(tokens[0] == _xIncrementInput)
				{
					_xLoc += Speed;
				}

				if (_xLoc > _canvasWidth)
				{
					Dispatcher.Invoke(DispatcherPriority.Background,
					                  new Action(() =>
					                             	{
					                             		//ResetPlotters();
					                             	}));
					_xLoc = 0;
				}

				Brush combiBrush;

				var channelString = tokens[0].Replace("A", "");
				int channel;
				if (!int.TryParse(channelString, out channel))
				{
					continue;
				}

				

				if (!_showChannel[channel]) continue;
				if (_currentBlendMode != BlendMode.Blend)
				{

					Dispatcher.Invoke(DispatcherPriority.Background,
							            new Action(() =>
							                        {
														var plotter = FindName("Plotter" + channel) as Plotter;
														var text = FindName(string.Format("Text{0}", channel)) as TextBlock;
														if (plotter != null && text != null)
														{
															text.Text = input.ToString();
															plotter.Plot(_xLoc + Speed, adjustedInput);
														}
							                        }
							            ));
				}
				else
				{
					Dispatcher.Invoke(DispatcherPriority.Background,
							            new Action(() =>
							                        {
														var plotter = FindName(string.Format("Plotter{0}", channel)) as Plotter;
														var text = FindName(string.Format("Text{0}", channel)) as TextBlock;
														if (plotter != null && text != null)
														{
															text.Text = input.ToString();
															combiBrush = plotter.DefaultBrush.CloneCurrentValue();
															combiBrush.Opacity = 0.8;
															Combiplotter.Plot(_xLoc + Speed, adjustedInput, combiBrush, plotter.DefaultStroke);
														}
							                        }
							            ));
				}

				//switch (tokens[0])
				//{
				//    case "A0":
				//        if (!_showA0) continue;
				//        if (_currentBlendMode != BlendMode.Blend)
				//        {

				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A0val.Text = input.ToString();
				//                                                Plotter0.Plot(_xLoc + Speed, adjustedInput);
				//                                            }
				//                                ));
				//        }
				//        else
				//        {
				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A0val.Text = input.ToString();
				//                                                combiBrush = Plotter0.DefaultBrush.CloneCurrentValue();
				//                                                combiBrush.Opacity = 0.8;
				//                                                Combiplotter.Plot(_xLoc + Speed, adjustedInput, combiBrush, Plotter0.DefaultStroke);
				//                                            }
				//                                ));
				//        }
				//        break;
				//    case "A1":
				//        if (!_showA1) continue;
				//        if (_currentBlendMode != BlendMode.Blend)
				//        {

				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A1val.Text = input.ToString();
				//                                                Plotter1.Plot(_xLoc + Speed, adjustedInput);
				//                                            }
				//                                ));
				//        }
				//        else
				//        {
				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A1val.Text = input.ToString();
				//                                                combiBrush = Plotter1.DefaultBrush.CloneCurrentValue();
				//                                                combiBrush.Opacity = 0.8;
				//                                                Combiplotter.Plot(_xLoc + Speed, adjustedInput, combiBrush, Plotter1.DefaultStroke);
				//                                            }
				//                                ));
				//        }
				//        break;
				//    case "A2":
				//        if (!_showA2) continue;
				//        if (_currentBlendMode != BlendMode.Blend)
				//        {

				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A2val.Text = input.ToString();
				//                                                Plotter2.Plot(_xLoc + Speed, adjustedInput);
				//                                            }
				//                                ));
				//        }
				//        else
				//        {
				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A2val.Text = input.ToString();
				//                                                combiBrush = Plotter2.DefaultBrush.CloneCurrentValue();
				//                                                combiBrush.Opacity = 0.8;
				//                                                Combiplotter.Plot(_xLoc + Speed, adjustedInput, combiBrush, Plotter2.DefaultStroke);
				//                                            }
				//                                ));
				//        }
				//        break;
				//    case "A3":
				//        if (!_showA3) continue;
				//        if (_currentBlendMode != BlendMode.Blend)
				//        {

				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A3val.Text = input.ToString();
				//                                                Plotter3.Plot(_xLoc + Speed, adjustedInput);
				//                                            }
				//                                ));
				//        }
				//        else
				//        {
				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A3val.Text = input.ToString();
				//                                                combiBrush = Plotter3.DefaultBrush.CloneCurrentValue();
				//                                                combiBrush.Opacity = 0.8;
				//                                                Combiplotter.Plot(_xLoc + Speed, adjustedInput, combiBrush, Plotter3.DefaultStroke);
				//                                            }
				//                                ));
				//        }
				//        break;
				//    case "A4":
				//        if (!_showA4) continue;
				//        if (_currentBlendMode != BlendMode.Blend)
				//        {

				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A4val.Text = input.ToString();
				//                                                Plotter4.Plot(_xLoc + Speed, adjustedInput);
				//                                            }
				//                                ));
				//        }
				//        else
				//        {
				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A4val.Text = input.ToString();
				//                                                combiBrush = Plotter4.DefaultBrush.CloneCurrentValue();
				//                                                combiBrush.Opacity = 0.8;
				//                                                Combiplotter.Plot(_xLoc + Speed, adjustedInput, combiBrush, Plotter4.DefaultStroke);
				//                                            }
				//                                ));
				//        }
				//        break;
				//    case "A5":
				//        if (!_showA5) continue;
				//        if (_currentBlendMode != BlendMode.Blend)
				//        {

				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                                            {
				//                                                A5val.Text = input.ToString();
				//                                                Plotter5.Plot(_xLoc + Speed, adjustedInput);
				//                                            }
				//                                ));
				//        }
				//        else
				//        {
				//            Dispatcher.Invoke(DispatcherPriority.Background,
				//                              new Action(() =>
				//                              {
				//                                  A5val.Text = input.ToString();
				//                                  combiBrush = Plotter5.DefaultBrush.CloneCurrentValue();
				//                                  combiBrush.Opacity = 0.8;
				//                                  Combiplotter.Plot(_xLoc + Speed, adjustedInput, combiBrush, Plotter5.DefaultStroke);
				//                              }
				//                                ));
				//        }
				//        break;
				//    default:
				//        continue;
				//}

				


			}
		}

		private void ResetPlotters()
		{
			Plotter0.Reset();
			Plotter1.Reset();
			Plotter2.Reset();
			Plotter3.Reset();
			Plotter4.Reset();
			Plotter5.Reset();
			Combiplotter.Reset();
		}

		private void SetPlottersMaxPlots(long width)
		{
			Plotter0.MaxPlots = width;
			Plotter1.MaxPlots = width;
			Plotter2.MaxPlots = width;
			Plotter3.MaxPlots = width;
			Plotter4.MaxPlots = width;
			Plotter5.MaxPlots = width;
		}

		private void SetAutoResets(bool autoReset)
		{
			Plotter0.AutoReset = autoReset;
			Plotter1.AutoReset = autoReset;
			Plotter2.AutoReset = autoReset;
			Plotter3.AutoReset = autoReset;
			Plotter4.AutoReset = autoReset;
			Plotter5.AutoReset = autoReset;
		}

		private void SetupCurrentMode()
		{
			switch (_currentBlendMode)
			{
				case BlendMode.Short:
					SetAutoResets(false);
					SetPlottersMaxPlots(100);
					break;
				case BlendMode.Blend:
					SetAutoResets(false);
					Combiplotter.MaxPlots = (long)(_canvasWidth * _numberActive);
					break;
				case BlendMode.Clear:
					SetAutoResets(true);
					SetPlottersMaxPlots((long)_canvasWidth - 10);
					break;
				default:
					SetAutoResets(false);
					SetPlottersMaxPlots((long)_canvasWidth - 10);
					break;
			}
			ResetPlotters();
		}

		private void BlendModeSelectorSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			_currentBlendMode = (BlendMode)BlendModeSelector.SelectedIndex;
			SetupCurrentMode();
		}
	}
}
