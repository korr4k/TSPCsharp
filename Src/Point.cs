using System;

namespace TSPCsharp
{
    //Custom class used to stored the coordinates of a point
    //Attention: only 2D points are actually implemented
    public class Point
    {
        private double x;
        private double y; 

        public Point (double x, double y)
        {
            this.x = x;
            this.y = y;
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
                return (int)(Math.Sqrt(xD * xD + yD * yD) + 0.5);
            }
            else if (pointType == "MAN_2D")
            {
                xD = Math.Abs(xD);
                yD = Math.Abs(yD);
                return (int)(xD + yD + 0.5);
            }
            else if (pointType == "MAX_2D")
            {
                xD = Math.Abs(xD);
                yD = Math.Abs(yD);

                int fI = Convert.ToInt32(xD + 0.5);
                int sI = Convert.ToInt32(yD + 0.5);
                if (fI >= sI)
                    return fI;
                else
                    return sI;
            }
            else if (pointType == "GEO")
            {
                double PI = Math.PI;

                int deg = (int)(p1.X + 0.5);
                double min = p1.X - deg;
                double latitude1 = PI * (deg + 5 * min / 3) / 180;

                deg = (int)(p1.Y + 0.5);
                min = p1.Y - deg;
                double longitude1 = PI * (deg + 5 * min / 3.0) / 180;

                deg = (int)(p2.X + 0.5);
                min = p2.X - deg;
                double latitude2 = PI * (deg + 5 * min / 3.0) / 180;

                deg = (int)(p2.Y + 0.5);
                min = p2.Y - deg;
                double longitude2 = PI * (deg + 5 * min / 3.0) / 180;

                double RRR = 6378.388;
                double q1 = Math.Cos(longitude1 - longitude2);
                double q2 = Math.Cos(latitude1 - latitude2);
                double q3 = Math.Cos(latitude1 + latitude2);

                return (int)((RRR * Math.Acos( 0.5 * ((1 + q1) * q2 - (1 - q1) * q3))) + 1);
            }
            else if (pointType == "ATT")
            {
                double rij = Math.Sqrt((xD * xD + yD * yD) / 10.0);
                int tij = Convert.ToInt32(rij);

                if (tij < rij)
                    return tij + 1;
                else
                    return tij;
            }
            else if (pointType == "CEIL_2D")
            {
                return Math.Ceiling(Math.Sqrt(xD * xD + yD * yD) + 0.5);
            }
            //If each statem is false, the used point type is not yet implemented
            throw new Exception("Bad input format");
        }        
    }
}
