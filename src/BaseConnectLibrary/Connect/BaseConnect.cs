using System;
using System.ComponentModel;
using System.Net;

/*
 * Author:������ƫ��
 * cnblogs:https://www.cnblogs.com/chonglu/p/16907536.html
 * nuget:https://www.nuget.org/packages/BaseConnect
 * github:https://github.com/luchong0813
 */

namespace Device.Extension.Connect
{
    /// <summary>
    /// ��������ί��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="data"></param>
    public delegate void ReceivedBytesEventHandler(string sender,byte[] data);

    public delegate void ConnectStatusChanged(bool connected);

    /// <summary>
    /// �豸ͨ�Ż���
    /// </summary>
    public abstract class BaseConnect: IDisposable
    {
        private readonly object ReceivedBytesEvent = new object();
        protected readonly object ConnectStatusEvent = new object();
        private EventHandlerList events=new EventHandlerList();
        private bool connected;
        /// <summary>
        /// ͨѶ��ַ
        /// </summary>
        public abstract string Address
        {
            get;
        }

        /// <summary>
        /// ����״̬
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
        /// ������
        /// </summary>
        /// <returns></returns>
        public abstract bool OpenConnect();

        /// <summary>
        /// �ر�����
        /// </summary>
        public abstract void CloseConnect();

        /// <summary>
        /// �������ݽ����߳�
        /// </summary>
        public abstract void StartReceive();

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        public abstract bool Send(byte[] datas);

        /// <summary>
        /// ע��������ݵĻص�
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
        /// ����״̬�仯
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
        /// ���յ����ݺ�Ļص�
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
                this.FireLogMessage("{0}��{1}","���������쳣",ex);
            }
        }

        /// <summary>
        /// ���յ����ݺ�Ļص�
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        protected void FireOnDataReceive(EndPoint sender,byte[] data) {
            try {
                this.FireOnDataReceive(sender.ToString(),data);
            }
            catch(Exception ex) {
                this.FireLogMessage("{0}��{1}", "���������쳣", ex);
            }
        }

        /// <summary>
        /// ��ӡ������־
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void FireLogMessage(string format,params object[] args)
        {
        }

        /// <summary>
        /// ��ӡ����ʱ������־
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        protected void FireRuntimeLogMessage(string format,params object[] args) {
        }

        /// <summary>
        /// ����״̬�仯
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
