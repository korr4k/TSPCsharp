using System;
using System.IO;
using System.Diagnostics;

namespace TSPCsharp
{
    class Program
    {

        //Setting constant value, to access them use Program.<name>
        public const int VERBOSE = 5;
        public const double EPSILON = 1E-9;
        public const int TICKS_PER_SECOND = 1000;

        //Stopwatch type is used to evaluate the execution time
        static Stopwatch clock;

        static void Main(string[] args)
        {
            if (args.Length < 4)
                throw new System.Exception("Input args should be at least 2");

            //Writing the inpute parameters
            if (VERBOSE >= 9)
            {
                Console.Write("Input parameters: ");
                for (int i = 0; i < args.Length; i++) 
                    Console.Write(args[i].ToString() + " ");
                Console.WriteLine("\n\n\n");
            }

            /*
             * Saving input parameters inside an Instance variable
             * Attention: if file name or timilimit are missing 
             * an exception will be throw 
            */
            Instance inst = new Instance();

            ParseInst(inst,args);

            //Reading the input file and storing its data into inst
            Populate(inst);


            //Starting the clock
            clock = new Stopwatch();
            clock.Start();


            //Starting the elaboration of the TSP problem
            //The boolean returning value tells if everything went fine
            if (!TSP.TSPOpt(inst, clock))
               throw new System.Exception("Impossible to find the optimal solution for the given instance");


            //Stopping the clock
            clock.Stop();

            //Printing the time needed to find the optimal solution
            //If it's equal to the input timelimit the solution is not the optimal
            Console.WriteLine("The optimal solution was found in " + clock.ElapsedMilliseconds/1000.0 + " s");
            

            //Only to check the output
            Console.ReadLine();
        }

        static void ParseInst(Instance inst, String[] input)
        {
            //input is equal to the args vector, every string is obtained by splitting
            //the input at each blank space
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == "-file")
                {
                    //Expecting that the next value is the file name
                    inst.InputFile = input[++i];
                    continue;
                }
                if (input[i] == "-timelimit")
                {
                    //Expecting that the next value is the time limit in seconds
                    inst.TimeLimit = Convert.ToDouble(input[++i]);
                    continue;
                }
            }

            //At least the input file name and the timelimit are needed
            if (inst.InputFile == null || inst.TimeLimit == 0)
                throw new Exception("File input name and/or timelimit are missing");
        }

        static void Populate(Instance inst)
        {
            //line will store each lines of the input file
            string line;
            //When true line is storing a point's coordinates of the problem
            bool readingCoordinates = false;

            try
            {
                //StreaReader types need to be disposed at the end
                //the 'using' key automatically handles that necessity
                using (StreamReader sr = new StreamReader("..\\..\\..\\..\\Data\\" + inst.InputFile))
                {
                    //The whole file is read
                    while ((line = sr.ReadLine()) != null)
                    {
                        //Content of those lines are simply printed 
                        if (line.StartsWith("NAME") || line.StartsWith("COMMENT") || line.StartsWith("TYPE"))
                        {
                            if (VERBOSE >= 5) Console.WriteLine(line);
                            continue;
                        }

                        //inst needs to be updated according to the value of his line
                        if (line.StartsWith("DIMENSION"))
                        {
                            //Setting the # of nodes for the problem
                            inst.NNodes = Convert.ToInt32(line.Remove(0, line.IndexOf(':') + 2));
                            //Allocating the space for the points that will be read
                            inst.Coord = new Point[inst.NNodes];
                            if (VERBOSE >= 5)
                                Console.WriteLine(line);
                            continue;
                        }

                        //inst needs to be updated according to the value of his line
                        if (line.StartsWith("EDGE_WEIGHT_TYPE"))
                        {
                            string tmp = line.Remove(0, line.IndexOf(':') + 2);
                            if (!(tmp == "EUC_2D" || tmp == "ATT"))
                                throw new System.Exception("Format error:  only EDGE_WEIGHT_TYPE == EUC_2D || ATT implemented so far!!!!!!");
                            //Storing the edge type is necessary to correctly evaluete the distance between two points
                            inst.EdgeType = tmp;
                            if (VERBOSE >= 5)
                                Console.WriteLine(line);
                            continue;
                        }

                        //When this line is read, the nexts contain the points' coordinates
                        if (line.StartsWith("NODE_COORD_SECTION"))
                        {
                            if (inst.NNodes <= 0)
                                throw new System.Exception("DIMENSION section should appear before NODE_COORD_SECTION section");
                            if (VERBOSE >= 5)
                                Console.WriteLine(line);
                            readingCoordinates = true;
                            continue;
                        }
                        

                        //This line signals the end of the file
                        if (line.StartsWith("EOF"))
                        {
                            Instance.Print(inst);
                            Console.WriteLine(line);
                            //Correct end of the file
                            break;
                        }


                        //if true, line contains a point's coordinate
                        if (readingCoordinates)
                        {

                            string[] elements = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            //First item of the line needs to be the index of the point
                            //Expected minimum index to be 1 and max to be instance.NNodes
                            int i = Convert.ToInt32(elements[0]);
                            if (i < 0 || i > inst.NNodes)
                                throw new System.Exception("Unknown node in NODE_COORD_SECTION section");
                            //Vectors starts at index 0 not 1, it is necessary to perform a -1
                            inst.Coord[i - 1] = new Point(Convert.ToDouble(elements[1]), Convert.ToDouble(elements[2]));

                            //Storing the smallest and biggest x and y coordinates
                            if (Convert.ToDouble(elements[1]) < inst.XMin)
                                inst.XMin = Convert.ToDouble(elements[1]);
                            if (Convert.ToDouble(elements[1]) > inst.XMax)
                                inst.XMax = Convert.ToDouble(elements[1]);
                            if (Convert.ToDouble(elements[2]) < inst.YMin)
                                inst.YMin = Convert.ToDouble(elements[2]);
                            if (Convert.ToDouble(elements[2]) > inst.YMax)
                                inst.YMax = Convert.ToDouble(elements[2]);

                            continue;
                        }

                        //Blank lines are ignored
                        if (line == "")
                            continue;

                        //If each if statement is false, the file contains at least one line with a wrong format
                        throw new System.Exception("Wrong format for the current simplified parser!!!!!!!!!");

                    }
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
        }
    }
}
