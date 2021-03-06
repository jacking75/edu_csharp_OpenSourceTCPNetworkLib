﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySuperSocketCore;


namespace echoServer
{
    class Program
    {
        static void Main(string[] args)
        {
            //RunAsync().Wait();
            // or
            RunAsyncVer2().Wait();
        }


        static Microsoft.Extensions.Logging.ILogger Logger;
        
        static void NetEventOnConnect(AppSession session)
        {
            Logger.LogInformation($"[NetEventOnConnect] A new session connected: {session.SessionID}");
        }

        static void NetEventOnCloese(AppSession session)
        {
            Logger.LogInformation($"[NetEventOnCloese] session DisConnected: {session.SessionID}");
        }

        static void NetEventOnReceive(AppSession session, AnalyzedPacket packet)
        {
            packet.SessionUniqueId = session.UniqueId;
            Logger.LogInformation($"[NetEventOnReceive] session: {session.SessionID}, ReceiveDataSize:{packet.Body.Length}");

            var pktID = packet.PacketId + 1;
            var packetLen = packet.Body.Length + 5;
            var dataSource = new byte[packetLen];
            Buffer.BlockCopy(BitConverter.GetBytes(packetLen), 0, dataSource, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(pktID), 0, dataSource, 2, 2);
            dataSource[4] = 0;
            Buffer.BlockCopy(packet.Body, 0, dataSource, 5, packet.Body.Length);
           
            session.Channel.SendTask(packet.Body.AsMemory());
        }
        
        

        static async Task RunAsync()
        {
            var serverOption = new ServerOptions();
            serverOption.Name = "TestServer";
            serverOption.Listeners = new ListenOptions[1];
            serverOption.Listeners[0] = new ListenOptions();
            serverOption.Listeners[0].Ip = "Any";
            serverOption.Listeners[0].Port = 11021;
            serverOption.Listeners[0].BackLog = 100;
            serverOption.Listeners[0].MaxRecvPacketSize = 512;
            serverOption.Listeners[0].MaxReceivBufferSize = 512 * 3;
            serverOption.Listeners[0].MaxSendPacketSize = 1024;            
            serverOption.Listeners[0].MaxSendingSize = 4096; 
            serverOption.Listeners[0].MaxReTryCount = 3;

            var server = CreateSocketServer<BinaryPipelineFilter>(serverOption);
            await server.StartAsync();

            Logger.LogInformation("The server is started.");
            while (Console.ReadLine().ToLower() != "c")
            {
                continue;
            }

            await server.StopAsync();
        }

        static IServer CreateSocketServer<TPipelineFilter>(ServerOptions serverOptions)
            where TPipelineFilter : IPipelineFilter, new()
        {
            var server = new SuperSocketServer();
            server.NetEventOnConnect = NetEventOnConnect;
            server.NetEventOnCloese = NetEventOnCloese;
            server.NetEventOnReceive = NetEventOnReceive;

            var services = new ServiceCollection();
            services.AddLogging();

            var pipelineFilterFactoryList = new List<IPipelineFilterFactory>();
            pipelineFilterFactoryList.Add( new DefaultPipelineFilterFactory<TPipelineFilter>());
            server.Configure(serverOptions, services, pipelineFilterFactoryList);

            Logger = SuperSocketServer.GetLogger();

            return server;
        }



        static async Task RunAsyncVer2()
        {
            var parameter = new ServerBuildParameter();
            parameter.NetEventOnConnect = NetEventOnConnect;
            parameter.NetEventOnCloese = NetEventOnCloese;
            parameter.NetEventOnReceive = NetEventOnReceive;
            parameter.serverOption.Name = "TestServer";
            parameter.serverOption.Listeners = new ListenOptions[1];
            parameter.serverOption.Listeners[0] = new ListenOptions();
            parameter.serverOption.Listeners[0].Ip = "Any";
            parameter.serverOption.Listeners[0].Port = 11021;
            parameter.serverOption.Listeners[0].BackLog = 100;
            parameter.serverOption.Listeners[0].MaxRecvPacketSize = 512;
            parameter.serverOption.Listeners[0].MaxReceivBufferSize = 512 * 3;
            parameter.serverOption.Listeners[0].MaxSendPacketSize = 1024;            
            parameter.serverOption.Listeners[0].MaxSendingSize = 4096;
            parameter.serverOption.Listeners[0].MaxReTryCount = 3;

            var pipelineFilterFactoryList = new List<IPipelineFilterFactory>();
            pipelineFilterFactoryList.Add(new DefaultPipelineFilterFactory<BinaryPipelineFilter>());

            var server = new SuperSocketServer();
            server.CreateSocketServer(parameter, pipelineFilterFactoryList);

            Logger = SuperSocketServer.GetLogger();

            await server.StartAsync();

            Logger.LogInformation("The server is started.");
            while (Console.ReadLine().ToLower() != "c")
            {
                continue;
            }

            await server.StopAsync();
        }

    }
}
