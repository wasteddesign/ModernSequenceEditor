﻿using BuzzGUI.Interfaces;
//using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace WDE.ModernSequenceEditor
{
    /// <summary>
    /// Interaction logic for TrackControl.xaml
    /// </summary>
    public partial class TrackControl : UserControl
	{
		ViewSettings viewSettings;
		public ViewSettings ViewSettings
		{
			set
			{
				viewSettings = value;
				//backgroundElement.ViewSettings = value;
			}

		}



		ISequence sequence;
		public ISequence Sequence
		{
			get { return sequence; }
			set
			{
				if (sequence != null)
				{
					sequence.PropertyChanged -= sequence_PropertyChanged;
					sequence.EventChanged -= sequence_EventChanged;
					sequence.SpanInserted -= sequence_SpanInserted;
					sequence.SpanDeleted -= sequence_SpanDeleted;
					sequence.SpanCleared -= sequence_SpanCleared;
				}

				sequence = value;
				EventsChanged();

				if (sequence != null)
				{
					sequence.PropertyChanged += sequence_PropertyChanged;
					sequence.EventChanged += sequence_EventChanged;
					sequence.SpanInserted += sequence_SpanInserted;
					sequence.SpanDeleted += sequence_SpanDeleted;
					sequence.SpanCleared += sequence_SpanCleared;
				}
			}
		}

		void sequence_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case "Events":
					EventsChanged();
					break;
			}
		}

		void sequence_SpanInserted(int time, int span)
		{
			if (!IsVisible)
			{
				EventsChanged();
				return;
			}
			else
			{
				for (int i = 0; i < eventCanvas.Children.Count; i++)
				{
					var e = eventCanvas.Children[i] as PatternElement;
					if (e.time >= time)
					{
						e.time += span;
						Canvas.SetTop(e, e.time * viewSettings.TickHeight);
					}
				}
			}
		}

		void sequence_SpanDeleted(int time, int span)
		{
			if (!IsVisible)
			{
				EventsChanged();
				return;
			}
			else
			{
				int rfirst = -1;
				int rcount = 0;

				for (int i = 0; i < eventCanvas.Children.Count; i++)
				{
					var e = eventCanvas.Children[i] as PatternElement;
					if (e.time >= time)
					{
						if (e.time < time + span)
						{
							if (rfirst < 0) rfirst = i;
							rcount++;
						}
						else
						{
							e.time -= span;
							Canvas.SetTop(e, e.time * viewSettings.TickHeight);
						}
					}
				}

				if (rfirst >= 0)
					eventCanvas.Children.RemoveRange(rfirst, rcount);

			}
		}

		void sequence_SpanCleared(int time, int span)
		{
			if (!IsVisible)
			{
				EventsChanged();
				return;
			}
			else
			{
				int rfirst = -1;
				int rcount = 0;

				for (int i = 0; i < eventCanvas.Children.Count; i++)
				{
					var e = eventCanvas.Children[i] as PatternElement;
					if (e.time >= time)
					{
						if (e.time < time + span)
						{
							if (rfirst < 0) rfirst = i;
							rcount++;
						}
					}
				}

				if (rfirst >= 0)
					eventCanvas.Children.RemoveRange(rfirst, rcount);

			}
		}

		public SequenceEditor Editor { get; private set; }
        private PatternResizeMode PatternResizeMode { get; set; }

        bool updatePending = false;
        private SequenceEvent SeResize;

        public int OriginalSnapTime { get; private set; }

        public TrackControl(SequenceEditor se)
		{
			Editor = se;
			if (Editor.ResourceDictionary != null) this.Resources.MergedDictionaries.Add(Editor.ResourceDictionary);
			InitializeComponent();

			this.IsVisibleChanged += (sender, e) =>
			{
				if (IsVisible)
				{
					if (updatePending)
					{
						updatePending = false;
						EventsChanged();
					}
				}
			};


		}

		public void EventsChanged()
		{
			TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

			Task.Factory.StartNew(() =>
			{
				if (!IsVisible)
				{
					updatePending = true;
					return;
				}

				foreach (PatternElement e in eventCanvas.Children)
                {
					e.PropertyChanged -= PE_PropertyChanged;
				}

				eventCanvas.Children.Clear();

				foreach (var e in sequence.Events)
				{
					var pv = new PatternElement(this, e.Key, e.Value, viewSettings);
					Canvas.SetTop(pv, e.Key * viewSettings.TickHeight);
					eventCanvas.Children.Add(pv);
                    pv.PropertyChanged += PE_PropertyChanged;

				}
			}, CancellationToken.None, TaskCreationOptions.None, uiScheduler);
		}

        private void PE_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
			if (e.PropertyName == "DragBottom")
			{
				this.Editor.PatternBottomDragStart(this, ((PatternElement)sender).se, this.sequence, PatternResizeMode.Bottom);
			}
			else if (e.PropertyName == "DragTop")
			{
				this.Editor.PatternBottomDragStart(this, ((PatternElement)sender).se, this.sequence, PatternResizeMode.Top);
			}
		}

        void sequence_EventChanged(int time)
		{
			TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

			Task.Factory.StartNew(() =>
			{
				SequenceEvent e;
				sequence.Events.TryGetValue(time, out e);

				for (int i = 0; i < eventCanvas.Children.Count; i++)
				{
					if ((eventCanvas.Children[i] as PatternElement).time == time)
					{
						(eventCanvas.Children[i] as PatternElement).PropertyChanged -= PE_PropertyChanged;
						eventCanvas.Children.RemoveAt(i);
						break;
					}
				}

				if (e != null)
				{
					int i = 0;
					for (i = 0; i < eventCanvas.Children.Count; i++)
					{
						if ((eventCanvas.Children[i] as PatternElement).time > time)
							break;
					}

					var pv = new PatternElement(this, time, e, viewSettings);
					Canvas.SetTop(pv, time * viewSettings.TickHeight);
					eventCanvas.Children.Insert(i, pv);
					pv.PropertyChanged += PE_PropertyChanged;

				}
			}, CancellationToken.None, TaskCreationOptions.None, uiScheduler);
		}

        internal void UpdateNoteHints()
        {
			for (int i = 0; i < eventCanvas.Children.Count; i++)
			{
				(eventCanvas.Children[i] as PatternElement).UpdatePatternEvents();
			}
		}

		internal void UpdateNoteHints(int index)
		{
			if (index < eventCanvas.Children.Count)
			{
				(eventCanvas.Children[index] as PatternElement).UpdatePatternEvents();
			}
		}
	}
}
