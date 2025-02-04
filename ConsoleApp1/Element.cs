using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimulationServer
{
    class Element
    {
        public bool stopRequested = false;
        public double currentTemp;
        public List<Element> neighbours;
        public bool isHeating;

        public Element()
        {

            currentTemp = 0;
            neighbours = new List<Element>();
        }

        public void RequestStop()
        {
            stopRequested = true;
        }

        public double GetTemperature()
        {
            lock (this)
            {
                return currentTemp;
            }
        }

        public void AddNeighbour(Element e)
        {
            neighbours.Add(e);
        }

        public override string? ToString()
        {
            double red = GetTemperature() / 100 * 255;
            double blue = 255 - red;

            return (int)red + ":0:" + (int)blue;
        }

        public RGB GetRGB()
        {
            double red = GetTemperature() / 100 * 255;
            double blue = 255 - red;
            return new RGB
            {
                Red = (int)red,
                Green = 0,
                Blue = (int)blue
            };
        }
    }
}
