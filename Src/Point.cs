using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSPCsharp
{
    class Point
    {
        private double x;
        private double y; 

        public Point (double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        public double X
        {
            get
            {
                return x;
            }

            set
            {
                x = value;
            }
        }

        public double Y
        {
            get
            {
                return y;
            }

            set
            {
                y = value;
            }
        }

        static double Distance( Point p1, Point p2, String pointType)
        {

            double xD = p1.X - p2.X;
            double yD = p1.Y - p2.Y;

            if (pointType == "EUC_2D")
            {
                return Math.Ceiling(Math.Sqrt(xD * xD + yD * yD));
            }
            else if (pointType == "ATT")
            {
                double tmp = Math.Sqrt(xD * xD + yD * yD) / 10.0;

                if (Math.Ceiling(tmp) < tmp)
                    return (int)tmp + 1;
                else
                    return tmp;
            }

            throw new Exception("Bad input format");
        }        
    }
}
