using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Net;

namespace COM_BY_TCP.Comm
{
    class CommService
    {
        private static readonly CommService _instance = new CommService();

        public static CommService GetInstance()
        {
            return _instance;
        }

        private SerialPort serialPort = null;
        private TCPServer tcpServer = null;

        private TCPClientState currClientState = null;

        public int _recvCount = 0;
        public int _sendCount = 0;

        public EventHandler<CommDataCountEventArgs> recvCountChanged = null;
        public EventHandler<CommDataCountEventArgs> sendCountChanged = null;
        public EventHandler tcpClientConnected = null;
        public EventHandler tcpClientDisconnected = null;

        public int recvCount
        {
            get
            {
                return _recvCount;
            }
        }

        public int sendCount
        {
            get
            {
                return _sendCount;
            }
        }

        public bool tcpServerIsRunning
        {
            get
            {
                return this.tcpServer != null && this.tcpServer.IsRunning;
            }
            
        }

        private CommService()
        {
            this.serialPort = new SerialPort();
            this.serialPort.WriteTimeout = 100;
            this.serialPort.DataReceived += serialPort_DataReceived;
        }

        public bool OpenSerialPort(String portName, int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
            this.serialPort.PortName = portName;
            this.serialPort.BaudRate = baudRate;
            this.serialPort.DataBits = dataBits;
            this.serialPort.Parity = parity;
            this.serialPort.StopBits = stopBits;

            if(this.serialPort.IsOpen)
            {
                this.serialPort.Close();
            }

            try
            {
                this.serialPort.Open();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("打开端口错误：{0}", ex.Message);
                return false;
            }
        }

        public void CloseSerialPort()
        {
            if(this.serialPort.IsOpen)
            {
                this.serialPort.Close();
            }
        }

        private void SendDataToSerial(byte[] data)
        {
            if (this.serialPort.IsOpen)
            {
                try
                {
                    this.serialPort.Write(data, 0, data.Length);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public bool StartTCPServer(IPAddress localIPAddress, int listenPort)
        {
            tcpServer = new TCPServer(localIPAddress, listenPort);
            tcpServer.Start();

            if(tcpServer.IsRunning)
            {
                tcpServer.ClientConnected += tcpServer_ClientConnected;
                tcpServer.ClientDisconnected += tcpServer_ClientDisconnected;
                tcpServer.DatagramReceived += tcpServer_DatagramReceived;
            }

            return tcpServer.IsRunning;
        }

        
        
        public void StopTCPServer()
        {
            if(tcpServer != null)
            {
                tcpServer.Stop();
            }
            
            currClientState = null;
        }

        public void ResetRecvCount()
        {
            this._recvCount = 0;
        }

        public void ResetSendCount()
        {
            this._sendCount = 0;
        }

        public void ResetAllCount()
        {
            ResetRecvCount();
            ResetSendCount();
        }

        private void tcpServer_ClientConnected(object sender, TcpClientConnectedEventArgs e)
        {
            if (currClientState == null)
            {
                currClientState = e.State;
                if(tcpClientConnected != null)
                {
                    tcpClientConnected(this, EventArgs.Empty);
                }
            }
        }

        private void tcpServer_DatagramReceived(object sender, DatagramReceivedEventArgs e)
        {
            if(e._state == currClientState)
            {
                SendDataToSerial(e._datagram);
                _recvCount += e._datagram.Length;
                if(recvCountChanged != null)
                {
                    recvCountChanged(this, new CommDataCountEventArgs(_recvCount));
                }
            }
        }

        private void tcpServer_ClientDisconnected(object sender, TcpClientDisconnectedEventArgs e)
        {
            if(e.State == currClientState)
            {
                currClientState = null;
                if (tcpClientDisconnected != null)
                {
                    tcpClientDisconnected(this, EventArgs.Empty);
                }
            }
        }


        public void StopService()
        {
            CloseSerialPort();
            StopTCPServer();
        }

        public bool serialIsOpen
        {
            get
            {
                return this.serialPort.IsOpen;
            }
        }

        public String PortName
        {
            get
            {
                return this.serialPort.PortName;
            }
        }

        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            System.IO.Ports.SerialPort sp = (System.IO.Ports.SerialPort)sender;
            if (sp.BytesToRead > 0 && sp.IsOpen)
            {
                try
                {
                    int count = sp.BytesToRead;
                    if (count > 0)
                    {
                        //Console.WriteLine("Recv线程id=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
                        byte[] buffer = new byte[count];
                        sp.Read(buffer, 0, count);

                        _sendCount += buffer.Length;                        

                        if(tcpServer != null && tcpServer.IsRunning && currClientState != null)
                        {
                            tcpServer.SyncSend(currClientState, buffer);

                            if (sendCountChanged != null)
                            {
                                sendCountChanged(this, new CommDataCountEventArgs(_sendCount));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }

    public class CommDataCountEventArgs : EventArgs
    {
        public int count
        {
            get;
            private set;
        }

        public CommDataCountEventArgs(int count_)
        {
            this.count = count_;
        }
    }
}
