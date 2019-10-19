using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace COM_BY_TCP.Comm
{
    class TCPServer : IDisposable
    {
        #region Fields
        //private const int _max_client = 10; //服务器程序允许的最大客户端连接数
        //private int _client_count = 0;  //当前的连接的客户端数
        private TcpListener _listener = null;   //服务器使用的异步TcpListener
        private List<TCPClientState> _clients;
        private bool disposed = false;
        #endregion

        #region Properties
        // 服务器是否正在运行
        public bool IsRunning { get; private set; }
        // 监听的IP地址
        public IPAddress Address { get; private set; }
        // 监听的端口
        public int Port { get; private set; }

        public List<TCPClientState> clients
        {
            get
            {
                return _clients;
            }
        }

        #endregion

        #region 构造函数
        public TCPServer(int listenPort)
            :this(IPAddress.Any, listenPort)
        {

        }

        public TCPServer(IPEndPoint localEP)
            : this(localEP.Address, localEP.Port)
        {
        }

        public TCPServer(IPAddress localIPAddress, int listenPort)
        {
            Address = localIPAddress;
            Port = listenPort;

            this._clients = new List<TCPClientState>();
 
            _listener = new TcpListener(Address, Port);
            //_listener.AllowNatTraversal(true);
        }
        #endregion

        #region Method
        // 启动服务器
        public bool Start()
        {
            if (!IsRunning)
            {
                try
                {
                    _listener.Start();
                    _listener.BeginAcceptTcpClient(
                      new AsyncCallback(HandleTcpClientAccepted), _listener);

                    IsRunning = true;

                    return true;
                }
                catch(SocketException e)
                {
                    Log.OutputLine("开启TCP服务端失败：" + e.Message);
                }

                return false;
            }

            return true;
        }

        public bool Start(int backlog)
        {
            if (!IsRunning)
            {
                try
                {
                    _listener.Start(backlog);
                    _listener.BeginAcceptTcpClient(
                      new AsyncCallback(HandleTcpClientAccepted), _listener);

                    IsRunning = true;

                    return true;
                }
                catch (SocketException e)
                {
                    Log.OutputLine("开启TCP服务端失败：" + e.Message);
                }

                return false;
            }

            return true;
        }

        public void Stop()
        {
            if (IsRunning)
            {
                IsRunning = false;
                //lock (this._clients)
                //{
                //    for (int i = 0; i < this._clients.Count; i++)
                //    {
                //        this._clients[i].Close();
                //    }
                //    this._clients.Clear();
                //}
                _listener.Stop();
                lock (this._clients)
                {
                    for (int i = 0; i < this._clients.Count; i++)
                    {
                        this._clients[i].TcpClient.Client.Disconnect(false);
                    }
                    this._clients.Clear();
                }
            }
        }

        // 处理客户端连接的函数
        private void HandleTcpClientAccepted(IAsyncResult ar)
        {
            if (IsRunning)
            {
                //TcpListener tcpListener = (TcpListener)ar.AsyncState;
                TcpClient client = _listener.EndAcceptTcpClient(ar);
                client.NoDelay = true;

                byte[] buffer = new byte[client.ReceiveBufferSize];

                TCPClientState state = new TCPClientState(client, buffer);
                lock (this._clients)
                {
                    this._clients.Add(state);
                    RaiseClientConnected(state);
                }

                NetworkStream stream = state.NetworkStream;
                //开始异步读取数据
                stream.BeginRead(state.Buffer, 0, state.Buffer.Length, HandleDataReceived, state);

                _listener.BeginAcceptTcpClient(
                  new AsyncCallback(HandleTcpClientAccepted), ar.AsyncState);
            }
        }

        // 数据接受回调函数
        private void HandleDataReceived(IAsyncResult ar)
        {
            if (IsRunning)
            {
                TCPClientState state = (TCPClientState)ar.AsyncState;
                if(state.TcpClient == null || !state.TcpClient.Connected)
                {
                    return;
                }
                NetworkStream stream = state.NetworkStream;

                int receivedBytes = 0;
                try
                {
                    receivedBytes = stream.EndRead(ar);
                }
                catch
                {
                    receivedBytes = 0;
                }

                if (receivedBytes == 0)
                {
                    // connection has been closed
                    lock (this._clients)
                    {
                        this._clients.Remove(state);
                        //触发客户端连接断开事件
                        RaiseClientDisconnected(state);

                        //Log.OutputLine("连接断开");
                        state.Close();
                
                        return;
                    }
                }

                // received byte and trigger event notification
                byte[] buff = new byte[receivedBytes];
                Buffer.BlockCopy(state.Buffer, 0, buff, 0, receivedBytes);
                //触发数据收到事件
                RaiseDataReceived(state, buff);

                try
                {
                    if (state.TcpClient.Connected)
                    {
                        // continue listening for tcp datagram packets
                        stream.BeginRead(state.Buffer, 0, state.Buffer.Length, HandleDataReceived, state);
                    }
                }
                catch
                {

                }
            }
        }

        // 发送数据
        public void SyncSend(TCPClientState state, byte[] data)
        {
            TcpClient client = state.TcpClient;

            if (!IsRunning)
                throw new InvalidProgramException("This TCP Scoket server has not been started.");

            if (client == null)
                throw new ArgumentNullException("client");

            if (data == null)
                throw new ArgumentNullException("data");

            if(client.GetStream().CanWrite)
            {
                client.GetStream().Write(data, 0, data.Length);
            }
        }

        public void AsyncSend(TCPClientState state, byte[] data)
        {
            RaisePrepareSend(state);

            TcpClient client = state.TcpClient;

            if (!IsRunning)
                throw new InvalidProgramException("This TCP Scoket server has not been started.");

            if (client == null)
                throw new ArgumentNullException("client");

            if (data == null)
                throw new ArgumentNullException("data");

            try
            {
                if(client != null)
                {
                    client.GetStream().BeginWrite(data, 0, data.Length, SendDataEnd, state);
                }
            }
            catch (Exception ex)
            {
                Log.OutputLine("AsyncSend 发生错误:{0}", ex.Message);
            }
        }

        // 发送数据完成处理函数
        private void SendDataEnd(IAsyncResult ar)
        {
            TcpClient client = ((TCPClientState)ar.AsyncState).TcpClient;
            try
            {
                if (client.Connected)
                {
                    client.GetStream().EndWrite(ar);
                    RaiseCompletedSend((TCPClientState)ar.AsyncState);
                }
            }
            catch(Exception ex)
            {
                Log.OutputLine("SendDataEnd 发生错误:{0}", ex.Message);
            }
        }
        #endregion

        #region 事件

        /// <summary>
        /// 与客户端的连接已建立事件
        /// </summary>
        public event EventHandler<TcpClientConnectedEventArgs> ClientConnected;
        /// <summary>
        /// 与客户端的连接已断开事件
        /// </summary>
        public event EventHandler<TcpClientDisconnectedEventArgs> ClientDisconnected;

        
        /// <summary>
        /// 触发客户端连接事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseClientConnected(TCPClientState state)
        {
            if (ClientConnected != null)
            {
                ClientConnected(this, new TcpClientConnectedEventArgs(state));
            }
        }
        /// <summary>
        /// 触发客户端连接断开事件
        /// </summary>
        /// <param name="client"></param>
        private void RaiseClientDisconnected(TCPClientState state)
        {
            if (ClientDisconnected != null)
            {
                ClientDisconnected(this, new TcpClientDisconnectedEventArgs(state));
            }
        }

        /// <summary>
        /// 接收到数据事件
        /// </summary>
        public event EventHandler<DatagramReceivedEventArgs> DatagramReceived;

        private void RaiseDataReceived(TCPClientState state, byte[] datagram)
        {
            if (DatagramReceived != null)
            {
                DatagramReceived(this, new DatagramReceivedEventArgs(state, datagram));
            }
        }

        /// <summary>
        /// 发送数据前的事件
        /// </summary>
        public event EventHandler PrepareSend;

        /// <summary>
        /// 触发发送数据前的事件
        /// </summary>
        /// <param name="state"></param>
        private void RaisePrepareSend(TCPClientState state)
        {
            if (PrepareSend != null)
            {
                PrepareSend(this, new TCPEventArgs(state));
            }
        }

        /// <summary>
        /// 数据发送完毕事件
        /// </summary>
        public event EventHandler CompletedSend;

        /// <summary>
        /// 触发数据发送完毕的事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseCompletedSend(TCPClientState state)
        {
            if (CompletedSend != null)
            {
                CompletedSend(this, new TCPEventArgs(state));
            }
        }

        /// <summary>
        /// 网络错误事件
        /// </summary>
        public event EventHandler NetError;
        /// <summary>
        /// 触发网络错误事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseNetError(TCPClientState state)
        {
            if (NetError != null)
            {
                NetError(this, new TCPEventArgs(state));
            }
        }

        /// <summary>
        /// 异常事件
        /// </summary>
        public event EventHandler OtherException;
        /// <summary>
        /// 触发异常事件
        /// </summary>
        /// <param name="state"></param>
        private void RaiseOtherException(TCPClientState state, string descrip)
        {
            if (OtherException != null)
            {
                OtherException(this, new TCPEventArgs(state, descrip));
            }
        }
        private void RaiseOtherException(TCPClientState state)
        {
            RaiseOtherException(state, "");
        }

        #endregion

        #region Close
        /// <summary>
        /// 关闭一个与客户端之间的会话
        /// </summary>
        /// <param name="state">需要关闭的客户端会话对象</param>
        public void Close(TCPClientState state)
        {
            if (state != null)
            {
                state.Close();

                lock (this._clients)
                {
                    this._clients.Remove(state);
                }
                //TODO 触发关闭事件
            }
        }

        public void CloseAll()
        {
            lock (this._clients)
            {
                foreach(TCPClientState client in this._clients)
                {
                    client.Close();
                }
                this._clients.Clear();
            }
        }
        
        #endregion

        #region 释放
        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
 
        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release 
        /// both managed and unmanaged resources; <c>false</c> 
        /// to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Stop();
                        if (_listener != null)
                        {
                            _listener = null;
                        }
                    }
                    catch (SocketException)
                    {
                        //TODO
                        //RaiseOtherException(null);
                    }
                }
                disposed = true;
            }
        }
        #endregion
    }

    public class TCPClientState
    {
        /// <summary>
        /// 与客户端相关的TcpClient
        /// </summary>
        public TcpClient TcpClient { get; private set; }

        /// <summary>
        /// 获取缓冲区
        /// </summary>
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// 获取网络流
        /// </summary>
        public NetworkStream NetworkStream
        {
            get { return TcpClient.GetStream(); }
        }

        public TCPClientState(TcpClient tcpClient, byte[] buffer)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            this.TcpClient = tcpClient;
            this.Buffer = buffer;
        }
        /// <summary>
        /// 关闭
        /// </summary>
        public void Close()
        {
            //关闭数据的接受和发送
            if (TcpClient != null)
            {
                TcpClient.Close();
                TcpClient = null;
            }
            
            Buffer = null;
        }
    }

    public class TCPEventArgs : EventArgs
    {
        /// <summary>
        /// 提示信息
        /// </summary>
        public string _msg;

        /// <summary>
        /// 客户端状态封装类
        /// </summary>
        public TCPClientState _state;

        /// <summary>
        /// 是否已经处理过了
        /// </summary>
        public bool IsHandled { get; set; }

        public TCPEventArgs(string msg)
        {
            this._msg = msg;
            IsHandled = false;
        }

        public TCPEventArgs(TCPClientState state)
        {
            this._state = state;
            IsHandled = false;
        }

        public TCPEventArgs(TCPClientState state, string msg)
        {
            this._state = state;
            this._msg = msg;
            IsHandled = false;
        }
    }

    public class DatagramReceivedEventArgs : TCPEventArgs
    {
        public byte[] _datagram;

        public DatagramReceivedEventArgs(TCPClientState state, string msg, byte[] datagram)
            : base(state, msg)
        {
            _datagram = datagram;
        }

        public DatagramReceivedEventArgs(TCPClientState state, byte[] datagram)
            : base(state)
        {
            _datagram = datagram;
        }
    }

    public class TcpClientConnectedEventArgs : EventArgs
    {
        public TCPClientState State { get; set; }
        public TcpClientConnectedEventArgs(TCPClientState state)
        {
            this.State = state;
        }
    }

    public class TcpClientDisconnectedEventArgs : EventArgs
    {
        public TCPClientState State { get; set; }
        public TcpClientDisconnectedEventArgs(TCPClientState state)
        {
            this.State = state;
        }
    }

    public class TcpDatagramReceivedEventArgs<T> : EventArgs
    {
        public TCPClientState State { get; set; }
        public T Datagram { set; get; }
        public TcpDatagramReceivedEventArgs(TCPClientState state, T datagram)
        {
            this.State = state;
            this.Datagram = datagram;
        }
    }
}
