﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Management;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using System.Diagnostics;

namespace IMDeyes
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        

        SerialPort ConnectPort;
        bool IsPortAvailable = true;
        string[] PortList;
        int LeftStick_X, LeftStick_Y, RightStick_X, RightStick_Y, Trigger;
        bool Button_A, Button_B, Button_X, Button_Y,
            Button_Start, Button_LB, DPad_L, DPad_D, DPad_U, DPad_R;
        GamePadState gamePadState = GamePad.GetState(PlayerIndex.One);
        StreamWriter writer;

        Thread datas;
        Thread connect;
        Thread write;

        int Frame = 0;

        private ObservableDataSource<System.Windows.Point>
            Point_Accel_X, Point_Accel_Y, Point_Accel_Z, Point_Gyro_X, Point_Gyro_Y, Point_Gyro_Z, Point_Angle_X, Point_Angle_Y;

        public MainWindow()
        {
            InitializeComponent();

            this.MouseLeftButtonDown += (sender, e) => this.DragMove();

            ConnectPort = new SerialPort();

            Moyashi = new string[12];

            Point_Accel_X = new ObservableDataSource<System.Windows.Point>();
            Point_Accel_X.SetXYMapping(point => point);
            Plotter_Accel.AddLineGraph(Point_Accel_X, Colors.DarkTurquoise, 1.0);
            Point_Accel_Y = new ObservableDataSource<System.Windows.Point>();
            Point_Accel_Y.SetXYMapping(point => point);
            Plotter_Accel.AddLineGraph(Point_Accel_Y, Colors.GreenYellow, 1.0);
            Point_Accel_Z = new ObservableDataSource<System.Windows.Point>();
            Point_Accel_Z.SetXYMapping(point => point);
            Plotter_Accel.AddLineGraph(Point_Accel_Z, Colors.Red, 1.0);
            Plotter_Accel.Legend.Remove();

            Point_Gyro_X = new ObservableDataSource<System.Windows.Point>();
            Point_Gyro_X.SetXYMapping(point => point);
            Plotter_Gyro.AddLineGraph(Point_Gyro_X, Colors.DarkTurquoise, 1.0);
            Point_Gyro_Y = new ObservableDataSource<System.Windows.Point>();
            Point_Gyro_Y.SetXYMapping(point => point);
            Plotter_Gyro.AddLineGraph(Point_Gyro_Y, Colors.GreenYellow, 1.0);
            Point_Gyro_Z = new ObservableDataSource<System.Windows.Point>();
            Point_Gyro_Z.SetXYMapping(point => point);
            Plotter_Gyro.AddLineGraph(Point_Gyro_Z, Colors.Red, 1.0);
            Plotter_Gyro.Legend.Remove();

            Point_Angle_X = new ObservableDataSource<System.Windows.Point>();
            Point_Angle_X.SetXYMapping(point => point);
            Plotter_Angle.AddLineGraph(Point_Angle_X, Colors.DarkTurquoise, 1.0);
            Point_Angle_Y = new ObservableDataSource<System.Windows.Point>();
            Point_Angle_Y.SetXYMapping(point => point);
            Plotter_Angle.AddLineGraph(Point_Angle_Y, Colors.GreenYellow, 1.0);
            Plotter_Angle.Legend.Remove();


            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 30;
            timer.Enabled = true;
            timer.Tick += new EventHandler(Control);

            System.Windows.Forms.Timer Write = new System.Windows.Forms.Timer();
            Write.Interval = 100;
            Write.Enabled = true;
            Write.Tick += new EventHandler(AsyncWrite);

            PortList = SerialPort.GetPortNames();

            ChangeUnder(2,"Component Initialized");
        }

        void AsyncWrite(object sender, EventArgs e)
        {
            try
            {
                write = new Thread(new ThreadStart(Write_Tick));
                write.Start();
            }
            catch (TimeoutException)
            {
                if (ConnectPort.IsOpen)
                {
                    ChangeUnder(3,"Write Timeout");
                    DisConnect();
                }
            }
            catch (Exception ex)
            {
                ChangeUnder(3, "Write Failed:" + ex.Message);
            }
        }

        void Write_Tick()
        {
            if (ConnectPort.IsOpen)
            {
                try
                {
                    ConnectPort.Write(Send);
                    writer.WriteLine("Send:" + Send);
                    SerialViewAdd(Send);
                }
                catch (TimeoutException)
                {
                    ChangeUnder(3, "Send Timeout");
                    DisConnect();
                }
                catch (Exception ex)
                {
                    ChangeUnder(3, "Send Failed: " + ex.Message);
                }
                
            }
            SerialViewAdd(Send);
        }


        void ReceiveViewAdd(string hoge)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if ((bool)ReceiveViewEnable.IsChecked)
                {
                    ReceiveView.Items.Insert(0, hoge);

                    if (ReceiveView.Items.Count > 500)
                    {
                        ReceiveView.Items.RemoveAt(499);
                    }
                }
            }));
        }

        void SerialViewAdd(string hoge)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if ((bool)SerialViewEnable.IsChecked)
                {
                    SerialView.Items.Insert(0, hoge);
                    if (SerialView.Items.Count > 300)
                    {
                        SerialView.Items.RemoveAt(299);
                    }
                }
            }));
        }

        void InfomationViewAdd(string hoge)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if ((bool)InfomationViewEnable.IsChecked)
                {
                    InfomationView.Items.Insert(0, hoge);
                    if (InfomationView.Items.Count > 300)
                    {
                        InfomationView.Items.RemoveAt(299);
                    }
                }
            }));
        }


        string Send;

        void Control(object sender, EventArgs e)
        {
            gamePadState = GamePad.GetState(PlayerIndex.One);
            LeftStick_X = (int)((gamePadState.ThumbSticks.Left.X + 1) * 127) + 1;
            LeftStick_Y = (int)((gamePadState.ThumbSticks.Left.Y + 1) * 127) + 1;
            RightStick_X = (int)((gamePadState.ThumbSticks.Right.X + 1) * 127) + 1;
            RightStick_Y = (int)((gamePadState.ThumbSticks.Right.Y + 1) * 127) + 1;
            Trigger = 255 - (int)((-gamePadState.Triggers.Left + gamePadState.Triggers.Right + 1) * 127);
            Bar_LeftStick_X.Value = (float)LeftStick_X / 255 * 100;
            Bar_LeftStick_Y.Value = (float)LeftStick_Y / 255 * 100;
            Bar_RightStick_X.Value = (float)RightStick_X / 255 * 100;
            Bar_RightStick_Y.Value = (float)RightStick_Y / 255 * 100;
            Bar_Trigger.Value = (float)Trigger / 255 * 100;

            Button_A = this.gamePadState.Buttons.RightShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Button_B = this.gamePadState.Buttons.B == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Button_X = this.gamePadState.Buttons.X == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Button_Y = this.gamePadState.Buttons.Y == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            Label();

            if (this.gamePadState.Buttons.A == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                LabelRefresh();
            }

            if (!Button_Start && this.gamePadState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                if (ConnectButton.IsEnabled)
                {
                    connect = new Thread(new ThreadStart(TryToConnectAsync));
                    connect.Start();
                }
            }

            if (this.gamePadState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                Sign_Button_Start.Background = Bar_LeftStick_X.Foreground;
            }
            else
            {
                Sign_Button_Start.Background = Bar_LeftStick_X.Background;
            }

            if (this.gamePadState.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                Sign_DPad_U.Background = Bar_LeftStick_X.Foreground;
            }
            else
            {
                Sign_DPad_U.Background = Bar_LeftStick_X.Background;
            }

            if (this.gamePadState.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                Sign_DPad_D.Background = Bar_LeftStick_X.Foreground;
            }
            else
            {
                Sign_DPad_D.Background = Bar_LeftStick_X.Background;
            }

            if (this.gamePadState.Buttons.LeftShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                Sign_Button_LB.Background = Bar_LeftStick_X.Foreground;
            }
            else
            {
                Sign_Button_LB.Background = Bar_LeftStick_X.Background;
            }

            if (Button_A)
            {
                Sign_Button_RB.Background = Bar_LeftStick_X.Foreground;
            }
            else
            {
                Sign_Button_RB.Background = Bar_LeftStick_X.Background;
            }

            if (Button_Y)
            {
                Sign_Button_Y.Background = Bar_LeftStick_X.Foreground;
            }
            else
            {
                Sign_Button_Y.Background = Bar_LeftStick_X.Background;
            }

            

            DPad_L = this.gamePadState.DPad.Left == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            DPad_D = this.gamePadState.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            DPad_U = this.gamePadState.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            DPad_R = this.gamePadState.DPad.Right == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Button_LB = this.gamePadState.Buttons.LeftShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            if (MValue.Length >= 4)
            {
                Bar_Motor_L.Value = MValue[0];
                Bar_Motor_R.Value = MValue[1];
                Bar_Motor_F.Value = MValue[2];
                Bar_Motor_B.Value = MValue[3];
                Label_Motor_L.Text = MValue[0].ToString();
                Label_Motor_B.Text = MValue[3].ToString();
                Label_Motor_R.Text = MValue[1].ToString();
                Label_Motor_F.Text = MValue[2].ToString();
                MValue = new int[0];
            }

            if (AValue.Length >= 8)
            {
                Bar_Accel_X.Value = AValue[0];
                Bar_Accel_Y.Value = AValue[1];
                Bar_Accel_Z.Value = AValue[2];
                Bar_Gyro_X.Value = AValue[3];
                Bar_Gyro_Y.Value = AValue[4];
                Bar_Gyro_Z.Value = AValue[5];
                Bar_Angle_X.Value = AValue[6];
                Bar_Angle_Y.Value = AValue[7];
                Bar_Angle_F.Value = Math.Sin(-1 * deg2rad(AValue[6]));
                Bar_Angle_B.Value = Math.Sin(deg2rad(AValue[6]));
                Bar_Angle_L.Value = Math.Sin(-1 * deg2rad(AValue[7]));
                Bar_Angle_R.Value = Math.Sin(deg2rad(AValue[7]));

                GraphRefresh();

                Label_Accel_X.Text = AValue[0].ToString();
                Label_Accel_Y.Text = AValue[1].ToString();
                Label_Accel_Z.Text = AValue[2].ToString();
                Label_Gyro_X.Text = AValue[3].ToString();
                Label_Gyro_Y.Text = AValue[4].ToString();
                Label_Gyro_Z.Text = AValue[5].ToString();
                Label_Angle_X.Text = AValue[6].ToString();
                Label_Angle_Y.Text = AValue[7].ToString();
                AValue = new int[0];
            }
            Button_Start = this.gamePadState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Send = "q" // initialize
                + "a" + RightStick_Y.ToString("000") // unused.
                + "b" + RightStick_X.ToString("000") // unused.
                + "c" + LeftStick_Y.ToString("000") // handle x
                + "d" + LeftStick_X.ToString("000") // handle y
                + "e" + Trigger.ToString("000")   // throttle
                + "f" + Convert.ToInt32(Button_A)  // enable switch

                // debug for PD :)
                + "h" + Label_AngleKp.Text
                + "i" + Label_AngleKd.Text
                + "j" + Label_GyroKp.Text
                + "k" + Label_GyroKd.Text
                + "l" + Label_AngleGain.Text
                + "m" + Label_GyroGain.Text
                + "r";
        }

        void GraphRefresh()
        {
                Point_Accel_X.AppendAsync(Dispatcher, new System.Windows.Point(Frame, Bar_Accel_X.Value));
                Point_Accel_Y.AppendAsync(Dispatcher, new System.Windows.Point(Frame, Bar_Accel_Y.Value));
                Point_Accel_Z.AppendAsync(Dispatcher, new System.Windows.Point(Frame, Bar_Accel_Z.Value));
                Point_Gyro_X.AppendAsync(Dispatcher, new System.Windows.Point(Frame, Bar_Gyro_X.Value));
                Point_Gyro_Y.AppendAsync(Dispatcher, new System.Windows.Point(Frame, Bar_Gyro_Y.Value));
                Point_Gyro_Z.AppendAsync(Dispatcher, new System.Windows.Point(Frame, Bar_Gyro_Z.Value));
                Point_Angle_X.AppendAsync(Dispatcher, new System.Windows.Point(Frame, Bar_Angle_X.Value));
                Point_Angle_Y.AppendAsync(Dispatcher, new System.Windows.Point(Frame, Bar_Angle_Y.Value));

                if (Frame >100)
                {
                    Point_Accel_X.Collection.Remove(Point_Accel_X.Collection.First());
                    Point_Accel_Y.Collection.Remove(Point_Accel_Y.Collection.First());
                    Point_Accel_Z.Collection.Remove(Point_Accel_Z.Collection.First());
                    Point_Gyro_X.Collection.Remove(Point_Gyro_X.Collection.First());
                    Point_Gyro_Y.Collection.Remove(Point_Gyro_Y.Collection.First());
                    Point_Gyro_Z.Collection.Remove(Point_Gyro_Z.Collection.First());
                    Point_Angle_X.Collection.Remove(Point_Angle_X.Collection.First());
                    Point_Angle_Y.Collection.Remove(Point_Angle_Y.Collection.First());
                }
                Frame++;
        }

        string[] Moyashi = null;
        int[] AValue = new int[0];
        int[] MValue = new int[0];
        string ReceiveData;

        private void DataEdit()
        {
            Moyashi = ReceiveData.Split(',');

            ReceiveViewAdd(ReceiveData);
            if (Moyashi.Length >= 5)
            {
                try
                {
                    if (Moyashi[0] == "A")
                    {
                        AValue = new int[8];
                        for (int i = 0; i < 8; i++)
                        {
                            AValue[i] = int.Parse(Moyashi[i + 1]);
                        }
                    }
                    else if (Moyashi[0] == "M")
                    {
                        MValue = new int[12];
                        for (int i = 0; i < 4; i++)
                        {
                            MValue[i] = int.Parse(Moyashi[i + 1]);
                        }
                    }
                }
                catch
                {
                    ChangeUnder(3,"Edit error:" + ReceiveData);
                }

            }
            else
            {
                if (!ReceiveData.Contains("[INFO]"))
                {
                    ChangeUnder(3, "KUFC:" + ReceiveData);
                }
                else
                {
                    InfomationViewAdd(ReceiveData);
                }
            }
        }


        void ConnectPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            if (ConnectPort.IsOpen)
            {
                try
                {
                    ReceiveData = ConnectPort.ReadLine();
                    writer.WriteLine("RECEIVE:" + ReceiveData);
                    datas = new Thread(new ThreadStart(DataEdit));
                    datas.Start();
                }
                catch (TimeoutException)
                {
                    ChangeUnder(3,"Receive Timeout");
                    DisConnect();
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ChangeUnder(3, "Receive error:" + ex.Message);
                    }));
                }
            }
        }

        private void TryToConnect(object sender, RoutedEventArgs e)
        {
            if (ConnectButton.IsEnabled)
            {
                connect = new Thread(new ThreadStart(TryToConnectAsync));
                connect.Start();
            }
        }
        void TryToConnectAsync()
        {
                if (ConnectPort.IsOpen)
                {
                    DisConnect();
                }
                else if (!ConnectPort.IsOpen)
                {
                    Connect();
                }
        }

        void DisConnect()
        {
            try
            {
                ConnectPort.Close();
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    ChangeUnder(2, "DisConnected");
                    ConnectButton.Content = "Connect";
                }));
            }
            catch (Exception ex)
            {
                ChangeUnder(3, "DisConnect Failed:" + ex.Message);
            }
        }
        void Connect()
        {
            if (IsPortAvailable)
            {

                try
                {
                    ConnectPort.PortName = "COM40";
                    ConnectPort.BaudRate = 9600;
                    ConnectPort.ReadTimeout = 1000;
                    ConnectPort.WriteTimeout = 1000;
                    ConnectPort.Encoding = ASCIIEncoding.UTF8;
                    ConnectPort.DataReceived += new SerialDataReceivedEventHandler(ConnectPort_DataReceived);

                    ChangeUnder(0, "Connecting");
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ConnectButton.IsEnabled = false;
                    })); ;
                    ConnectPort.Open();
                    ChangeUnder(1, "Connection established");

                    Encoding sjisEnc = Encoding.GetEncoding("Shift_JIS");

                    writer =
                        new StreamWriter(@"Log\IMDEYE" + DateTime.Today.Year.ToString() + "_" +
                        DateTime.Now.Month.ToString() + "_" +
                        DateTime.Now.Day.ToString() + "_" +
                        DateTime.Now.Hour.ToString() + "_" +
                        DateTime.Now.Minute.ToString() + "_" +
                        DateTime.Now.Second.ToString()
                            + ".txt", true, sjisEnc);

                    writer.WriteLine("Connected:" + ConnectPort.PortName + ":" + ConnectPort.BaudRate.ToString());

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ConnectButton.IsEnabled = true;
                        ConnectButton.Content = "DisConnect";
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ConnectButton.IsEnabled = true;
                    }));
                    ChangeUnder(3,"Failed:" + ex.Message);
                }
            }
            else
            {
                ChangeUnder(3,"Port does not exist");
            }
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        void ChangeUnder(int color,string message)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                switch (color)
                {
                    case(0):
                        Grid_Under.Background = Brushes.DarkViolet;
                        WindowBoader.BorderBrush = Brushes.DarkViolet;  ///Thread
                        break;
                    case (1):
                        Grid_Under.Background = Brushes.DarkOrange;
                        WindowBoader.BorderBrush = Brushes.DarkOrange;    //Conencting
                        break;
                    case (2):
                        Grid_Under.Background = Brushes.DodgerBlue;
                        WindowBoader.BorderBrush = Brushes.DodgerBlue;    //DisConnecting
                        break;
                    case (3):
                        Grid_Under.Background = Brushes.DarkRed;
                        WindowBoader.BorderBrush = Brushes.DarkRed;      //Error
                        break;
                    default:
                        break;
                }
                Label_Under.Text = message;
            }));
        }

        private void Refresh(object sender, RoutedEventArgs e)
        {
            LabelRefresh();
        }
        void LabelRefresh()
        {
            Label_AngleKp.Text = Box_AngleKp.Text;
            Label_AngleKd.Text = Box_AngleKd.Text;
            Label_GyroKp.Text = Box_GyroKp.Text;
            Label_GyroKd.Text = Box_GyroKd.Text;
            Label_AngleGain.Text = Box_AngleGain.Text;
            Label_GyroGain.Text = Box_GyroGain.Text;
        }

        private void Label()
        {
            Box_AngleKp.Background = Brushes.White;
            Box_AngleKd.Background = Brushes.White;
            Box_GyroKp.Background = Brushes.White;
            Box_GyroKd.Background = Brushes.White;
            Box_AngleGain.Background = Brushes.White;
            Box_GyroGain.Background = Brushes.White;
            if (Label_AngleKp.Text != Box_AngleKp.Text)
            {
                Box_AngleKp.Background = Brushes.CornflowerBlue;
            }
            if (Label_AngleKd.Text != Box_AngleKd.Text)
            {
                Box_AngleKd.Background = Brushes.CornflowerBlue;
            }
            if (Label_GyroKp.Text != Box_GyroKp.Text)
            {
                Box_GyroKp.Background = Brushes.CornflowerBlue;
            }
            if (Label_GyroKd.Text != Box_GyroKd.Text)
            {
                Box_GyroKd.Background = Brushes.CornflowerBlue;
            }
            if (Label_AngleGain.Text != Box_AngleGain.Text)
            {
                Box_AngleGain.Background = Brushes.CornflowerBlue;
            }
            if (Label_GyroGain.Text != Box_GyroGain.Text)
            {
                Box_GyroGain.Background = Brushes.CornflowerBlue;
            }
        }

        static double deg2rad(double deg)
        {
            return (deg / 180) * Math.PI;
        }


    }
}