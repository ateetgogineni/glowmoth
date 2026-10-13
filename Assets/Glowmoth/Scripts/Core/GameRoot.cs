using UnityEngine;

namespace Glowmoth
{
    /// <summary>
    /// Entry point and screen flow. Boots automatically after the first scene loads, then builds every system
    /// from code: camera, background, particles, audio, level and UI. The scene itself can stay empty.
    ///
    ///   Menu -> Level Select -> Playing <-> Paused
    ///                              |-> Result (win/lose) -> Next / Retry / Levels
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public enum State { Menu, LevelSelect, Playing, Paused, Result }

        public static GameRoot Instance { get; private set; }
        public State Current { get; private set; }
        public int LevelIndex { get; private set; }

        Camera cam;
        Background background;
        LevelController level;
        UIManager ui;
        int lastWidth, lastHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance == null) new GameObject("Glowmoth").AddComponent<GameRoot>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Mobile performance settings.
            Application.targetFrameRate = GameConfig.TargetFps;
            QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = false;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            // Generate all content (art atlas, sounds) once.
            SaveSystem.Load();
            SpriteFactory.Build();
            SfxSynth.Build();

            cam = CreateCamera();
            background = Background.Create(transform);
            Fx.Create(transform);
            AudioManager.Create(transform);
            level = LevelController.Create(transform, cam);
            level.Won += OnLevelWon;
            level.Lost += OnLevelLost;
            ui = UIManager.Create(this, level);

            FitCamera();
            ShowMenu();
        }

        Camera CreateCamera()
        {
            var go = new GameObject("Camera");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -10f);
            var c = go.AddComponent<Camera>();
            c.orthographic = true;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 50f;
            c.allowHDR = false;
            c.allowMSAA = false;
            go.AddComponent<AudioListener>();
            return c;
        }

        /// <summary>Always show the full playfield width; taller phones simply see more sky above and below.</summary>
        void FitCamera()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            float aspect = (float)lastWidth / Mathf.Max(1, lastHeight);
            cam.orthographicSize = Mathf.Max(GameConfig.ViewMinHalfHeight, GameConfig.ViewHalfWidth / aspect);
            background.Fit(cam);
        }

        void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight) FitCamera();
            if (Input.GetKeyDown(KeyCode.Escape)) OnBack(); // Android back button
        }

        // ------------------------------------------------------------------ flow

        public void ShowMenu()
        {
            Current = State.Menu;
            Time.timeScale = 1f;
            background.SetWorld(0);
            AudioManager.Instance.PlayMusic(0);
            level.StartDemo();
            ui.ShowMenu();
        }

        public void ShowLevelSelect()
        {
            Current = State.LevelSelect;
            Time.timeScale = 1f;
            background.SetWorld(0);
            AudioManager.Instance.PlayMusic(0);
            level.StartDemo();
            ui.ShowLevelSelect();
        }

        public void PlayLevel(int index)
        {
            if (index < 0 || index >= LevelDatabase.Count || !SaveSystem.IsUnlocked(index)) return;
            StartLevel(index);
        }

        /// <summary>Dev tools only (AutoPlayCapture): starts any level, ignoring locks.</summary>
        public void PlayLevelForCapture(int index)
        {
            if (index >= 0 && index < LevelDatabase.Count) StartLevel(index);
        }

        void StartLevel(int index)
        {
            LevelIndex = index;
            Current = State.Playing;
            Time.timeScale = 1f;
            int world = LevelDatabase.WorldOf(index);
            background.SetWorld(world);
            AudioManager.Instance.PlayMusic(world);
            level.Load(index);
            ui.ShowHud(index);
        }

        public void Restart()
        {
            PlayLevel(LevelIndex);
        }

        public void NextLevel()
        {
            if (LevelIndex + 1 < LevelDatabase.Count) PlayLevel(LevelIndex + 1);
            else ShowLevelSelect();
        }

        public void Pause()
        {
            if (Current != State.Playing) return;
            Current = State.Paused;
            Time.timeScale = 0f;
            ui.ShowPause();
        }

        public void Resume()
        {
            if (Current != State.Paused) return;
            Current = State.Playing;
            Time.timeScale = 1f;
            ui.HidePause();
        }

        void OnLevelWon(int stars)
        {
            Current = State.Result;
            bool newBest = SaveSystem.SubmitStars(LevelIndex, stars);
            ui.ShowWin(LevelIndex, stars, newBest);
        }

        void OnLevelLost(string reason)
        {
            Current = State.Result;
            ui.ShowLose(reason);
        }

        void OnBack()
        {
            switch (Current)
            {
                case State.Playing: Pause(); break;
                case State.Paused: Resume(); break;
                case State.LevelSelect: ShowMenu(); break;
                case State.Result: ShowLevelSelect(); break;
                case State.Menu: Application.Quit(); break;
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Pause(); // auto-pause when the app goes to the background
        }
    }
}
