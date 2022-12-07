using System;
using System.ComponentModel;
using System.Net;

/*
 * Author:傲慢与偏见
 * cnblogs:https://www.cnblogs.com/chonglu/p/16907536.html
 * nuget:https://www.nuget.org/packages/BaseConnect
 * github:https://github.com/luchong0813
 */

namespace Device.Extension.Connect
{
    /// <summary>
    /// 接收数据委托
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="data"></param>
    public delegate void ReceivedBytesEventHandler(string sender,byte[] data);

    public delegate void ConnectStatusChanged(bool connected);

    /// <summary>
    /// 设备通信基类
    /// </summary>
    public abstract class BaseConnect: IDisposable
    {
        private readonly object ReceivedBytesEvent = new object();
        protected readonly object ConnectStatusEvent = new object();
        private EventHandlerList events=new EventHandlerList();
        private bool connected;
        /// <summary>
        /// 通讯地址
        /// </summary>
        public abstract string Address
        {
            get;
        }

        /// <summary>
        /// 连接状态
        /// </summary>
        /// <returns></returns>
        public bool Connected
        {
            get
            {
                return this.connected;
            }
            protected set
            {
                lock (ConnectStatusEvent)
                {
                    if (connected != value)
                    {
                        connected = value;
                        this.FireOnConnectStatusChanged(value);
                    }
                }
            }
        }

        /// <summary>
        /// 打开连接
        /// </summary>
        /// <returns></returns>
        public abstract bool OpenConnect();

        /// <summary>
        /// 关闭连接
        /// </summary>
        public abstract void CloseConnect();

        /// <summary>
        /// 启动数据接收线程
        /// </summary>
        public abstract void StartReceive();

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        public abstract bool Send(byte[] datas);

        /// <summary>
        /// 注册接收数据的回调
        /// </summary>
        public event ReceivedBytesEventHandler ReceivedBytes {
            add {
                this.events.AddHandler(ReceivedBytesEvent,value);
            }
            remove {
                this.events.RemoveHandler(ReceivedBytesEvent,value);
            }
        }

        /// <summary>
        /// 连接状态变化
        /// </summary>
        public event ConnectStatusChanged ConnectStatusChanged
        {
            add
            {
                this.events.AddHandler(ConnectStatusEvent, value);
            }
            remove
            {
                this.events.RemoveHandler(ConnectStatusEvent, value);
            }
        }

        /// <summary>
        /// 接收到数据后的回调
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void FireOnDataReceive(string sender,byte[] data) {
            try {
                ReceivedBytesEventHandler callBack = this.events[ReceivedBytesEvent] as ReceivedBytesEventHandler;
                if(callBack != null) {
                    callBack(sender,data);
                }
            }
            catch(Exception ex) {
                this.FireLogMessage("{0}：{1}","接收数据异常",ex);
            }
        }

        /// <summary>
        /// 接收到数据后的回调
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void FireOnDataReceive(EndPoint sender,byte[] data) {
            try {
                this.FireOnDataReceive(sender.ToString(),data);
            }
            catch(Exception ex) {
                this.FireLogMessage("{0}：{1}", "接收数据异常", ex);
            }
        }

        /// <summary>
        /// 打印调试日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void FireLogMessage(string format,params object[] args)
        {
        }

        /// <summary>
        /// 打印运行时调试日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void FireRuntimeLogMessage(string format,params object[] args) {
        }

        /// <summary>
        /// 连接状态变化
        /// </summary>
        /// <param name="connected"></param>
        protected void FireOnConnectStatusChanged(bool connected) {
            ConnectStatusChanged callBack = this.events[ConnectStatusEvent] as ConnectStatusChanged;
            if (callBack != null)
            {
                callBack.BeginInvoke(connected,null,null);
            }
        }

        public override string ToString()
        {
            return this.Address;
        }

        public void Dispose() {
            this.CloseConnect();
            GC.SuppressFinalize(this);
        }

        ~BaseConnect() {
            Dispose();
        }
    }

}
