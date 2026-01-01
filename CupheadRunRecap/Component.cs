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
using System.Reflection;

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
        private ComponentSettings settings;

        private bool isRunning = false;
        private bool isRunInProgreess = false;
        private bool savedSceneData = false;

        public string previousSceneName;

        public string sceneName = "";
        public bool isLoading = false;
        public float scoringTime = 0f;

        public string level;
        public float levelTime;
        public int hpBonus;
        public int parries;
        public int superMeter;
        public int coins;
        public bool useCoinsInsteadOfSuperMeter;
        public Mode difficulty;
        public int starSkipCounter;
        public int starSkipCounterOld;


        public TimeSpan? SegmentStartTime;
        public TimeSpan? SegmentEndTime;

        private JObject recapJson;

        public Component(LiveSplitState state)
        {

            settings = new ComponentSettings();
            log = new LogManager();

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
                            if (isRunInProgreess)
                            {
                                MainLoop();
                            }
                        }
                        catch (Exception ex)
                        {
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
            sceneName = Model.CurrentState.Run.Metadata.CustomVariableValue("scene name");
            bool.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("loading"), out isLoading);
            float.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("scoring time"), out scoringTime);

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
        }
        private void SaveLevelData(string sceneName)
        {
            SegmentEndTime = Model.CurrentState.CurrentTime.GameTime;

            var attempt = GetCurrentAttempt();
            if (attempt == null) return;

            JObject sceneObj = new JObject
            {
                ["name"] = sceneName.Substring(6),
                ["levelTime"] = ((float)Math.Truncate(scoringTime * 100) / 100),
                ["endTime"] = FormatTime(SegmentEndTime.Value)
            };

            ((JArray)attempt["scenes"]).Add(sceneObj);
            SaveJson();
        }
        private void StoreScoreboardData()
        {
            level = "win";
            log.AddEntry(new EventLogEntry(levelTime.ToString()));
            int scoringHits = 0;
            int.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("hits"), out scoringHits);
            hpBonus = (scoringHits >= 3) ? 0 : (3 - scoringHits);
            int.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("parries"), out parries);
            int.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("super meter"), out superMeter);
            int.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("coins"), out coins);
            bool.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("use coins instead of super meter"), out useCoinsInsteadOfSuperMeter);
            Enum.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("difficulty"), out difficulty);
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

            int.TryParse(Model.CurrentState.Run.Metadata.CustomVariableValue("star skip counter raw"), out starSkipCounter);

            if (starSkipCounter > starSkipCounterOld)
            {
                sceneObj["starSkips"] = starSkipCounter - starSkipCounterOld;
                starSkipCounterOld = starSkipCounter;
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
            starSkipCounterOld = 0;
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
            starSkipCounterOld = 0;
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