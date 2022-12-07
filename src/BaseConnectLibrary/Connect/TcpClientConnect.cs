using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace Device.Extension.Connect
{
    public class TcpClientConnect : BaseConnect
    {
        private Socket sConn;
        private int listenPort;
        private IPEndPoint remoteEP;
        private bool isRun;
        private bool keepHeartbeat;
        private int heartbeatInterval = 1000;
        private int reconnectInterval = 3000;
        private int receiveBuffSize = 1024;
        private byte[] heartbeatBytes = new byte[0];
        private AutoResetEvent receiveEvent = new AutoResetEvent(false);
        private AutoResetEvent heartbeatEvent = new AutoResetEvent(false);
        private AutoResetEvent reconnectEvent = new AutoResetEvent(false);
        public TcpClientConnect()
        {
        }

        /// <summary>
        /// TCP客户端连接对象
        /// </summary>
        public TcpClientConnect(string remoteIp, int remotePort, int localPort = 0)
        {
            remoteEP = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
            this.listenPort = localPort;
        }

        /// <summary>
        /// 本地使用的端口
        /// </summary>
        public int ListenPort
        {
            get
            {
                return listenPort;
            }
        }

        /// <summary>
        /// 打开连接
        /// </summary>
        /// <returns></returns>
        public override bool OpenConnect()
        {
            this.isRun = true;
            this.StartReceive();
            //启动后检查一次状态
            //ThreadPool.RegisterWaitForSingleObject(
            //    new AutoResetEvent(false),
            //    (state,timeout) => {
            //        if (this.isRun && !this.Connected) {
            //            this.FireOnConnectStatusChanged(this.Connected);
            //        }
            //    },
            //    null,
            //    3000,
            //    true);
            return true;
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public override void CloseConnect()
        {
            try
            {
                if (this.sConn != null)
                {
                    this.isRun = false;
                    if (this.sConn.Connected)
                    {
                        this.sConn.Disconnect(false);
                        receiveEvent.WaitOne(500);
                    }
                    heartbeatEvent.Set();
                    reconnectEvent.Set();
                    this.sConn.Close();
                    this.sConn = null;
                }
            }
            catch (Exception ex)
            {
                this.FireLogMessage("TCP服务器关闭连接异常：{0}", ex);
            }
            finally
            {
                this.Connected = false;
            }
        }

        /// <summary>
        /// 开始启动接收线程
        /// </summary>
        public override void StartReceive()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                byte[] receiveData = new byte[receiveBuffSize];
                while (this.isRun)
                {
                    try
                    {
                        if (receiveData.Length != receiveBuffSize)
                        {
                            receiveData = new byte[receiveBuffSize];
                        }
                        if (this.isRun && !this.Connected)
                        {
                            this.StartReConnect();
                            continue;
                        }
                        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                        int num = this.sConn.ReceiveFrom(receiveData, ref endPoint);
                        if (num <= 0)
                        {
                            this.Connected = false;
                            this.FireLogMessage("与TCP服务器连接断开");
                        }
                        else
                        {
                            byte[] tempBuff = new byte[num];
                            Buffer.BlockCopy(receiveData, 0, tempBuff, 0, num);
                            this.FireOnDataReceive(endPoint, tempBuff);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.FireLogMessage("从TCP服务器接收数据异常：{0}", ex.Message);
                        this.Connected = false;
                    }
                }
                receiveEvent.Set();
            });
        }

        /// <summary>
        /// 启动断线重连
        /// </summary>
        private void StartReConnect()
        {
            this.FireLogMessage("开始连接TCP服务器");
            if (this.sConn != null && this.sConn.Connected)
            {
                this.sConn.Disconnect(false);
                this.sConn.Close();
            }
            IPEndPoint localIpAddress = new IPEndPoint(IPAddress.Any, listenPort);
            this.sConn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.sConn.SendTimeout = 3000;
            this.sConn.Bind(localIpAddress);//绑定本地监听端口
            int num = 1;
            while (this.isRun)
            {
                try
                {
                    if (CheckNetState(remoteEP.Address.ToString()))
                    {
                        this.sConn.Connect(remoteEP);
                        this.Connected = true;
                        return;
                    }
                }
                catch (Exception ex)
                {

                    this.FireRuntimeLogMessage("与TCP服务器第{0}重连失败:{1}", num, ex.Message);
                }
                num++;
                reconnectEvent.WaitOne(reconnectInterval);
            }
        }

        /// <summary>
        /// 检测设备IP是否在线
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        private bool CheckNetState(string ipAddress)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingOptions pingOptions = new PingOptions();
                    byte[] bytes = new byte[8] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    PingReply pingReply = ping.Send(ipAddress, 700, bytes, pingOptions);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }



        /// <summary>
        /// 启动心跳线程
        /// </summary>
        public void StartHeartbeat()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                while (this.isRun && this.keepHeartbeat)
                {
                    this.Send(heartbeatBytes);
                    heartbeatEvent.WaitOne(heartbeatInterval); //心跳时间间隔
                }
            });
        }

        /// <summary>
        /// 发送数据到服务端
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        public override bool Send(byte[] datas)
        {
            lock (this)
            {
                if (!this.Connected || !this.isRun)
                {
                    return false;
                }
                try
                {
                    this.sConn.Send(datas);
                    return true;
                }
                catch (Exception e)
                {
                    this.FireLogMessage("Tcp发送消息异常:{0}", e);
                    if (this.sConn.Connected)
                    {
                        this.sConn.Disconnect(false);
                    }
                    this.Connected = false;
                }
                return false;
            }
        }

        /// <summary>
        /// 是否保持心跳
        /// </summary>
        public bool KeepHeartbeat
        {
            get
            {
                return keepHeartbeat;
            }
            set
            {
                keepHeartbeat = value;
            }
        }

        /// <summary>
        /// 心跳数据
        /// </summary>
        public byte[] HeartbeatBytes
        {
            get
            {
                return heartbeatBytes;
            }
            set
            {
                heartbeatBytes = value;
            }
        }

        /// <summary>
        /// 心跳时间间隔 毫秒
        /// </summary>
        public int HeartbeatInterval
        {
            get
            {
                return heartbeatInterval;
            }
            set
            {
                heartbeatInterval = value;
            }
        }

        /// <summary>
        /// 接收数据缓冲区大小
        /// </summary>
        public int ReceiveBuffSize
        {
            get
            {
                return receiveBuffSize;
            }
            set
            {
                receiveBuffSize = value;
            }
        }

        /// <summary>
        /// 断线重连的时间间隔 毫秒
        /// </summary>
        public int ReconnectInterval
        {
            get
            {
                return reconnectInterval;
            }
            set
            {
                reconnectInterval = value;
            }
        }

        public override string Address
        {
            get { return this.remoteEP.ToString(); }
        }

    }
}
