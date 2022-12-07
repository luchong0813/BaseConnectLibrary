using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Device.Extension.Connect
{
    public class UdpConnect:BaseConnect {
        private Socket sConn;
        private int listenPort;
        private IPEndPoint remoteEP;
        private IPEndPoint localIpAddress;
        private bool isRun;
        private int receiveBuffSize = 1024;
        private AutoResetEvent receiveEvent = new AutoResetEvent(false);

        /// <summary>
        /// Udp连接对象
        /// </summary>
        public UdpConnect()
        {
        }

        /// <summary>
        /// Udp连接对象
        /// </summary>
        public UdpConnect(string remoteIp,int remotePort,int localPort=0)
        {
            this.listenPort = localPort;
            remoteEP = new IPEndPoint(IPAddress.Parse(remoteIp),remotePort);
        }

        /// <summary>
        /// 本地使用的端口
        /// </summary>
        public int ListenPort {
            get {
                return listenPort;
            }
        }

        /// <summary>
        /// 打开连接
        /// </summary>
        /// <returns></returns>
        public override bool OpenConnect() {
            bool flag = false;
            localIpAddress = new IPEndPoint(IPAddress.Any,listenPort);
            try {
                this.CloseConnect();
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = (IOC_IN | IOC_VENDOR | 12);
                this.sConn = new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
                this.sConn.IOControl((int)SIO_UDP_CONNRESET,new byte[] { Convert.ToByte(false) },null);
                this.sConn.Bind(localIpAddress);//绑定本地监听端口
                if (remoteEP.Address.Equals(IPAddress.Broadcast))
                {
                    this.sConn.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                }
                flag = true;
                localIpAddress = (IPEndPoint)this.sConn.LocalEndPoint;
                this.Connected = true;
            }
            catch(Exception ex) {
                this.FireLogMessage("Udp:{0} 创建异常：{1}",localIpAddress,ex);
                this.Connected = false;
            }
            this.isRun = true;
            this.StartReceive();
            return flag;
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public override void CloseConnect() {
            try {
                if(this.sConn != null) {
                    this.isRun = false;
                    this.sConn.Close();
                    this.sConn = null;
                }
            }
            catch(Exception ex) {
                this.FireLogMessage("Udp:{0} 关闭异常：{1}",this.localIpAddress,ex);
            }
            finally
            {
                this.Connected = false;
            }
        }

        /// <summary>
        /// 开始启动接收线程 作为服务端需要调用，作为客户端不需要调用
        /// </summary>
        public override void StartReceive() {
            ThreadPool.QueueUserWorkItem(delegate {
                byte[] receiveData = new byte[receiveBuffSize];
                while(this.isRun) {
                    try {
                        EndPoint endPoint = new IPEndPoint(IPAddress.Any,0);
                        int num = this.sConn.ReceiveFrom(receiveData,ref endPoint);
                        if(num > 0 && (endPoint.ToString().Equals(this.remoteEP.ToString()) || this.remoteEP.Address.ToString().Equals("255.255.255.255"))) { 
                        
                            byte[] tempBuff = new byte[num];
                            Buffer.BlockCopy(receiveData,0,tempBuff,0,num);
                            this.FireOnDataReceive(endPoint,tempBuff);
                        }
                    }
                    catch(Exception ) {
                        this.FireLogMessage("Udp连接关闭");
                    }
                }
                receiveEvent.Set();
            });
        }

        /// <summary>
        /// 发送数据到服务端
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        public override bool Send(byte[] datas) {
            lock(this) {
                if(this.remoteEP == null) {
                    this.FireLogMessage("Udp:{0} 发送数据时发现未指定发送地址",this.localIpAddress);
                    return false;
                }
                this.sConn.SendTo(datas,remoteEP);
                return true;
            }
        }

        public bool SendTo(byte[] datas,IPEndPoint remoteEp) {
            if(remoteEP == null) {
                this.FireLogMessage("Udp:{0} 发送数据时发现未指定发送地址",this.localIpAddress);
                return false;
            }
            this.sConn.SendTo(datas,remoteEp);
            return true;
        }

        /// <summary>
        /// 接收数据缓冲区大小
        /// </summary>
        public int ReceiveBuffSize {
            get {
                return receiveBuffSize;
            }
            set {
                receiveBuffSize = value;
            }
        }

        public override string Address {
            get { return this.remoteEP.ToString(); }
        }

    }
}
