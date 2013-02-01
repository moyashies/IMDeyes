using System;
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
        int LeftStick_X, LeftStick_Y, RightStick_X, RightStick_Y, TT;
        bool Button_A, Button_B, Button_X, Button_Y,
            Button_Shiitake, Button_LB, DPad_L, DPad_D, DPad_U, DPad_R;
        GamePadState gamePadState = GamePad.GetState(PlayerIndex.One);
        StreamWriter writer;

        int SelectedLabels = 0;

        Thread datas;
        Thread connect;
        Thread write;

        int Frame = 0;

        private ObservableDataSource<System.Windows.Point>
            ACLX, ACLY, ACLZ, GYRX, GYRY, GYRZ, ANGX, ANGY, ANGZ, MOTOFRLB;

        public MainWindow()
        {
            InitializeComponent();

            ConnectPort = new SerialPort();

            Moyashi = new string[13];

            ACLX = new ObservableDataSource<System.Windows.Point>();
            ACLX.SetXYMapping(point => point);
            AcclGraph.AddLineGraph(ACLX, Colors.Blue, 1.0);
            ACLY = new ObservableDataSource<System.Windows.Point>();
            ACLY.SetXYMapping(point => point);
            AcclGraph.AddLineGraph(ACLY, Colors.Green, 1.0);
            ACLZ = new ObservableDataSource<System.Windows.Point>();
            ACLZ.SetXYMapping(point => point);
            AcclGraph.AddLineGraph(ACLZ, Colors.Red, 1.0);
            AcclGraph.Legend.Remove();

            GYRX = new ObservableDataSource<System.Windows.Point>();
            GYRX.SetXYMapping(point => point);
            GyroGraph.AddLineGraph(GYRX, Colors.Blue, 1.0);
            GYRY = new ObservableDataSource<System.Windows.Point>();
            GYRY.SetXYMapping(point => point);
            GyroGraph.AddLineGraph(GYRY, Colors.Green, 1.0);
            GYRZ = new ObservableDataSource<System.Windows.Point>();
            GYRZ.SetXYMapping(point => point);
            GyroGraph.AddLineGraph(GYRZ, Colors.Red, 1.0);
            GyroGraph.Legend.Remove();

            ANGX = new ObservableDataSource<System.Windows.Point>();
            ANGX.SetXYMapping(point => point);
            AngleGraph.AddLineGraph(ANGX, Colors.Blue, 1.0);
            ANGY = new ObservableDataSource<System.Windows.Point>();
            ANGY.SetXYMapping(point => point);
            AngleGraph.AddLineGraph(ANGY, Colors.Green, 1.0);
            ANGZ = new ObservableDataSource<System.Windows.Point>();
            ANGZ.SetXYMapping(point => point);
            AngleGraph.AddLineGraph(ANGZ, Colors.Red, 1.0);
            AngleGraph.Legend.Remove();


            MOTOFRLB = new ObservableDataSource<System.Windows.Point>();
            MOTOFRLB.SetXYMapping(point => point);
            MotoGraph.AddLineGraph(MOTOFRLB, Colors.Purple, 1.0);
            MotoGraph.Legend.Remove();

            AcceKp.Value = 4.0 / 4 * 10;
            AcceKd.Value = 2.0 / 4 * 10;
            GyroKp.Value = 12;
            GyroKd.Value = 6.0 / 4 * 10;
            AcceKp_Value.Text = AcceKp.Value.ToString();
            AcceKd_Value.Text = AcceKd.Value.ToString();
            GyroKp_Value.Text = GyroKp.Value.ToString();
            GyroKd_Value.Text = GyroKd.Value.ToString();

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 30;
            timer.Enabled = true;
            timer.Tick += new EventHandler(Control);

            System.Windows.Forms.Timer Write = new System.Windows.Forms.Timer();
            Write.Interval = 100;
            Write.Enabled = true;
            Write.Tick += new EventHandler(AsyncWrite);

            PortList = SerialPort.GetPortNames();

            StatusView.Text = "Controller:Disable\r\nPort:Disable";
            InfomationViewAdd("Component initialized");
            InfomationViewAdd("Press Button");
        }

        void AsyncWrite(object sender, EventArgs e)
        {
            try
            {
                write = new Thread(new ThreadStart(Write_Tick));
                write.Start();
            }
            catch (Exception ex)
            {
                InfomationViewAdd("Write Failed: " + ex.Message);
            }
        }

        void Write_Tick()
        {
            if (ConnectPort.IsOpen)
            {
                try
                {
                    ConnectPort.Write(Send);
                    writer.WriteLine("SEND:" + Send);
                    SerialViewAdd(Send);
                }
                catch (TimeoutException)
                {
                    if (ConnectPort.IsOpen)
                    {
                        InfomationViewAdd("Send Timeout");
                        DisConnect();
                    }
                }
                catch (Exception ex)
                {
                    InfomationViewAdd("Send error: \r\n" + ex.Message);
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
                    ItemCountRefresh();
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
                    ItemCountRefresh();
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
                    if (InfomationView.Items.Count > 100)
                    {
                        InfomationView.Items.RemoveAt(99);
                    }
                    ItemCountRefresh();
                }
            }));
        }

        void ItemCountRefresh()
        {
            DebugText.Text = SerialView.Items.Count.ToString() + "\r\n" +
                                 ReceiveView.Items.Count.ToString() + "\r\n" +
                                     InfomationView.Items.Count.ToString();
        }

        string Send;

        void Control(object sender, EventArgs e)
        {
            gamePadState = GamePad.GetState(PlayerIndex.One);
            LeftStick_X = (int)((gamePadState.ThumbSticks.Left.X + 1) * 127) + 1;
            LeftStick_Y = (int)((gamePadState.ThumbSticks.Left.Y + 1) * 127) + 1;
            RightStick_X = (int)((gamePadState.ThumbSticks.Right.X + 1) * 127) + 1;
            RightStick_Y = (int)((gamePadState.ThumbSticks.Right.Y + 1) * 127) + 1;
            TT = 255 - (int)((-gamePadState.Triggers.Left + gamePadState.Triggers.Right + 1) * 127);
            LSX.Value = (float)LeftStick_X / 255 * 100;
            LSY.Value = (float)LeftStick_Y / 255 * 100;
            RSX.Value = (float)RightStick_X / 255 * 100;
            RSY.Value = (float)RightStick_Y / 255 * 100;
            T.Value = (float)TT / 255 * 100;

            int DPadRange = 1;

            Button_A = this.gamePadState.Buttons.RightShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Button_B = this.gamePadState.Buttons.B == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Button_X = this.gamePadState.Buttons.X == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Button_Y = this.gamePadState.Buttons.Y == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            if (this.gamePadState.Buttons.A == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {

            }

            if (!Button_Shiitake && this.gamePadState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                connect = new Thread(new ThreadStart(TryToConnectAsync));
                connect.Start();
            }

            if (this.gamePadState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                StartS.Background = LSX.Foreground;
            }
            else
            {
                StartS.Background = LSX.Background;
            }
            if (this.gamePadState.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                UpS.Background = LSX.Foreground;
            }
            else
            {
                UpS.Background = LSX.Background;
            }
            if (this.gamePadState.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                DownS.Background = LSX.Foreground;
            }
            else
            {
                DownS.Background = LSX.Background;
            }
            if (this.gamePadState.Buttons.BigButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                ShiitakeS.Background = LSX.Foreground;
            }
            else
            {
                ShiitakeS.Background = LSX.Background;
            }

            if (this.gamePadState.Buttons.LeftShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                LBS.Background = LSX.Foreground;
            }
            else
            {
                LBS.Background = LSX.Background;
            }

            if (Button_A)
            {
                AS.Background = LSX.Foreground;
            }
            else
            {
                AS.Background = LSX.Background;
            }

            if (Button_Y)
            {
                YS.Background = LSX.Foreground;
            }
            else
            {
                YS.Background = LSX.Background;
            }

            if (Button_Y)
            {
                DPadRange = 10;
            }
            else
            {
                DPadRange = 1;
            }

            if (!DPad_U && this.gamePadState.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {

                switch (SelectedLabels)
                {
                    case 0:
                        AcceKp.Value = AcceKp.Value + DPadRange;
                        AcceKp_Value.Text = AcceKp.Value.ToString();
                        break;
                    case 1:
                        AcceKd.Value = AcceKd.Value + DPadRange;
                        AcceKd_Value.Text = AcceKd.Value.ToString();
                        break;
                    case 2:
                        GyroKp.Value = GyroKp.Value + DPadRange;
                        GyroKp_Value.Text = GyroKp.Value.ToString();
                        break;
                    case 3:
                        GyroKd.Value = GyroKd.Value + DPadRange;
                        GyroKd_Value.Text = GyroKd.Value.ToString();
                        break;
                }
            }

            if (!DPad_D && this.gamePadState.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                switch (SelectedLabels)
                {
                    case 0:
                        AcceKp.Value = AcceKp.Value - DPadRange;
                        AcceKp_Value.Text = AcceKp.Value.ToString();
                        break;
                    case 1:
                        AcceKd.Value = AcceKd.Value - DPadRange;
                        AcceKd_Value.Text = AcceKd.Value.ToString();
                        break;
                    case 2:
                        GyroKp.Value = GyroKp.Value - DPadRange;
                        GyroKp_Value.Text = GyroKp.Value.ToString();
                        break;
                    case 3:
                        GyroKd.Value = GyroKd.Value - DPadRange;
                        GyroKd_Value.Text = GyroKd.Value.ToString();
                        break;
                }
            }



            if (!Button_LB && this.gamePadState.Buttons.LeftShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                if (SelectedLabels != 3)
                {
                    SelectedLabels++;
                }
                else
                {
                    SelectedLabels = 0;
                }

                switch (SelectedLabels)
                {
                    case 0:
                        GyroKd.BorderThickness = new Thickness(0, 0, 0, 0);
                        AcceKp.BorderThickness = new Thickness(2, 2, 2, 2);
                        SelectLabel.Text = "AcceKp";
                        break;
                    case 1:
                        AcceKp.BorderThickness = new Thickness(0, 0, 0, 0);
                        AcceKd.BorderThickness = new Thickness(2, 2, 2, 2);
                        SelectLabel.Text = "AcceKd";
                        break;
                    case 2:
                        AcceKd.BorderThickness = new Thickness(0, 0, 0, 0);
                        GyroKp.BorderThickness = new Thickness(2, 2, 2, 2);
                        SelectLabel.Text = "GyroKp";
                        break;
                    case 3:
                        GyroKp.BorderThickness = new Thickness(0, 0, 0, 0);
                        GyroKd.BorderThickness = new Thickness(2, 2, 2, 2);
                        SelectLabel.Text = "GyroKd";
                        break;
                }
            }

            DPad_L = this.gamePadState.DPad.Left == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            DPad_D = this.gamePadState.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            DPad_U = this.gamePadState.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            DPad_R = this.gamePadState.DPad.Right == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Button_LB = this.gamePadState.Buttons.LeftShoulder == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            if (MValue.Length >= 4)
            {
                ML.Value = MValue[0];
                MR.Value = MValue[1];
                MF.Value = MValue[2];
                MB.Value = MValue[3];
                MotoL_Value.Text = MValue[0].ToString();
                MotoB_Value.Text = MValue[3].ToString();
                MotoR_Value.Text = MValue[1].ToString();
                MotoF_Value.Text = MValue[2].ToString();
                MValue = new int[0];
            }

            if (AValue.Length >= 9)
            {
                ACX.Value = AValue[0];
                ACY.Value = AValue[1];
                ACZ.Value = AValue[2];
                GYX.Value = AValue[3];
                GYY.Value = AValue[4];
                GYZ.Value = AValue[5];
                AGX.Value = AValue[6];
                AGY.Value = AValue[7];
                AGZ.Value = AValue[8];

                GraphRefresh();

                AcclX_Value.Text = AValue[0].ToString();
                AcclY_Value.Text = AValue[1].ToString();
                AcclZ_Value.Text = AValue[2].ToString();
                GyroX_Value.Text = AValue[3].ToString();
                GyroY_Value.Text = AValue[4].ToString();
                GyroZ_Value.Text = AValue[5].ToString();
                AngleX_Value.Text = AValue[6].ToString();
                AngleY_Value.Text = AValue[7].ToString();
                AngleZ_Value.Text = AValue[8].ToString();
                AValue = new int[0];
            }
            Button_Shiitake = this.gamePadState.Buttons.Start == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Send = "q" // initialize
                + "a" + RightStick_Y.ToString("000") // unused.
                + "b" + RightStick_X.ToString("000") // unused.
                + "c" + LeftStick_Y.ToString("000") // handle x
                + "d" + LeftStick_X.ToString("000") // handle y
                + "e" + TT.ToString("000")   // throttle
                + "f" + Convert.ToInt32(Button_A)  // enable switch

                // debug for PD :)
                + "h" + AcceKp.Value.ToString() // acce KP -
                + "i" + AcceKd.Value.ToString() // acce KP +
                + "j" + GyroKp.Value.ToString() // acce KD +
                + "k" + GyroKd.Value.ToString() // acce KD -
                + "r";
        }

        void GraphRefresh()
        {
            try
            {
                ACLX.AppendAsync(Dispatcher, new System.Windows.Point(Frame, ACX.Value));
                ACLY.AppendAsync(Dispatcher, new System.Windows.Point(Frame, ACY.Value));
                ACLZ.AppendAsync(Dispatcher, new System.Windows.Point(Frame, ACZ.Value));
                GYRX.AppendAsync(Dispatcher, new System.Windows.Point(Frame, GYX.Value));
                GYRY.AppendAsync(Dispatcher, new System.Windows.Point(Frame, GYY.Value));
                GYRZ.AppendAsync(Dispatcher, new System.Windows.Point(Frame, GYZ.Value));
                ANGX.AppendAsync(Dispatcher, new System.Windows.Point(Frame, AGX.Value));
                ANGY.AppendAsync(Dispatcher, new System.Windows.Point(Frame, AGY.Value));
                ANGZ.AppendAsync(Dispatcher, new System.Windows.Point(Frame, AGZ.Value));
                if (MR.Value != 18000)
                {
                    MOTOFRLB.AppendAsync(Dispatcher, new System.Windows.Point(MR.Value - ML.Value, MF.Value - MB.Value));
                }
                if (Frame > 200)
                {
                    ACLX.Collection.Remove(ACLX.Collection.First());
                    ACLY.Collection.Remove(ACLY.Collection.First());
                    ACLZ.Collection.Remove(ACLZ.Collection.First());
                    GYRX.Collection.Remove(GYRX.Collection.First());
                    GYRY.Collection.Remove(GYRY.Collection.First());
                    GYRZ.Collection.Remove(GYRZ.Collection.First());
                    ANGX.Collection.Remove(ANGX.Collection.First());
                    ANGY.Collection.Remove(ANGY.Collection.First());
                    ANGZ.Collection.Remove(ANGZ.Collection.First());
                }
                if (Frame > 20)
                {
                    MOTOFRLB.Collection.Remove(MOTOFRLB.Collection.First());
                }
                Frame++;
            }
            catch (System.StackOverflowException overFlowExcept)
            {
                InfomationViewAdd("StackOverflow: " + overFlowExcept.Message);
            }
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
                        AValue = new int[9];
                        for (int i = 0; i < 9; i++)
                        {
                            AValue[i] = int.Parse(Moyashi[i + 1]);
                        }
                    }
                    else if (Moyashi[0] == "M")
                    {
                        MValue = new int[13];
                        for (int i = 0; i < 4; i++)
                        {
                            MValue[i] = int.Parse(Moyashi[i + 1]);
                        }
                    }
                }
                catch
                {
                    InfomationViewAdd("Edit error\r\n" + ReceiveData);
                }

            }
            else
            {
                InfomationViewAdd("KUFC \r\n" + ReceiveData);
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
                    if (ConnectPort.IsOpen)
                    {
                        InfomationViewAdd("Receive Timeout");
                        DisConnect();
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        InfomationViewAdd("Receive error: \r\n " + ex.Message);
                    }));
                }
            }
        }

        private void TryToConnect(object sender, RoutedEventArgs e)
        {
            connect = new Thread(new ThreadStart(TryToConnectAsync));
            connect.Start();
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
                    InfomationViewAdd("DisConnected");
                    StatusView.Text = "Controller:Enable\r\nPort:Disable";
                    ConnectButton.Content = "Connect";
                }));
            }
            catch (Exception ex)
            {
                InfomationViewAdd("Failed:" + ex.Message);
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

                    InfomationViewAdd("Connecting  \r\nPort:" + ConnectPort.PortName + " \r\nBaudRate:" + ConnectPort.BaudRate.ToString());
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        ConnectButton.IsEnabled = false;
                    })); ;
                    ConnectPort.Open();
                    InfomationViewAdd("Connection established");

                    Encoding sjisEnc = Encoding.GetEncoding("Shift_JIS");

                    writer =
                        new StreamWriter(@"Log\IMDEYE" + DateTime.Today.Year.ToString() + "_" +
                        DateTime.Now.Month.ToString() + "_" +
                        DateTime.Now.Day.ToString() + "_" +
                        DateTime.Now.Hour.ToString() + "_" +
                        DateTime.Now.Minute.ToString() + "_" +
                        DateTime.Now.Second.ToString()
                            + ".txt", true, sjisEnc);

                    writer.WriteLine("CONNECTED " + ConnectPort.PortName + " " + ConnectPort.BaudRate.ToString());

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        StatusView.Text = "Controller:Enable\r\nPort:" + ConnectPort.PortName + "\r\nBaudRate:9600";
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
                    InfomationViewAdd("Failed:" + ex.Message);
                }
            }
            else
            {
                InfomationViewAdd("Port does not exist");
            }
        }
    }
}