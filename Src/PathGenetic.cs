using System;

namespace TSPCsharp
{
    public class PathGenetic : PathStandard
    {
        int nRoulette;

        double fitness;

        public PathGenetic(int[] path, double cost)
        {
            this.path = path;
            this.cost = cost;
            CalculateFitness();
            NRoulette = -1;
        }

        public PathGenetic(int[] path, Instance inst)
        {
            this.path = path;
            this.cost = CalculateCost(path, inst);
            CalculateFitness();
            NRoulette = -1;
        }

        public PathGenetic()
        {
            path = null;
            cost = Double.MaxValue;
            fitness = -1;
            NRoulette = -1;
        }

        public double Fitness
        {
            get
            {
                return fitness;
            }
            set
            {
                fitness = value;
            }
        }

        public int NRoulette
        {
            get
            {
                return nRoulette;
            }
            set
            {
                nRoulette = value;
            }
        }

        private void CalculateFitness()
        {
            fitness = 1 / cost;         
        }
    }
}
