using System;


namespace TSPCsharp
{
    class Instance
    {
        //input data
        int nNodes;
        Point[] coord;


        // parameters 
        string edgeType;                        // used in Point.distance()
        double timeLimit;                       // overall time limit, in sec.s
        string inputFile;                       // input file

        //global data
        double tStart;
        double zBest;                           // best sol. available  
        double tBest;                           // time for the best sol. available  
        double[] bestSol;                       // best sol. available    
        double bestLb;                          // best lower bound available

        // model;     
        int xStart;
        int qStart;
        int bigQStart;
        int sStart;
        int bigSStart;
        int yStart;
        int fStart;
        int zStart;

        public int NNodes
        {
            get
            {
                return nNodes;
            }

            set
            {
                nNodes = value;
            }
        }

        public string EdgeType
        {
            get
            {
                return edgeType;
            }

            set
            {
                edgeType = value;
            }
        }

        public double TimeLimit
        {
            get
            {
                return timeLimit;
            }

            set
            {
                timeLimit = value;
            }
        }

        public string InputFile
        {
            get
            {
                return inputFile;
            }

            set
            {
                inputFile = value;
            }
        }

        public double TStart
        {
            get
            {
                return tStart;
            }

            set
            {
                tStart = value;
            }
        }

        public double ZBest
        {
            get
            {
                return zBest;
            }

            set
            {
                zBest = value;
            }
        }

        public double TBest
        {
            get
            {
                return tBest;
            }

            set
            {
                tBest = value;
            }
        }

        public double[] BestSol
        {
            get
            {
                return bestSol;
            }

            set
            {
                bestSol = value;
            }
        }

        public double BestLb
        {
            get
            {
                return bestLb;
            }

            set
            {
                bestLb = value;
            }
        }
        
        public Point[] Coord
        {
            get
            {
                return coord;
            }

            set
            {
                coord = value;
            }
        }

        public int ZStart
        {
            get
            {
                return zStart;
            }

            set
            {
                zStart = value;
            }
        }

        static public void Print(Instance inst)
        {
            for (int i = 0; i < inst.NNodes; i++)
                Console.WriteLine("Point #" + (i + 1) + "= (" + inst.Coord[i].X + "," + inst.Coord[i].Y + ")");
        }
    }
}
