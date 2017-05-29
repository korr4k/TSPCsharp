using System;

namespace TSPCsharp
{
    // A PathStandard represents contains the path and the cost of a solution of TSP

    public class PathStandard : ICloneable
    {
        internal int[] path;

        internal double cost;

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public double Cost
        {
            get
            {
                return cost;
            }
            set
            {
                cost = value;
            }
        }

        public int[] Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
            }
        }

        public PathStandard()
        {
            path = null;
            cost = Double.MaxValue;
        }

        public PathStandard(int[] path, Instance inst)
        {
            this.path = path;
            cost = CalculateCost(path, inst);
        }

        internal double CalculateCost(int[] path, Instance instance)
        {
            int costPath = 0;
            for (int i = 0; i < instance.NNodes - 1; i++)
                costPath += (int)Point.Distance(instance.Coord[path[i]], instance.Coord[path[i + 1]], instance.EdgeType);
            costPath += (int)Point.Distance(instance.Coord[path[0]], instance.Coord[path[instance.NNodes - 1]], instance.EdgeType);
            return costPath;
        }
    }
}
