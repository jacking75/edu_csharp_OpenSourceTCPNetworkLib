using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;

namespace MySuperSocketKestrelCore
{
    public class SuperSocketServer : IServer
    {
        public Action<AppSession> NetEventOnConnect;
        public Action<AppSession> NetEventOnCloese;
        public Action<AppSession, AnalyzedPacket> NetEventOnReceive;

        private IServiceCollection _serviceCollection;

        private IServiceProvider _serviceProvider;

        public ServerOptions Options { get; private set; }

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Server's name
        /// </summary>
        /// <returns>the name of the server instance</returns>
        public string Name 
        {
            get { return Options.Name; }
        }

        private IList<ITransport> _transports;

        protected internal ILoggerFactory LoggerFactory { get; private set; }

        static public Microsoft.Extensions.Logging.ILogger GetLogger() { return _logger; }
        static private Microsoft.Extensions.Logging.ILogger _logger;


        private bool _configured = false;
                
        private ITransportFactory _transportFactory;

        C3SockNetUtil.IUniqueIdGenerator SessionUniqueIdGen = new C3SockNetUtil.UniqueIdGenSimple();


        public void CreateSocketServer(ServerBuildParameter parameter, List<IPipelineFilter> pipelineFilterFactoryList)
        {
            NetEventOnConnect = parameter.NetEventOnConnect;
            NetEventOnCloese = parameter.NetEventOnCloese;
            NetEventOnReceive = parameter.NetEventOnReceive;
                       

            var services = new ServiceCollection();
            services.AddLogging();

            services.AddSingleton<ITransportFactory, SocketTransportFactory>(); // SocketTransportFactory를 외부에서 받도록 한다.
            
            Configure(parameter.serverOption, services, pipelineFilterFactoryList);
        }


        public bool Configure(ServerOptions options,  IServiceCollection services, List<IPipelineFilter> pipelineFilterList)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Options = options;

            if (services == null)
            {
                services = new ServiceCollection();
            }

            _serviceCollection = services.AddOptions(); // activate options     

            _serviceCollection.AddSingleton<IApplicationLifetime, SuperSocketApplicationLifetime>(); // ?

            _serviceProvider = services.BuildServiceProvider();

            LoggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            LoggerFactory.AddNLog();
            _logger = LoggerFactory.CreateLogger("SuperSocket");


            _transportFactory = _serviceProvider.GetRequiredService<ITransportFactory>();
            if (_transportFactory == null)
            {
                throw new ArgumentNullException(nameof(ITransportFactory));
            }

            _transports = new List<ITransport>();
                        
            var index = 0;
            foreach (var l in options.Listeners)
            {                
                var CreateSessionFunc = CreateConnectionFunc(l, pipelineFilterList[index]);

                var connectionDispatcher = new ConnectionDispatcher();
                connectionDispatcher.NewClientAccepted = CreateSessionFunc;
                _transports.Add(_transportFactory.Create(new SuperSocketEndPointInformation(l), connectionDispatcher));
            }

            return _configured = true;
        }

        Func<TransportConnection, AppSession> CreateConnectionFunc(ListenOptions option, IPipelineFilter pipelineFilter)
        {
            return (x) => { 
                var session = new AppSession();

                void OnPackageReceived(AnalyzedPacket packet)
                {
                    NetEventOnReceive(session, packet);
                }

                void CloseEvent()
                {
                    NetEventOnCloese(session);
                }

                var channel = new TCPPipeChannel(x, pipelineFilter);
                channel.OnPackageReceived = OnPackageReceived;
                channel.OnClosed = CloseEvent;
                channel.SetSendOption(option.MaxSendPacketSize, option.MaxSendingSize, option.MaxSendReTryCount);

                session.SetChannel(channel);
                session.SetUniqueId(NextSessionUniqueId(), Guid.NewGuid().ToString());

                NetEventOnConnect(session);
                return session;
            };
        }

        public async Task<bool> StartAsync()
        {
            if (!_configured)
            {
                _logger.LogError("The server has not been initialized successfully!");
            }
                        
            foreach (var transport in _transports)
            {
                try
                {
                    await transport.BindAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to bind the transport {transport.ToString()}.");
                    return false;
                }
            }
                        
            _logger.LogInformation("Server Start !!!");
            return true;
        }

        public async Task StopAsync()
        {
            foreach (var transport in _transports)
            {
                await transport.UnbindAsync();
                await transport.StopAsync();
            }
        }


        public UInt64 NextSessionUniqueId() { return SessionUniqueIdGen.NextId(); }
    }
}