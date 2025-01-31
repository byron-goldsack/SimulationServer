using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class ThermalSimulation
    {
        public int delayMs = 10;
        public int pollingDelayMs = 10;
        public double heatCoefficient = 0.5;
        public bool continueSending;
        Element[,] matrix;
        public ThermalSimulation()
        {
            matrix = new Element[20, 20];
        }

        public void BeginSimulation()
        {

            List<Thread> threads = new List<Thread>();

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

            Console.WriteLine("Matrix created");

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

            Console.WriteLine("Neighbours added");

            int k = 0;
            foreach (Thread t in threads)
            {
                t.Start();
                Console.WriteLine("Thread " + k + " started");
                k++;
            }

            Console.WriteLine("Threads started");

        }

        void PlateGo(Element e)
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

                    e.currentTemp += (av - e.currentTemp) * heatCoefficient;
                }
                //Console.WriteLine("adjusting temp to: " + e.currentTemp);

                Thread.Sleep(delayMs);
            }
        }

        public string CreateMatrixRepresentation()
        {
            string result = "";
            foreach (Element e in matrix)
            {
                result += e.ToString();
                result += ";";
            }
            return result;
        }

        public void Stop()
        {
            foreach(Element e in matrix)
            {
                e.stopRequested = true;
            }
        }

        public void ParseParams(List<string> parameters)
        {
            string variant = parameters[0];

            if (variant == "coef")
            {
                heatCoefficient = double.Parse(parameters[1]) / 100;
                Console.WriteLine("coef change: " + heatCoefficient);
            }
            else
            {
                int x = int.Parse(parameters[1]);
                int y = int.Parse(parameters[2]);
                int temp = int.Parse(parameters[3]);
                int coef = int.Parse(parameters[4]);

                matrix[x, y].currentTemp = temp;

                if (variant == "down")
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
    }
}
