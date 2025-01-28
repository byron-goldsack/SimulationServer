using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ConsoleApp1;

class Program
{
    int delayMs = 10;
    int pollingDelayMs = 10;
    double heatCoefficient = 0.5;
    static async Task Main(string[] args)
    {
        HttpListener httpListener = new HttpListener();
        httpListener.Prefixes.Add("http://127.0.0.1:2024/");
        httpListener.Start();
        Console.WriteLine("Listening...");

        Element[,] matrix = new Element[10, 10];
        List<Thread> threads = new List<Thread>();

        Random rnd = new Random();

        Program p = new Program();

        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for(int j = 0; j < matrix.GetLength(1); j++)
            {
                Element newEl = new Element();
                //newEl.currentTemp = rnd.NextDouble() * 100;
                newEl.currentTemp = 0;
                matrix[i, j] = newEl;
                Thread newThread = new Thread(() => p.PlateGo(newEl, p));
                threads.Add(newThread);
            }
        }

        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                if(i > 0)
                {
                    matrix[i,j].AddNeighbour(matrix[i-1,j]);
                }
                if(j > 0)
                {
                    matrix[i, j].AddNeighbour(matrix[i, j - 1]);
                }
                if(i < matrix.GetLength(0) - 1)
                {
                    matrix[i, j].AddNeighbour(matrix[i + 1, j]);
                }
                if (j < matrix.GetLength(1) - 1)
                {
                    matrix[i, j].AddNeighbour(matrix[i, j+1]);
                }
            }
        }

        foreach(Thread t in threads)
        {
            t.Start();
        }


        while (true)
        {
            HttpListenerContext context = await httpListener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
                WebSocket webSocket = wsContext.WebSocket;
                
                Thread Sender = new Thread(() => p.SendLoop(webSocket, matrix));
                Sender.Start();

                // Keep the connection open for further communication
                await ReceiveLoop(webSocket, matrix, p);
                break;
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    private static async Task ReceiveLoop(WebSocket webSocket, Element[,] matrix, Program p)
    {
        while (true)
        {
            byte[] buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            string text = Encoding.Default.GetString(buffer);
            List<string> parameters = text.Split(':').ToList();
            try
            {
                string variant = parameters[0];

                if(variant == "coef")
                {
                    p.heatCoefficient = double.Parse(parameters[1]) / 100;
                    Console.WriteLine("coef change: " + p.heatCoefficient);
                }
                else { 
                    int x = int.Parse(parameters[1]);
                    int y = int.Parse(parameters[2]);
                    int temp = int.Parse(parameters[3]);
                    int coef = int.Parse(parameters[4]);

                    matrix[x, y].currentTemp = temp;
            
                    if(variant == "down")
                    {
                        matrix[x, y].isHeating = true;
                    }
                    else
                    {
                        foreach (Element e in matrix)
                        {
                            e.isHeating = false;
                        }
                        Console.WriteLine("reset holds");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine(text);
                Console.WriteLine("Parameters: " + parameters);
                Console.WriteLine("Parameters count: " + parameters.Count);
            }

        }
        //await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
    }

    void SendLoop(WebSocket webSocket, Element[,] matrix)
    {
        while (true)
        {
            string message = CreateMatrixRepresentation(matrix);
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            Thread.Sleep(pollingDelayMs);
        }
    }

    void PlateGo(Element e, Program p)
    {
        while (e.stopRequested == false)
        {
            if (!e.isHeating)
            {
                double av = 0;
                foreach (Element e2 in e.neighbours)
                {
                    av += e2.GetTemperature();
                }
                av /= e.neighbours.Count;

                e.currentTemp += (av - e.currentTemp) * p.heatCoefficient;
            }
            //Console.WriteLine("adjusting temp to: " + e.currentTemp);

            Thread.Sleep(delayMs);
        }
    }

    static string CreateMatrixRepresentation(Element[,] matrix)
    {
        string result = "";
        foreach(Element e in matrix)
        {
            result += e.ToString();
            result += ";";
        }
        return result;
    }
}