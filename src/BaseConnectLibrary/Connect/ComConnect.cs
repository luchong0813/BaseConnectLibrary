using System;
using System.IO.Ports;
using System.Threading;

namespace Device.Extension.Connect
{
    public class ComConnect:BaseConnect {
        private SerialPort sConn;
        private bool isRun;
        private int receiveBuffSize = 1024;

        /// <summary>
        /// Com连接对象
        /// </summary>
        public ComConnect() {
        }

        /// <summary>
        /// Com连接对象
        /// </summary>
        public ComConnect(string comPortName,int comBaudRate,Parity comParity = Parity.None)
        {
            this.PortName = comPortName;
            this.BaudRate = comBaudRate;
            this.Parity = comParity;
        }

        /// <summary>
        /// 打开连接
        /// </summary>
        /// <returns></returns>
        public override bool OpenConnect() {
            bool flag = false;
            try {
                this.CloseConnect();
                this.sConn = new SerialPort(PortName,BaudRate,Parity);
                this.sConn.Open();
                this.FireLogMessage("打开串口:{0}",PortName);
                flag = true;
            }
            catch(Exception ex) {
                this.FireLogMessage("串口打开异常：{0}",ex);
            }
            this.isRun = true;
            this.StartReceive();
            this.Connected= this.sConn != null && this.sConn.IsOpen;
            return flag;
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public override void CloseConnect() {
            try {
                this.isRun = false;
                if(this.sConn != null && this.sConn.IsOpen) {
                    this.sConn.Close();
                    this.sConn = null;
                    this.FireLogMessage("串口被主动关闭");
                }
            }
            catch(Exception ex) {
                this.FireLogMessage("串口关闭异常：{0}",ex);
            }
            finally
            {
                this.Connected = false;
            }
        }

        public override void StartReceive() {
            ThreadPool.QueueUserWorkItem(delegate {
                while(this.isRun && this.sConn.IsOpen) {
                    try {
                        if(this.sConn.BytesToRead > 0) {
                            byte[] tempBuff = new byte[this.sConn.BytesToRead];
                            this.sConn.Read(tempBuff,0,tempBuff.Length);
                            this.FireOnDataReceive(this.Address,tempBuff);
                            continue;
                        }
                        Thread.Sleep(50);
                    }
                    catch(Exception ex) {
                        this.FireLogMessage("串口接收数据异常：{0}",ex);
                    }
                }
                this.FireLogMessage("串口停止接收数据");
            });
        }

        /// <summary>
        /// 发送数据到服务端
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        public override bool Send(byte[] datas) {
            lock(this) {
                if(this.sConn == null || !this.sConn.IsOpen) {
                    this.FireLogMessage("串口发送数据时发现未正确打开");
                    return false;
                }
                this.sConn.Write(datas,0,datas.Length);
                return true;
            }
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

        public string PortName { get; }

        public int BaudRate { get; }

        public Parity Parity { get; }

        public override string Address {
            get {
                return this.PortName;
            }
        }
    }
}
