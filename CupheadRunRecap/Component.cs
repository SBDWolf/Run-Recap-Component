using LiveSplit.Model;
using LiveSplit.UI;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.Xml;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using LiveSplit.Options;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

namespace CupheadRunRecap
{
    public class Component : LiveSplit.UI.Components.IComponent
    {
        public TimerModel Model { get; set; }
        public string ComponentName => "Cuphead Run Recap";

        public const int REFRESH_RATE = 120;
        public const string RUN_RECAP_FILEPATH = "./run_recap.rrc";
        public const string RUN_RECAP_TREE_VERSION = "v0.2";

        public IDictionary<string, Action> ContextMenuControls => null;

        private LogManager log;
        private MemoryManager memory;
        private ComponentSettings settings;

        private bool isRunning = false;
        private bool isRunInProgreess = false;
        private bool savedSceneData = false;

        public string previousSceneName;

        public string level;
        public float levelTime;
        public int hpBonus;
        public int parries;
        public int superMeter;
        public int coins;
        public bool useCoinsInsteadOfSuperMeter;

        public TimeSpan? SegmentStartTime;
        public TimeSpan? SegmentEndTime;

        private JObject recapJson;

        public Component(LiveSplitState state)
        {

            settings = new ComponentSettings();
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

        private void LoadOrCreateJson()
        {
            if (File.Exists(RUN_RECAP_FILEPATH))
            {
                recapJson = JObject.Parse(File.ReadAllText(RUN_RECAP_FILEPATH));
            }
            else
            {
                recapJson = new JObject
                {
                    ["version"] = RUN_RECAP_TREE_VERSION,
                    ["attempts"] = new JArray()
                };
            }
        }

        private JObject GetCurrentAttempt()
        {
            int id = Model.CurrentState.Run.AttemptHistory.Last().Index + 1;
            var attempts = (JArray)recapJson["attempts"];

            return attempts.FirstOrDefault(a => (int)a["id"] == id) as JObject;
        }

        private JObject CreateNewAttempt()
        {
            int id = Model.CurrentState.Run.AttemptHistory.Last().Index + 1;

            JObject attempt = new JObject
            {
                ["id"] = id,
                ["scenes"] = new JArray()
            };

            ((JArray)recapJson["attempts"]).Add(attempt);
            SaveJson();

            return attempt;
        }

        private void SaveJson()
        {
            File.WriteAllText(RUN_RECAP_FILEPATH, recapJson.ToString());
        }

        public void StartComponent()
        {
            if (isRunning) { return; }
            isRunning = true;

            // set .rrc file icon if it's not already set
            string dllDir = Path.GetDirectoryName(
            typeof(Component).Assembly.Location);

            string iconPath = Path.Combine(dllDir, "rrc.ico");

            FileTypeRegister.EnsureRRCFileIcon(iconPath);


            Task.Factory.StartNew(delegate ()
            {
                try
                {
                    Stopwatch stopWatch = new Stopwatch();
                    log.AddEntry(new EventLogEntry("About to start MainLoop"));
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

            // just loading the scoreboard -> save segment time and level time
            if (sceneName == "scene_win" && isLoading && !savedSceneData)
            {
                log.AddEntry(new EventLogEntry("Saving Level Data"));
                SaveLevelData(previousSceneName);
                StoreScoreboardData();
                savedSceneData = true;
            }
            // exiting the scoreboard -> save segment time, scoring data, and save it all to the xml
            else if (previousSceneName == "scene_win" && isLoading && !savedSceneData)
            {
                log.AddEntry(new EventLogEntry("Saving Scoreboard Data"));
                SaveScoreboardData();
                ClearScoreboardData();
                savedSceneData = true;
            }
            // loading without sceneName change -> level retry, so save segment time and level time
            //else if (isLoading && sceneName == previousSceneName && !savedSceneData)
            //{
            //    log.AddEntry(new EventLogEntry("Saving Level Data (Retry)"));
            //    log.AddEntry(new EventLogEntry("sceneName: " + sceneName));
            //    log.AddEntry(new EventLogEntry("previousSceneName: " + previousSceneName));
            //    SaveLevelData(previousSceneName);
            //    savedSceneData = true;
            //}
            // any other scene change -> save segment time
            else if (isLoading && !savedSceneData)
            {
                log.AddEntry(new EventLogEntry("Saving Generic Scene Data"));
                SaveGenericSceneData(previousSceneName);
                savedSceneData = true;
            }



            if (isLoading)
            {
                previousSceneName = sceneName;
            }
            else
            {
                savedSceneData = false;
            }
        }
        private void SaveLevelData(string sceneName)
        {
            SegmentEndTime = Model.CurrentState.CurrentTime.GameTime;

            var attempt = GetCurrentAttempt();
            if (attempt == null) return;

            JObject sceneObj = new JObject
            {
                ["name"] = sceneName.Substring(6),
                ["levelTime"] = ((float)Math.Truncate(memory.ScoringTime() * 100) / 100).ToString("F2"),
                ["endTime"] = FormatTime(SegmentEndTime.Value)
            };

            ((JArray)attempt["scenes"]).Add(sceneObj);
            SaveJson();
        }
        private void StoreScoreboardData()
        {
            level = "win";
            log.AddEntry(new EventLogEntry(levelTime.ToString()));
            var scoringHits = memory.ScoringHits();
            hpBonus = (scoringHits >= 3) ? 0 : (3 - scoringHits);
            parries = memory.ScoringParries();
            superMeter = memory.ScoringSuperMeter();
            coins = memory.ScoringCoins();
            useCoinsInsteadOfSuperMeter = memory.ScoringUseCoinsInsteadOfSuperMeter();
        }
        private void SaveScoreboardData()
        {
            SegmentEndTime = Model.CurrentState.CurrentTime.GameTime;

            var attempt = GetCurrentAttempt();
            if (attempt == null) return;

            JObject sceneObj = new JObject
            {
                ["name"] = level,
                ["hp"] = hpBonus,
                ["parries"] = parries,
            };

            if (!useCoinsInsteadOfSuperMeter)
            {
                sceneObj["superMeter"] = superMeter;
            }
            else
            {
                sceneObj["coins"] = coins;
            }

            sceneObj["endTime"] = FormatTime(SegmentEndTime.Value);

            ((JArray)attempt["scenes"]).Add(sceneObj);
            SaveJson();
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
        }
        private void SaveGenericSceneData(string sceneName)
        {
            SegmentEndTime = Model.CurrentState.CurrentTime.GameTime;

            var attempt = GetCurrentAttempt();
            if (attempt == null) return;

            JObject sceneObj = new JObject
            {
                ["name"] = sceneName.Substring(6),
                ["endTime"] = FormatTime(SegmentEndTime.Value)
            };

            ((JArray)attempt["scenes"]).Add(sceneObj);
            SaveJson();
        }
        private string FormatTime(TimeSpan t)
        {
            return $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";
        }
        public void Update(IInvalidator invalidator, LiveSplitState lvstate, float width, float height, LayoutMode mode) { }
        public void OnReset(object sender, TimerPhase e) {
            isRunInProgreess = false;
            savedSceneData = true;
            previousSceneName = "none";
        }
        public void OnResume(object sender, EventArgs e) { }
        public void OnPause(object sender, EventArgs e) { }
        private void OnStart(object sender, EventArgs e)
        {
            log.AddEntry(new EventLogEntry("Starting OnStart"));
            isRunInProgreess = true;
            savedSceneData = true;

            LoadOrCreateJson();
            CreateNewAttempt();

            previousSceneName = "none";
            SegmentStartTime = Model.CurrentState.CurrentTime.GameTime;
            log.AddEntry(new EventLogEntry("Finishing OnStart"));
        }
        public void OnUndoSplit(object sender, EventArgs e) { }
        public void OnSkipSplit(object sender, EventArgs e) { }
        public void OnSplit(object sender, EventArgs e) {
            // if the final split has been hit, save data about the final segment
            if (Model.CurrentState.CurrentSplitIndex >= Model.CurrentState.Run.Count)
            {
                isRunInProgreess = false;

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