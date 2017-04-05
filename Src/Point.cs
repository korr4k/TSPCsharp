using System;

namespace TSPCsharp
{
    //Custom class used to stored the coordinates of a point
    //Attention: only 2D points are actually implemented
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

         public static double Distance( Point p1, Point p2, String pointType)
        {

            //Implamentation of the distance algorithms proposed by the official documentation

            double xD = p1.X - p2.X;
            double yD = p1.Y - p2.Y;

            if (pointType == "EUC_2D")
            {
                //Ceiling is used to round at the next int value
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

            //If each statem is false, the used point type is not yet implemented
            throw new Exception("Bad input format");
        }        
    }
}
