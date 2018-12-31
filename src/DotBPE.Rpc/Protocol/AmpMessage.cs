using DotBPE.Rpc.Codec;

namespace DotBPE.Rpc.Protocol
{
    /// <summary>
    /// 协议消息
    /// </summary>
    public class AmpMessage : InvokeMessage, Peach.Messaging.IMessage
    {

        public static AmpMessage HEARBEAT = CreateRequestMessage(0,0,true);

        /// <summary>
        /// 第一个版本为18个字节头固定长度
        /// </summary>
        public const int VERSION_0_HEAD_LENGTH = 18;
        /// <summary>
        /// 加强版本则为19个字节头固定长度
        /// </summary>
        public const int VERSION_1_HEAD_LENGTH = 19;


        /// <summary>
        /// 状态码
        /// </summary>
        public int Code { get; set; }

        //0 默认为Protobuf 1 MessagePack 2 = JSON
        public CodecType CodecType { get; set; }

        /// <summary>
        /// 实际的请求数据
        /// </summary>
        public byte[] Data { get; set; }

        public override bool IsHeartBeat
        {
            get
            {
                return ServiceId == 0 && MessageId == 0;
            }
        }

        public override int Length {
            get
            {
                int hl = Version == 0 ? VERSION_0_HEAD_LENGTH : VERSION_1_HEAD_LENGTH;
                return hl + Data.Length;
            }
        }

        /// <summary>
        /// 消息标识
        /// </summary>
        public string Id
        {
            get
            {
                return $"{ServiceId}|{MessageId}|{Sequence}";
            }
        }


        /// <summary>
        /// 调用服务的唯一消息号 确定哪个方法
        /// </summary>
        public ushort MessageId { get; set; }

        public override string MethodIdentifier { get { return $"{ServiceId}${MessageId}"; } }

        /// <summary>
        /// 请求的序列号
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 调用服务的唯一服务号 确定哪个服务
        /// </summary>
        public ushort ServiceId { get; set; }

        public override string ServiceIdentifier { get { return $"{ServiceId}$0"; } }

        /// <summary>
        /// 协议版本0/1
        /// </summary>
        public byte Version { get; set; }


        public string FriendlyServiceName { get;set;}

        public static AmpMessage CreateRequestMessage(ushort serviceId,ushort messageId,bool withOutResponse =false)
        {
            AmpMessage msg = new AmpMessage();
            msg.ServiceId = serviceId;
            msg.MessageId = messageId;
            msg.Version = 0;
            msg.CodecType = 0;
            msg.InvokeMessageType = withOutResponse? InvokeMessageType.InvokeWithoutResponse: InvokeMessageType.Request;
            return msg;
        }


        public static AmpMessage CreateResponseMessage(string requestId)
        {
            var data = requestId.Split('|');
            AmpMessage message = new AmpMessage()
            {
                ServiceId = ushort.Parse(data[0]),
                MessageId = ushort.Parse(data[1]),
                InvokeMessageType = InvokeMessageType.Response
            };
            return message;
        }

        public static AmpMessage CreateResponseMessage(ushort serviceId, ushort messageId)
        {
            AmpMessage message = new AmpMessage()
            {
                ServiceId = serviceId,
                MessageId = messageId,
                InvokeMessageType = InvokeMessageType.Response
            };
            return message;
        }
    }
}
