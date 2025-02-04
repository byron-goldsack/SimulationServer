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
        public bool continueSending;
        Element[,] matrix;
        List<Thread> threads;
        bool isReloading = false;
        public ThermalSimulation(int defaultSize)
        {
            matrix = new Element[defaultSize, defaultSize];
            threads = new List<Thread>();
        }

        public void BeginSimulation(int size)
        {
            UnblockAll();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            isReloading = true;
            Stop();
            matrix = new Element[size, size];
            threads = new List<Thread>();

            Random rnd = new Random();

            continueSending = true;

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    Element newEl = new Element();
                    newEl.currentTemp = 0;
                    matrix[i, j] = newEl;
                    Thread newThread = new Thread(() => PlateGo(newEl));
                    threads.Add(newThread);
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

            int k = 0;
            foreach (Thread t in threads)
            {
                t.Start();
                k++;
            }

            Console.WriteLine("Threads started");
            isReloading = false;
            sw.Stop();
            Console.WriteLine("Time to load/reload: " + sw.ElapsedMilliseconds);
        }

        void PlateGo(Element e)
        {
            while (e.stopRequested == false)
            {
                if (!e.isHeating && !e.isBlocked)
                {
                    double av = 0;
                    int nonBlockingNeighbours = 0;
                    foreach (Element e2 in e.neighbours)
                    {
                        if (!e2.isBlocked)
                        {
                            av += e2.GetTemperature();
                            nonBlockingNeighbours++;
                        }
                    }
                    av /= nonBlockingNeighbours;

                    e.currentTemp += (av - e.currentTemp) * heatCoefficient;
                }
                Thread.Sleep(delayMs);
            }
        }

        public Matrix CreateMatrixRepresentation()
        {
            var matrixMessageRepresentation = new Matrix();
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
            foreach (Element e in matrix)
            {
                if(e != null)
                {
                    e.stopRequested = true;
                }
            }
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
