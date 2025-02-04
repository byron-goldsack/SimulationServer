using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using SimulationServer;

class Program
{

    static async Task Main(string[] args)
    {
        var simulation = new ThermalSimulation(20);
        simulation.BeginSimulation(20);

        HttpListener httpListener = new HttpListener();
        httpListener.Prefixes.Add("http://127.0.0.1:2024/");
        httpListener.Start();
        Console.WriteLine("Listening...");

        while (true)
        {
            HttpListenerContext context = await httpListener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
                Console.WriteLine("Websocket connection accepted");
                WebSocket webSocket = wsContext.WebSocket;

                Thread Sender = new Thread(() => SendLoop(webSocket, simulation));
                Sender.Start();

                // Keep the connection open for further communication
                await ReceiveLoop(webSocket, simulation);
            }
            else
            {
                Console.WriteLine("Http request received");
                HttpListenerRequest request = context.Request;

                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
        simulation.Stop();
    }

    static void SendLoop(WebSocket webSocket, ThermalSimulation sim)
    {
        while (sim.continueSending)
        {
            Matrix message = sim.CreateMatrixRepresentation();
            byte[] buffer = message.ToByteArray();
            //TODO: only send if new
            webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);

            Thread.Sleep(sim.pollingDelayMs);
        }
    }

    private static async Task ReceiveLoop(WebSocket webSocket, ThermalSimulation sim)
    {
        while (sim.continueSending && webSocket.State == WebSocketState.Open)
        {
            byte[] buffer = new byte[1024 * 4];

            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                byte[] messageBytes = new byte[result.Count];
                Array.Copy(buffer, messageBytes, result.Count);

                try
                {
                    ClientMessage message = ClientMessage.Parser.ParseFrom(messageBytes);
                    sim.ParseParams(message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error parsing mouse event: " + e.Message);
                }
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }
}