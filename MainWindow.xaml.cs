using System.Diagnostics;
using System.Drawing;
using System.Media;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;




namespace Pomodoro {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        readonly DispatcherTimer timer = new();
        readonly DispatcherTimer mouseLeftHoldTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        readonly DispatcherTimer alarmClock = new() { Interval = TimeSpan.FromSeconds(1) };
        System.Timers.Timer pomodoroTimer = new();
        readonly Stopwatch stopwatch = new();
        TimeSpan originalTime = TimeSpan.Zero;
        TimeSpan remainingTime;
        readonly SoundPlayer soundPlayer = new();
        System.Windows.Point marqueeStart;
        System.Windows.Point mousePosition;
        System.Windows.Point ballStartTrajectory;
        short ballHeld = 0; //used for making trajectory
        bool animateBall = false;
        bool ballThrown = false;
        bool marqueeSelectionActive = false;
        bool letThisShitWork = false;
        bool mouseLeftHeld = false;
        bool showPlay = true;
        bool inSession = false;        
        short heartFrames = 0;
        short frame = 0;
        short ticksPerFrame = 3;
        short tickCount = 0;
        short maxFrames = 8;
        int minRun = 10;
        int maxRun = 51;
        short minimumCycles = 0; //Minimum number of cycles to complete before changing state
        double transformX = 0;
        double transformY = 0;
        short scale = 0;
        double xSLope;
        double ySlope;
        bool chase = false;
        bool bounced = false;




        double resetIntervals, intervals, focus, shortBreak, longBreak;
        string phase = "Focus";



        (double, double) movementDistance = (0, 0);
        readonly List<short> poles = [1, -1];
        readonly static float scaler = 0.8f;


        //Dictionary of states to (maxFrames, ticksPerFrame, (width, height))
        private readonly Dictionary<string, (short, short, (short, short))> states = new() {
                                                                                { "Alarm", (2, 10, (115, 110)) }, { "Asleep", (6, 12, (75, 60)) }, { "Catch", (7, 2, (100, 170)) },
                                                                                { "Draw", (4, 10, (80, 130)) }, { "Drowsy", (7, 10, (75, 60)) }, { "Entry", (8, 3, (75, 70)) },
                                                                                { "Exit", (8, 3, (185, 200)) }, { "Idle", (6, 8, (75, 60)) }, { "Run", (6, 4, (100, 75)) } };
        string currentState = "Entry";

        readonly Random rand = new();
        readonly double workWidth = System.Windows.SystemParameters.WorkArea.Width;
        readonly double workHeight = System.Windows.SystemParameters.WorkArea.Height;
        (double, double) fenceArea = (System.Windows.SystemParameters.WorkArea.Width, System.Windows.SystemParameters.WorkArea.Height);
        (double, double) fenceLocation = (0, 0);
        public MainWindow() {
            InitializeComponent();
            this.Width = workWidth;
            this.Height = workHeight;
            Canvas.SetLeft(rabbitRect, workWidth / 8);
            Canvas.SetTop(rabbitRect, workHeight / 8);
            Canvas.SetLeft(marqueeLabel, (workWidth/2) - (marqueeLabel.Width/2));
            Canvas.SetTop(marqueeLabel, 50);
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
            ballHeld++;
            if(animateBall && !ballThrown) {
                mousePosition = Mouse.GetPosition(this);
                Debug.WriteLine($"Mouse is positioned at: ({mousePosition.X}, {mousePosition.Y})");
                if(ballHeld > 3 || (ballStartTrajectory.X == 0 && ballStartTrajectory.Y == 0)) {
                    ballHeld = 0;
                    ballStartTrajectory = mousePosition;
                }
                ballImageTransform.X = mousePosition.X - ballImage.Width/2;
                ballImageTransform.Y = mousePosition.Y - ballImage.Height/2;
            }
            if (animateBall && ballThrown) {
                Tuple<double, double> nextTraj = MoveBall(xSLope, ySlope, 0.98);
                xSLope = nextTraj.Item1;
                ySlope = nextTraj.Item2;
                if (Math.Abs(xSLope) < 0.5 && Math.Abs(ySlope) < 0.5) {
                    animateBall = false;
                    ballThrown = false;
                }
            }


            if (currentState == "Run") {
                if (chase) {
                    // Recalculate target vector each frame so the rabbit continuously chases the moving ball
                    movementDistance = (
                        ballImageTransform.X - (rabbitTransform.X + Canvas.GetLeft(rabbitRect)),
                        ballImageTransform.Y - (rabbitTransform.Y + Canvas.GetTop(rabbitRect))
                    );

                    // Calculate the magnitude of the movement vector
                    double magnitude = Math.Sqrt(movementDistance.Item1 * movementDistance.Item1 + movementDistance.Item2 * movementDistance.Item2);

                    if (magnitude <= 10) {
                        animateBall = false;
                        ballThrown = false;
                        chase = false;
                        ballImage.Visibility = Visibility.Collapsed;
                        ChangeState("Catch");
                        Canvas.SetLeft(rabbitRect, 0);
                        Canvas.SetTop(rabbitRect, 0);
                        rabbitTransform.X = Math.Min(ballImageTransform.X, fenceLocation.Item1 + fenceArea.Item1 + rabbitRect.Width);
                        rabbitTransform.Y = Math.Max(ballImageTransform.Y - 122, 0);
                    } else {

                        // Limit the movement to a maximum of 5 units per frame
                        double maxMovement = 7.0;
                        if (magnitude > maxMovement) {
                            double scale = maxMovement / magnitude;
                            movementDistance = (movementDistance.Item1 * scale, movementDistance.Item2 * scale);
                        }

                        // Move the rabbit
                        MoveRabbit(movementDistance.Item1, movementDistance.Item2);
                    }
                } else {
                    MoveRabbit(movementDistance.Item1 / (ticksPerFrame * maxFrames), movementDistance.Item2 / (ticksPerFrame * maxFrames));
                }
            }
            if (tickCount >= ticksPerFrame) {
                tickCount = 0;
                frame++;
                heartFrames--;
                if(currentState == "Catch") {
                    if (rabbitScale.ScaleX > 0) {
                        if (rabbitTransform.X + 5 < fenceArea.Item1 + fenceLocation.Item2) {
                            rabbitTransform.X -= 5;
                        }
                    } else if (rabbitTransform.X + 5 < fenceArea.Item1 + fenceLocation.Item2) {
                        rabbitTransform.X += 5;
                    }
                }
                if (frame >= maxFrames) {
                    frame = 0;
                    minimumCycles--;
                    if (currentState == "Alarm") {
                        minimumCycles++; // Prevent changing state at the end of the cycle so the alarm animation can loop until dismissed
                    } else if (currentState == "Exit") {
                        Application.Current.Shutdown(); // Close the application when the exit animation finishes                        
                    } else if (minimumCycles <= 0) {
                        if (currentState == "Draw") {
                            if (bounced) {
                                rabbitTransform.Y += 50;
                                bounced = false;
                            }
                            DropImage();
                        }
                        if (chase) {
                            ChangeState("Run");
                        } else {
                            ChangeState();
                        }
                    }
                }
                rabbitRect.Source = new BitmapImage(new Uri(GetUri()));
            }
            if (heartFrames <= 0) {
                heart.Opacity = 0;
            }

        }

        private Tuple<double, double> MoveBall(double xSlope, double ySlope, double decelerate) {
            double nextX = (xSlope) * decelerate;
            double nextY=  (ySlope) * decelerate;
            if ((nextX + ballImageTransform.X + ballImage.Width >= workWidth) || (nextX + ballImageTransform.X <= ballImage.Width)) {
                nextX *= -1;
            }
            if ((nextY + ballImageTransform.Y + ballImage.Height >= workHeight) || (nextY + ballImageTransform.Y <= ballImage.Height)) {
                nextY *= -1;
            }
            ballImageTransform.X += nextX;
            ballImageTransform.Y += nextY;
            return Tuple.Create(nextX, nextY);
        }

        private void Ball_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            optionsGrid.Visibility = Visibility.Collapsed;
            ballImage.Visibility = Visibility.Visible;
            animateBall = true;
            THEWINDOW.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
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
                    if (((movementDistance.Item1 + rabbitRect.Width + rabbitTransform.X) >= fenceArea.Item1) || ((movementDistance.Item2 + rabbitRect.Height + rabbitTransform.Y) >= fenceArea.Item2)) {
                        currentState = "Idle";
                    } else if (Math.Pow(Math.Pow(movementDistance.Item1, 2) + Math.Pow(movementDistance.Item2, 2), 0.5) >= (Math.Pow(Math.Pow(fenceArea.Item1, 2) + Math.Pow(fenceArea.Item2, 2), 0.5))) { 
                        currentState = "Idle";
                    } else {
                        currentState = "Run";
                    }
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

        private void PlayAudio(System.IO.Stream path) {
            soundPlayer.Stream = path;
            soundPlayer.LoadAsync();
            soundPlayer.LoadCompleted += (s, e) => soundPlayer.Play();
        }

        private void SetStateInfo() {
            tickCount = 0;
            frame = 0;
            maxFrames = states[currentState].Item1;
            ticksPerFrame = states[currentState].Item2;
            if (scale > 0) {
                rabbitRect.Width = states[currentState].Item3.Item1 / (Math.Pow(scaler, scale));
                rabbitRect.Height = states[currentState].Item3.Item2 / (Math.Pow(scaler, scale));
            } else if (scale < 0) {
                rabbitRect.Width = states[currentState].Item3.Item1 * (Math.Pow(scaler, -scale));
                rabbitRect.Height = states[currentState].Item3.Item2 * (Math.Pow(scaler, -scale));
            } else {
                rabbitRect.Width = states[currentState].Item3.Item1;
                rabbitRect.Height = states[currentState].Item3.Item2;
            }
            rabbitRect.Source = new BitmapImage(new Uri(GetUri()));
            if (currentState == "Run") { 
                if (!chase) {

                    movementDistance.Item1 = rand.Next(minRun, maxRun) * poles[rand.Next(0, 2)];
                    movementDistance.Item2 = rand.Next(minRun, maxRun) * poles[rand.Next(0, 2)];
                }
            }
            if (currentState == "Asleep") {
                minimumCycles = 10;
            } else if (currentState == "Draw"){
                minimumCycles = 3;
            } else {
                minimumCycles = 1;
            }
            if (currentState == "Exit") {
                rabbitTransform.Y -= 40;
            }
        }
        private void MoveRabbit(double x, double y) {
            double currentX = Canvas.GetLeft(rabbitRect) + rabbitTransform.X;
            double currentY = Canvas.GetTop(rabbitRect) + rabbitTransform.Y;
            if (currentX + x >= (rabbitRect.Width / 2) && (currentX + x <= fenceLocation.Item1 + fenceArea.Item1 && currentX + x >= fenceLocation.Item1)) {
                if (x < 0) {
                    if (currentX + x - rabbitRect.Width/2 >= fenceLocation.Item1) {
                        if(rabbitScale.ScaleX < 0) {
                            rabbitScale.ScaleX *= -1;
                        }
                        transformX += x;
                        rabbitTransform.X += x;
                    }
                } else {
                    if (currentX + x + rabbitRect.Width/2 <= fenceLocation.Item1 + fenceArea.Item1) {
                        if(rabbitScale.ScaleX > 0) {
                            rabbitScale.ScaleX *= -1;
                        }
                        transformX += x;
                        rabbitTransform.X += x;
                    }
                }
            }
            if (currentY + y <= fenceLocation.Item2 + fenceArea.Item2 - rabbitRect.Height && currentY + y >= fenceLocation.Item2) {
                transformY += y;
                rabbitTransform.Y += y;
            }
            if (currentY <= fenceLocation.Item2) {
                transformY += 5;
                rabbitTransform.Y += 5;
            } else if (currentY >= fenceLocation.Item2 + fenceArea.Item2) {
                transformY -= 5;
                rabbitTransform.Y -= 5;
            }
            if (currentX <= fenceLocation.Item1) {
                transformX += 5;
                rabbitTransform.X += 5;
            } else if (currentX >= fenceLocation.Item1 + fenceArea.Item1) {
                transformX -= 5;
                rabbitTransform.X -= 5;
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
            System.Windows.Point mousePosition = Mouse.GetPosition(canvas);
            transformX = mousePosition.X - Canvas.GetLeft(rabbitRect) - (rabbitRect.Width / 2);
            transformY = mousePosition.Y - Canvas.GetTop(rabbitRect) - (rabbitRect.Height / 2);

            // Immediately apply the calculated translation so the rabbit follows the mouse
            if (rabbitScale.ScaleX < 0) {
                rabbitScale.ScaleX *= -1; // Flip the rabbit to face right if it was facing left
            }

            rabbitTransform.X = transformX;
            rabbitTransform.Y = transformY;


            rabbitRect.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/rabbitHold.png"));
            if (scale > 0) {
                rabbitRect.Width = 70 / (Math.Pow(scaler, scale));
                rabbitRect.Height = 100 / (Math.Pow(scaler, scale));
            } else if (scale < 0) {
                rabbitRect.Width = 70 * (Math.Pow(scaler, -scale));
                rabbitRect.Height = 100 * (Math.Pow(scaler, -scale));
            } else {
                rabbitRect.Width = 70;
                rabbitRect.Height = 100;
            }
            // Capture the mouse so we continue receiving move/up events while dragging
            Mouse.Capture(rabbitRect);

        }
        private void Canvas_MouseMove(object? sender, MouseEventArgs e) {
            if (!mouseLeftHeld) return;

            // Update the rabbit position continuously while the left mouse button is held
            System.Windows.Point mousePosition = e.GetPosition(canvas);
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
                    currentX = Canvas.GetLeft(rabbitRect) + rabbitTransform.X;
                } else {
                    currentX = Canvas.GetLeft(rabbitRect) + rabbitTransform.X + (rabbitRect.Width / 2);
                }
                double currentY = Canvas.GetTop(rabbitRect) + rabbitTransform.Y;
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
            double proposedX = Canvas.GetLeft(rabbitRect) + transformX;
            double proposedY = Canvas.GetTop(rabbitRect) + transformY;
            if ((proposedX < (fenceLocation.Item1) || proposedX > (fenceLocation.Item1 + fenceArea.Item1))
                || (proposedY < (fenceLocation.Item2) || proposedY > (fenceLocation.Item2 + fenceArea.Item2))) {
                rabbitTransform.X = fenceLocation.Item1 + (fenceArea.Item1 / 2) - (rabbitRect.Width / 2);
                rabbitTransform.Y = fenceLocation.Item2 + (fenceArea.Item2 / 2) - (rabbitRect.Height / 2);
                Canvas.SetTop(optionsGrid, 0);
                Canvas.SetLeft(optionsGrid, 0);
                Canvas.SetTop(heart, 0);
                Canvas.SetLeft(heart, 0);
                optionsTransform.X = rabbitTransform.X + 50;
                optionsTransform.Y = rabbitTransform.Y;
                heartTransform.X = rabbitTransform.X + (rabbitRect.Width / 2) - (heart.Width / 2);
                heartTransform.Y = rabbitTransform.Y - (heart.Height / 2);

                marqueeLabel.BeginAnimation(UIElement.OpacityProperty, null);
                marqueeLabel.Content = "Out of bounds! Returning to fence...";
                marqueeLabel.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 0, 0));
                marqueeLabel.Opacity = 1;
                DoubleAnimation outOfBounds = new() {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(3),
                    FillBehavior = FillBehavior.Stop
                };
                outOfBounds.Completed += (s, ev) => {
                    marqueeLabel.Visibility = Visibility.Collapsed;
                    marqueeLabel.Opacity = 1; // Reset opacity for next use
                    marqueeLabel.BeginAnimation(UIElement.OpacityProperty, null); // Clear the animation to reset the property
                };
                marqueeLabel.Visibility = Visibility.Visible;
                marqueeLabel.BeginAnimation(UIElement.OpacityProperty, outOfBounds);

                ChangeState("Idle");
            }
            ChangeState(currentState);
            timer.Start();
        }

        private void UpdateAlarmClock(object? sender, EventArgs e) {
            remainingTime = originalTime - stopwatch.Elapsed;
            double minutes = Math.Floor(remainingTime.TotalMinutes);
            double seconds = Math.Floor(remainingTime.TotalSeconds % 60);
            alarmStatus.Content = $"{minutes:00} : {seconds:00}";
        }
        private void RabbitRect_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            double currentX;
            if (rabbitScale.ScaleX == -1) {
                currentX = Canvas.GetLeft(rabbitRect) + rabbitTransform.X;
            } else {
                currentX = Canvas.GetLeft(rabbitRect) + rabbitTransform.X + rabbitRect.Width + 10;
            }
            double currentY = Canvas.GetTop(rabbitRect) + transformY;
            if (inSession) {
                alarmStatusRow.Height = new GridLength(40);
                alarmClock.Tick += UpdateAlarmClock;
                alarmClock.Start();
            }

            optionsGrid.Visibility = Visibility.Visible;
            if (currentX + optionsGrid.Width > workWidth) {
                currentX = workWidth - optionsGrid.Width - 10;
            } else if (currentX < 0) {
                currentX = 0;
            }
            if (currentY + optionsGrid.Height > workHeight) {
                currentY = workHeight - optionsGrid.Height - 10;
            } else if (currentY < 0) {
                currentY = 0;
            }
            
            optionsTransform.X = currentX;
            optionsTransform.Y = currentY;
        }
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (optionsGrid.Visibility == Visibility.Visible) {
                if (!optionsGrid.IsMouseOver) {
                    optionsGrid.Visibility = Visibility.Collapsed;
                    alarmClock.Stop();
                    alarmClock.Tick -= UpdateAlarmClock;
                    alarmStatusRow.Height = new GridLength(0);
                    alarmStatus.Content = " : ";
                }
            }
            if (alarmSettings.Visibility == Visibility.Visible) {
                if (!alarmSettings.IsMouseOver) {
                    alarmSettings.Visibility = Visibility.Collapsed;
                    ChangeState();
                    rabbitTransform.X += 30;
                }
            }
        }
        private void Window_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if (marqueeSelectionActive) {
                marqueeSelectionActive = false;
                marqueeLabel.Visibility = Visibility.Collapsed;
                canvas.Background = System.Windows.Media.Brushes.Transparent;
            }
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (marqueeSelectionActive && marqueeLabel.Visibility == Visibility.Visible) {
                marqueeStart = e.GetPosition(canvas);
                marqueeRect.Visibility = Visibility.Visible;
                letThisShitWork = true;
            } else if (marqueeSelectionActive) {
                marqueeLabel.Visibility = Visibility.Visible;
            }
        }
        private void Window_MouseMove(object sender, MouseEventArgs e) {
            if (marqueeLabel.Visibility == Visibility.Visible && marqueeSelectionActive && letThisShitWork) {
                System.Windows.Point currentPosition = e.GetPosition(canvas);
                double x = Math.Min(currentPosition.X, marqueeStart.X);
                double y = Math.Min(currentPosition.Y, marqueeStart.Y);
                double width = Math.Abs(marqueeStart.X - currentPosition.X);
                double height = Math.Abs(marqueeStart.Y - currentPosition.Y);
                Canvas.SetLeft(marqueeRect, x);
                Canvas.SetTop(marqueeRect, y);
                marqueeRect.Width = width;
                marqueeRect.Height = height;
            }
        }
        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (marqueeLabel.Visibility == Visibility.Visible && marqueeSelectionActive && letThisShitWork) {
                marqueeSelectionActive = false;
                letThisShitWork = false;
                marqueeLabel.Visibility = Visibility.Collapsed;
                canvas.Background = System.Windows.Media.Brushes.Transparent;

                // Clear any existing opacity animations and start a fade-out
                marqueeRect.BeginAnimation(UIElement.OpacityProperty, null);
                DoubleAnimation marqueeFade = new() {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(3),
                    FillBehavior = FillBehavior.Stop
                };

                // Capture fence location/size now (before we change the marquee for the next use)
                fenceLocation.Item1 = Canvas.GetLeft(marqueeRect);
                fenceLocation.Item2 = Canvas.GetTop(marqueeRect);
                fenceArea.Item1 = marqueeRect.Width;
                fenceArea.Item2 = marqueeRect.Height;

                marqueeFade.Completed += (s, ev) => {
                    // Hide and reset marquee once the fade finished so the user actually sees the animation
                    marqueeRect.Visibility = Visibility.Collapsed;
                    marqueeRect.Opacity = 1; // Reset opacity for next use
                    marqueeRect.BeginAnimation(UIElement.OpacityProperty, null); // Clear the animation to reset the property

                    // Reset marquee rectangle to default size and position for next use
                    marqueeRect.Width = 0;
                    marqueeRect.Height = 0;
                    Canvas.SetLeft(marqueeRect, 0);
                    Canvas.SetTop(marqueeRect, 0);
                };

                marqueeRect.BeginAnimation(UIElement.OpacityProperty, marqueeFade);

                Canvas.SetLeft(rabbitRect, 0);
                Canvas.SetTop(rabbitRect, 0);
                rabbitTransform.X = fenceLocation.Item1 + (fenceArea.Item1 / 2) - (rabbitRect.Width / 2);
                rabbitTransform.Y = fenceLocation.Item2 + (fenceArea.Item2 / 2) - (rabbitRect.Height / 2);
                transformX = rabbitTransform.X;
                transformY = rabbitTransform.Y;
            }
            if (animateBall && !ballThrown) {
                ballThrown = true;
                THEWINDOW.Background = System.Windows.Media.Brushes.Transparent;
                xSLope = mousePosition.X - ballStartTrajectory.X;
                ySlope = mousePosition.Y - ballStartTrajectory.Y;
                chase = true;
                ChangeState("Run");
            }
        }
        private void Fence_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            marqueeSelectionActive = true;
            optionsGrid.Visibility = Visibility.Collapsed;
            BrushConverter converter = new();
            System.Windows.Media.Brush brush = (System.Windows.Media.Brush?)converter.ConvertFrom("#190d0d0d") ?? System.Windows.Media.Brushes.Transparent;
            canvas.Background = brush;
            marqueeLabel.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255));
            marqueeLabel.Content = "Select Fence Area (Right Click to Cancel)";
        }

        private void AddPlayOptions() {
            ball.Visibility = Visibility.Visible;
            crayon.Visibility = Visibility.Visible;
        }

        private void RemovePlayOptions() {
            ball.Visibility = Visibility.Collapsed;
            crayon.Visibility = Visibility.Collapsed;
        }

        private void NumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            // Check if the input text is a whole number (0-9)
            Regex regex = FilterOutNonDigits();
            e.Handled = regex.IsMatch(e.Text);
        }
        private void StartShortBreakTimer() {
            pomodoroTimer = new System.Timers.Timer(shortBreak * 60 * 1000) {
                AutoReset = false
            };
            phase = "Short Break";
            // Start the timer thread-side, but update the UI-tied timing fields on the dispatcher
            pomodoroTimer.Elapsed += StartPomodoroTimer;
            pomodoroTimer.Start();
            Dispatcher.Invoke(() => {
                originalTime = TimeSpan.FromMinutes(shortBreak);
                stopwatch.Restart();
            });
            LaunchBanner("       SHORT BREAK       ", "#330066");
        }
        private void StartLongBreakTimer() {
            pomodoroTimer = new System.Timers.Timer(longBreak * 60 * 1000) {
                AutoReset = false
            };
            phase = "Long Break";
            // Start the timer thread-side, but update the UI-tied timing fields on the dispatcher
            pomodoroTimer.Elapsed += RestartPomodoroTimer;
            pomodoroTimer.Start();
            Dispatcher.Invoke(() => {
                AddPlayOptions();
                originalTime = TimeSpan.FromMinutes(longBreak);
                stopwatch.Restart();
            });
            LaunchBanner("L", "#190066");
        }
        private void RestartPomodoroTimer(object? sender, ElapsedEventArgs e) {
            Debug.WriteLine("Long break ended. Pomodoro cycle complete.");
            Dispatcher.Invoke(() => {
                RemovePlayOptions();
            });
            intervals = resetIntervals;
            StartPomodoroTimer();
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
            System.Windows.Media.Brush brush = (System.Windows.Media.Brush?)converter.ConvertFrom(color) ?? System.Windows.Media.Brushes.Transparent;
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
                PlayAudio(Properties.Resources.sharpAlarm2);
            } else if (color == "#330066") {
                bannerStartImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerDarkPurple.png"));
                bannerEndImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerDarkPurple.png"));
                PlayAudio(Properties.Resources.softAlarm2);
            } else if (color == "#190066") {
                bannerStartImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerPurple.png"));
                bannerEndImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/bannerPurple.png"));
                PlayAudio(Properties.Resources.softAlarm2);
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


        private void Minus_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            scale--;
            if (scale <= -2) { 
                minus.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/minusDisabled.png"));
                return;
            }
            if (scale < 2) {
                plus.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/plus.png"));
            }
            rabbitRect.Width *= scaler;
            rabbitRect.Height *= scaler;
            ballImage.Width *= scaler;
            ballImage.Height *= scaler;
            minRun = (int)(minRun * scaler);
            maxRun = (int)(maxRun * scaler);
            if (minRun > 50) { minRun = 50; }
            if (maxRun > 50) { maxRun = 50; }
        }
        private void Plus_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            scale++;
            if (scale >= 2) {
                plus.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/plusDisabled.png"));
                return;
            }
            if (scale > -2) {
                minus.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/minus.png"));
            }
            rabbitRect.Width /= scaler;
            rabbitRect.Height /= scaler;
            ballImage.Width /= scaler;
            ballImage.Height /= scaler;
            minRun = (int)(minRun / scaler);
            maxRun = (int)(maxRun / scaler);
        }
        private void Alarm_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            optionsGrid.Visibility = Visibility.Collapsed;
            alarmSettings.Visibility = Visibility.Visible;
            ChangeState("Alarm");
            rabbitTransform.X -= 30;
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex FilterOutNonDigits();

        private void Exit_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            ChangeState("Exit");
        }

        private void Crayon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            ChangeState("Draw");
            optionsGrid.Visibility = Visibility.Collapsed;
            if(Canvas.GetTop(rabbitRect) + rabbitTransform.Y + 50 < fenceLocation.Item2) {
                rabbitTransform.Y -= 50;
                bounced = true;
            }
        }
        private void DropImage() {
            if (fenceArea.Item1 + fenceLocation.Item1 < rabbitTransform.X + Canvas.GetLeft(rabbitRect) + rabbitRect.Width) {
                droppedImageTransform.X = rabbitTransform.X + Canvas.GetLeft(rabbitRect);
            } else {
                droppedImageTransform.X = rabbitTransform.X + Canvas.GetLeft(rabbitRect) + rabbitRect.Width;
            }

            if (fenceArea.Item2 + fenceLocation.Item2 < rabbitRect.Height + Canvas.GetTop(rabbitRect) + rabbitTransform.Y) {
                droppedImageTransform.Y = rabbitTransform.Y + Canvas.GetTop(rabbitRect);
            } else {
                droppedImageTransform.Y = rabbitTransform.Y + Canvas.GetTop(rabbitRect) + rabbitRect.Height/2;
            }
            int num = rand.Next(1, 11);
            droppedImage.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Sprites/Static/drawing{num}.png"));
            droppedImage.Width = 30;
            droppedImage.Height = 21;
            droppedImage.Visibility = Visibility.Visible;
        }

        private void DroppedImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            droppedImage.Width = 400;
            droppedImage.Height = 280;
            double centerX = workWidth / 2 - droppedImage.Width;
            double centerY = workHeight / 2 - droppedImage.Height;
            droppedImageTransform.X = centerX;
            droppedImageTransform.Y = centerY;
            DoubleAnimation lowerImage = new() {
                From = droppedImageTransform.Y,
                To = SystemParameters.PrimaryScreenHeight,
                Duration = TimeSpan.FromSeconds(1),
                FillBehavior = FillBehavior.Stop,
                BeginTime = TimeSpan.FromSeconds(3)
            };
            lowerImage.Completed += (s, e) => {
                droppedImageTransform.BeginAnimation(TranslateTransform.YProperty, null);
                droppedImage.Visibility = Visibility.Collapsed;
            };
            droppedImageTransform.BeginAnimation(TranslateTransform.YProperty, lowerImage);
        }

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
                ChangeState();
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
            // Start the timer thread-side, but update originalTime and stopwatch on the UI thread
            Dispatcher.Invoke(() => {
                originalTime = TimeSpan.FromMinutes(focus);
                Debug.WriteLine($"Starting Pomodoro Timer for {focus} minutes.");
                stopwatch.Restart();
            });
            pomodoroTimer.Elapsed += PomodoroTimer_Elapsed;
            pomodoroTimer.Start();
            LaunchBanner("       STUDY TIME!!!       ", "#0015ff");
        }

    }
}