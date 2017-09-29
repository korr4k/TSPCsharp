using System;

namespace TSPCsharp
{
    // A PathStandard represents contains the path and the cost of a solution of TSP

    public class PathStandard : ICloneable
    {
        internal int[] path { get; set; }

        internal double cost { get; set; }

        public object Clone()
        {
            return MemberwiseClone();
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

        public double CalculateCost(int[] path, Instance instance)
        {
            int costPath = 0;
            for (int i = 0; i < instance.nNodes - 1; i++)
                costPath += (int)Point.Distance(instance.coord[path[i]], instance.coord[path[i + 1]], instance.edgeType);
            costPath += (int)Point.Distance(instance.coord[path[0]], instance.coord[path[instance.nNodes - 1]], instance.edgeType);
            return costPath;
        }
    }
}
