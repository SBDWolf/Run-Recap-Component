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
using System.Globalization;
using System.Text.RegularExpressions;
using LiveSplit.TimeFormatters;

namespace CupheadRunRecap
{
    public class Component : LiveSplit.UI.Components.IComponent
    {
        public TimerModel Model { get; set; }
        public string ComponentName => "Cuphead Run Recap";

        public const int REFRESH_RATE = 120;
        //public const string RUN_RECAP_FILEPATH = "./run_recap.rrc";
        public const string RUN_RECAP_TREE_VERSION = "v1.2";

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
        public Mode difficulty;
        public int starSkipAmount;

        public int starSkipCounter;
        public float starSkipCounterDecimal;
        public TimeSpan? DifficultyTickerStartTime;
        public TimeSpan? DifficultyTickerEndTime;


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

        public static string DefaultRunRecapDirectory
        {
            get
            {
                string exeDir = Path.GetDirectoryName(
                    typeof(Component).Assembly.Location);

                return Path.Combine(exeDir, "Run Recap");
            }
        }

        private string RunRecapFilePath
        {
            get
            {
                string baseDir = EnsureRunRecapDirectory();

                string lssPath = Model?.CurrentState?.Run?.FilePath;

                string fileName;
                if (string.IsNullOrWhiteSpace(lssPath))
                {
                    fileName = "UnsavedSplits.rrc";
                }
                else
                {
                    string splitsName = Path.GetFileNameWithoutExtension(lssPath);
                    fileName = $"{splitsName}.rrc";
                }

                return Path.Combine(baseDir, fileName);
            }
        }

        private string EnsureRunRecapDirectory()
        {
            string dir = settings.RunRecapDirectory;

            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = DefaultRunRecapDirectory;
            }

            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch
            {
                // fallback if directory is invalid or inaccessible
                dir = DefaultRunRecapDirectory;
                Directory.CreateDirectory(dir);
            }

            return dir;
        }

        private void LoadOrCreateJson()
        {
            // TODO: when pushing a new version to the run recap tree, this method should probably check for if the file has been loaded with an obsolete version and convert the file if so

            if (File.Exists(RunRecapFilePath))
            {
                recapJson = JObject.Parse(File.ReadAllText(RunRecapFilePath));
            }
            else
            {
                recapJson = new JObject();
            }

            if (recapJson["version"] == null)
            {
                recapJson["version"] = RUN_RECAP_TREE_VERSION;
            }

            if (recapJson["attempts"] == null || recapJson["attempts"].Type != JTokenType.Array)
            {
                recapJson["attempts"] = new JArray();
            }

            ConvertOldVersionsAndFixIds();
            SaveJson();
        }

        private void ConvertOldVersionsAndFixIds()
        {
            log.AddEntry(new EventLogEntry("hello is this even running?"));
            var attempts = (JArray)recapJson["attempts"];

            string recapJsonVersion = recapJson["version"].ToString();

            // convert from v1.0
            if (recapJsonVersion == "v1.0")
            {
                FixCultureVariantStrings(attempts);
                RemoveZeroValueFields(attempts);
                recapJson["version"] = RUN_RECAP_TREE_VERSION;
            }

            // convert from v1.1
            if (recapJsonVersion == "v1.1")
            {
                log.AddEntry(new EventLogEntry("detected version 1.1"));
                RemoveZeroValueFields(attempts);
                log.AddEntry(new EventLogEntry("saving new run recap version"));
                recapJson["version"] = RUN_RECAP_TREE_VERSION;
            }

            // rebuild IDs if corrupted or missing
            for (int i = 0; i < attempts.Count; i++)
            {
                attempts[i]["id"] = i;
            }

        }
        private void FixCultureVariantStrings(JArray attempts)
        {
            for (int i = 0; i < attempts.Count; i++)
            {
                var scenes = (JArray)attempts[i]["scenes"];
                for (int j = 0; j < scenes.Count; j++)
                {
                    // fix level time being a culture-variant string on prior versions
                    JToken levelTimeToken = scenes[j]["levelTime"];
                    if (levelTimeToken == null || levelTimeToken.Type != JTokenType.String)
                        continue;

                    string levelTimeStr = levelTimeToken.ToString();
                    if (TryParseCultureAgnosticFloat(levelTimeStr, out float levelTime))
                    {
                        levelTime = (float)Math.Truncate(levelTime * 100) / 100;
                        scenes[j]["levelTime"] = levelTime;
                    }
                }
            }
        }

        private void RemoveZeroValueFields(JArray attempts)
        {
            try
            {
                foreach (JObject attempt in attempts)
                {

                    var scenes = (JArray)attempt["scenes"];
                    foreach (JObject scene in scenes)
                    {
                        // we only need to handle win screen scenes as legacy versions did not have the possibility of generating 0 values elsewhere
                        if (scene["name"]?.ToString() != "win")
                            continue;

                        List<string> propertiesToCheck = new List<string>()
                        {
                            "hp",
                            "parries",
                            "superMeter",
                            "coins",
                        };

                        List<string> propertiesToRemove = new List<string>();

                        foreach (string property in propertiesToCheck)
                        {
                            if (scene[property]?.Value<int>() == 0)
                            {
                                propertiesToRemove.Add(property);
                            }
                        }

                        foreach (string propName in propertiesToRemove)
                        {
                            scene.Remove(propName);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                log.AddEntry(new EventLogEntry(ex.ToString()));
            }

        }
        private bool TryParseCultureAgnosticFloat(string input, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            input = input.Trim();

            int lastDot = input.LastIndexOf('.');
            int lastComma = input.LastIndexOf(',');

            // determine decimal separator by last occurrence
            char decimalSeparator;
            if (lastDot > lastComma)
            {
                decimalSeparator = '.';
            }
            else
            {
                decimalSeparator = ',';
            }

            // remove all integer separators, and replace the decimal separator with a period
            string cleaned = input
                .Replace(decimalSeparator == '.' ? "," : ".", "")
                .Replace(decimalSeparator, '.');

            return float.TryParse(
                cleaned,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }
        private JObject GetCurrentAttempt()
        {
            var attempts = recapJson?["attempts"] as JArray;
            if (attempts == null || attempts.Count == 0)
                return null;

            return attempts.Last as JObject;
        }

        private JObject CreateNewAttempt()
        {
            JArray attempts = (JArray)recapJson["attempts"];

            int newId = attempts.Count;

            int lssAttemptId = Model.CurrentState.Run.AttemptHistory.Last().Index + 1;

            JObject attempt = new JObject
            {
                ["id"] = newId,
                ["lssAttemptId"] = lssAttemptId,
                ["startedAt"] = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                ["scenes"] = new JArray()
            };

            attempts.Add(attempt);

            SaveJson();
            return attempt;
        }

        private void SaveJson()
        {
            File.WriteAllText(RunRecapFilePath, recapJson.ToString());
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
                        catch (Exception ex)
                        {
                            log.AddEntry(new EventLogEntry(ex.ToString()));
                        }
                        if (!settings.StarSkipDisplayAsInt)
                        {
                            Model.CurrentState.Run.Metadata.SetCustomVariable("Star Skip Counter", starSkipCounter.ToString());
                        }
                        else
                        {
                            Model.CurrentState.Run.Metadata.SetCustomVariable("Star Skip Counter", (Math.Truncate(starSkipCounterDecimal * 100) / 100).ToString());
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
            log.AddEntry(new EventLogEntry(sceneName));
            //log.AddEntry(new EventLogEntry("sceneName new new new: " + sceneName.ToString()));
            bool isLoading = memory.Loading();
            log.AddEntry(new EventLogEntry(isLoading.ToString()));
            float scoringTime = memory.ScoringTime();
            //log.AddEntry(new EventLogEntry("scoringDifficulty new new new: " + scoringDifficulty.ToString()));

            // just loading the scoreboard -> save segment time and level time
            if (sceneName == "scene_win" && isLoading && !savedSceneData)
            {
                log.AddEntry(new EventLogEntry("Saving Level Data"));
                SaveLevelData(previousSceneName);
                StoreScoreboardData();
                savedSceneData = true;
                //log.AddEntry(new EventLogEntry("scoringTime new new new: " + scoringTime.ToString()));
            }
            // exiting the scoreboard -> save segment time, scoring data, and save it all to the xml
            else if (previousSceneName == "scene_win" && isLoading && !savedSceneData)
            {
                log.AddEntry(new EventLogEntry("Saving Scoreboard Data"));
                SaveScoreboardData();
                ClearScoreboardData();
                savedSceneData = true;
            }
            // loading into a new part of the King Dice fight -> save segment time and level time (using a > 0.2f comparison to prevent this from running in case a frame is missed)
            else if (sceneName.StartsWith("scene_level_dice") && scoringTime > 0.2f && isLoading && !savedSceneData)
            {
                log.AddEntry(new EventLogEntry("Saving King Dice Level Data"));
                SaveLevelData(previousSceneName);
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
                //log.AddEntry(new EventLogEntry("previousSceneName: " + previousSceneName));
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

            // star skip counter main logic
            // relies on the wasm autosplitter exposing a few custom variables
            // first we take them, then calculate the time difference between those custom variables changing value...
            // ...and that's how we determine whether a star skip has occurred
            // we then feed this to a new custom variable
            if (sceneName == "scene_win")
            {
                MonitorStarSkip();
            }
        }
        private void MonitorStarSkip()
        {
            bool startedCounting = false;
            bool.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("difficulty ticker started counting"), out startedCounting);
            bool finishedCounting = false;
            bool.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("difficulty ticker finished counting"), out finishedCounting);
            if (DifficultyTickerStartTime != null)
            {
                log.AddEntry(new EventLogEntry("DifficultyTickerStartTime: " + DifficultyTickerStartTime.Value.TotalMilliseconds.ToString()));
            }
            if (DifficultyTickerEndTime != null)
            {
                log.AddEntry(new EventLogEntry("DifficultyTickerEndTime: " + DifficultyTickerEndTime.Value.TotalMilliseconds.ToString()));
            }

            if (!startedCounting)
            {
                DifficultyTickerStartTime = null;
                DifficultyTickerEndTime = null;
                return;
            }
            if (DifficultyTickerStartTime == null)
            {
                DifficultyTickerStartTime = Model.CurrentState.CurrentTime.GameTime;
            }
            //log.AddEntry(new EventLogEntry("DifficultyTickerStartTime: " + DifficultyTickerStartTime.Value.TotalMilliseconds.ToString()));
            if (!finishedCounting || DifficultyTickerEndTime != null)
            {
                return;
            }
            
            DifficultyTickerEndTime = Model.CurrentState.CurrentTime.GameTime;
            log.AddEntry(new EventLogEntry("DifficultyTickerEndTime after assignment: " + DifficultyTickerEndTime.Value.TotalMilliseconds.ToString()));
            TimeSpan? difficultyTickerTimeDifference = DifficultyTickerEndTime - DifficultyTickerStartTime;
            log.AddEntry(new EventLogEntry("difficultyTickerTimeDifference: " + difficultyTickerTimeDifference.Value.TotalMilliseconds.ToString()));
            log.AddEntry(new EventLogEntry("difficulty: " + difficulty.ToString()));
            if (difficulty == Mode.Easy)
            {
                if (difficultyTickerTimeDifference.Value.TotalMilliseconds < 100)
                {
                    log.AddEntry(new EventLogEntry("Easy mode incrementing starSkipCounter"));
                    starSkipCounter += 1;
                    starSkipCounterDecimal += 1;
                    starSkipAmount = 1;
                }
            }
            else if (difficulty == Mode.Normal)
            {
                if (difficultyTickerTimeDifference.Value.TotalMilliseconds < 100) {
                    starSkipCounter += 2;
                    starSkipCounterDecimal += 1;
                    starSkipAmount = 2;
                }
                else if (difficultyTickerTimeDifference.Value.TotalMilliseconds < 600)
                {
                    log.AddEntry(new EventLogEntry("Normal mode incrementing starSkipCounter"));
                    starSkipCounter += 1;
                    starSkipCounterDecimal += 0.5f;
                    starSkipAmount = 1;
                }
            }
            else if (difficulty == Mode.Hard)
            {
                if (difficultyTickerTimeDifference.Value.TotalMilliseconds < 100)
                {
                    starSkipCounter += 3;
                    starSkipCounterDecimal += 1;
                    starSkipAmount = 3;
                }
                else if (difficultyTickerTimeDifference.Value.TotalMilliseconds < 600)
                {
                    starSkipCounter += 2;
                    starSkipCounterDecimal += (1/3) * 2;
                    starSkipAmount = 2;
                }
                else if (difficultyTickerTimeDifference.Value.TotalMilliseconds < 1100)
                {
                    starSkipCounter += 1;
                    starSkipCounterDecimal += 1/3;
                    starSkipAmount = 1;
                }
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
                ["levelTime"] = ((float)Math.Truncate(memory.ScoringTime() * 100) / 100),
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
            var currentDifficulty = memory.ScoringDifficulty();
            if (currentDifficulty != Mode.None)
            {
                difficulty = currentDifficulty;
            }
        }
        private void SaveScoreboardData()
        {
            SegmentEndTime = Model.CurrentState.CurrentTime.GameTime;

            var attempt = GetCurrentAttempt();
            if (attempt == null) return;

            JObject sceneObj = new JObject
            {
                ["name"] = level,
            };
            if (hpBonus != 0)
            {
                sceneObj["hp"] = hpBonus;
            }
            if (hpBonus != 0)
            {
                sceneObj["parries"] = parries;
            }

            if (!useCoinsInsteadOfSuperMeter)
            {
                if (superMeter != 0)
                {
                    sceneObj["superMeter"] = superMeter;
                }
            }
            else
            {
                if (coins != 0)
                {
                    sceneObj["coins"] = coins;
                }
            }

            if (starSkipAmount != 0)
            {
                sceneObj["starSkips"] = starSkipAmount;
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
            difficulty = Mode.None;
            starSkipAmount = 0;
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
            previousSceneName = "scene_none";
            starSkipCounter = 0;
            starSkipCounterDecimal = 0;
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

            previousSceneName = "scene_none";
            starSkipCounter = 0;
            starSkipCounterDecimal = 0;
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
        public void SetSettings(XmlNode document) { 
            settings.SetSettings(document);
            settings.RunRecapDirectory = EnsureRunRecapDirectory();
        }
        public XmlNode GetSettings(XmlDocument document) { return settings.GetSettings(document); }
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