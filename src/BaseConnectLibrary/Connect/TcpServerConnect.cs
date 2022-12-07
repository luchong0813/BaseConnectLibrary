using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Device.Extension.Connect
{
    public class TcpServerConnect:BaseConnect {
        private Socket sConn;
        private int listenPort;
        private bool isRun;
        private int receiveBuffSize = 1024;
        private Dictionary<EndPoint,Socket> clients = new Dictionary<EndPoint,Socket>();//TCP客户端
        private AutoResetEvent receiveEvent = new AutoResetEvent(false);
        public TcpServerConnect()
        {
        }

        /// <summary>
        /// TCP服务端对象
        /// </summary>
        public TcpServerConnect(int localPort)
        {
            this.listenPort = localPort;
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
                this.isRun = false;
                List<Socket> clientList = this.GetAllSocketClient();
                foreach (Socket client in clientList)
                {
                    try
                    {
                        client.Close();
                    }
                    catch (Exception ex)
                    {
                        this.FireLogMessage("TCP服务器关闭连接异常：{0}", ex);
                    }
                }
                this.ClearSocketClient(); //注意多线程同步操作
                if (this.sConn != null)
                {
                    this.sConn.Close();
                    this.sConn = null;
                }
                receiveEvent.WaitOne(500);
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
        public override void StartReceive() {
            IPEndPoint localEp = new IPEndPoint(IPAddress.Any,this.listenPort);
            sConn = new Socket(localEp.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
            sConn.ReceiveBufferSize = receiveBuffSize;
            this.sConn.Bind(localEp);
            this.sConn.Listen(100);
            ThreadPool.QueueUserWorkItem(delegate {
                while(this.isRun) {
                    try {
                        Socket client = sConn.Accept();//
                        this.AddSocketClient(client);//注意多线程同步操作
                        this.ReceiveClientData(client);
                    }
                    catch(Exception ex) {
                        this.FireLogMessage("TCP服务器接收连接异常：{0}",ex.Message);
                        this.Connected = false;
                    }
                }
                receiveEvent.Set();
            });
        }

        private void ReceiveClientData(Socket client) {
            ThreadPool.QueueUserWorkItem(state => {
                byte[] receiveData = new byte[receiveBuffSize];
                while(this.isRun) {
                    try {
                        int num = client.Receive(receiveData);
                        if(num == 0) {//客户端断开连接
                            this.RemoveSocketClient(client);
                            return;
                        }
                        byte[] tempBuff = new byte[num];
                        Buffer.BlockCopy(receiveData,0,tempBuff,0,num);
                        this.FireOnDataReceive(client.RemoteEndPoint,tempBuff);
                    }
                    catch(Exception) {
                        //客户端断开连接
                        this.RemoveSocketClient(client);
                        return;
                    }
                }
            });
        }

        /// <summary>
        /// 发送数据到服务端
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        public override bool Send(byte[] datas) {
            if(!this.isRun) {
                return false;
            }
            ThreadPool.QueueUserWorkItem(state => { //异步发送防止阻塞
                List<Socket> clientList = this.GetAllSocketClient();
                foreach(Socket client in clientList) {
                    try {
                        client.Send(datas);
                    }
                    catch(Exception e) {
                        //发送失败则断开连接
                        this.RemoveSocketClient(client);
                        this.FireLogMessage("TcpServer发送消息异常:{0}",e);
                        
                    }
                }
            });
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
            get { return this.sConn.LocalEndPoint.ToString(); }
        }


        #region 连接客户端维护

        private bool AddSocketClient(Socket client) {
            lock(clients) {
                if(!clients.ContainsKey(client.RemoteEndPoint)) {
                    clients.Add(client.RemoteEndPoint,client);
                    this.Connected = true;
                    this.FireLogMessage("{0}:已连接[{1}]端口",client.RemoteEndPoint,this.listenPort);
                    return true;
                }

                return false;
            }
        }

        private bool RemoveSocketClient(Socket client) {
            lock(clients) {
                try {
                    this.FireLogMessage("{0}:断开[{1}]端口连接",client.RemoteEndPoint,this.listenPort);
                    if(clients.ContainsKey(client.RemoteEndPoint)) {
                        clients.Remove(client.RemoteEndPoint);
                        client.Close();
                        this.Connected = this.clients.Count > 0;
                        return true;
                    }
                }
                catch(Exception) {
                    this.Connected = this.clients.Count > 0;
                }

                return false;
            }
        }

        private Socket GetSocketClient(EndPoint address) {
            lock(clients) {
                if(clients.ContainsKey(address)) {
                    return clients[address];
                }

                return null;
            }
        }

        private List<Socket> GetAllSocketClient() {
            lock(clients) {
                return this.clients.Values.ToList();
            }
        }

        private void ClearSocketClient() {
            lock(clients) {
                this.clients.Clear();
            }
        }

        #endregion
    }
}
