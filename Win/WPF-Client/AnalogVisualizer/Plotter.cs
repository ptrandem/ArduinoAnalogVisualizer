using System;
using System.Windows;
using System.Windows.Media;

namespace AnalogVisualizer
{
	public class Plotter : FrameworkElement
	{
		private readonly VisualCollection _visuals;
		private Point _lastPlot;

		public Plotter()
		{
			_visuals = new VisualCollection(this);
			LastPlot = new Point(0,0);
			MaxPlots = 100;
			InsertFirst = false;
			AutoReset = false;
		}

		public Brush DefaultBrush { get; set; }
		public int DefaultStroke { get; set; }
		public long MaxPlots { get; set; }
		public bool InsertFirst { get; set; }
		public bool AutoReset { get; set; }
		
		public Point LastPlot
		{
			get { return _lastPlot; }
			set { _lastPlot = value; }
		}

		public void Plot(double x, double y)
		{
			Plot(x, y, DefaultBrush, DefaultStroke);
		}
		
		public void Plot(double x, double y, Brush brush, int stroke)
		{
			var visual = new DrawingVisual();
			var context = visual.RenderOpen();
			if (AutoReset && LastPlot.X > x)
			{
				Reset();
			}
			if (Math.Abs(_lastPlot.X - x) > 1)
			{
				_lastPlot.X = x;
				_lastPlot.Y = y;
			}
			context.DrawLine(new Pen(brush, stroke), LastPlot, new Point(x, y) );
			context.Close();

			if (InsertFirst)
			{
				_visuals.Insert(0, visual);
				if (_visuals.Count > MaxPlots)
				{
					_visuals.RemoveAt(_visuals.Count - 1);
				}
			}
			else
			{
				_visuals.Add(visual);
				if (_visuals.Count > MaxPlots)
				{
					_visuals.RemoveAt(0);
				}

			}

			_lastPlot.X = x;
			_lastPlot.Y = y;
		}


		public void Reset()
		{
			_visuals.Clear();
			_lastPlot.X = 0;
		}

		#region FrameworkElement Members
		protected override Visual GetVisualChild(int index)
		{
			return _visuals[index];
		}

		protected override int VisualChildrenCount
		{
			get
			{
				return _visuals.Count;
			}
		}
		#endregion

	}
}
