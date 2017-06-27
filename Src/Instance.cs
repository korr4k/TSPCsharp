using System;


namespace TSPCsharp
{
    public class Instance
    {
        // input data
        int nNodes;
        Point[] coord;

        // parameters 
        string edgeType;                        // used in Point.distance()
        double timeLimit;                       // overall time limit, in sec.s
        string inputFile;                       // input file

        // global data
        double tStart;                          // real starting time 
        double zBest;                           // best sol. available  
       
        double[] bestSol;                       // best sol. available    
        double bestLb;                          // best lower bound available
        int sizePopulation;


        // model:   
        //int xStart;
        //int qStart;
        //int bigQStart;
        //int sStart;
        //int bigSStart;
        //int yStart;
        //int fStart;
        int zStart;

        // parameters used to build GNUPlot panel
        double xMin = Double.MaxValue;
        double xMax = Double.MinValue;
        double yMin = Double.MaxValue;
        double yMax = Double.MinValue;


        //get and set methods for all parameters:

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

        public double XMin
        {
            get
            {
                return xMin;
            }

            set
            {
                xMin = value;
            }
        }

        public double XMax
        {
            get
            {
                return xMax;
            }

            set
            {
                xMax = value;
            }
        }

        public double YMin
        {
            get
            {
                return yMin;
            }

            set
            {
                yMin = value;
            }
        }

        public double YMax
        {
            get
            {
                return yMax;
            }

            set
            {
                yMax = value;
            }
        }


        public int SizePopulation

        {
            get
            {
                return sizePopulation;
            }

            set
            {
                sizePopulation = value;
            }

        }
    
        //Used to print the points stored in instance.Coord vector
        static public void Print(Instance inst)
        {
            for (int i = 0; i < inst.NNodes; i++)
                Console.WriteLine("Point #" + (i + 1) + "= (" + inst.Coord[i].X + "," + inst.Coord[i].Y + ")");
        }
    }
}
