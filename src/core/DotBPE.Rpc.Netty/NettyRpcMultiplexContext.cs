using DotBPE.Rpc.Codes;
using DotBPE.Rpc.Options;
using DotNetty.Buffers;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotBPE.Rpc.Netty
{
    public class NettyRpcMultiplexContext<TMessage> : IRpcContext<TMessage> where TMessage : InvokeMessage
    {
        private readonly ILogger Logger;
        private readonly IMessageCodecs<TMessage> _codecs;
        private readonly Bootstrap _bootstrap;
        private List<IChannel> _channels = new List<IChannel>();

        private bool _autoReConnect = true;

        private static object _lockObj = new object();

        private int seq = 0;

        public EndPoint RemoteAddress { get; set; }
        public EndPoint LocalAddress { get; set; }

        public NettyRpcMultiplexContext(Bootstrap bootstrap, IMessageCodecs<TMessage> codecs, ILogger logger)
        {
            this._bootstrap = bootstrap;
            this._codecs = codecs;
            this.Logger = logger;
        }

        public Task CloseAsync()
        {
            this._autoReConnect = false; //禁止自动重连
            Logger.LogDebug("开始主动关闭连接");
            lock (_lockObj)
            {
                _channels.ForEach(async (channel) =>
                {
                    if (channel.Open && channel.Active)
                    {
                        await channel.CloseAsync();
                    }
                });
                Logger.LogDebug("主动关闭连接结束");
                _channels.Clear();
                Logger.LogDebug("清理内在连接");
            }
            return Task.CompletedTask;
        }

        public async Task SendAsync(TMessage data)
        {
            var channel = TryGetOneRandom();
            if (channel == null)
            {
               throw new Exceptions.RpcException("获取channel失败");

            }

            if (!channel.Active || !channel.IsWritable || !channel.Open)
            {
                Logger.LogWarning("ChannelId={0} is invalid,Active={1},IsWritable={2},Open={3}", channel.Id.AsLongText(), channel.Active, channel.IsWritable, channel.Open);
            }

            if (channel.Open)
            {
                var buff = GetBuffer(channel, data);
                Logger.LogDebug("ChannelId={0} Send Message ={1}", channel.Id.AsLongText(), data.MethodIdentifier);
                await channel.WriteAndFlushAsync(buff);
                Logger.LogDebug("ChannelId={0} Send Message ={1} Completed", channel.Id.AsLongText(), data.MethodIdentifier);
            }
            else
            {
                Logger.LogWarning("ChannelId={0} is invalid,ready to remove it", channel.Id.AsLongText());
                await channel.DisconnectAsync();
                // StartConnect(channel.RemoteAddress); //启动自动重连
                await SendAsync(data); // 重新调用一次 ，直到链接被移除完
            }
        }

        private IByteBuffer GetBuffer(IChannel channel, TMessage message)
        {
            var buff = channel.Allocator.Buffer(message.Length);
            IBufferWriter writer = NettyBufferManager.CreateBufferWriter(buff);
            this._codecs.Encode(message, writer);
            return buff;
        }



        public Task InitAsync(EndPoint endpoint, RpcClientOption clientOption)
        {
            RemoteAddress = endpoint;
            int multiplexCount = clientOption != null && clientOption.MultiplexCount>0 ? clientOption.MultiplexCount : 1;
            Logger.LogDebug("Ready to Init {0} Connections", multiplexCount);
            return CreateConnection(endpoint, multiplexCount);
        }

        private async Task CreateConnection(EndPoint endpoint, int count)
        {
            if (count > 1)
            {
                for (var i = 0; i < count; i++)
                {
                    var channel = await this._bootstrap.ConnectAsync(endpoint).ConfigureAwait(false);
                    Logger.LogInformation("注册链接成功,remoteAddress={0},channelId={1}", endpoint, channel.Id.AsLongText());
                    _channels.Add(channel);
                }
            }
            else
            {
                IChannel channel = await this._bootstrap.ConnectAsync(endpoint).ConfigureAwait(false);
                Logger.LogInformation("注册链接成功,remoteAddress={0},channelId={1}", endpoint, channel.Id.AsLongText());
                _channels.Add(channel);
            }

            Logger.LogDebug("Inited {0} Connections", _channels.Count);
        }

       

        private bool TryRemoveById(string id)
        {
            lock (_lockObj)
            {
                var channel = _channels.Find(x => x.Id.AsLongText() == id);
                if (channel != null)
                {
                    return _channels.Remove(channel);
                }
            }
            Logger.LogWarning("移除Channel时，发现Channel不在对应的队列中,{0}", id);
            return false;
        }

        private bool TryRemove(IChannel channel)
        {
            lock (_lockObj)
            {
                if (_channels.Contains(channel))
                {
                   return _channels.Remove(channel);
                }
            }
            Logger.LogWarning("移除Channel时，发现Channel不在对应的队列中,{0}", channel.Id.AsLongText());
            return false;
        }

        /// <summary>
        /// 要实现特定的轮询算法 ，就要重写这个实现
        /// </summary>
        /// <returns></returns>
        private IChannel TryGetOneRandom()
        {
            IChannel channel = null;
            lock (_lockObj)
            {
                if (_channels.Count == 0)
                {
                    throw new Exceptions.RpcCommunicationException("当前没有可用链接");
                }

                int id = Interlocked.Increment(ref this.seq);
                var index = Math.Abs(id % _channels.Count); // 获取一个IChannel
                channel = _channels[index];
            }
            return channel;
        }

        private Task StartConnect(EndPoint endpoint,int mCount)
        {
            int tryCount = 0;

            var task= Task.Factory.StartNew(async () =>
            {
                while (_autoReConnect)
                {
                    tryCount++;
                    if (tryCount >= 100000)
                    {
                        tryCount = 1;
                        Logger.LogDebug("reconnect to {0} 100000 times, but fail, restart !", endpoint);
                        break;
                    }
                    //Logger.LogInformation("will reconnect to {0} after {1} ms, try {2} times", endpoint, tryCount * 1000, tryCount);
                    try
                    {
                        await CreateConnection(endpoint, mCount);
                        break;
                    }
                    catch
                    {
                        Logger.LogWarning("reconnect {0} failed，try {1} times", endpoint, tryCount);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            });

            return task;
        }

        public void OnContextInActived(ConnectionEventArgs args)
        {
            bool removed = TryRemoveById(args.ChannelId);
            if (this._autoReConnect && removed)
            {
                StartConnect(args.RemotePoint, 1);
            }
        }
    }
}
