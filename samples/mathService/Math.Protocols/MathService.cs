using DotBPE.Rpc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Math.Protocols
{
    public class MathService: IMathService,IRpcService
    {        
        public Task<RpcResult<SumRes>> SumAsync(SumReq req)
        {
            RpcResult<SumRes> result = new RpcResult<SumRes>() { Data = new SumRes() };
            result.Data.Total = req.A + req.B;
            return Task.FromResult(result);
        }
    }

    public interface IMathService {
        Task<RpcResult<SumRes>> SumAsync(SumReq req);
    }

}
