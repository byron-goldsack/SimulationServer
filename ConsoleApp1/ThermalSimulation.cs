using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using System.Diagnostics;

namespace SimulationServer
{
    class ThermalSimulation
    {
        public int delayMs = 10;
        public int pollingDelayMs = 10;
        public double heatCoefficient = 0.5;
        bool isReloading = false;

        public bool continueSending;

        Element[,] matrix;
        //List<Thread> threads;
        CancellationTokenSource cts;

        public ThermalSimulation(int defaultSize)
        {
            matrix = new Element[defaultSize, defaultSize];
            //threads = new List<Thread>();
            cts = new CancellationTokenSource();
            continueSending = true;
        }

        public void BeginSimulation(int size)
        {
            Console.WriteLine("sizing at: " + size);
            UnblockAll();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            isReloading = true;
            Stop();
            matrix = new Element[size, size];
            //threads = new List<Thread>();
            cts = new CancellationTokenSource();

            Random rnd = new Random();

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    Element newEl = new Element();
                    newEl.currentTemp = 0;
                    matrix[i, j] = newEl;
                }
            }

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (i > 0)
                    {
                        matrix[i, j].AddNeighbour(matrix[i - 1, j]);
                    }
                    if (j > 0)
                    {
                        matrix[i, j].AddNeighbour(matrix[i, j - 1]);
                    }
                    if (i < matrix.GetLength(0) - 1)
                    {
                        matrix[i, j].AddNeighbour(matrix[i + 1, j]);
                    }
                    if (j < matrix.GetLength(1) - 1)
                    {
                        matrix[i, j].AddNeighbour(matrix[i, j + 1]);
                    }
                }
            }

            Task.Run(() => SimulationLoop(cts.Token));

            Console.WriteLine("Sim started");
            isReloading = false;
            sw.Stop();
            Console.WriteLine("Time to load/reload: " + sw.ElapsedMilliseconds);
        }

        async void SimulationLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    Parallel.For(0, matrix.GetLength(0), i =>
                    {
                        Parallel.For(0, matrix.GetLength(1), j =>
                        {
                            Element e = matrix[i, j];
                            if (!e.isHeating && !e.isBlocked)
                            {
                                double av = 0;
                                int nonBlockedNeighbours = 0;
                                foreach (Element n in e.neighbours)
                                {
                                    if (!n.isBlocked)
                                    {
                                        av += n.GetTemperature();
                                        nonBlockedNeighbours++;
                                    }
                                }
                                if (nonBlockedNeighbours > 0)
                                {
                                    av /= nonBlockedNeighbours;
                                    e.currentTemp += (av - e.currentTemp) * heatCoefficient;
                                }
                            }
                        });

                    });
                    await Task.Delay(delayMs, token);
                }

            }
            catch(TaskCanceledException ex)
            {
                Console.WriteLine("Task cancelled");
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error in simulation loop: " + ex.Message);
            }
        }

        public Matrix CreateMatrixRepresentation()
        {
            var matrixMessageRepresentation = new Matrix();
            if (matrix[0,0] == null) return matrixMessageRepresentation;
            //string result = "";

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (isReloading){
                        //dont send messages when updating
                        //matrixMessageRepresentation.Values.Add(new RGB { Red=15, Green=255, Blue=0});
                    }
                    else
                    {
                        //result += matrix[i, j].ToString();
                        matrixMessageRepresentation.Values.Add(matrix[i, j].GetRGB());
                    }
                }
            }
            return matrixMessageRepresentation;
        }

        public void Stop()
        {
            cts.Cancel();
        }

        public void UnblockAll()
        {
            foreach (Element e in matrix)
            {
                if (e != null)
                {
                    e.isBlocked = false;
                }
            }
        }

        public void ParseParams(ClientMessage message)
        {
            switch (message.MsgCase)
            {
                case ClientMessage.MsgOneofCase.Mouse:
                    matrix[message.Mouse.XPosition, message.Mouse.YPosition].currentTemp = message.Mouse.Temperature;
                    if (message.Mouse.Event == "down")
                    {
                        matrix[message.Mouse.XPosition, message.Mouse.YPosition].isHeating = true;
                    }
                    else if (message.Mouse.Event == "up")
                    {
                        foreach (Element el in matrix)
                        {
                            el.isHeating = false;
                        }
                    }
                    else if (message.Mouse.Event == "block")
                    {
                        matrix[message.Mouse.XPosition, message.Mouse.YPosition].isBlocked = true;
                    }
                    else if (message.Mouse.Event == "unblock")
                    {
                        matrix[message.Mouse.XPosition, message.Mouse.YPosition].isBlocked = false;
                    }
                    break;

                case ClientMessage.MsgOneofCase.Settings:
                    if (message.Settings.HasCoef)
                    {
                        heatCoefficient = message.Settings.Coef / 100;
                    }
                    else if (message.Settings.HasSize)
                    {
                        BeginSimulation(message.Settings.Size);
                    }
                    break;
                case ClientMessage.MsgOneofCase.Scenario:
                    Console.WriteLine("Scenario received: " + message.Scenario.Scenario);
                    switch (message.Scenario.Scenario)
                    {
                        case "chamber":
                            BeginSimulation(10);
                            break;
                        case "unblock":
                            UnblockAll();
                            break;
                        case "heatall":
                            foreach(Element e in matrix)
                            {
                                if (e != null) e.currentTemp = 100;
                            }
                            break;
                        case "coolall":
                            foreach (Element e in matrix)
                            {
                                if (e != null) e.currentTemp = 0;
                            }
                            break;

                    }
                    break;
            }
        }
    }
}
