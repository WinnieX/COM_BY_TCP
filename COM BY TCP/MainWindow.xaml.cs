using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.Net;
using COM_BY_TCP.Comm;

namespace COM_BY_TCP
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        CommService commSvc = CommService.GetInstance();
        //enum
        public MainWindow()
        {
            InitializeComponent();
            InitUI();
        }

        void InitUI()
        {
            int[] baudRates = new int[] { 
                110, 300, 600, 
                1200, 2400, 4800, 9600, 
                14400, 19200, 38400, 56000, 57600, 
                115200, 128000, 256000,
            };
            Dictionary<String, int> baudRateDict = new Dictionary<String, int>();
            foreach (int rate in baudRates)
            {
                baudRateDict[rate.ToString()] = rate;
            }

            cmbBaudRates.ItemsSource = baudRateDict;
            cmbBaudRates.SelectedValuePath = "Value";
            cmbBaudRates.DisplayMemberPath = "Key";
            cmbBaudRates.SelectedValue = 57600;

            int[] dataBits = new int[] { 
                5, 6, 7, 8,
            };
            Dictionary<String, int> dataBitsDict = new Dictionary<String, int>();
            foreach (int bits in dataBits)
            {
                dataBitsDict[bits.ToString()] = bits;
            }

            cmbDataBits.ItemsSource = dataBitsDict;
            cmbDataBits.SelectedValuePath = "Value";
            cmbDataBits.DisplayMemberPath = "Key";
            cmbDataBits.SelectedValue = 8;

            Dictionary<String, Parity> parityDict = new Dictionary<string, Parity>()
            {
                {"无校验", Parity.None},
                {"奇校验", Parity.Odd},
                {"偶校验", Parity.Even},
                {"1 校验", Parity.Mark},
                {"0 校验", Parity.Space},
            };

            cmbParitys.ItemsSource = parityDict;
            cmbParitys.SelectedValuePath = "Value";
            cmbParitys.DisplayMemberPath = "Key";
            cmbParitys.SelectedValue = Parity.None;

            Dictionary<String, StopBits> stopBitsDict = new Dictionary<string, StopBits>()
            {
                {"1 位", StopBits.One},
                {"1.5 位", StopBits.OnePointFive},
                {"2 位", StopBits.Two},
            };

            cmbStopBits.ItemsSource = stopBitsDict;
            cmbStopBits.SelectedValuePath = "Value";
            cmbStopBits.DisplayMemberPath = "Key";
            cmbStopBits.SelectedValue = StopBits.One;

            String[] ports = SerialPort.GetPortNames();
            if (ports != null && ports.Length > 0)
            {
                Dictionary<String, String> portsDict = new Dictionary<String, String>();
                foreach (String port in ports)
                {
                    portsDict[port] = port;
                }

                cmbPortNames.ItemsSource = portsDict;
                cmbPortNames.SelectedValuePath = "Value";
                cmbPortNames.DisplayMemberPath = "Key";
                cmbPortNames.SelectedValue = ports[0];
            }
            else
            {
                cmbPortNames.IsEnabled = false;
                btnOpen.IsEnabled = false;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Init();
        }

        private void Init()
        {
            commSvc.tcpClientConnected += tcpServer_tcpClientConnected;
            commSvc.tcpClientDisconnected += tcpServer_tcpClientDisconnected;
            commSvc.recvCountChanged += tcpServer_recvCountChanged;
            commSvc.sendCountChanged += tcpServer_sendCountChanged;
        }

        private void tcpServer_tcpClientConnected(object sender, EventArgs e)
        {
            //this.txtTCPStatus.Text = "TCP服务端接受TCP客户端连接";
            Action<bool> statusChanged = new Action<bool>(connectedStatusChanged);
            this.Dispatcher.BeginInvoke(statusChanged, true);
        }

        private void tcpServer_tcpClientDisconnected(object sender, EventArgs e)
        {
            //this.txtTCPStatus.Text = "TCP客户端断开连接";
            Action<bool> statusChanged = new Action<bool>(connectedStatusChanged);
            this.Dispatcher.BeginInvoke(statusChanged, false);
        }

        private void connectedStatusChanged(bool isConnect)
        {
            if(isConnect)
            {
                this.txtTCPStatus.Text = "接受TCP客户端连接";
            }
            else
            {
                this.txtTCPStatus.Text = "TCP客户端断开连接";
            }
        }

        private void tcpServer_recvCountChanged(object sender, CommDataCountEventArgs e)
        {
            Action<int> countChanged = new Action<int>(recvCountChanged);
            this.Dispatcher.BeginInvoke(countChanged, e.count);
        }

        private void tcpServer_sendCountChanged(object sender, CommDataCountEventArgs e)
        {
            Action<int> countChanged = new Action<int>(sendCountChanged);
            this.Dispatcher.BeginInvoke(countChanged, e.count);
        }

        private void recvCountChanged(int recvCount)
        {
            this.txtRecvCount.Text = recvCount.ToString();
        }

        private void sendCountChanged(int sendCount)
        {
            this.txtSendCount.Text = sendCount.ToString();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            commSvc.StopService();
        }        

        private void btn_Click(object sender, RoutedEventArgs e)
        {
            if (sender.Equals(this.btnOpen))
            {
                OpenSerialPort();
            }
            else if (sender.Equals(this.btnStartTCPServer))
            {
                StartTCPServer();
            }
            else if (sender.Equals(this.btnResetRecv))
            {
                ResetRecvCount();
            }
            else if (sender.Equals(this.btnResetSend))
            {
                ResetSendCount();
            }
            else if (sender.Equals(this.btnResetAll))
            {
                ResetAllCount();
            }
        }

        private void OpenSerialPort()
        {
            if(commSvc.serialIsOpen)
            {
                this.commSvc.CloseSerialPort();
                this.btnOpen.Content = "打开串口";
                this.txtSerialStatus.Text = commSvc.PortName + "已经关闭";
                groupSerialProperties.IsEnabled = false;
            }
            else
            {
                String portName = (String)this.cmbPortNames.SelectedValue;
                int baudRate = (int)this.cmbBaudRates.SelectedValue;
                int dataBits = (int)this.cmbDataBits.SelectedValue;
                Parity parity = (Parity)this.cmbParitys.SelectedValue;
                StopBits stopBits = (StopBits)this.cmbStopBits.SelectedValue;

                //Console.WriteLine(portName, );
                if(commSvc.OpenSerialPort(portName, baudRate, dataBits, parity, stopBits))
                {
                    this.btnOpen.Content = "关闭串口";
                    this.txtSerialStatus.Text = commSvc.PortName + "打开成功";
                }
                else
                {
                    this.txtSerialStatus.Text = commSvc.PortName + "打开失败！";
                }
                groupSerialProperties.IsEnabled = !this.commSvc.serialIsOpen;
            }
        }

        private void StartTCPServer()
        {
            if(!commSvc.tcpServerIsRunning)
            {
                IPAddress listenIPAddr;
                if (IPAddress.TryParse(this.txtIPAddr.Text, out listenIPAddr))
                {
                    short port = 0;
                    if (short.TryParse(this.txtPort.Text, out port))
                    {
                        if (commSvc.StartTCPServer(listenIPAddr, port))
                        {
                            this.btnStartTCPServer.Content = "关闭TCP服务";
                            this.txtTCPStatus.Text = "TCP服务端已经开启";
                            this.groupTCPServer.IsEnabled = false;
                        }
                        else
                        {
                            this.txtTCPStatus.Text = "启动TCP服务端失败";
                        }
                    }
                    else
                    {
                        MessageBox.Show("端口号输入有误！");
                    }
                }
                else
                {
                    MessageBox.Show("IP地址输入有误！");
                }
            }
            else
            {
                commSvc.StopTCPServer();
                this.btnStartTCPServer.Content = "开启TCP服务";
                this.txtTCPStatus.Text = "TCP服务端已经关闭";
                this.groupTCPServer.IsEnabled = true;
            }
        }

        private void ResetRecvCount()
        {
            commSvc.ResetRecvCount();
            recvCountChanged(commSvc.recvCount);
        }

        private void ResetSendCount()
        {
            commSvc.ResetSendCount();
            sendCountChanged(commSvc.sendCount);
        }

        private void ResetAllCount()
        {
            commSvc.ResetAllCount();
            recvCountChanged(commSvc.recvCount);
            sendCountChanged(commSvc.sendCount);
        }
    }
}
