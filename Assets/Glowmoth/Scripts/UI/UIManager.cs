using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Glowmoth
{
    /// <summary>
    /// Builds every screen from code on one overlay canvas: Main Menu, Level Select, HUD, Pause and Result
    /// (win/lose). Screens fade with CanvasGroups; text only changes on events, never per frame.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.07f, 0.05f, 0.13f, 1f);
        static readonly Color PanelColor = new Color(0.10f, 0.08f, 0.19f, 0.96f);
        static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color LockedColor = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color DimColor = new Color(0.02f, 0.01f, 0.05f, 0.62f);
        static readonly Color Muted = new Color(1f, 1f, 1f, 0.62f);
        static readonly Color StarOff = new Color(1f, 1f, 1f, 0.16f);

        GameRoot game;
        LevelController level;
        RectTransform root;
        ScreenFader menu, levels, hud, pause, result;
        WorldPalette pal;

        // Menu
        Text menuStars;
        Image menuSound, menuMusic;

        // Level select
        Text levelsStars;
        readonly UIButton[] levelButtons = new UIButton[LevelDatabase.Count];
        readonly Image[] levelLocks = new Image[LevelDatabase.Count];
        readonly Image[] levelStars = new Image[LevelDatabase.Count * 3];

        // HUD
        Text hudTitle, hudLamps, hudFlies;
        Image hudLampIcon, hudFlyIcon;
        CanvasGroup hintGroup;
        Text hintText;
        float hintTime;

        // Pause
        Image pauseSound, pauseMusic;

        // Result
        Text resultTitle, resultInfo;
        GameObject resultStarsRow;
        readonly Image[] resultStars = new Image[3];
        UIButton resultNext;
        Coroutine starRoutine;
        readonly WaitForSecondsRealtime starDelay = new WaitForSecondsRealtime(0.22f);

        public static UIManager Create(GameRoot game, LevelController level)
        {
            var go = new GameObject("UIManager");
            go.transform.SetParent(game.transform, false);
            var ui = go.AddComponent<UIManager>();
            ui.game = game;
            ui.level = level;
            ui.pal = Palettes.Worlds[0];
            ui.Build();
            level.StatsChanged += ui.RefreshHud;
            return ui;
        }

        // ============================================================================ construction

        void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.35f;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            root = UIKit.Stretch(UIKit.Node("SafeArea", canvasGo.transform));
            root.gameObject.AddComponent<SafeArea>();

            // Order matters: later screens draw on top.
            BuildMenu();
            BuildLevelSelect();
            BuildHud();
            BuildPause();
            BuildResult();
        }

        ScreenFader NewScreen(string name)
        {
            RectTransform rt = UIKit.Stretch(UIKit.Node(name, root));
            rt.gameObject.AddComponent<CanvasGroup>();
            var fader = rt.gameObject.AddComponent<ScreenFader>();
            fader.HideInstant();
            return fader;
        }

        static Image Dim(Transform parent)
        {
            Image dim = UIKit.MakeImage(parent, "Dim", SpriteFactory.Pixel, DimColor, false, true); // blocks taps on the game
            UIKit.Stretch(dim.rectTransform);
            return dim;
        }

        static Image Panel(Transform parent, Vector2 size)
        {
            Image panel = UIKit.MakeImage(parent, "Panel", SpriteFactory.Panel, PanelColor, true, true);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return panel;
        }

        void BuildMenu()
        {
            menu = NewScreen("MainMenu");
            Transform t = menu.transform;
            WorldPalette p = Palettes.Worlds[0];

            Text title = UIKit.MakeText(t, "Title", "GLOWMOTH", 150, p.accent);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.76f), Vector2.zero, new Vector2(1040f, 220f));
            Text sub = UIKit.MakeText(t, "Subtitle", "Light the way home.", 54, p.text, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.69f), Vector2.zero, new Vector2(1000f, 90f));

            UIKit.MakeButton(t, "Play", new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(560f, 170f), p.accent,
                game.ShowLevelSelect, "PLAY", null, Ink, 76);

            menuSound = UIKit.MakeButton(t, "Sound", new Vector2(0.5f, 0.245f), new Vector2(-110f, 0f), new Vector2(170f, 170f),
                ButtonColor, ToggleSound, null, SpriteFactory.IconSoundOn).Icon;
            menuMusic = UIKit.MakeButton(t, "Music", new Vector2(0.5f, 0.245f), new Vector2(110f, 0f), new Vector2(170f, 170f),
                ButtonColor, ToggleMusic, null, SpriteFactory.IconMusic).Icon;

            Image star = UIKit.MakeImage(t, "StarIcon", SpriteFactory.Star, p.accent);
            UIKit.Place(star.rectTransform, new Vector2(0.5f, 0.15f), new Vector2(-80f, 0f), new Vector2(64f, 64f));
            menuStars = UIKit.MakeText(t, "Stars", "0 / 60", 52, p.text, TextAnchor.MiddleLeft);
            UIKit.Place(menuStars.rectTransform, new Vector2(0.5f, 0.15f), new Vector2(70f, 0f), new Vector2(220f, 80f));

            Text help = UIKit.MakeText(t, "Help", "Tap to place the lamp. The moth follows the light it can see.", 38, Muted,
                TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Place(help.rectTransform, new Vector2(0.5f, 0.06f), Vector2.zero, new Vector2(960f, 110f));
        }

        void BuildLevelSelect()
        {
            levels = NewScreen("LevelSelect");
            Transform t = levels.transform;
            Image dim = Dim(t);
            dim.color = new Color(0.02f, 0.01f, 0.05f, 0.45f);

            UIButton back = UIKit.MakeButton(t, "Back", new Vector2(0f, 1f), new Vector2(100f, -95f), new Vector2(140f, 140f),
                ButtonColor, game.ShowMenu, null, SpriteFactory.IconArrow);
            back.Icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // arrow points left

            Text title = UIKit.MakeText(t, "Title", "Choose a level", 66, Color.white);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(600f, 100f));

            Image star = UIKit.MakeImage(t, "StarIcon", SpriteFactory.Star, Palettes.Worlds[0].accent);
            UIKit.Place(star.rectTransform, new Vector2(1f, 1f), new Vector2(-215f, -95f), new Vector2(54f, 54f));
            levelsStars = UIKit.MakeText(t, "Stars", "0", 46, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(levelsStars.rectTransform, new Vector2(1f, 1f), new Vector2(-95f, -95f), new Vector2(170f, 80f));

            const float sectionStart = -290f, sectionStep = 370f;
            for (int w = 0; w < LevelDatabase.WorldNames.Length; w++)
            {
                WorldPalette wp = Palettes.Worlds[w];
                Text worldTitle = UIKit.MakeText(t, "World" + w, LevelDatabase.WorldNames[w], 50, wp.accent);
                UIKit.Place(worldTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, sectionStart - w * sectionStep), new Vector2(900f, 70f));
            }

            for (int i = 0; i < LevelDatabase.Count; i++)
            {
                int index = i; // captured by the click handler (allocated once at startup)
                int w = LevelDatabase.WorldOf(i), c = i % LevelDatabase.LevelsPerWorld;
                var pos = new Vector2(-400f + c * 200f, sectionStart - 135f - w * sectionStep);
                UIButton b = UIKit.MakeButton(t, "Level" + (i + 1), new Vector2(0.5f, 1f), pos, new Vector2(172f, 196f), ButtonColor,
                    () => game.PlayLevel(index), UIKit.Numbers[i + 1], null, Color.white, 66);
                b.Label.rectTransform.offsetMin = new Vector2(0f, 46f); // number sits above the stars
                levelButtons[i] = b;

                for (int s = 0; s < 3; s++)
                {
                    Image st = UIKit.MakeImage(b.Background.transform, "Star" + s, SpriteFactory.Star, StarOff);
                    UIKit.Place(st.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-46f + s * 46f, -52f), new Vector2(42f, 42f));
                    levelStars[i * 3 + s] = st;
                }

                Image lockIcon = UIKit.MakeImage(b.Background.transform, "Lock", SpriteFactory.IconLock, new Color(1f, 1f, 1f, 0.45f));
                UIKit.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
                levelLocks[i] = lockIcon;
            }
        }

        void BuildHud()
        {
            hud = NewScreen("HUD");
            Transform t = hud.transform;

            UIKit.MakeButton(t, "Pause", new Vector2(0f, 1f), new Vector2(95f, -90f), new Vector2(130f, 130f), ButtonColor,
                game.Pause, null, SpriteFactory.IconPause);

            hudTitle = UIKit.MakeText(t, "Title", "", 44, Color.white);
            UIKit.Place(hudTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(-40f, -90f), new Vector2(560f, 80f));

            hudLampIcon = UIKit.MakeImage(t, "LampIcon", SpriteFactory.Lantern, Color.white);
            UIKit.Place(hudLampIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-330f, -90f), new Vector2(62f, 62f));
            hudLamps = UIKit.MakeText(t, "Lamps", "0/0", 44, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(hudLamps.rectTransform, new Vector2(1f, 1f), new Vector2(-235f, -90f), new Vector2(120f, 70f));

            hudFlyIcon = UIKit.MakeImage(t, "FlyIcon", SpriteFactory.Glow, Color.white);
            UIKit.Place(hudFlyIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-140f, -90f), new Vector2(70f, 70f));
            hudFlies = UIKit.MakeText(t, "Flies", "0/0", 44, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(hudFlies.rectTransform, new Vector2(1f, 1f), new Vector2(-55f, -90f), new Vector2(100f, 70f));

            RectTransform hint = UIKit.Node("Hint", t);
            UIKit.Place(hint, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(980f, 220f));
            hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = false;
            hintGroup.interactable = false;
            hintText = UIKit.MakeText(hint, "Text", "", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Stretch(hintText.rectTransform);
        }

        void BuildPause()
        {
            pause = NewScreen("Pause");
            Transform t = pause.transform;
            Dim(t);
            Transform panel = Panel(t, new Vector2(760f, 1000f)).transform;

            Text title = UIKit.MakeText(panel, "Title", "Paused", 84, Color.white);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 370f), new Vector2(700f, 120f));

            UIKit.MakeButton(panel, "Resume", new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(520f, 140f),
                Palettes.Worlds[0].accent, game.Resume, "Resume", null, Ink, 58);
            UIKit.MakeButton(panel, "Restart", new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(520f, 140f),
                ButtonColor, game.Restart, "Restart", null, Color.white, 58);
            UIKit.MakeButton(panel, "Levels", new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(520f, 140f),
                ButtonColor, game.ShowLevelSelect, "Levels", null, Color.white, 58);

            pauseSound = UIKit.MakeButton(panel, "Sound", new Vector2(0.5f, 0.5f), new Vector2(-90f, -330f), new Vector2(140f, 140f),
                ButtonColor, ToggleSound, null, SpriteFactory.IconSoundOn).Icon;
            pauseMusic = UIKit.MakeButton(panel, "Music", new Vector2(0.5f, 0.5f), new Vector2(90f, -330f), new Vector2(140f, 140f),
                ButtonColor, ToggleMusic, null, SpriteFactory.IconMusic).Icon;
        }

        void BuildResult()
        {
            result = NewScreen("Result");
            Transform t = result.transform;
            Dim(t);
            Transform panel = Panel(t, new Vector2(820f, 920f)).transform;

            resultTitle = UIKit.MakeText(panel, "Title", "", 80, Color.white);
            UIKit.Place(resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(760f, 120f));

            RectTransform row = UIKit.Node("Stars", panel);
            UIKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(600f, 180f));
            resultStarsRow = row.gameObject;
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 175f, y = i == 1 ? 18f : 0f, size = i == 1 ? 170f : 140f;
                Image outline = UIKit.MakeImage(row, "Outline" + i, SpriteFactory.StarOutline, StarOff);
                UIKit.Place(outline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
                Image fill = UIKit.MakeImage(row, "Star" + i, SpriteFactory.Star, Color.white);
                UIKit.Place(fill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
                resultStars[i] = fill;
            }

            resultInfo = UIKit.MakeText(panel, "Info", "", 42, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Place(resultInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(740f, 200f));

            UIKit.MakeButton(panel, "Levels", new Vector2(0.5f, 0.5f), new Vector2(-230f, -300f), new Vector2(160f, 160f),
                ButtonColor, game.ShowLevelSelect, null, SpriteFactory.IconGrid);
            UIKit.MakeButton(panel, "Retry", new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(160f, 160f),
                ButtonColor, game.Restart, null, SpriteFactory.IconRestart);
            resultNext = UIKit.MakeButton(panel, "Next", new Vector2(0.5f, 0.5f), new Vector2(230f, -300f), new Vector2(160f, 160f),
                Palettes.Worlds[0].accent, game.NextLevel, null, SpriteFactory.IconArrow, Ink);
        }

        // ============================================================================ screen flow

        void HideAll()
        {
            menu.Hide();
            levels.Hide();
            hud.Hide();
            pause.Hide();
            result.Hide();
        }

        public void ShowMenu()
        {
            HideAll();
            menu.Show();
            menuStars.text = UIKit.Numbers[SaveSystem.TotalStars()] + " / " + UIKit.Numbers[LevelDatabase.Count * 3];
            RefreshToggles();
        }

        public void ShowLevelSelect()
        {
            HideAll();
            levels.Show();
            levelsStars.text = UIKit.Numbers[SaveSystem.TotalStars()];
            for (int i = 0; i < LevelDatabase.Count; i++)
            {
                bool unlocked = SaveSystem.IsUnlocked(i);
                int stars = SaveSystem.GetStars(i);
                WorldPalette wp = Palettes.ForLevel(i);
                UIButton b = levelButtons[i];
                b.Button.interactable = unlocked;
                b.Background.color = unlocked ? WorldPalette.WithAlpha(wp.wall, 0.9f) : LockedColor;
                b.Label.enabled = unlocked;
                levelLocks[i].enabled = !unlocked;
                for (int s = 0; s < 3; s++)
                {
                    Image st = levelStars[i * 3 + s];
                    st.enabled = unlocked;
                    st.color = s < stars ? wp.accent : StarOff;
                }
            }
        }

        public void ShowHud(int index)
        {
            pal = Palettes.ForLevel(index);
            LevelData d = LevelDatabase.Get(index);
            HideAll();
            hud.Show();
            hudTitle.text = UIKit.Numbers[index + 1] + ". " + d.Name;
            hudTitle.color = pal.text;
            hudLampIcon.color = pal.lamp;
            hudFlyIcon.color = pal.firefly;
            hintText.text = d.Hint;
            hintText.color = pal.text;
            hintTime = string.IsNullOrEmpty(d.Hint) ? 0f : 7f;
            hintGroup.alpha = hintTime > 0f ? 1f : 0f;
            RefreshHud();
        }

        /// <summary>Called by LevelController.StatsChanged (only when a lamp is placed or a firefly collected).</summary>
        void RefreshHud()
        {
            if (level == null) return;
            hudLamps.text = UIKit.Numbers[Mathf.Min(99, level.Taps)] + "/" + UIKit.Numbers[level.Par];
            hudLamps.color = level.Taps <= level.Par ? Color.white : Muted;
            bool hasFlies = level.FireflyTotal > 0;
            hudFlyIcon.enabled = hasFlies;
            hudFlies.enabled = hasFlies;
            if (hasFlies) hudFlies.text = UIKit.Numbers[level.FirefliesCollected] + "/" + UIKit.Numbers[level.FireflyTotal];
            if (level.Taps >= 2 && hintTime > 1f) hintTime = 1f; // fade the tip once the player gets going
        }

        public void ShowPause()
        {
            RefreshToggles();
            pause.Show();
        }

        public void HidePause()
        {
            pause.Hide();
        }

        public void ShowWin(int index, int stars, bool newBest)
        {
            pal = Palettes.ForLevel(index);
            LevelData d = LevelDatabase.Get(index);
            bool last = index == LevelDatabase.Count - 1;

            resultTitle.text = last ? "Dawn has come!" : "Home safe!";
            resultTitle.color = pal.accent;
            string info = "Lamps used: " + level.Taps + "   (par " + d.Par + ")";
            if (level.FireflyTotal > 0) info += "\nFireflies: " + level.FirefliesCollected + " / " + level.FireflyTotal;
            if (newBest) info += "\nNew best!";
            if (last) info += "\nYou guided the moth through every world.";
            resultInfo.text = info;

            resultStarsRow.SetActive(true);
            resultNext.Button.gameObject.SetActive(!last);
            resultNext.Background.color = pal.accent;
            result.Show();

            if (starRoutine != null) StopCoroutine(starRoutine);
            starRoutine = StartCoroutine(PopStars(stars));
        }

        public void ShowLose(string reason)
        {
            resultTitle.text = "Oops!";
            resultTitle.color = pal.hazard;
            resultInfo.text = reason + "\nTry a different path.";
            resultStarsRow.SetActive(false);
            resultNext.Button.gameObject.SetActive(false);
            result.Show();
        }

        IEnumerator PopStars(int count)
        {
            for (int i = 0; i < 3; i++)
            {
                resultStars[i].transform.localScale = Vector3.zero;
                resultStars[i].color = pal.accent;
            }
            yield return starDelay;
            for (int i = 0; i < count; i++)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxSynth.Pop, 0.6f, 0f);
                float t = 0f;
                while (t < 1f)
                {
                    t = Mathf.Min(1f, t + Time.unscaledDeltaTime * 4f);
                    float s = UIKit.EaseOutBack(t);
                    resultStars[i].transform.localScale = new Vector3(s, s, 1f);
                    yield return null;
                }
                yield return starDelay;
            }
            starRoutine = null;
        }

        // ============================================================================ settings

        void ToggleSound()
        {
            SaveSystem.Sound = !SaveSystem.Sound;
            RefreshToggles();
        }

        void ToggleMusic()
        {
            SaveSystem.Music = !SaveSystem.Music;
            if (AudioManager.Instance != null) AudioManager.Instance.RefreshMusic();
            RefreshToggles();
        }

        void RefreshToggles()
        {
            Sprite s = SaveSystem.Sound ? SpriteFactory.IconSoundOn : SpriteFactory.IconSoundOff;
            menuSound.sprite = s;
            pauseSound.sprite = s;
            Color m = SaveSystem.Music ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            menuMusic.color = m;
            pauseMusic.color = m;
        }

        void Update()
        {
            // Only the gameplay tip animates here; it uses scaled time so it pauses with the game.
            if (hintTime <= 0f) return;
            hintTime -= Time.deltaTime;
            hintGroup.alpha = Mathf.Clamp01(hintTime);
        }
    }
}
