// SunPet Windows 版 —— 孙宇晨「太对了」桌宠
// 行为对齐 macOS 原版 (github.com/Ganzhe2028/SunPet)：
//   点头像随机说一句，28 句一轮内不重复；连点替换当前句不叠播；
//   拖动换位置并记住；说话时摇头+上下蹦；字幕气泡；右键全套菜单。
// 仅用系统自带 .NET Framework 4.x WPF，无任何外部依赖，不联网。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SunPetWin
{
    // clips.json 里的一条
    public class Clip
    {
        public int id { get; set; }
        public string label { get; set; }
        public string file { get; set; }
        public double duration { get; set; }
    }

    public class PetWindow : Window
    {
        // ---- 状态（对应原版 AppDelegate） ----
        string petName = "太对了", petHeader = "孙宇晨 · 太对了";
        List<Clip> clips = new List<Clip>();
        List<int> bag = new List<int>();
        int lastID = -1;
        bool randomMode = true;
        int pinnedID = -1;
        double volume = 0.65;
        double size = 180;                     // 头像边长，窗口 = size x (size+48)
        double petX = double.NaN, petY = double.NaN;
        Random rng = new Random();
        MediaPlayer player;
        bool playedSinceOpen;

        // ---- 界面 ----
        Image face;
        Border caption;
        TextBlock capText;
        RotateTransform rot = new RotateTransform();
        TranslateTransform bob = new TranslateTransform();
        DispatcherTimer animTimer, capTimer;

        // 说话动画相位（原版：30fps，每帧 +0.38）
        double phase;

        // 拖动判定（原版：位移 > 4px 算拖动，否则算点击）
        bool dragged;
        Point downScreen;
        double downLeft, downTop;

        string appDir, clipsDir, facePath, jsonPath, settingsPath;

        public PetWindow(string baseDir, string name, string header)
        {
            petName = name; petHeader = header;
            appDir = baseDir;
            clipsDir = Path.Combine(appDir, "Assets", "Clips");
            facePath = Path.Combine(appDir, "Assets", "face.png");
            jsonPath = Path.Combine(appDir, "Assets", "clips.json");
            settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DesktopPets", petName, "settings.ini");

            LoadClips();
            LoadSettings();

            // ---- 窗口：无边框、透明、置顶、不占任务栏、不抢焦点 ----
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Title = petName;
            Width = size; Height = size + 48;
            ToolTip = "点击随机播放一句 · 拖动移动 · 右键设置";

            BuildContent();
            ResetPositionIfSaved();

            MouseLeftButtonDown += OnLeftDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnLeftUp;
            MouseRightButtonUp += OnRightUp;
            Closing += (s, e) => SaveAll();

            SourceInitialized += (s, e) =>
            {
                // 不抢焦点：点击窗口不激活，键盘焦点留在用户原来的窗口
                HwndSource src = (HwndSource)PresentationSource.FromVisual(this);
                src.AddHook(WndProc);
                // 从 Alt+Tab 列表里隐身
                IntPtr h = new WindowInteropHelper(this).Handle;
                int style = GetWindowLong(h, GWL_EXSTYLE);
                SetWindowLong(h, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);
            };

            animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            animTimer.Tick += delegate { phase += 0.38; double s = Math.Sin(phase);
                rot.Angle = s * 3.15; bob.Y = -Math.Abs(s) * 3; };
            capTimer = new DispatcherTimer();
            capTimer.Tick += delegate { capTimer.Stop(); caption.Visibility = Visibility.Collapsed; };

            ShowCaption("点我一下 · 右键设置", 5);
        }

        void LoadClips()
        {
            var ser = new JavaScriptSerializer();
            clips = ser.Deserialize<List<Clip>>(File.ReadAllText(jsonPath, Encoding.UTF8));
            if (clips == null || clips.Count == 0)
                throw new InvalidOperationException("clips.json 是空的");
        }

        // ---- 布局：上字幕条(38px)，下面圆脸，四边 12px ----
        void BuildContent()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            capText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            caption = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(184, 0, 0, 0)), // 72% 黑
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                Child = capText,
                Margin = new Thickness(4, 4, 4, 0),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(caption, 0);
            grid.Children.Add(caption);

            face = new Image
            {
                Source = new BitmapImage(new Uri(facePath)),
                Stretch = Stretch.Uniform,
                Margin = new Thickness(12, 2, 12, 12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup { Children = { rot, bob } }
            };
            Grid.SetRow(face, 1);
            grid.Children.Add(face);

            Content = grid;
        }

        // ---- 播放 ----
        void PlayNext()
        {
            if (!randomMode && pinnedID > 0)
            {
                Clip pinned = clips.FirstOrDefault(c => c.id == pinnedID);
                if (pinned != null) { Play(pinned); return; }
                randomMode = true; // 固定句失效自动退回随机，原版同样处理
            }
            PlayRandom();
        }

        void PlayRandom()
        {
            if (bag.Count == 0)
            {
                bag = Enumerable.Range(0, clips.Count).ToList();
                for (int i = bag.Count - 1; i > 0; i--)   // Fisher-Yates 洗牌
                {
                    int j = rng.Next(i + 1);
                    int t = bag[i]; bag[i] = bag[j]; bag[j] = t;
                }
                // 新一轮洗完后，最后一句恰好是上一句说的 → 换到开头，避免连着同一句
                if (bag.Count > 1 && clips[bag[bag.Count - 1]].id == lastID)
                { int t = bag[0]; bag[0] = bag[bag.Count - 1]; bag[bag.Count - 1] = t; }
            }
            if (bag.Count == 0) return;
            int idx = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            Play(clips[idx]);
        }

        void Play(Clip clip)
        {
            if (player != null)
            {
                player.Stop(); player.Close(); player = null;
            }
            MediaPlayer p = new MediaPlayer();
            p.Volume = volume;
            playedSinceOpen = false;
            p.MediaOpened += delegate { if (!playedSinceOpen) { playedSinceOpen = true; p.Play(); } };
            p.MediaEnded += delegate { Dispatcher.BeginInvoke((Action)delegate { StopAnim(); }); };
            p.MediaFailed += delegate(object s, ExceptionEventArgs e)
            {
                Dispatcher.BeginInvoke((Action)delegate
                {
                    StopAnim();
                    ShowCaption("播放失败：" + (e.ErrorException != null ? e.ErrorException.Message : "未知错误"), 5);
                });
            };
            player = p;
            p.Open(new Uri(Path.Combine(clipsDir, clip.file)));
            p.Play();  // 通常立刻就能排队播放；万一没排上，MediaOpened 里会补一次
            lastID = clip.id;
            StartAnim();
            ShowCaption(clip.label, clip.duration + 0.5);
        }

        void StartAnim() { phase = 0; animTimer.Start(); }
        void StopAnim() { animTimer.Stop(); phase = 0; rot.Angle = 0; bob.Y = 0; }

        void ShowCaption(string text, double seconds)
        {
            capText.Text = text;
            caption.Visibility = Visibility.Visible;
            capTimer.Stop();
            capTimer.Interval = TimeSpan.FromSeconds(seconds);
            capTimer.Start();
        }

        // ---- 鼠标：左键按住拖动 / 松开播放，右键菜单 ----
        // 位移全部用 DIP 坐标算，高 DPI 缩放下拖动不跳
        void OnLeftDown(object sender, MouseButtonEventArgs e)
        {
            dragged = false;
            downScreen = e.GetPosition(this);
            downLeft = Left; downTop = Top;
            Mouse.Capture(this);
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            Point cur = e.GetPosition(this);
            double dx = cur.X - downScreen.X, dy = cur.Y - downScreen.Y;
            if (!dragged && Math.Sqrt(dx * dx + dy * dy) > 4) dragged = true;
            if (dragged) { Left = downLeft + dx; Top = downTop + dy; }
        }

        void OnLeftUp(object sender, MouseButtonEventArgs e)
        {
            Mouse.Capture(null);
            if (dragged) SaveAll(); else PlayNext();
        }

        void OnRightUp(object sender, MouseButtonEventArgs e)
        {
            if (dragged) return;
            ContextMenu = BuildMenu();
            ContextMenu.IsOpen = true;
        }

        // ---- 右键菜单（条目对齐原版 makeMenu）----
        MenuItem Item(string header, RoutedEventHandler onClick, bool checkmark)
        {
            var mi = new MenuItem { Header = header, IsChecked = checkmark };
            if (onClick != null) mi.Click += onClick;
            return mi;
        }

        ContextMenu BuildMenu()
        {
            var m = new ContextMenu();
            m.Items.Add(new MenuItem { Header = petHeader + "（" + clips.Count + "句）", IsEnabled = false });

            var mode = new MenuItem { Header = "播放方式" };
            Clip pinned = clips.FirstOrDefault(c => c.id == pinnedID);
            string fixTitle = "固定播一句";
            if (!randomMode && pinned != null)
                fixTitle = string.Format("固定播一句（{0:00} {1}）", pinned.id, pinned.label);
            mode.Items.Add(Item("随机一句（一轮不重复）",
                delegate { randomMode = true; pinnedID = -1; SaveAll(); }, randomMode));
            mode.Items.Add(Item(fixTitle, delegate { randomMode = false; SaveAll(); }, !randomMode));
            m.Items.Add(mode);

            var choose = new MenuItem { Header = "挑一句" };
            foreach (Clip c in clips)
            {
                int id = c.id;
                MenuItem mi = new MenuItem
                {
                    Header = string.Format("{0:00}  {1}", c.id, c.label),
                    IsChecked = (!randomMode && pinnedID == c.id)
                };
                mi.Click += delegate
                {
                    randomMode = false; pinnedID = id; SaveAll();
                    Clip pick = clips.FirstOrDefault(x => x.id == id);
                    if (pick != null) Play(pick);   // 原版：选完立刻播新固定句
                };
                choose.Items.Add(mi);
            }
            m.Items.Add(choose);

            var vol = new MenuItem { Header = "音量" };
            int[] vols = new int[] { 0, 25, 50, 65, 80, 100 };
            foreach (int n in vols)
            {
                int v = n;
                vol.Items.Add(Item(n == 0 ? "静音" : n + "%", delegate
                {
                    volume = v / 100.0;
                    if (player != null) player.Volume = volume;
                    SaveAll();
                }, Math.Abs(volume - n / 100.0) < 0.01));
            }
            m.Items.Add(vol);

            var sz = new MenuItem { Header = "头像大小" };
            var sizes = new Dictionary<string, double>();
            sizes["小"] = 130; sizes["中"] = 180; sizes["大"] = 240;
            foreach (KeyValuePair<string, double> kv in sizes)
            {
                double v = kv.Value;
                sz.Items.Add(Item(kv.Key, delegate
                {
                    size = v;
                    Width = size; Height = size + 48;
                    SaveAll();
                }, Math.Abs(size - kv.Value) < 0.5));
            }
            m.Items.Add(sz);

            m.Items.Add(Item("移回屏幕右下角", delegate { ResetPosition(); SaveAll(); }, false));
            m.Items.Add(new Separator());
            m.Items.Add(Item("退出桌宠", delegate { Close(); }, false));
            return m;
        }

        void ResetPosition()
        {
            Rect wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 32;
            Top = wa.Bottom - Height - 24;
        }

        void ResetPositionIfSaved()
        {
            ResetPosition();
            if (!double.IsNaN(petX) && !double.IsNaN(petY))
            {
                Rect vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                                   SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
                // 原版判据：至少 80px 落在可见屏幕内才恢复，否则回右下角
                if (petX >= vs.X - 80 && petY >= vs.Y - 80 &&
                    petX <= vs.Right - 80 && petY <= vs.Bottom - 80)
                { Left = petX; Top = petY; }
            }
        }

        // ---- 设置持久化：AppData\SunPet\settings.ini ----
        void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsPath)) return;
                var map = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(settingsPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                string v;
                if (map.TryGetValue("volume", out v)) volume = Math.Min(Math.Max(double.Parse(v), 0), 1);
                if (map.TryGetValue("size", out v)) size = Math.Min(Math.Max(double.Parse(v), 120), 260);
                if (map.TryGetValue("randomMode", out v)) bool.TryParse(v, out randomMode);
                if (map.TryGetValue("pinnedID", out v)) int.TryParse(v, out pinnedID);
                if (map.TryGetValue("petX", out v)) double.TryParse(v, out petX);
                if (map.TryGetValue("petY", out v)) double.TryParse(v, out petY);
                if (!randomMode && clips.FirstOrDefault(c => c.id == pinnedID) == null)
                    randomMode = true;   // 固定句失效自动退回随机
            }
            catch { }
        }

        void SaveAll()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                var sb = new StringBuilder();
                sb.AppendLine("volume=" + volume.ToString("0.##"));
                sb.AppendLine("size=" + size.ToString("0"));
                sb.AppendLine("randomMode=" + randomMode);
                sb.AppendLine("pinnedID=" + pinnedID);
                sb.AppendLine("petX=" + Left.ToString("0.#"));
                sb.AppendLine("petY=" + Top.ToString("0.#"));
                File.WriteAllText(settingsPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        // ---- Win32 ----
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WM_MOUSEACTIVATE = 0x0021;

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEACTIVATE) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
            return IntPtr.Zero;
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            // 通用化：同目录 pet.json 指定 { "name": "...", "header": "..." }，
            // 缺省按「太对了」跑。换角色 = 换 Assets + pet.json，代码不动。
            string petName = "太对了", petHeader = "孙宇晨 · 太对了";
            string petJson = Path.Combine(appDir, "pet.json");
            if (File.Exists(petJson))
            {
                try
                {
                    var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                        File.ReadAllText(petJson, Encoding.UTF8));
                    object v;
                    if (d != null && d.TryGetValue("name", out v) && v != null) petName = v.ToString();
                    if (d != null && d.TryGetValue("header", out v) && v != null) petHeader = v.ToString();
                }
                catch { }
            }
            bool created;
            Mutex mutex = new Mutex(true, "DesktopPet_" + petName + "_SingleInstance", out created);
            if (!created)
            {
                MessageBox.Show("已经有一只「" + petName + "」在桌面上了。", petName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                if (!File.Exists(Path.Combine(appDir, "Assets", "face.png")) ||
                    !File.Exists(Path.Combine(appDir, "Assets", "clips.json")))
                    throw new InvalidOperationException("桌宠资源无法读取：Assets 目录不完整");
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                var win = new PetWindow(appDir, petName, petHeader);
                GC.KeepAlive(mutex);
                app.Run(win);
            }
            catch (Exception ex)
            {
                MessageBox.Show("桌宠资源无法读取：" + ex.Message, petName,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
