﻿using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ModernSequenceEditor.Interfaces;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WDE.ModernSequenceEditor
{
    public class PatternElement : Canvas, INotifyPropertyChanged
	{
		static readonly int MaxCacheableWidth = WPFExtensions.DPI == 96 ? 1024 : 0;

		internal SequenceEvent se;
		ViewSettings viewSettings;
		TrackControl tc;
		bool renderPending = false;
		internal int time;
		Canvas macCanvas;
		Canvas eventHintCanvas;
		//Canvas patternPlayCanvas;
		Brush patternEventHintBrush;
		Brush patternEventHintBrushOff;
		Brush patternEventHintParam;
		Brush patternEventBorder;
		Brush trFill;
		Brush trStroke;

		public PatternElement(TrackControl tc, int time, SequenceEvent se, ViewSettings vs)
		{
			this.tc = tc;
			this.time = time;
			this.se = se;
			viewSettings = vs;

			this.IsVisibleChanged += (sender, e) =>
			{
				if (IsVisible)
				{
					if (renderPending)
					{
						renderPending = false;
						InvalidateVisual();
					}
				}
			};

			// Is there property changed event for "PatternEdited"?
			if (se.Pattern != null)
				se.Pattern.PropertyChanged += Pattern_PropertyChanged;
			
            SequenceEditor.Settings.PropertyChanged += Settings_PropertyChanged;
			this.MouseRightButtonDown += PatternElement_MouseRightButtonDown;
            this.Unloaded += PatternElement_Unloaded;

            tc.Editor.PropertyChanged += Editor_PropertyChanged;

			patternEventHintBrush = tc.TryFindResource("SeqEdPatternEventNote") as SolidColorBrush;
			if (patternEventHintBrush == null)
				patternEventHintBrush = Brushes.LightGreen;

			patternEventHintBrushOff = tc.TryFindResource("SeqEdPatternEventNoteOff") as SolidColorBrush;
			if (patternEventHintBrushOff == null)
				patternEventHintBrushOff = Brushes.Gray;

			patternEventHintParam = tc.TryFindResource("SeqEdPatternEventParam") as SolidColorBrush;
			if (patternEventHintParam == null)
				patternEventHintParam = Brushes.Orange;

			patternEventBorder = tc.TryFindResource("SeqEdPatternEventBorder") as SolidColorBrush;
			if (patternEventBorder == null)
				patternEventBorder = Brushes.Black;

			trFill = tc.TryFindResource("PatternBoxBgRectFillBrush") as SolidColorBrush;
			trStroke = tc.TryFindResource("PatternBoxBgRectStrokeBrush") as SolidColorBrush;
			trFill = trFill != null ? trFill : new SolidColorBrush(Color.FromArgb(0x90, 0xe0, 0xe0, 0xff));
			trStroke = trStroke != null ? trStroke : new SolidColorBrush(Color.FromArgb(0x40, 0x33, 0x33, 0x33));

			patternEventHintBrush.Freeze();
			patternEventHintBrushOff.Freeze();
			patternEventHintParam.Freeze();
			patternEventBorder.Freeze();

			DrawElements();

			this.ClipToBounds = true;
		}

        private void Editor_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {  
			if (e.PropertyName == "PatternPlayMode")
            {
				var ppe = (PatternPlayEvent)sender;
				if (ppe.pat == se.Pattern || (se.Pattern != null && se.Pattern.IsPlayingSolo))
					if (ppe.mode == PatternPlayMode.Looping || ppe.mode == PatternPlayMode.Play)
						PlayAnimation(ppe.mode == PatternPlayMode.Looping);
			}
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
			if (e.PropertyName == "PatternBoxEventHint" || e.PropertyName == "EventToolTip" || e.PropertyName == "PatternEventHeight")
				UpdatePatternEvents();
		}

        private void Pattern_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
			if (e.PropertyName != "LastEngineThread")
			{
				//UpdatePatternEvents();
				if (!Global.Buzz.Playing)
					StopAnimation();
			}
		}


		private void PatternElement_Unloaded(object sender, RoutedEventArgs e)
        {
			if (se.Pattern != null)
				se.Pattern.PropertyChanged -= Pattern_PropertyChanged;
			SequenceEditor.Settings.PropertyChanged -= Settings_PropertyChanged;

			this.MouseRightButtonDown -= PatternElement_MouseRightButtonDown;

			tc.Editor.PropertyChanged -= Editor_PropertyChanged;
		}

        private void PatternElement_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
			if (macCanvas != null && macCanvas.ContextMenu != null)
			{
				macCanvas.ContextMenu.IsOpen = true;
				e.Handled = true;
			}
        }

        static BrushSet[] brushSet = new BrushSet[3];
		static Brush[] borderBrush = new Brush[1];
		static SolidColorBrush textBrush;

		public static Tuple<string, BrushSet>[] PatternBrushes;

		static Typeface font = new Typeface("Segoe UI");
        private bool patternAnimationStarted;
        private bool looping;

        public static void InvalidateResources()
		{	
			brushSet[0] = null;
			PatternVisualCache.Clear();
		}

		//protected override void OnRender(DrawingContext dc)
		private void DrawElements()
		{
			//DebugConsole.WriteLine("PatternElement.OnRender " + IsVisible.ToString());
			/*
			if (!IsVisible)
			{
				renderPending = true;
				return;
			}
			*/

			if (brushSet[0] == null)
			{
				brushSet[0] = new BrushSet(tc.TryFindResource("PatternBoxBrush") as SolidColorBrush);
				brushSet[1] = new BrushSet(tc.TryFindResource("BreakBoxBrush") as SolidColorBrush);
				brushSet[2] = new BrushSet(tc.TryFindResource("MuteBoxBrush") as SolidColorBrush);

				// Don't know why these are not found above.
				if (brushSet[1].Brush == null || brushSet[1].Brush.Color == Color.FromArgb(0, 0xff, 0xff, 0xff))
					brushSet[1] = new BrushSet(new SolidColorBrush(Global.Buzz.ThemeColors["SE Break Box"]));

				if (brushSet[2].Brush == null || brushSet[2].Brush.Color == Color.FromArgb(0, 0xff, 0xff, 0xff))
					brushSet[2] = new BrushSet(new SolidColorBrush(Global.Buzz.ThemeColors["SE Mute Box"]));

				borderBrush[0] = tc.TryFindResource("PatternBorderBrush") as Brush;
				textBrush = tc.TryFindResource("PatternTextBrush") as SolidColorBrush;
				if (textBrush.Color == Color.FromArgb(0, 0xff, 0xff, 0xff))
					textBrush = new SolidColorBrush(Global.Buzz.ThemeColors["SE Text"]);

				if (textBrush.CanFreeze) textBrush.Freeze();
				
				for (int i = 0; i < borderBrush.Length; i++)
				{
					if (borderBrush[i] != null && borderBrush[i].CanFreeze) borderBrush[i].Freeze();
				}

				var pc = tc.TryFindResource("PatternColors") as NamedColor[];
				if (pc != null) PatternBrushes = pc.Select(nc => Tuple.Create(nc.Name, new BrushSet(new SolidColorBrush(nc.Color)))).ToArray();
			}

			int span = se.Pattern != null ? se.Span : viewSettings.NonPlayPattenSpan;
			string text = se.Pattern != null ? se.Pattern.Name : "";
			double w = viewSettings.TrackWidth;
			double h = span * viewSettings.TickHeight;
			
			int spanReal = se.Pattern != null ? se.Pattern.Length : viewSettings.NonPlayPattenSpan;
			double h2 = spanReal * viewSettings.TickHeight;

			string cktext = "";

			int bi = 0;
			switch (se.Type)
			{
				case SequenceEventType.PlayPattern: bi = 0; cktext = text; break;
				case SequenceEventType.Break: bi = 1; cktext = "<break>";  break;
				case SequenceEventType.Mute: bi = 2; cktext = "<mute>"; break;
				case SequenceEventType.Thru: bi = 2; cktext = "<thru>"; break;
			}

			BrushSet bs;
			int ci = -1;

			if (se.Type == SequenceEventType.PlayPattern && PatternBrushes != null)
			{
				PatternEx pex = null;
				
				if (viewSettings.PatternAssociations.TryGetValue(se.Pattern, out pex))
					ci = pex.ColorIndex % PatternBrushes.Length;

				if (ci < 0 && SequenceEditor.Settings.PatternBoxColors == PatternBoxColorModes.Pattern)
					ci = tc.Sequence.Machine.Patterns.IndexOf(se.Pattern) % PatternBrushes.Length;
			}

			if (ci >= 0)
				bs = PatternBrushes[ci].Item2;
			else
				bs = brushSet[bi];

			Children.Clear();

			PatternVisual childVisual = new PatternVisual(w, h, cktext, font, textBrush, borderBrush[0], bs.Brush, bs.HighlightBrush, bs.ShadowBrush);
			Children.Add(new VisualHost { Visual = childVisual } );

			eventHintCanvas = new Canvas() { Width = w - 2, Height = h - 2, Margin = new Thickness(1, 1, 1, 1), ClipToBounds = true, SnapsToDevicePixels = true };
			Children.Add(eventHintCanvas);

			UpdatePatternEvents();

			// Check if machine can draw to pattern
			IModernSequencerMachineInterface mac = se.Pattern != null ? se.Pattern.Machine.ManagedMachine as IModernSequencerMachineInterface : null;

			if (mac != null)
			{
				macCanvas = mac.PrepareCanvasForSequencer(se.Pattern, SequencerLayout.Vertical, viewSettings.TickHeight, time, w, h);
				if (macCanvas != null)
				{
					if (tc.Editor.ResourceDictionary != null && macCanvas.ContextMenu != null)
						macCanvas.ContextMenu.Resources.MergedDictionaries.Add(tc.Editor.ResourceDictionary);
					
					Children.Add(macCanvas);
				}
			}

			rectPlayAnim = new Rectangle() { Width = w - 2, Height = 0, Margin = new Thickness(1, 1, 1, 1), ClipToBounds = true, SnapsToDevicePixels = true };
			rectPlayAnim.IsHitTestVisible = false;
			
			this.Children.Add(rectPlayAnim);
			TextBlock tb = new TextBlock() { Margin = new Thickness(6, 1, 2, 1), Foreground = textBrush, FontFamily = new FontFamily("Segoe UI"), FontSize = 12, Text = cktext, Width = w - 10, FlowDirection = FlowDirection.LeftToRight };
			tb.IsHitTestVisible = false;

			if (SequenceEditor.Settings.PatternNameBackground)
			{
				Rectangle textRect = new Rectangle() { Margin = new Thickness(4, 2, 2, 1), Fill = trFill, Stroke = trStroke, Width = w - 8, Height = 15, IsHitTestVisible=false };
				this.Children.Add(textRect);
			}

			this.Children.Add(tb);

			this.IsHitTestVisible = true;
			this.Background = Brushes.Transparent;
			this.Width = w;
			this.Height = h;
			this.MouseLeftButtonDown += (sender, e) =>
			{
				if (e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.Shift && SequenceEditor.Settings.ClickPlayPattern)
				{
					var pat = se.Pattern;
					var seq = tc.Sequence;

					if (seq != null && pat != null)
					{
						ISong song = tc.Editor.Song;
						int bar = SequenceEditor.Settings.ClickPlayPatternSyncToTick;
						var time = song.LoopStart + ((song.PlayPosition - song.LoopStart + bar) / bar * bar % (song.LoopEnd - song.LoopStart));

						SequenceEventType seqType = SequenceEventType.PlayPattern;
						tc.Editor.UpdatePatternAmin(seq, pat, seqType, time, PatternPlayMode.Play);
					}

					e.Handled = true;
				}
				else if (e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.Control && SequenceEditor.Settings.ClickPlayPattern)
				{
					var pat = se.Pattern;
					var seq = tc.Sequence;

					if (seq != null && pat != null)
					{
						ISong song = tc.Editor.Song;
						int bar = SequenceEditor.Settings.ClickPlayPatternSyncToTick;
						var time = song.LoopStart + ((song.PlayPosition - song.LoopStart + bar) / bar * bar % (song.LoopEnd - song.LoopStart));

						SequenceEventType seqType = SequenceEventType.PlayPattern;
						tc.Editor.UpdatePatternAmin(seq, pat, seqType, time, PatternPlayMode.Looping);
					}

					e.Handled = true;
				}
			};

			Line dragTop = new Line() { X1 = 1, Y1 = 2, X2 = w - 1, Y2 = 2, Stroke = Brushes.Transparent, StrokeThickness = 5 };
			dragTop.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
			this.Children.Add(dragTop);
			Line dragBottom = new Line() { X1 = 1, Y1 = h2 - 2, X2 = w - 1, Y2 = h2 - 2, Stroke = Brushes.Transparent, StrokeThickness = 5 };
			dragBottom.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
			this.Children.Add(dragBottom);

			dragBottom.MouseEnter += (sender, e) =>
			{
				if (Keyboard.Modifiers == ModifierKeys.Control)
					Mouse.OverrideCursor = Cursors.SizeNS;
			};
			dragBottom.MouseLeave += (sender, e) =>
			{
				Mouse.OverrideCursor = null;
			};
			dragBottom.PreviewMouseLeftButtonDown += (sender, e) =>
			{
				if (Keyboard.Modifiers == ModifierKeys.Control)
				{
					this.PropertyChanged.Raise(this, "DragBottom");
					e.Handled = true;
				}
			};


			dragTop.MouseEnter += (sender, e) =>
			{
				if (Keyboard.Modifiers == ModifierKeys.Control)
					Mouse.OverrideCursor = Cursors.SizeNS;
			};
			dragTop.MouseLeave += (sender, e) =>
			{
				Mouse.OverrideCursor = null;
			};
			dragTop.PreviewMouseLeftButtonDown += (sender, e) =>
			{	
				if (Keyboard.Modifiers == ModifierKeys.Control)
				{
					this.PropertyChanged.Raise(this, "DragTop");
					e.Handled = true;
				}
			};

		}

        private void DragBottom_MouseEvent(object sender, MouseEventArgs e)
        {
			if (macCanvas != null)
			{
				macCanvas.RaiseEvent(e);
			}
		}

        DispatcherTimer dtAnimTimer;
		Rectangle rectPlayAnim;

        public event PropertyChangedEventHandler PropertyChanged;

        internal void StopAnimation()
        {
			if (dtAnimTimer != null)
				dtAnimTimer.Stop();

			if (rectPlayAnim != null)
				rectPlayAnim.Height = 0;
		}

        internal void PlayAnimation(bool loop)
        {
			patternAnimationStarted = false;
			looping = loop;

			rectPlayAnim.Fill = GetLGBForPatternPlay(loop);

			if (dtAnimTimer != null)
				dtAnimTimer.Stop();

			dtAnimTimer = new DispatcherTimer();
			dtAnimTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / 30);
			dtAnimTimer.Tick += (sender, e) =>
			{
				int span = se.Pattern != null ? se.Span : 10;
				double w = viewSettings.TrackWidth - 2;
				double h = span * viewSettings.TickHeight;

				if (tc.Sequence.PlayingPattern == se.Pattern || se.Pattern.IsPlayingSolo)
				{	
					patternAnimationStarted = true;
					rectPlayAnim.Width = w;
					if (se.Pattern.PlayPosition > 0)
						rectPlayAnim.Height = (se.Pattern.PlayPosition / (double)PatternEvent.TimeBase) * viewSettings.TickHeight;
				}
				else if (patternAnimationStarted)
                {	
					patternAnimationStarted = false;
					dtAnimTimer.Stop();
					rectPlayAnim.Height = 0;
				}
			};

			dtAnimTimer.Start();
        }

		private LinearGradientBrush GetLGBForPatternPlay(bool loop)
        {
			LinearGradientBrush playLGB;
			
			if (loop)
            {
				playLGB = tc.TryFindResource("ClickPlayPatternLooping") as LinearGradientBrush;
				if (playLGB == null)
				{
					playLGB = new LinearGradientBrush();
					playLGB.StartPoint = new Point(0, 0);
					playLGB.EndPoint = new Point(0, 1);

					GradientStop transparentGS = new GradientStop();
					transparentGS.Color = Color.FromArgb(0x01, 0x6C, 0xA8, 0xDA);
					transparentGS.Offset = 0.0;
					playLGB.GradientStops.Add(transparentGS);

					transparentGS = new GradientStop();
					transparentGS.Color = Color.FromArgb(0x1f, 0x6C, 0xA8, 0xEA);
					transparentGS.Offset = 0.65;
					playLGB.GradientStops.Add(transparentGS);

					GradientStop greenGS = new GradientStop();
					greenGS.Color = Color.FromArgb(0xef, 0x6C, 0xA8, 0xFA);
					greenGS.Offset = 1.0;
					playLGB.GradientStops.Add(greenGS);
				}
			}
			else
            {
				playLGB = tc.TryFindResource("ClickPlayPattern") as LinearGradientBrush;
				if (playLGB == null)
				{
					playLGB = new LinearGradientBrush();
					playLGB.StartPoint = new Point(0, 0);
					playLGB.EndPoint = new Point(0, 1);

					GradientStop transparentGS = new GradientStop();
					transparentGS.Color = Color.FromArgb(0x01, 0x37, 0xD2, 0x12);
					transparentGS.Offset = 0.0;
					playLGB.GradientStops.Add(transparentGS);

					transparentGS = new GradientStop();
					transparentGS.Color = Color.FromArgb(0x1f, 0x37, 0xD2, 0x12);
					transparentGS.Offset = 0.65;
					playLGB.GradientStops.Add(transparentGS);

					GradientStop greenGS = new GradientStop();
					greenGS.Color = Color.FromArgb(0xef, 0x37, 0xD2, 0x12);
					greenGS.Offset = 1.0;
					playLGB.GradientStops.Add(greenGS);
				}
			}

			return playLGB;
        }

        internal void UpdatePatternEvents()
        {
			if (se.Pattern != null && eventHintCanvas != null)
			{	
				int span = se.Pattern != null ? se.Pattern.Length : 4;
				double w = viewSettings.TrackWidth;
				double h = span * viewSettings.TickHeight;

				if (eventHintCanvas != null)
				{
					eventHintCanvas.Children.Clear();
					DrawPatternEvents(eventHintCanvas, se.Pattern, w, h, patternEventHintBrush, patternEventHintBrushOff, patternEventHintParam, patternEventBorder);
				}
			}
		}

		private void DrawPatternEvents(Canvas eventHintCanvas, IPattern pat, double w, double h, Brush color, Brush offColor, Brush paramColor, Brush patternEventBorder)
		{
			double lineWidth = 4;
			double lineHeight = h / (double)pat.Length;
			double marginPercent = 0.1;
			double marginLeft = w * marginPercent / 2.0;
			double drawWidth = w * (1 - marginPercent);
			double drawHeight = h;
			//double previousY = double.MinValue;
			//double MinYDistance = 2;

			if (lineHeight < SequenceEditor.Settings.PatternEventHeight)
				lineHeight = SequenceEditor.Settings.PatternEventHeight;

			string toolTip;
			
			PatternBoxEventHintType hintType = SequenceEditor.Settings.PatternBoxEventHint;

			if (hintType == PatternBoxEventHintType.Detail)
			{
				for (int i = 0; i < pat.Columns.Count; i++)
				{
					foreach (PatternEvent pe in pat.Columns[i].GetEvents(0, pat.Length * PatternEvent.TimeBase))
					{
						toolTip = "";
						
						Brush col = color;
						if (pat.Columns[i].Parameter.Type == ParameterType.Note && pe.Value == Note.Off)
						{
							col = offColor;
							toolTip = "Off";
						}
						else if (pat.Columns[i].Parameter.Type == ParameterType.Note)
						{
							toolTip = BuzzNote.TryToString(pe.Value);
						}
						else if (pat.Columns[i].Parameter.Type != ParameterType.Note)
						{
							col = paramColor;
							toolTip = pe.Value.ToString("X");
						}

						int time = pe.Time;
						double relativeY = (pe.Time / (double)PatternEvent.TimeBase) / (double)pat.Length;
						double relativeX = i / (double)pat.Columns.Count;

						double X1 = marginLeft + relativeX * drawWidth;
						double Y1 = relativeY * drawHeight - 1;

						Rectangle eventRect = new Rectangle()
						{
							Width = lineWidth,
							Height = lineHeight,
							Fill = col,
							SnapsToDevicePixels = true
						};

						if (SequenceEditor.Settings.EventToolTip)
							eventRect.ToolTip = toolTip;

						if (lineHeight > 3)
							eventRect.Stroke = patternEventBorder;

						Canvas.SetLeft(eventRect, X1);
						Canvas.SetTop(eventRect, Y1);
						eventRect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
						eventHintCanvas.Children.Add(eventRect);
					}

				}
			}
			else if (hintType == PatternBoxEventHintType.Note)
			{
				// Count Note columns
				int noteCols = 0;
				for (int i = 0; i < pat.Columns.Count; i++)
				{
					if (pat.Columns[i].Track != -1 && pat.Columns[i].Parameter != null && pat.Columns[i].Parameter.Type == ParameterType.Note && pat.Columns[i].Track < pat.Machine.TrackCount)
						noteCols++;
				}

				if (noteCols == 0)
					return;

				lineWidth = drawWidth / (double)noteCols;
				double lineWidthCenter = 0.1 * lineWidth / 2.0;
				lineWidth *= 0.9;


				int noteColNum = 0;

				for (int i = 0; i < pat.Columns.Count; i++)
				{
					if (pat.Columns[i].Parameter.Type == ParameterType.Note)
					{
						foreach (PatternEvent pe in pat.Columns[i].GetEvents(0, pat.Length * PatternEvent.TimeBase))
						{
							toolTip = "";

							Brush col = pe.Value != Note.Off ? color : offColor;

							if (pe.Value == Note.Off)
                            {
								toolTip = "Off";
                            }
							else
                            {
								toolTip = BuzzNote.TryToString(pe.Value);
							}

							int time = pe.Time;
							double relativeY = (pe.Time / (double)PatternEvent.TimeBase) / (double)pat.Length;
							double relativeX = noteColNum / (double)noteCols;

							double X1 = marginLeft + lineWidthCenter + relativeX * drawWidth;
							double Y1 = relativeY * drawHeight - 1;

							Rectangle eventRect = new Rectangle()
							{
								Width = lineWidth,
								Height = lineHeight,
								Fill = col,
								SnapsToDevicePixels = true
							};

							if (SequenceEditor.Settings.EventToolTip)
								eventRect.ToolTip = toolTip;

							if (lineHeight > 3)
								eventRect.Stroke = patternEventBorder;

							Canvas.SetLeft(eventRect, X1);
							Canvas.SetTop(eventRect, Y1);
							eventRect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
							eventHintCanvas.Children.Add(eventRect);
						}
						noteColNum++;
					}
				}
			}
			else if (hintType == PatternBoxEventHintType.Midi)
			{
				const int TimeBase = 960;
				var me = pat.PatternEditorMachineMIDIEvents;

				// Count Note columns
				int noteCols = 0;
				for (int i = 0; i < pat.Columns.Count; i++)
				{
					if (pat.Columns[i].Track != -1 && pat.Columns[i].Parameter != null && pat.Columns[i].Parameter.Type == ParameterType.Note && pat.Columns[i].Track < pat.Machine.TrackCount)
						noteCols++;
				}

				if (noteCols == 0)
					return;

				lineWidth = drawWidth / (double)noteCols;
				double lineWidthCenter = 0.1 * lineWidth / 2.0;
				lineWidth *= 0.9;

				List<Tuple<int, ChannelMessage>> events = new List<Tuple<int, ChannelMessage>>();

				for (int i = 0; i < me.Length / 2; i++)
				{
					Tuple<int, ChannelMessage> item = new Tuple<int, ChannelMessage>(me[2 * i + 0], new ChannelMessage(me[2 * i + 1]));
					events.Add(item);
				}

				int[] noteColumns = new int[noteCols];
				int noteColumnPos = 0;

				foreach (var eventTuple in events)
				{
					int midiTime = eventTuple.Item1;
					ChannelMessage val = eventTuple.Item2;
					toolTip = "";

					// Move noteColumnPos as far left as possible
					for (int i = 0; i < noteCols; i++)
						if (noteColumns[i] == 0)
						{
							noteColumnPos = i;
							break;
						}

					Brush col = val.Command != ChannelCommand.NoteOff ? color : offColor;
					if (val.Command != ChannelCommand.NoteOn && val.Command != ChannelCommand.NoteOff)
						col = paramColor;

					double relativeY = (midiTime / (double)TimeBase) / (double)pat.Length;

					if (val.Command == ChannelCommand.NoteOn)
					{
						bool found = false;
						for (int i = 0; i < noteCols; i++)
							if (noteColumns[(noteColumnPos + i) % noteCols] == 0)
							{
								noteColumnPos = (noteColumnPos + i) % noteCols;
								found = true;
								break;
							}

						if (!found)
							noteColumnPos = (noteColumnPos + 1) % noteCols;

						noteColumns[noteColumnPos] = val.Data1;

						toolTip = BuzzNote.TryToString(BuzzNote.FromMIDINote(val.Data1));
					}
					else if (val.Command == ChannelCommand.NoteOff)
					{
						bool found = false;
						for (int i = 0; i < noteCols; i++)
							if (noteColumns[i] == val.Data1)
							{
								noteColumnPos = i;
								found = true;
								noteColumns[i] = 0;
								break;
							}

						if (!found) // Don't draw note off if parent was not found
							continue;

						toolTip = "Off";
					}
					else
                    {

                    }

					double relativeX = noteColumnPos / (double)noteCols;

					double X1 = marginLeft + lineWidthCenter + relativeX * drawWidth;
					double Y1 = relativeY * drawHeight - 1;

                    //if (Y1 - previousY > MinYDistance)
					//{

					Rectangle eventRect = new Rectangle()
					{
						Width = lineWidth,
						Height = lineHeight,
						Fill = col,
						SnapsToDevicePixels = true
					};

					if (SequenceEditor.Settings.EventToolTip)
						eventRect.ToolTip = toolTip;

					if (lineHeight > 3)
						eventRect.Stroke = patternEventBorder;

					Canvas.SetLeft(eventRect, X1);
					Canvas.SetTop(eventRect, Y1);
					eventRect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
					eventHintCanvas.Children.Add(eventRect);

						//previousY = Y1;
					//}
				}
			}
			else if (hintType == PatternBoxEventHintType.MidiSimple)
			{
				const int TimeBase = 960;
				var me = pat.PatternEditorMachineMIDIEvents;

				toolTip = "";

				Dictionary<int, ChannelMessage> events = new Dictionary<int, ChannelMessage>();

				for (int i = 0; i < me.Length / 2; i++)
					events[me[2 * i + 0]] = new ChannelMessage(me[2 * i + 1]);

				lineWidth = drawWidth;
				//double lineWidthCenter = 0.1 * lineWidth / 2.0;
				//lineWidth *= 0.9;

				foreach (int midiTime in events.Keys)
				{
					ChannelMessage val = events[midiTime];

					Brush col = val.Command != ChannelCommand.NoteOff ? color : offColor;
					if (val.Command != ChannelCommand.NoteOn && val.Command != ChannelCommand.NoteOff)
						col = paramColor;

					if (val.Command == ChannelCommand.NoteOn)
                    {
						toolTip = BuzzNote.TryToString(BuzzNote.FromMIDINote(val.Data1));
					}
					else if (val.Command == ChannelCommand.NoteOff)
					{
						toolTip = "Off";
					}

					double relativeY = (midiTime / (double)TimeBase) / (double)pat.Length;
					
					double X1 = marginLeft;// + lineWidth; + relativeX * drawWidth;
					double Y1 = relativeY * drawHeight - 1;

					Rectangle eventRect = new Rectangle()
					{
						Width = lineWidth,
						Height = lineHeight,
						Fill = col,
						SnapsToDevicePixels = true
					};

					if (SequenceEditor.Settings.EventToolTip)
						eventRect.ToolTip = toolTip;

					if (lineHeight > 3)
						eventRect.Stroke = patternEventBorder;

					Canvas.SetLeft(eventRect, X1);
					Canvas.SetTop(eventRect, Y1);
					eventRect.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
					eventHintCanvas.Children.Add(eventRect);
				}
			}
		}

		//PatternVisual childVisual;

		//protected override int VisualChildrenCount { get { return childVisual != null ? 1 : 0; } }
		//protected override Visual GetVisualChild(int index) { if (childVisual == null) throw new ArgumentOutOfRangeException(); else return childVisual; }
	}

	public enum PatternPlayMode { Stop, Play, Looping };
    public class PatternPlayEvent : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
		public IPattern pat;
		public PatternPlayMode mode;

		public PatternPlayEvent(IPattern p, PatternPlayMode m)
        {
			pat = p;
			mode = m;
        }
	}
}
