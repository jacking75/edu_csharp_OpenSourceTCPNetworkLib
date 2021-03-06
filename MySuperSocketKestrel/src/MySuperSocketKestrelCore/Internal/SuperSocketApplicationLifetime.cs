using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Buffers;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using System.Threading;

namespace MySuperSocketKestrelCore
{
    internal class SuperSocketApplicationLifetime : IApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {            
        }
    }
}