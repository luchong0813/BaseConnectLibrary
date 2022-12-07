namespace Device.Extension.Connect
{
    public class NullConnect : BaseConnect
    {

        /// <summary>
        /// 空连接对象
        /// </summary>
        public NullConnect()
        {
        }

        public override string Address {
            get {
                return string.Empty;
            }
        }


        public override bool OpenConnect()
        {
            return false;
        }

        public override void CloseConnect() {
           
        }

        public override void StartReceive() {
            
        }

        public override bool Send(byte[] datas)
        {
            return false;
        }
    }
}
