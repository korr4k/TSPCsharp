using System;

namespace TSPCsharp
{
    //PathGenetic is an extension of a PathStandard. PathGenetic is needed into Genetic algorithms
    public class PathGenetic : PathStandard
    {
        internal int nRoulette { get; set; }
        internal double fitness { get; set; }

        public PathGenetic(int[] path, double cost) : base()
        {
            this.path = path;
            this.cost = cost;
            CalculateFitness();
            nRoulette = -1;
        }

        public PathGenetic(int[] path, Instance inst) : base(path, inst)
        {
            CalculateFitness();
            nRoulette = -1;
        }

        public PathGenetic(): base()
        {
            fitness = -1;
            nRoulette = -1;
        }

        private void CalculateFitness()
        {
            fitness = 1 / cost;         
        }
    }
}
