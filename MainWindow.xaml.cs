using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Animation;



namespace Pomodoro {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        readonly DispatcherTimer timer = new();
        readonly DispatcherTimer mouseLeftHoldTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        System.Timers.Timer pomodoroTimer = new();
        readonly Stopwatch stopwatch = new();
        TimeSpan originalTime = TimeSpan.Zero;
        TimeSpan remainingTime;
        bool mouseLeftHeld = false;
        bool showPlay = true;
        bool inSession = false;        
        short heartFrames = 0;
        short frame = 0;
        short ticksPerFrame = 3;
        short tickCount = 0;
        short maxFrames = 8;
        short minimumCycles = 0; //Minimum number of cycles to complete before changing state
        double transformX = 0;
        double transformY = 0;

        double resetIntervals, intervals, focus, shortBreak, longBreak;
        string phase = "Focus";



        (double, double) movementDistance = (0, 0);
        readonly List<short> poles = [1, -1];
        readonly static float scaler = 0.8f;


        //Dictionary of states to (maxFrames, ticksPerFrame, (width, height))
        private readonly Dictionary<string, (short, short, (short, short))> states = new() {
                                                                                { "Alarm", (2, 10, (115, 110)) }, { "Asleep", (6, 12, (75, 70)) }, { "Catch", (8, 3, (100, 170)) },
                                                                                { "Draw", (4, 10, (90, 140)) }, { "Drowsy", (7, 10, (75, 70)) }, { "Entry", (8, 3, (185, 200)) },
                                                                                { "Exit", (8, 3, (185, 200)) }, { "Idle", (6, 8, (75, 60)) }, { "Run", (6, 4, (100, 75)) } };
        string currentState = "Entry";

        readonly Random rand = new();
        readonly double workWidth = System.Windows.SystemParameters.WorkArea.Width;
        readonly double workHeight = System.Windows.SystemParameters.WorkArea.Height;
        public MainWindow() {
            InitializeComponent();
            this.Width = workWidth;
            this.Height = workHeight;
            Canvas.SetLeft(rabbitRect, workWidth / 8);
            Canvas.SetTop(rabbitRect, workHeight / 8);
            mouseLeftHoldTimer.Tick += MouseLeftHoldTimer_Tick;
            // Track mouse movements so the rabbit can follow while the left button is held
            canvas.MouseMove += Canvas_MouseMove;
            // Handle unexpected loss of mouse capture so the rabbit doesn't stay attached
            rabbitRect.LostMouseCapture += RabbitRect_LostMouseCapture;
            timer.Tick += UpdateFrames;
            timer.Interval = TimeSpan.FromMilliseconds(20);
            timer.Start();
        }

        private void UpdateFrames(object? sender, EventArgs e) {
            tickCount++;
            if (currentState == "Run") {
                MoveRabbit(movementDistance.Item1 / (ticksPerFrame * maxFrames), movementDistance.Item2 / (ticksPerFrame * maxFrames));
            }
            if (tickCount >= ticksPerFrame) {
                tickCount = 0;
                frame++;
                heartFrames--;
                if (frame >= maxFrames) {
                    frame = 0;
                    if (minimumCycles > 0) {
                        minimumCycles--;
                    } else if (currentState == "Alarm") {
                        minimumCycles++; // Prevent changing state at the end of the cycle so the alarm animation can loop until dismissed
                    } else {
                        ChangeState();
                    }
                }
                rabbitRect.Source = new BitmapImage(new Uri(GetUri()));
            }
            if (heartFrames <= 0) {
                heart.Opacity = 0;
            }
        }

        private void ChangeState() {
            if (currentState == "Entry") {
                Canvas.SetTop(rabbitRect, Canvas.GetTop(rabbitRect) + 130);
                Canvas.SetLeft(rabbitRect, Canvas.GetLeft(rabbitRect) + 90);
            }
            if (currentState == "Drowsy") {
                currentState = "Asleep";
            } else {
                int state = rand.Next(101);
                if (state < 10) {
                    currentState = "Drowsy";
                } else if (10 < state && state < 20) {
                    currentState = "Run";
                } else if (state > 20) {
                    currentState = "Idle";
                }
            }
            SetStateInfo();
        }

        private void ChangeState(string overloadState) {
            currentState = overloadState;
            SetStateInfo();
        }
        private string GetUri() {
            return $"pack://application:,,,/Assets/Sprites/{currentState}/{currentState}{frame}.png";
        }

        private void SetStateInfo() {
            tickCount = 0;
            frame = 0;
            maxFrames = states[currentState].Item1;
            ticksPerFrame = states[currentState].Item2;
            rabbitRect.Width = states[currentState].Item3.Item1;
            rabbitRect.Height = states[currentState].Item3.Item2;
            rabbitRect.Source = new BitmapImage(new Uri(GetUri()));
            if (currentState == "Run") {
                movementDistance.Item1 = rand.Next(10, 51) * poles[rand.Next(0, 2)];
                movementDistance.Item2 = rand.Next(10, 51) * poles[rand.Next(0, 2)];
            }
            if (currentState == "Asleep") {
                minimumCycles = 10;
            } else {
                minimumCycles = 1;
            }
        }

        private void MoveRabbit(double x, double y) {
            double currentX = Canvas.GetLeft(rabbitRect) + transformX;
            double currentY = Canvas.GetTop(rabbitRect) + transformY;
            bool notMovingX = true;
            bool notMovingY = true;
            if (currentX + x >= (rabbitRect.Width / 2) && currentX + x <= workWidth - rabbitRect.Width) {
                if (x < 0) {
                    if (rabbitScale.ScaleX < 0) {
                        rabbitScale.ScaleX *= -1;
                    }
                } else {
                    if (rabbitScale.ScaleX > 0) {
                        rabbitScale.ScaleX *= -1;
                    }
                }
                transformX += x;
                rabbitTransform.X += x;
                notMovingX = false;
            }
            if (currentY + y >= (rabbitRect.Height / 2) && currentY + y <= workHeight - rabbitRect.Height) {
                transformY += y;
                rabbitTransform.Y += y;
                notMovingY = false;
            }
            if (notMovingX && notMovingY) {
                ChangeState("Idle");
            }

        }

        private void MouseLeftHoldTimer_Tick(object? sender, EventArgs e) {
            mouseLeftHoldTimer.Stop();
            // If the left mouse button is no longer pressed (user released before the hold timer fired),
            // abort starting the drag so the rabbit doesn't become attached.
            if (Mouse.LeftButton != MouseButtonState.Pressed) {
                mouseLeftHeld = false;
                return;
            }

            mouseLeftHeld = true;
            timer.Stop();

            // Get mouse position relative to the canvas (same coordinate space as the rabbit)
            Point mousePosition = Mouse.GetPosition(canvas);
            transformX = mousePosition.X - Canvas.GetLeft(rabbitRect) - (rabbitRect.Width / 2);
            transformY = mousePosition.Y - Canvas.GetTop(rabbitRect) - (rabbitRect.Height / 2);

            // Immediately apply the calculated translation so the rabbit follows the mouse
            if (rabbitScale.ScaleX < 0) {
                rabbitScale.ScaleX *= -1; // Flip the rabbit to face right if it was facing left
            }

            rabbitTransform.X = transformX;
            rabbitTransform.Y = transformY;


            rabbitRect.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/rabbitHold.png"));
            rabbitRect.Width = 70;
            rabbitRect.Height = 100;
            // Capture the mouse so we continue receiving move/up events while dragging
            Mouse.Capture(rabbitRect);

        }

        private void Canvas_MouseMove(object? sender, MouseEventArgs e) {
            if (!mouseLeftHeld) return;

            // Update the rabbit position continuously while the left mouse button is held
            Point mousePosition = e.GetPosition(canvas);
            transformX = mousePosition.X - Canvas.GetLeft(rabbitRect) - (rabbitRect.Width / 2);
            transformY = mousePosition.Y - Canvas.GetTop(rabbitRect) - (rabbitRect.Height / 2);
            rabbitTransform.X = transformX;
            rabbitTransform.Y = transformY;
        }

        private void RabbitRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            mouseLeftHeld = false;
            mouseLeftHoldTimer.Start();
        }
        private void RabbitRect_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            mouseLeftHoldTimer.Stop();
            Mouse.Capture(null);

            if (!mouseLeftHeld) {
                double currentX;
                if (rabbitScale.ScaleX == -1) {
                    currentX = Canvas.GetLeft(rabbitRect) + transformX;
                } else {
                    currentX = Canvas.GetLeft(rabbitRect) + transformX + (rabbitRect.Width / 2);
                }
                double currentY = Canvas.GetTop(rabbitRect) + transformY;
                heartTransform.X = currentX - (heart.Width / 2);
                heartTransform.Y = currentY - (heart.Height / 2);
                heart.Opacity = 1;
                heartFrames = 5;
            } else {
                ChangeState("Idle");
                timer.Start();
            }
            mouseLeftHeld = false;
        }

        private void RabbitRect_LostMouseCapture(object? sender, MouseEventArgs e) {
            // If we lost capture while dragging, end the drag gracefully
            if (!mouseLeftHeld) return;
            mouseLeftHeld = false;
            Mouse.Capture(null);
            ChangeState(currentState);
            timer.Start();
        }

        private void RabbitRect_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            double currentX;
            if (rabbitScale.ScaleX == -1) {
                currentX = Canvas.GetLeft(rabbitRect) + transformX;
            } else {
                currentX = Canvas.GetLeft(rabbitRect) + transformX + rabbitRect.Width + 10;
            }
            double currentY = Canvas.GetTop(rabbitRect) + transformY;
            optionsGrid.Visibility = Visibility.Visible;
            if (currentX + optionsGrid.Width > workWidth) {
                currentX = workWidth - optionsGrid.Width - 10;
            }
            if (currentY + optionsGrid.Height > workHeight) {
                currentY = workHeight - optionsGrid.Height - 10;
            }
            optionsTransform.X = currentX;
            optionsTransform.Y = currentY;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (optionsGrid.Visibility == Visibility.Visible) {
                if (!optionsGrid.IsMouseOver) {
                    optionsGrid.Visibility = Visibility.Collapsed;
                }
            }
            if (alarmSettings.Visibility == Visibility.Visible) {
                if (!alarmSettings.IsMouseOver) {
                    alarmSettings.Visibility = Visibility.Collapsed;
                }
                ChangeState();
                rabbitTransform.X += 30;
            }
        }
        private void NumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            // Check if the input text is a whole number (0-9)
            Regex regex = FilterOutNonDigits();
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Minus_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            rabbitScale.ScaleX *= scaler;
            rabbitScale.ScaleY *= scaler;
        }

        private void Plus_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            rabbitScale.ScaleX /= scaler;
            rabbitScale.ScaleY /= scaler;
        }

        private void Alarm_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            optionsGrid.Visibility = Visibility.Collapsed;
            alarmSettings.Visibility = Visibility.Visible;
            ChangeState("Alarm");
            rabbitTransform.X -= 30;
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex FilterOutNonDigits();

        private void PlayButtonImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            showPlay = !showPlay;
            if (showPlay) {
                playButtonImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/playArrow.png"));
                if (inSession) {
                    pomodoroTimer.Stop();
                    stopwatch.Stop();
                    remainingTime = originalTime - stopwatch.Elapsed;
                    Debug.WriteLine($"Pausing Pomodoro Timer with {remainingTime.TotalSeconds} seconds remaining in {phase} phase.");
                }
            } else {
                playButtonImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/pause.png"));
                // Start timer
                if (!inSession) {
                    if (!double.TryParse(pomodoroIntervals.Text, out intervals)) { intervals = 4; }
                    if (!double.TryParse(pomodoroIntervals.Text, out resetIntervals)) { resetIntervals = 4; }
                    if (!double.TryParse(focusTime.Text, out focus)) { focus = 25; }
                    if (!double.TryParse(shortBreakTime.Text, out shortBreak)) { shortBreak = 5; }
                    if (!double.TryParse(longBreakTime.Text, out longBreak)) { longBreak = 25; }
                    StartPomodoroTimer();
                } else {
                    switch (phase) {
                        case "Focus":
                            pomodoroTimer = new System.Timers.Timer(remainingTime.TotalMilliseconds) { AutoReset = false };
                            originalTime = TimeSpan.FromMilliseconds(remainingTime.TotalMilliseconds);
                            pomodoroTimer.Elapsed += PomodoroTimer_Elapsed;
                            pomodoroTimer.Start();
                            stopwatch.Restart();
                            break;
                        case "Short Break":
                            pomodoroTimer = new System.Timers.Timer(remainingTime.TotalMilliseconds) { AutoReset = false };
                            originalTime = TimeSpan.FromMilliseconds(remainingTime.TotalMilliseconds);
                            pomodoroTimer.Elapsed += StartPomodoroTimer;
                            pomodoroTimer.Start();
                            stopwatch.Restart();
                            break;
                        case "Long Break":
                            pomodoroTimer = new System.Timers.Timer(remainingTime.TotalMilliseconds) { AutoReset = false };
                            originalTime = TimeSpan.FromMilliseconds(remainingTime.TotalMilliseconds);
                            pomodoroTimer.Elapsed += RestartPomodoroTimer;
                            pomodoroTimer.Start();
                            stopwatch.Restart();
                            break;
                        }
                    Debug.WriteLine($"Resuming Pomodoro Timer for {remainingTime.TotalSeconds} seconds in {phase} phase.");
                }
                alarmSettings.Visibility = Visibility.Collapsed;
            }
        }




        private void ReplayButtonImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            pomodoroTimer?.Stop();
            stopwatch.Stop();
            originalTime = TimeSpan.Zero;
            pomodoroIntervals.Text = "4";
            focusTime.Text = "25";
            shortBreakTime.Text = "5";
            longBreakTime.Text = "25";
            inSession = false;
            showPlay = true;
            playButtonImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/playArrow.png"));
            Debug.WriteLine("Resetting Pomodoro Timer to default values.");
        }
        private void PomodoroTimer_Elapsed(object? sender, ElapsedEventArgs e) {
            intervals--;
            if (intervals > 0) {
                Debug.WriteLine($"Pomodoro session ended. Starting short break for {shortBreak} minutes.");
                StartShortBreakTimer();
            } else if (intervals <= 0) {
                Debug.WriteLine($"All Pomodoro sessions completed. Starting long break for {longBreak} minutes.");
                StartLongBreakTimer();
            }
        }

        private void StartPomodoroTimer(object? sender = null, ElapsedEventArgs? e = null) {
            pomodoroTimer = new System.Timers.Timer(focus * 60 * 1000) {
                AutoReset = false
            };
            phase = "Focus";
            inSession = true;
            originalTime = TimeSpan.FromMinutes(focus);
            Debug.WriteLine($"Starting Pomodoro Timer for {focus} minutes.");
            pomodoroTimer.Elapsed += PomodoroTimer_Elapsed;
            pomodoroTimer.Start();
            stopwatch.Start();
            LaunchBanner("       STUDY TIME!!!       ", "#0015ff");
        }
        private void StartShortBreakTimer() {
            pomodoroTimer = new System.Timers.Timer(shortBreak * 60 * 1000) {
                AutoReset = false
            };
            phase = "Short Break";
            originalTime = TimeSpan.FromMinutes(shortBreak);
            pomodoroTimer.Elapsed += StartPomodoroTimer;
            pomodoroTimer.Start();
            stopwatch.Restart();
            LaunchBanner("       SHORT BREAK       ", "#330066");
        }
        private void StartLongBreakTimer() {
            pomodoroTimer = new System.Timers.Timer(longBreak * 60 * 1000) {
                AutoReset = false
            };
            phase = "Long Break";
            originalTime = TimeSpan.FromMinutes(longBreak);
            pomodoroTimer.Elapsed += RestartPomodoroTimer;
            pomodoroTimer.Start();
            stopwatch.Restart();
            LaunchBanner("L", "#190066");
        }
        private void RestartPomodoroTimer(object? sender, ElapsedEventArgs e) {
            Debug.WriteLine("Long break ended. Pomodoro cycle complete.");
            intervals = resetIntervals;
            StartPomodoroTimer();
            //Dispatcher.Invoke(() => {
            //    showPlay = true;
            //    playButtonImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/playArrow.png"));
            //});
        }

        private void LaunchBanner(string newText, string color) {

            // Ensure we run UI updates on the dispatcher (LaunchBanner may be called from timer threads)
            if (!Dispatcher.CheckAccess()) {
                // Use Invoke so subsequent code runs immediately and in-order on the UI thread
                Dispatcher.Invoke(() => LaunchBanner(newText, color));
                return;
            }

            // Factor textboxes into an array for easier assignment
            Label[] textBoxes = [
                bannerTextBox1, bannerTextBox2, bannerTextBox3, bannerTextBox4,
                bannerTextBox5, bannerTextBox6, bannerTextBox7
            ];

            // Set text and background for each textbox
            BrushConverter converter = new();
            Brush brush = (Brush?)converter.ConvertFrom(color) ?? Brushes.Transparent;
            if (newText == "L") {
                bannerTextBox1.Content = "LONG BREAK!";
                bannerTextBox2.Content = "GOOD JOB!";
                bannerTextBox3.Content = "LONG BREAK!";
                bannerTextBox4.Content = "GOOD JOB!";
                bannerTextBox5.Content = "LONG BREAK!";
                bannerTextBox6.Content = "GOOD JOB!";
                bannerTextBox7.Content = "LONG BREAK!";
                foreach(var tb in textBoxes) {
                    tb.Background = brush;
                }
            } else {
                foreach (var tb in textBoxes) {
                    tb.Content = newText;
                    tb.Background = brush;
                }
            }
            

            // Calculate widths deterministically instead of relying on measured Width which can be NaN
            double bannerTextWidth = SystemParameters.WorkArea.Width / 2 + 300;
            foreach (var tb in textBoxes) tb.Width = bannerTextWidth;

            // Set start/end images based on color
            if (color == "#0015ff") {
                bannerStartImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerBlue.png"));
                bannerEndImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerBlue.png"));
            } else if (color == "#330066") {
                bannerStartImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerDarkPurple.png"));
                bannerEndImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerDarkPurple.png"));
            } else if (color == "#190066") {
                bannerStartImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerPurple.png"));
                bannerEndImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerPurple.png"));
            }

            // Compute the total panel width explicitly to avoid NaN from Auto sizing
            double startWidth = double.IsNaN(bannerStartImage.Width) ? 0 : bannerStartImage.Width;
            double endWidth = double.IsNaN(bannerEndImage.Width) ? 0 : bannerEndImage.Width;
            double panelWidth = startWidth + endWidth + textBoxes.Length * bannerTextWidth;

            // Apply explicit width and initial transform
            stackPanelBanner.Width = panelWidth;
            stackPanelBannerTransform.X = -panelWidth;
            stackPanelBannerTransform.Y = 50;
            stackPanelBanner.Visibility = Visibility.Visible;

            // Remove any existing animation on the property to ensure a fresh start
            stackPanelBannerTransform.BeginAnimation(TranslateTransform.XProperty, null);

            DoubleAnimation moveBanner = new() {
                From = -panelWidth,
                To = panelWidth + SystemParameters.WorkArea.Width,
                Duration = TimeSpan.FromSeconds(10),
                FillBehavior = FillBehavior.Stop
            };
            moveBanner.Completed += (s, e) => {
                stackPanelBanner.Visibility = Visibility.Collapsed;
                // Clear any lingering animation and reset transform to start position
                stackPanelBannerTransform.BeginAnimation(TranslateTransform.XProperty, null);
                stackPanelBannerTransform.X = -panelWidth;
            };

            stackPanelBannerTransform.BeginAnimation(TranslateTransform.XProperty, moveBanner);
        }
    }
}