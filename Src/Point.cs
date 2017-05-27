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
                return Convert.ToInt32(Math.Sqrt(xD * xD + yD * yD) + 0.5);
            }
            else if (pointType == "MAN_2D")
            {
                xD = Math.Abs(xD);
                yD = Math.Abs(yD);
                return Convert.ToInt32(xD + yD + 0.5);
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
            else if(pointType == "GEO")
            {
                double PI = 3.141592;

                int deg = Convert.ToInt32(p1.X);
                double min = p1.X - deg;
                double latitude1 = PI * (deg + 0.5 * min / 3.0) / 180;
                deg = Convert.ToInt32(p1.Y);
                min = p1.Y - deg;
                double longitude1 = PI * (deg + 0.5 * min / 3.0) / 180;

                deg = Convert.ToInt32(p2.X);
                min = p2.X - deg;
                double latitude2 = PI * (deg + 0.5 * min / 3.0) / 180;
                deg = Convert.ToInt32(p2.Y);
                min = p2.Y - deg;
                double longitude2 = PI * (deg + 0.5 * min / 3.0) / 180;

                double RRR = 6378.388;
                double q1 = Math.Cos(longitude1 - longitude2);
                double q2 = Math.Cos(latitude1 - latitude2);
                double q3 = Math.Cos(latitude1 + latitude2);

                return Convert.ToInt32((RRR * Math.Acos(0.5 * ((1.0 + q1) * q2 - (1.0 - q1) * q3))) + 1.0);
            }
            else if (pointType == "ATT")
            {
                double rij = Math.Sqrt((xD * xD + yD * yD) / 10.0);
                int tij = Convert.ToInt32(rij);

                if (tij < rij)
                    return tij+1;
                else
                    return tij;
            }else if(pointType == "CEIL_2D")
            {
                return Math.Ceiling(Math.Sqrt(xD * xD + yD * yD) + 0.5);
            }

            //If each statem is false, the used point type is not yet implemented
            throw new Exception("Bad input format");
        }        
    }
}
