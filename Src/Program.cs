using System;
using System.IO;
using System.Diagnostics;

namespace TSPCsharp
{
    class Program
    {
        public const int VERBOSE = 50;
        const double XSMALL = 1E-5; 		
        const double EPSILON = 1E-9;		
        const int TICKS_PER_SECOND = 1000;

        static Stopwatch clock;

        static void Main(string[] args)
        {
            if (args.Length < 2)
                throw new System.Exception("Input args should be at least 2");

            if (VERBOSE >= 2)
            {
                Console.Write("Input parameters: ");
                for (int i = 0; i < args.Length; i++) 
                    Console.Write(args[i].ToString() + " ");
                Console.WriteLine("\n\n\n");
            }
            
            Instance inst = ParseInst(args);

            Populate(inst);

            clock = new Stopwatch();
            clock.Start();

            if (!TSP.TSPOpt(inst, clock))
               throw new System.Exception("Impossible to find the optimal solution for the given instance");

            clock.Stop();

            Console.WriteLine("The optimal solution was found in " + clock.ElapsedMilliseconds/1000.0 + " s");

            Console.ReadLine();
        }

        static Instance ParseInst( String[] input)
        {
            Instance inst = new Instance();

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == "-file")
                {
                    inst.InputFile = input[++i];
                    continue;
                }
                if (input[i] == "-timelimit")
                {
                    inst.TimeLimit = Convert.ToDouble(input[++i]);
                    continue;
                }
            }

            return inst;
        }

        static void Populate(Instance inst)
        {
            string line;
            bool readingCoordinates = false;

            try
            {
                using (StreamReader sr = new StreamReader("..\\..\\..\\..\\Data\\" + inst.InputFile))
                {
                    while ((line = sr.ReadLine()) != null)
                    {

                        if (line.StartsWith("NAME") || line.StartsWith("COMMENT") || line.StartsWith("TYPE"))
                        {
                            if (VERBOSE >= 1) Console.WriteLine(line);
                            continue;
                        }

                        if (line.StartsWith("DIMENSION"))
                        {
                            inst.NNodes = Convert.ToInt32(line.Remove(0, 11));
                            inst.Coord = new Point[inst.NNodes];
                            if (VERBOSE >= 1)
                                Console.WriteLine(line);
                            continue;
                        }

                        if (line.StartsWith("EDGE_WEIGHT_TYPE"))
                        {
                            string tmp = line.Remove(0, 19);
                            if (!(tmp == "EUC_2D" || tmp == "ATT"))
                                throw new System.Exception("Format error:  only EDGE_WEIGHT_TYPE == EUC_2D || ATT implemented so far!!!!!!");
                            inst.EdgeType = tmp;
                            if (VERBOSE >= 1)
                                Console.WriteLine(line);
                            continue;
                        }

                        if (line.StartsWith("NODE_COORD_SECTION"))
                        {
                            if (inst.NNodes <= 0)
                                throw new System.Exception("DIMENSION section should appear before NODE_COORD_SECTION section");
                            if (VERBOSE >= 1)
                                Console.WriteLine(line);
                            readingCoordinates = true;
                            continue;
                        }

                        if (line.StartsWith("EOF"))
                        {
                            Instance.Print(inst);
                            Console.WriteLine(line);
                            break;
                        }

                        if (readingCoordinates)
                        {

                            string[] elements = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            int i = Convert.ToInt32(elements[0]);
                            if (i < 0 || i > inst.NNodes)
                                throw new System.Exception("Unknown node in NODE_COORD_SECTION section");
                            inst.Coord[i - 1] = new Point(Convert.ToInt32(elements[1]), Convert.ToInt32(elements[2]));
                            continue;
                        }

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
