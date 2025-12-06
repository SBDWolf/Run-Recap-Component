using LiveSplit.Model;
using LiveSplit.UI;
using System.Diagnostics;
using System.Xml;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using LiveSplit.Options;
using System.Linq;
using System.IO;
using System.ComponentModel;

namespace CupheadRunRecap
{

    public class Component : LiveSplit.UI.Components.IComponent
    {
        public TimerModel Model { get; set; }
        public string ComponentName { get { return "Cuphead Run Recap"; } }
        public const int REFRESH_RATE = 120;
        public const string RUN_RECAP_FILEPATH = "./run_recap.xml";
        public IDictionary<string, Action> ContextMenuControls { get { return null; } }
        //private LogicManager logic;
        private LogManager log;
        private MemoryManager memory;
        private ComponentSettings settings;
        private bool isRunning = false;
        public XmlDocument runRecapTree;
        public string previousSceneName;


        public Component(LiveSplitState state)
        {

            settings = new ComponentSettings();
            //logic = new LogicManager(settings);
            log = new LogManager();
            memory = new MemoryManager();

            if (state != null)
            {
                Model = new TimerModel() { CurrentState = state };
                Model.InitializeGameTime();
                Model.CurrentState.IsGameTimePaused = true;
                state.OnReset += OnReset;
                state.OnPause += OnPause;
                state.OnResume += OnResume;
                state.OnStart += OnStart;
                state.OnSplit += OnSplit;
                state.OnUndoSplit += OnUndoSplit;
                state.OnSkipSplit += OnSkipSplit;
            }

            StartComponent();
        }

        public void StartComponent()
        {
            log.AddEntry(new EventLogEntry("Attempting to start component"));
            if (isRunning) { return; }
            isRunning = true;

            Task.Factory.StartNew(delegate ()
            {
                try
                {
                    Stopwatch stopWatch = new Stopwatch();
                    log.AddEntry(new EventLogEntry(isRunning.ToString()));
                    while (isRunning)
                    {
                        stopWatch.Reset();
                        stopWatch.Start();
                        try
                        {
                            if (memory.HookProcess())
                            {
                                MainLoop();
                            }
                        }
                        catch(Exception ex) {
                            log.AddEntry(new EventLogEntry(ex.ToString()));
                        }

                        stopWatch.Stop();

                        long mainLoopDuration = stopWatch.ElapsedMilliseconds;
                        long inverse_refresh_rate = 1000 / REFRESH_RATE;
                        if (inverse_refresh_rate > mainLoopDuration)
                        {
                            Thread.Sleep((int)(inverse_refresh_rate - mainLoopDuration));
                        }
                    }
                }
                catch(Exception ex) {
                    log.AddEntry(new EventLogEntry(ex.ToString()));
                }

            }, TaskCreationOptions.LongRunning);
        }
        private void MainLoop()
        {
            string sceneName = memory.SceneName();
            //log.AddEntry(new EventLogEntry(sceneName));
            //System.Diagnostics.Debug.WriteLine(sceneName);
            if (sceneName == "scene_win" && previousSceneName.StartsWith("scene_level"))
            {
                log.AddEntry(new EventLogEntry("Attempting to log level data"));
                runRecapTree.Load(RUN_RECAP_FILEPATH);
                XmlNode attemptNode = runRecapTree.SelectSingleNode($".//Attempt[@id='{Model.CurrentState.Run.AttemptHistory.Last().Index + 1}']");
                
                if (attemptNode != null)
                {
                    log.AddEntry(new EventLogEntry("Found attemptNode"));
                    XmlElement levelElement = runRecapTree.CreateElement("Level");
                    XmlAttribute sceneNameAttribute = runRecapTree.CreateAttribute("sceneName");
                    sceneNameAttribute.Value = previousSceneName;
                    levelElement.Attributes.Append(sceneNameAttribute);

                    XmlElement levelTimeElement = runRecapTree.CreateElement("Time");
                    levelTimeElement.InnerText = ((float)Math.Truncate(memory.ScoringTime() * 100) / 100).ToString("F2");
                    levelElement.AppendChild(levelTimeElement);

                    XmlElement levelParriesElement = runRecapTree.CreateElement("Parries");
                    levelParriesElement.InnerText = memory.ScoringParries().ToString();
                    levelElement.AppendChild(levelParriesElement);

                    attemptNode.AppendChild(levelElement);
                    runRecapTree.Save(RUN_RECAP_FILEPATH);
                }


            }
            previousSceneName = sceneName;
        }

        public void Update(IInvalidator invalidator, LiveSplitState lvstate, float width, float height, LayoutMode mode) { }
        public void OnReset(object sender, TimerPhase e) { }
        public void OnResume(object sender, EventArgs e) { }
        public void OnPause(object sender, EventArgs e) { }
        public void OnStart(object sender, EventArgs e)
        {
            try
            {
                log.AddEntry(new EventLogEntry("Creating document for attempt " + (Model.CurrentState.Run.AttemptHistory.Last().Index + 1).ToString()));
                runRecapTree = new XmlDocument();
                if (!File.Exists(RUN_RECAP_FILEPATH))
                {

                    var decl = runRecapTree.CreateXmlDeclaration("1.0", "UTF-8", null);
                    runRecapTree.AppendChild(decl);

                    // Root element
                    XmlElement root = runRecapTree.CreateElement("RunRecap");
                    runRecapTree.AppendChild(root);
                }
                else
                {
                    runRecapTree.Load(RUN_RECAP_FILEPATH);
                }
                XmlElement attempt = runRecapTree.CreateElement("Attempt");
                XmlAttribute attemptNumber = runRecapTree.CreateAttribute("id");
                attemptNumber.Value = (Model.CurrentState.Run.AttemptHistory.Last().Index + 1).ToString();
                attempt.Attributes.Append(attemptNumber);
                runRecapTree.DocumentElement.AppendChild(attempt);
                runRecapTree.Save(RUN_RECAP_FILEPATH);
                log.AddEntry(new EventLogEntry("Successfully created document for attempt " + (Model.CurrentState.Run.AttemptHistory.Last().Index + 1).ToString()));


                previousSceneName = "none";
            }
            catch(Exception ex)
            {
                log.AddEntry(new EventLogEntry(ex.ToString()));
            }

        }
        public void OnUndoSplit(object sender, EventArgs e) { }
        public void OnSkipSplit(object sender, EventArgs e) { }
        public void OnSplit(object sender, EventArgs e) { }
        public Control GetSettingsControl(LayoutMode mode) { return settings; }
        public void SetSettings(XmlNode document) { settings.SetSettings(document); }
        public XmlNode GetSettings(XmlDocument document) { return settings.UpdateSettings(document); }
        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion) { }
        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion) { }
        public float HorizontalWidth { get { return 0; } }
        public float MinimumHeight { get { return 0; } }
        public float MinimumWidth { get { return 0; } }
        public float PaddingBottom { get { return 0; } }
        public float PaddingLeft { get { return 0; } }
        public float PaddingRight { get { return 0; } }
        public float PaddingTop { get { return 0; } }
        public float VerticalHeight { get { return 0; } }
        public void Dispose()
        {
            if (Model != null)
            {
                Model.CurrentState.OnReset -= OnReset;
                Model.CurrentState.OnPause -= OnPause;
                Model.CurrentState.OnResume -= OnResume;
                Model.CurrentState.OnStart -= OnStart;
                Model.CurrentState.OnSplit -= OnSplit;
                Model.CurrentState.OnUndoSplit -= OnUndoSplit;
                Model.CurrentState.OnSkipSplit -= OnSkipSplit;
                Model = null;
            }
            settings.Dispose();
        }

    }
}