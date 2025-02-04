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
        public bool isBlocked;

        public Element()
        {
            isBlocked = false;
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

        public RGB GetRGB()
        {
            double red = GetTemperature() / 100 * 255;
            double blue = 255 - red;

            if (isBlocked)
            {
                return new RGB
                {
                    Red = 0,
                    Green = 0,
                    Blue = 0
                };
            }

            return new RGB
            {
                Red = (int)red,
                Green = 0,
                Blue = (int)blue
            };
        }
    }
}
