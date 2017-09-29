using System;

namespace TSPCsharp
{
    public class Point
    {
        internal double x { get; set; }
        internal double y { get; set; }

        public Point(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public static double Distance(Point p1, Point p2, String pointType)
        {
            //Implamentation of the distance algorithms proposed by the official documentation

            double xD = p1.x - p2.x;
            double yD = p1.y - p2.y;

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

                int deg = (int)(p1.x + 0.5);
                double min = p1.x - deg;
                double latitude1 = PI * (deg + 5 * min / 3) / 180;

                deg = (int)(p1.y + 0.5);
                min = p1.y - deg;
                double longitude1 = PI * (deg + 5 * min / 3.0) / 180;

                deg = (int)(p2.x + 0.5);
                min = p2.x - deg;
                double latitude2 = PI * (deg + 5 * min / 3.0) / 180;

                deg = (int)(p2.y + 0.5);
                min = p2.y - deg;
                double longitude2 = PI * (deg + 5 * min / 3.0) / 180;

                double RRR = 6378.388;
                double q1 = Math.Cos(longitude1 - longitude2);
                double q2 = Math.Cos(latitude1 - latitude2);
                double q3 = Math.Cos(latitude1 + latitude2);

                return (int)((RRR * Math.Acos(0.5 * ((1 + q1) * q2 - (1 - q1) * q3))) + 1);
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
