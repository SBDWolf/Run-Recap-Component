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
using System.Reflection;

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
        public string lastSeenSceneName;
        //public bool storedScoreboardData = false;
        //public bool savedScoreboardData = false;
        public bool savedSceneData = false;
        public string level;
        public float levelTime;
        public int hpBonus;
        public int parries;
        public int superMeter;
        public int coins;
        public bool useCoinsInsteadOfSuperMeter;
        public bool isRunInProgreess = false;
        public TimeSpan? SegmentStartTime;
        public TimeSpan? SegmentEndTime;


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
                            if (memory.HookProcess() && isRunInProgreess)
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
            bool isLoading = memory.Loading();
            //log.AddEntry(new EventLogEntry(sceneName));
            //System.Diagnostics.Debug.WriteLine(sceneName);

            // just loading the scoreboard -> mark current loadless time
            if (sceneName == "scene_win" && isLoading && !savedSceneData)
            {
                SaveLevelData(lastSeenSceneName);
                //ResetSegmentTiming();
                StoreScoreboardData();
                savedSceneData = true;
            }
            // exiting the scoreboard -> mark current loadless time, scoring data, and save it all to the xml
            else if (lastSeenSceneName == "scene_win" && isLoading && !savedSceneData)
            {
                SaveScoreboardData();
                ClearScoreboardData();
                //ResetSegmentTiming();
                savedSceneData = true;
            }
            // any other scene change -> save segment time
            else if (isLoading && !savedSceneData)
            {
                SaveGenericSceneData(lastSeenSceneName);
                savedSceneData = true;
            }

            if (!isLoading)
            {
                savedSceneData = false;
            }


            if (sceneName != previousSceneName)
            {
                lastSeenSceneName = previousSceneName;
                log.AddEntry(new EventLogEntry("Scene change: sceneName " + sceneName + " - previousSceneName " + previousSceneName + " - lastSeenSceneName " + lastSeenSceneName));
            }
            previousSceneName = sceneName;
        }
        private void SaveLevelData(string sceneName)
        {
            SegmentEndTime = Model.CurrentState.CurrentTime.GameTime;
            XmlNode attemptNode = LoadRunRecapFileAndLocateCurrentAttempt();

            if (attemptNode != null)
            {
                XmlElement levelElement = runRecapTree.CreateElement("Scene");
                XmlAttribute sceneNameAttribute = runRecapTree.CreateAttribute("sceneName");
                sceneNameAttribute.Value = sceneName;
                levelElement.Attributes.Append(sceneNameAttribute);

                XmlElement levelTimeElement = runRecapTree.CreateElement("LevelTime");
                levelTimeElement.InnerText = ((float)Math.Truncate(memory.ScoringTime() * 100) / 100).ToString("F2");
                levelElement.AppendChild(levelTimeElement);

                //TODO: add nullability checks?


                //XmlElement StartTimeElement = runRecapTree.CreateElement("StartTime");
                //StartTimeElement.InnerText = string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D3}",
                //    SegmentStartTime.Value.Hours,
                //    SegmentStartTime.Value.Minutes,
                //    SegmentStartTime.Value.Seconds,
                //    SegmentStartTime.Value.Milliseconds
                //);
                //levelElement.AppendChild(StartTimeElement);

                XmlElement EndTimeElement = runRecapTree.CreateElement("EndTime");
                EndTimeElement.InnerText = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                    SegmentEndTime.Value.Hours,
                    SegmentEndTime.Value.Minutes,
                    SegmentEndTime.Value.Seconds,
                    SegmentEndTime.Value.Milliseconds
                );
                levelElement.AppendChild(EndTimeElement);

                attemptNode.AppendChild(levelElement);
                runRecapTree.Save(RUN_RECAP_FILEPATH);
            }
            SegmentEndTime = null;
        }
        private void StoreScoreboardData()
        {
            //SegmentStartTime = Model.CurrentState.CurrentTime.GameTime;
            level = "scene_win";
            log.AddEntry(new EventLogEntry(levelTime.ToString()));
            var scoringHits = memory.ScoringHits();
            hpBonus = (scoringHits >= 3) ? 0 : (3 - scoringHits);
            parries = memory.ScoringParries();
            superMeter = memory.ScoringSuperMeter();
            coins = memory.ScoringCoins();
            useCoinsInsteadOfSuperMeter = memory.ScoringUseCoinsInsteadOfSuperMeter();
            //savedScoreboardData = false;
            //storedScoreboardData = true;
        }
        private void SaveScoreboardData()
        {
            SegmentEndTime = Model.CurrentState.CurrentTime.GameTime;
            XmlNode attemptNode = LoadRunRecapFileAndLocateCurrentAttempt();

            if (attemptNode != null)
            {
                XmlElement levelElement = runRecapTree.CreateElement("Scene");
                XmlAttribute sceneNameAttribute = runRecapTree.CreateAttribute("sceneName");
                sceneNameAttribute.Value = level;
                levelElement.Attributes.Append(sceneNameAttribute);

                XmlElement levelHPElement = runRecapTree.CreateElement("HPBonus");
                levelHPElement.InnerText = hpBonus.ToString();
                levelElement.AppendChild(levelHPElement);

                XmlElement levelParriesElement = runRecapTree.CreateElement("Parries");
                levelParriesElement.InnerText = parries.ToString();
                levelElement.AppendChild(levelParriesElement);

                if (!useCoinsInsteadOfSuperMeter)
                {
                    XmlElement levelSuperMeterElement = runRecapTree.CreateElement("SuperMeter");
                    levelSuperMeterElement.InnerText = superMeter.ToString();
                    levelElement.AppendChild(levelSuperMeterElement);
                }
                else
                {
                    XmlElement levelCoinsElement = runRecapTree.CreateElement("Coins");
                    levelCoinsElement.InnerText = coins.ToString();
                    levelElement.AppendChild(levelCoinsElement);
                }
                //TODO: add nullability checks - can crash if the timer is started after a scoreboard's start

                
                //XmlElement StartTimeElement = runRecapTree.CreateElement("StartTime");
                //StartTimeElement.InnerText = string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D3}",
                //    SegmentStartTime.Value.Hours,
                //    SegmentStartTime.Value.Minutes,
                //    SegmentStartTime.Value.Seconds,
                //    SegmentStartTime.Value.Milliseconds
                //);
                //levelElement.AppendChild(StartTimeElement);

                XmlElement EndTimeElement = runRecapTree.CreateElement("EndTime");
                EndTimeElement.InnerText = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                    SegmentEndTime.Value.Hours,
                    SegmentEndTime.Value.Minutes,
                    SegmentEndTime.Value.Seconds,
                    SegmentEndTime.Value.Milliseconds
                );
                levelElement.AppendChild(EndTimeElement);

                attemptNode.AppendChild(levelElement);
                runRecapTree.Save(RUN_RECAP_FILEPATH);
                //savedScoreboardData = true;
            }
            SegmentEndTime = null;
        }
        private void ClearScoreboardData()
        {
            level = "";
            levelTime = 0f;
            hpBonus = 3;
            parries = 0;
            superMeter = 0;
            coins = 0;
            useCoinsInsteadOfSuperMeter = false;
            //savedScoreboardData = true;
            //storedScoreboardData = false;
        }
        private void SaveGenericSceneData(string sceneName)
        {
            SegmentEndTime = Model.CurrentState.CurrentTime.GameTime;
            XmlNode attemptNode = LoadRunRecapFileAndLocateCurrentAttempt();

            if (attemptNode != null)
            {
                XmlElement levelElement = runRecapTree.CreateElement("Scene");
                XmlAttribute sceneNameAttribute = runRecapTree.CreateAttribute("sceneName");
                sceneNameAttribute.Value = sceneName;
                levelElement.Attributes.Append(sceneNameAttribute);
                //TODO: add nullability checks?


                //XmlElement StartTimeElement = runRecapTree.CreateElement("StartTime");
                //StartTimeElement.InnerText = string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D3}",
                //    SegmentStartTime.Value.Hours,
                //    SegmentStartTime.Value.Minutes,
                //    SegmentStartTime.Value.Seconds,
                //    SegmentStartTime.Value.Milliseconds
                //);
                //levelElement.AppendChild(StartTimeElement);

                XmlElement EndTimeElement = runRecapTree.CreateElement("EndTime");
                EndTimeElement.InnerText = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                    SegmentEndTime.Value.Hours,
                    SegmentEndTime.Value.Minutes,
                    SegmentEndTime.Value.Seconds,
                    SegmentEndTime.Value.Milliseconds
                );
                levelElement.AppendChild(EndTimeElement);

                attemptNode.AppendChild(levelElement);
                runRecapTree.Save(RUN_RECAP_FILEPATH);
            }
            SegmentEndTime = null;
        }
        //private void ResetSegmentTiming()
        //{
        //    SegmentStartTime = SegmentEndTime;
        //    SegmentEndTime = null;
        //}
        private XmlNode LoadRunRecapFileAndLocateCurrentAttempt()
        {
            runRecapTree.Load(RUN_RECAP_FILEPATH);
            return runRecapTree.SelectSingleNode($".//Attempt[@id='{Model.CurrentState.Run.AttemptHistory.Last().Index + 1}']");
        }
        public void Update(IInvalidator invalidator, LiveSplitState lvstate, float width, float height, LayoutMode mode) { }
        public void OnReset(object sender, TimerPhase e) { }
        public void OnResume(object sender, EventArgs e) { }
        public void OnPause(object sender, EventArgs e) { }
        public void OnStart(object sender, EventArgs e)
        {
            isRunInProgreess = true;
            try
            {
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

                previousSceneName = "none";
            }
            catch(Exception ex)
            {
                log.AddEntry(new EventLogEntry(ex.ToString()));
            }

            SegmentStartTime = Model.CurrentState.CurrentTime.GameTime;
        }
        public void OnUndoSplit(object sender, EventArgs e) { }
        public void OnSkipSplit(object sender, EventArgs e) { }
        public void OnSplit(object sender, EventArgs e) {
            // if the final split has been hit, save data about the final segment
            log.AddEntry(new EventLogEntry("OnSplit Event"));
            if (Model.CurrentState.CurrentSplitIndex >= Model.CurrentState.Run.Count)
            {
                isRunInProgreess = false;
                log.AddEntry(new EventLogEntry("Last Split Hit"));
                string sceneName = memory.SceneName();
                // if we're in the middle of a level, save that level's data
                if (sceneName.StartsWith("scene_level"))
                {
                    SaveLevelData(sceneName);
                }
                // else, the player is likely running some oddball custom category, so save generic scene data
                else
                {
                    SaveGenericSceneData(sceneName);
                }

                XmlNode attemptNode = LoadRunRecapFileAndLocateCurrentAttempt();

                if (attemptNode != null)
                {
                    XmlElement RunTimeElement = runRecapTree.CreateElement("RunTime");
                    TimeSpan? gameTime = Model.CurrentState.CurrentTime.GameTime;
                    RunTimeElement.InnerText = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                        gameTime.Value.Hours,
                        gameTime.Value.Minutes,
                        gameTime.Value.Seconds,
                        gameTime.Value.Milliseconds
                    );

                    attemptNode.AppendChild(RunTimeElement);
                    runRecapTree.Save(RUN_RECAP_FILEPATH);
                }
            }
        }
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