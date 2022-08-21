using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WDE.ModernSequenceEditor
{
    public struct TimeSignatureStruct
    {
        public int time;
        public int step;
    }

    public struct PatternAssociationsStruct
    {
        public string mac;
        public string pattern;
        public int ci;
    }

    public class VUTargetStruct
    {
        public string machineFrom;
        public string machineTo;
        public int index;

        public VUTargetStruct()
        {
            index = -1;
        }
    }

    [MachineDecl(Name = "Modern Sequence Editor", ShortName = "ModernSeq", Author = "WDE", MaxTracks = 8)]

    public class ModernSequenceEditorMachine : IBuzzMachine, INotifyPropertyChanged
    {
        IBuzzMachineHost host;

        CustomSequencerWindow customSequencerWindow;

        public SequenceEditor Seq { get; set; }
        public Window NonBuzzWindow { get; set; }

        public ModernSequenceEditorMachine(IBuzzMachineHost host)
        {
            this.host = host;
            Global.Buzz.Song.MachineAdded += Song_MachineAdded;
            Global.Buzz.Song.MachineRemoved += Song_MachineRemoved;
        }


        private void Song_MachineRemoved(IMachine obj)
        {
            if (host.Machine == obj && Seq != null)
            {
                Global.Buzz.Song.MachineAdded -= Song_MachineAdded;
                Global.Buzz.Song.MachineRemoved -= Song_MachineRemoved;

                if (customSequencerWindow != null)
                {   
                    customSequencerWindow.SequenceEditorWindow.Close();
                    customSequencerWindow.SequenceEditorWindow.PreviewKeyDown -= SequenceEditorWindow_PreviewKeyDown;
                    customSequencerWindow.Dispose();
                }
                if (NonBuzzWindow != null)
                {
                    NonBuzzWindow.Close();
                }

                Seq.Release();
            }

            //obj.PatternAdded -= Machine_PatternAdded;
        }

        private void Song_MachineAdded(IMachine obj)
        {
            if (host.Machine == obj)
            {
                if (SequenceEditor.Settings.SequenceEditorMode == SequenceEditorMode.Integrated)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    if (!CustomSequencerWindow.OneInstanceCreated)
                    {
                        ResourceDictionary skin = GetBuzzThemeResources();

                        Seq = new SequenceEditor(host.Machine.Graph.Buzz, skin);
                        Seq.PropertyChanged += Sequencer_PropertyChanged;
                        Seq.Song = host.Machine.Graph.Buzz.Song;
                        customSequencerWindow = new CustomSequencerWindow(Seq);
                        customSequencerWindow.SequenceEditorWindow.PreviewKeyDown += SequenceEditorWindow_PreviewKeyDown;
                    }
                    }));
                }
                else
                {
                    if (MachineState.IsVisible)
                    {
                        host.Machine.DoubleClick();
                    }
                }
            }

            //obj.PatternAdded += Machine_PatternAdded;
        }

        //private void Machine_PatternAdded(IPattern obj)
        //{
        //    obj.Length = SequenceEditor.Settings.PatternLength;
        //}

        internal ResourceDictionary GetBuzzThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\SequenceEditor\\SequenceEditor.xaml";

                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\SequenceEditor\\SequenceEditor.xaml";
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }

            return skin;
        }

        public void SequenceEditorWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    e.Handled = true;
                }
                else if (e.Key == Key.S)
                {
                    Global.Buzz.ExecuteCommand(BuzzCommand.SaveFile);
                    e.Handled = true;
                }
                else if (e.Key == Key.O)
                {
                    Global.Buzz.ExecuteCommand(BuzzCommand.OpenFile);
                    e.Handled = true;
                }
                else if (e.Key == Key.N)
                {
                    Global.Buzz.ExecuteCommand(BuzzCommand.NewFile);
                    e.Handled = true;
                }
                else if (e.Key == Key.W)
                {
                    CreateNewSeqencerWindow();
                    e.Handled = true;
                }
            }
            else
            {
                if (e.Key == Key.F2)
                {
                    Global.Buzz.ActiveView = BuzzView.PatternView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F3)
                {
                    Global.Buzz.ActiveView = BuzzView.MachineView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F4)
                {
                    Global.Buzz.ActiveView = BuzzView.SequenceView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F5)
                {

                    Global.Buzz.Playing = true;
                    e.Handled = true;
                }
                else if (e.Key == Key.F6)
                {
                    if (Seq != null)
                    {
                        Seq.PlayCursor();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.F7)
                {
                    Global.Buzz.Recording = true;
                    e.Handled = true;
                }
                else if (e.Key == Key.F8)
                {
                    Global.Buzz.Recording = false;
                    Global.Buzz.Playing = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.F9)
                {
                    Global.Buzz.ActiveView = BuzzView.WaveTableView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F10 || e.SystemKey == Key.F10)
                {
                    Global.Buzz.ActiveView = BuzzView.SongInfoView;
                    e.Handled = true;
                }
                else if (e.Key == Key.F12 || e.SystemKey == Key.F12)
                {
                    Global.Buzz.AudioDeviceDisabled = !Global.Buzz.AudioDeviceDisabled;
                    e.Handled = true;
                }
            }
        }

        internal void CreateNewSeqencerWindow()
        {
            Window nonBuzzWindow = NonBuzzWindow;
            if (nonBuzzWindow != null)
            {
                nonBuzzWindow.PreviewKeyDown -= SequenceEditorWindow_PreviewKeyDown;
                nonBuzzWindow.Content = null;
                nonBuzzWindow.Close();
                nonBuzzWindow = null;
            }
            else
            {
                ResourceDictionary rd = GetBuzzThemeResources();

                nonBuzzWindow = new Window();
                nonBuzzWindow.WindowStyle = WindowStyle.SingleBorderWindow;

                SequenceEditor seqWin = new SequenceEditor(host.Machine.Graph.Buzz, rd);
                seqWin.PropertyChanged += Sequencer_PropertyChanged;
                nonBuzzWindow.Content = seqWin;
                nonBuzzWindow.Title = host.Machine.Name;

                seqWin.MinHeight = 400;
                seqWin.MinWidth = 600;

                seqWin.Song = host.Machine.Graph.Buzz.Song;
                seqWin.SetVisibility(true);
                UpdateSeqViewData(seqWin);

                nonBuzzWindow.MinHeight = 600;
                nonBuzzWindow.MinWidth = 800;
                nonBuzzWindow.ShowInTaskbar = true;

                nonBuzzWindow.PreviewKeyDown += SequenceEditorWindow_PreviewKeyDown;
                nonBuzzWindow.Show();

                nonBuzzWindow.Closed += (sender2, e2) =>
                {
                    nonBuzzWindow.PreviewKeyDown -= SequenceEditorWindow_PreviewKeyDown;

                    NonBuzzWindow = null;
                };

                NonBuzzWindow = nonBuzzWindow;
            }
        }

        public void ImportFinished(IDictionary<string, string> machineNameMap)
        {
            if (SequenceEditor.Settings.SequenceEditorMode == SequenceEditorMode.Integrated)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (customSequencerWindow != null)
                    {
                        UpdateSeqViewData(customSequencerWindow.SequenceEditor);
                        customSequencerWindow.UpdateView();
                    }
                }));
            }
            else
            {
                if (GUI != null)
                    UpdateSeqViewData(GUI.Seq);
            }
        }

        [ParameterDecl(ValueDescriptions = new[] { "no", "yes" })]
        public bool Dummy { get; set; }

        // actual machine ends here. the stuff below demonstrates some other features of the api.

        public static int MAX_NUM_TIME_SIGNATURES = 1000;
        public static int MAX_NUM_PATTERN_ASSOCIATIONS = 1000;
        public static int MAX_NUM_VU_TARGETS = 100;

        public class State : INotifyPropertyChanged
        {
            public State()
            {
                TimeSignatures = new TimeSignatureStruct[MAX_NUM_TIME_SIGNATURES];
                patternAssociations = new PatternAssociationsStruct[MAX_NUM_PATTERN_ASSOCIATIONS];
                vUTargets = new VUTargetStruct[MAX_NUM_VU_TARGETS];
            }  // NOTE: parameterless constructor is required by the xml serializer

            private int numTimeSignatures = 0;
            private TimeSignatureStruct[] timeSignatures;
            private int numPatternAssocialtions = 0;
            private PatternAssociationsStruct[] patternAssociations;
            private int numVUTargets = 0;
            private VUTargetStruct[] vUTargets;

            private double zoom = 2.0;

            public int NumTimeSignatures { get => numTimeSignatures; set => numTimeSignatures = value; }
            public TimeSignatureStruct[] TimeSignatures { get => timeSignatures; set => timeSignatures = value; }
            public int NumPatternAssocialtions { get => numPatternAssocialtions; set => numPatternAssocialtions = value; }
            public PatternAssociationsStruct[] PatternAssociations { get => patternAssociations; set => patternAssociations = value; }
            public double Zoom { get => zoom; set => zoom = value; }
            public bool IsVisible { get; set; }
            public double WndWidth { get; set; }
            public double WndHeight { get; set; }
            public double WndTop { get; set; }
            public double WndLeft { get; set; }
            public int WndState { get; set; }
            public VUTargetStruct[] VUTargets { get => vUTargets; set => vUTargets = value; }
            public int NumVUTargets { get => numVUTargets; set => numVUTargets = value; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        State machineState = new State();
        public State MachineState           // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get
            {
                return machineState;
            }
            set
            {
                machineState = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("MachineState"));
            }
        }

        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                yield return new MenuItemVM()
                {
                    Text = "About...",
                    Command = new SimpleCommand()
                    {
                        CanExecuteDelegate = p => true,
                        ExecuteDelegate = p => MessageBox.Show("Modern Sequence Editor 2021. Version 1.3.5 | 99.8% based on BuzzGUI.SequenceEditor.")
                    }
                };
            }
        }

        public IBuzzMachineHost Host { get => host; set => host = value; }
        public ModernSequenceEditorGUI GUI { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        internal void SetGUI(ModernSequenceEditorGUI modernSequenceEditorGUI)
        {
            this.GUI = modernSequenceEditorGUI;
        }

        public void UpdateSeqViewData(SequenceEditor seqenceEditor)
        {
            TimeSignatureList tsl = new TimeSignatureList();
            TimeSignatureStruct[] tss = MachineState.TimeSignatures;
            for (int i = 0; i < MachineState.NumTimeSignatures; i++)
                tsl.Set(tss[i].time, tss[i].step);


            PatternAssociationsStruct[] pas = MachineState.PatternAssociations;
            Tuple<string, string, int>[] pa = new Tuple<string, string, int>[MachineState.NumPatternAssocialtions];
            for (int i = 0; i < MachineState.NumPatternAssocialtions; i++)
                pa[i] = new Tuple<string, string, int>(pas[i].mac, pas[i].pattern, pas[i].ci);

            VUTargetStruct[] vus = MachineState.VUTargets;
            Tuple<int, string, string>[] VUConns = new Tuple<int, string, string>[MachineState.NumVUTargets];
            for (int i = 0; i < MachineState.NumVUTargets; i++)
            {
                VUConns[i] = new Tuple<int, string, string>(vus[i].index, vus[i].machineFrom, vus[i].machineTo);
            }

            seqenceEditor.SetViewSettings(tsl, MachineState.Zoom, pa, VUConns);

            seqenceEditor.zoomSlider.ValueChanged += (sender2, e2) =>
            {
                MachineState.Zoom = seqenceEditor.zoomSlider.Value;
            };

            SequenceEditor.ViewSettings.TimeSignatureList.Changed += () =>
            {
                int i = 0;
                TimeSignatureStruct[] tss2 = MachineState.TimeSignatures;
                foreach (Tuple<int, int> bar in SequenceEditor.ViewSettings.TimeSignatureList.GetBars(SequenceEditor.ViewSettings.SongEnd))
                {
                    if (i >= ModernSequenceEditorMachine.MAX_NUM_TIME_SIGNATURES)
                        break;
                    tss2[i].time = bar.Item1;
                    tss2[i].step = bar.Item2;
                    i++;
                }
                MachineState.NumTimeSignatures = i;
            };

            SequenceEditor.ViewSettings.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "PatternAssociations")
                {
                    PatternAssociationsStruct[] pas2 = MachineState.PatternAssociations;
                    int i = 0;
                    foreach (KeyValuePair<IPattern, PatternEx> pat in SequenceEditor.ViewSettings.PatternAssociations)
                    {
                        if (i >= ModernSequenceEditorMachine.MAX_NUM_PATTERN_ASSOCIATIONS)
                            break;

                        pas2[i].mac = pat.Key.Machine.Name;
                        pas2[i].pattern = pat.Key.Name;
                        pas2[i].ci = pat.Value.ColorIndex;
                        i++;
                    }
                    MachineState.NumPatternAssocialtions = i;

                    // Copy colors to horizontal sequencer
                    BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.Clear();
                    foreach (var pamse in SequenceEditor.ViewSettings.PatternAssociations)
                    {
                        BuzzGUI.SequenceEditor.PatternEx paex = new BuzzGUI.SequenceEditor.PatternEx();
                        paex.ColorIndex = pamse.Value.ColorIndex;
                        BuzzGUI.SequenceEditor.SequenceEditor.ViewSettings.PatternAssociations.Add(pamse.Key, paex);
                    }
                }
                if (e.PropertyName == "VUMeterTarget")
                {
                    VUTargetStruct[] vus2 = MachineState.VUTargets;
                    int i = 0;
                    foreach (KeyValuePair<ISequence, IMachineConnection> con in SequenceEditor.ViewSettings.VUMeterMachineConnection)
                    {
                        if (i >= ModernSequenceEditorMachine.MAX_NUM_VU_TARGETS)
                            break;

                        if (con.Value != null)
                        {
                            int index = seqenceEditor.Song.Sequences.IndexOf(con.Key);
                            if (index >= 0)
                            {
                                vus2[i] = new VUTargetStruct();
                                vus2[i].machineFrom = con.Value.Source.Name;
                                vus2[i].machineTo = con.Value.Destination.Name;
                                vus2[i].index = index;
                                i++;
                            }
                        }
                    }
                    MachineState.NumVUTargets = i;
                }
            };
        }

        internal void Sequencer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "OpenNewWindow")
            {
                CreateNewSeqencerWindow();
            }
        }
    }

    [MachineGUIFactoryDecl(PreferWindowedGUI = true, IsGUIResizable = true, UseThemeStyles = true)]
    public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new ModernSequenceEditorGUI(); } }
    public class ModernSequenceEditorGUI : UserControl, IMachineGUI
    {
        IMachine machine;
        ModernSequenceEditorMachine modernSeqMachine;
        SequenceEditor seq;

        public IMachine Machine
        {
            get { return machine; }
            set
            {
                if (machine != null)
                {
                }

                machine = value;

                if (machine != null)
                {
                    modernSeqMachine = (ModernSequenceEditorMachine)machine.ManagedMachine;
                }
            }
        }

        public SequenceEditor Seq { get => seq; set => seq = value; }
        public Grid MachineViewGrid { get; private set; }

        public ModernSequenceEditorGUI()
        {
            if (SequenceEditor.Settings.SequenceEditorMode != SequenceEditorMode.ParameterWindow)
            {
                Content = new TextBlock() { Text = "This is not the window you are looking for.\nTry Sequence View (F4).\n\nI have spoken.", FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin= new Thickness(0, 20, 0 , 0) };

                this.Loaded += (sender, e) =>
                {
                    var wnd = (MachineGUIWindow)this.Parent;
                    wnd.MinWidth = 300;
                    wnd.MinHeight = 200;
                };
            }

            if (SequenceEditor.Settings.SequenceEditorMode == SequenceEditorMode.ParameterWindow)
            {
                this.Loaded += ParameterWindow_Loaded;
                this.Unloaded += ModernSequenceEditorGUI_Unloaded;

                this.IsVisibleChanged += (sender, e) =>
                {
                    modernSeqMachine.MachineState.IsVisible = this.IsVisible;
                };
            }
        }

        private void ModernSequenceEditorGUI_Unloaded(object sender, RoutedEventArgs e)
        {
            if (modernSeqMachine.NonBuzzWindow != null)
            {
                modernSeqMachine.NonBuzzWindow.Close();
            }
        }

        void ParameterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Resources.MergedDictionaries.Add(modernSeqMachine.GetBuzzThemeResources());

            seq = new SequenceEditor(machine.Graph.Buzz, this.Resources);
            seq.PropertyChanged += modernSeqMachine.Sequencer_PropertyChanged;
            modernSeqMachine.Seq = seq;
            seq.MinHeight = 400;
            seq.MinWidth = 600;

            var wnd = (MachineGUIWindow)this.Parent;
            wnd.SizeToContent = SizeToContent.Manual;

            wnd.MinHeight = 600;
            wnd.MinWidth = 800;
            wnd.ShowInTaskbar = true;

            this.Height = 600;

            if (modernSeqMachine.MachineState.WndWidth > 0)
            {
                Width = modernSeqMachine.MachineState.WndWidth;
                //wnd.Height = modernSeqMachine.MachineState.WndHeight;
                Height = modernSeqMachine.MachineState.WndHeight;
                wnd.Top = modernSeqMachine.MachineState.WndTop;
                wnd.Left = modernSeqMachine.MachineState.WndLeft;
                wnd.WindowState = (WindowState)modernSeqMachine.MachineState.WndState;
            }

            this.EnableEvents(wnd);

            // Remove TextInput events so that the keys don't act as MIDI keyboard.
            //Utils.ClearEventInvocations(wnd, "PreviewTextInput");

            this.Content = seq;
            seq.Song = machine.Graph.Buzz.Song;
            seq.SetVisibility(true);

            //UpdateSeqViewData(seq);

            modernSeqMachine.SetGUI(this);
            wnd.PreviewKeyDown += modernSeqMachine.SequenceEditorWindow_PreviewKeyDown;

            e.Handled = true; // This removes the keyboard == MIDI keyboard feature for machines
        }

        private void EnableEvents(MachineGUIWindow wnd)
        {
            wnd.LocationChanged += (sender, e) =>
            {
                modernSeqMachine.MachineState.WndTop = wnd.Top;
                modernSeqMachine.MachineState.WndLeft = wnd.Left;
            };

            wnd.SizeChanged += (sender, e) =>
            {
                modernSeqMachine.MachineState.WndWidth = this.Width;// wnd.Width;
                modernSeqMachine.MachineState.WndHeight = this.Height;// wnd.Height;
            };

            wnd.StateChanged += (sender, e) =>
            {
                modernSeqMachine.MachineState.WndState = (int)wnd.WindowState;
            };
        }
    }
}
