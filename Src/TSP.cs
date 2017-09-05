using ILOG.Concert;
using ILOG.CPLEX;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
namespace TSPCsharp
{
    class TSPIncumbent: Cplex.IncumbentCallback
    {
        public bool upgradeInc = false;

        private System.Object lockThis = new System.Object();
        public TSPIncumbent()
        {
         
        }

        public override void Main()
        {
            double c = GetIncumbentObjValue();
       
            if (GetIncumbentObjValue() != 1E+75)
            {
                lock (lockThis)
                {
                    if (upgradeInc == false)
                        upgradeInc = true;
                    else
                        Reject();
                }
            }    
        }
    }
    
    class TSPInformativeCallback : Cplex.MIPInfoCallback
    {
        TSPIncumbent tspI;
 
        //Constructor of the class
        public TSPInformativeCallback(TSPIncumbent tspI)
        {
            this.tspI = tspI;
        }

        public override void Main()
        {
            double c = GetIncumbentObjValue();

           if (tspI.upgradeInc == true)
           {
                tspI.upgradeInc = false;
               //Stop the solver                          
                Abort();
           } 
       }
   }

   /*
   class TSPInformativeCallback : Cplex.MIPInfoCallback
   {
       //Stored the value of the previousIncumbent
       double previusIncumbent = Double.MaxValue;

       //Constructor of the class
       public TSPInformativeCallback()
       {

       }

       public override void Main()
       {
           //Only if the Incumbent solution is upgrade stop the solver
           if (GetIncumbentObjValue() < previusIncumbent)
           {
               //Sored the actual incumbet for the next iteration
               previusIncumbent = GetIncumbentObjValue();
               //Stop the solver                          
               Abort();
           }
       }
   }

   */
    class TSP
    {
        [DllImport("ConcordeDLL.dll")]
        //public static extern void Concorde(char[] fileName, int timeLimit);
        public static extern int Concorde(StringBuilder fileName, int timeLimit);

        //"Main" method
        static public bool TSPOpt(Instance instance, Stopwatch clock)
        {
            Cplex cplex = new Cplex();
            Process process = Utility.InitProcess(instance);

            //Real starting time is stored inside instance
            instance.TStart = clock.ElapsedMilliseconds / 1000.0;

            clock.Stop();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("\nPress enter to continue, attention: the display will be cleared");
            Console.ReadLine();
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.White;

            Console.Write("Insert 1 to use the classic loop method, 2 to use the optimal branch & cut , 3 to use heuristics methods , 4 to use matheuristics: ");

            switch (Console.ReadLine())
            {
                case "1":
                    {
                        Console.Write("\nInsert 1 for normal resolution, 2 to specifie the % precion, 3 to use only a # of the nearest edges, 4 to use both previous options: ");
                        switch (Console.ReadLine())
                        {
                            case "1":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    Utility.MipTimelimit(cplex, instance, clock);
                                    //Calling the proper resolution method
                                    Loop(cplex, instance, clock, process, -1, -1);
                                    break;
                                }

                            case "2":
                                {
                                    Console.Write("\nWrite the % precision: ");
                                    //storing the percentage selected
                                    double percentage = Convert.ToDouble(Console.ReadLine());
                                    //Clock restart
                                    clock.Start();
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    Utility.MipTimelimit(cplex, instance, clock);
                                    //Calling the proper resolution method
                                    Loop(cplex, instance, clock, process, percentage, -1);
                                    break;
                                }

                            case "3":
                                {
                                    Console.Write("\nWrite the # of nearest edges: ");
                                    //number of nearest edges selected
                                    int numb = Convert.ToInt32(Console.ReadLine());
                                    //Clock restart
                                    clock.Start();
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    Utility.MipTimelimit(cplex, instance, clock);
                                    //Calling the proper resolution method
                                    Loop(cplex, instance, clock, process , -1, numb);
                                    break;
                                }

                            case "4":
                                {
                                    Console.Write("\nWrite the % precision: ");
                                    //storing the percentage selected
                                    double percentage = Convert.ToDouble(Console.ReadLine());
                                    Console.Write("Write the # of nearest edges: ");
                                    //number of nearest edges selected
                                    int numb = Convert.ToInt32(Console.ReadLine());
                                    clock.Start();
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    Utility.MipTimelimit(cplex, instance, clock);
                                    //Calling the proper resolution method
                                    Loop(cplex, instance, clock, process, percentage, numb);
                                    break;
                                }

                            default:
                                throw new System.Exception("Bad argument");
                        }
                        break;
                    }

                case "2":
                    {
                        Console.Write("\nInsert 1 to use LazyCallback, 2 to use UserCutCallBack: ");
                        switch (Console.ReadLine())
                        {
                            case "1":
                                {
                                    //Restarting the clock
                                    clock.Start();
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    Utility.MipTimelimit(cplex, instance, clock);
                                    //Calling the proper resolution method
                                    CallBackMethod(cplex, instance, clock, process);
                                    break;
                                }
                            case "2":
                                {
                                    //Restarting the clock
                                    clock.Restart();
                                    instance.XBest = Concorde(new StringBuilder(instance.InputFile), (int)instance.TimeLimit);
                                    Utility.PrintGNUPlot(process, instance.InputFile, 1, instance.XBest, -1);
                                    Console.WriteLine("Best solution: " + instance.BestLb);
                                    break;
                                }
                            default:
                                throw new System.Exception("Bad argument");
                        }
                        break;
                    }
                case "3":
                    {
                        //Random seed for random variable
                        Random rnd = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);

                        Console.Write("\nInsert 1 to use multi start 2OPT, 2 to use Tabu-Search, 3 to use VNS or 4 to use Genetic algorithm: ");
                        switch (Console.ReadLine())
                        {
                            case "1":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    MultiStart(instance, process, rnd, clock, "Multi Start 2OPT");
                                    break;
                                }
                            case "2":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    TabuSearch(instance, process, rnd, clock, "Tabu-Search");
                                    break;
                                }
                            case "3":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    VNS(instance, process, rnd, clock, "VNS");
                                    break;
                                }
                            case "4":
                                {
                                    Console.Write("\nWrite the size of the population : ");
                                    //storing the percentage selected
                                    int sizePopulation = Convert.ToInt32(Console.ReadLine());
                                    clock.Start();
                                    //Calling the proper resolution method
                                    GeneticAlgorithm(instance, process, rnd, clock, sizePopulation,"GeneticAlgorithm");
                                    break;
                                }
                            default:
                                throw new System.Exception("Bad argument");
                        }
                        break;
                    }

                case "4":
                    {
                        //Random seed for random variable
                        Random rnd = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);

                        Console.Write("\nInsert 1 to use HardFixing, 2 to use Local Branch, 3 Polishing: ");
                        switch (Console.ReadLine())
                        {
                            case "1":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    HardFixing(cplex, instance, process, rnd, clock);
                                    break;
                                }
                            case "2":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    LocalBranching(cplex, instance, process, rnd, clock);
                                    
                                    break;
                                }
                            case "3":
                                {
                                    Console.Write("\nWrite the size of the population : ");
                                    //storing the percentage selected
                                    int sizePopulation = Convert.ToInt32(Console.ReadLine());
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    Polishing(cplex, instance, process, rnd, sizePopulation, clock);

                                    break;
                                }                          
                            default:
                                throw new System.Exception("Bad argument");
                        }
                        break;
                    }
                    
                default:
                    throw new System.Exception("Bad argument");
            }
            return true;
        }

        //Handle the resolution method without callbacks
        static void Loop(Cplex cplex, Instance instance, Stopwatch clock, Process process, double perc, int numb)
        {
            int typeSol = 1;

            //epGap is false when EpGap parameter is at default
            bool epGap = false;

            //allEdges is true when all possible links have their upper bound to 1
            bool allEdges = true;

            //Setting EpGap if a valid percentage is specified
            if (perc >= 0 && perc <= 1)
            {
                cplex.SetParam(Cplex.DoubleParam.EpGap, perc);

                epGap = true;

                typeSol = 0;
            }

            //numb is equal to -1 when all links upper bound are 1
            if (numb != -1)
            {
                allEdges = false;

                typeSol = 0;
            }

            //Building the maximum # of links that involves each node
            INumVar[] x = Utility.BuildModel(cplex, instance, numb);

            //Allocating the correct space to store the optimal solution
            //Only links from node i to i with i < i are considered
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Init buffers
            int[] relatedComponents = new int[instance.NNodes];
            List<ILinearNumExpr> rcExpr = new List<ILinearNumExpr>();
            List<int> bufferCoeffRC = new List<int>();

            //Temporary variable
            double[] linksDistances = new double[(instance.NNodes - 1) * instance.NNodes / 2]; ;

            do
            {
                //When only one related component is found and ehuristics methods are active they are disabled
                if (rcExpr.Count == 1)
                {
                    epGap = false;

                    allEdges = true;

                    cplex.SetParam(Cplex.DoubleParam.EpGap, 1e-06);

                    Utility.ResetVariables(x);

                    typeSol = 1;
                }

                //Cplex solves the current model
                cplex.Solve();

                //Initializing the arrays used to eliminate the subtour
                Utility.InitCC(relatedComponents);

                rcExpr = new List<ILinearNumExpr>();
                bufferCoeffRC = new List<int>();

                //Init the StreamWriter for the current solution
                StreamWriter file = new StreamWriter(instance.InputFile + ".dat", false);

                //Storing the optimal value of the objective function
                instance.BestLb = cplex.ObjValue;

                //Blank line
                Console.WriteLine();

                //Printing the optimal solution and the GNUPlot input file
                for (int i = 0; i < instance.NNodes; i++)
                {
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {

                        //Retriving the correct index position for the current link inside x
                        int position = Utility.xPos(i, j, instance.NNodes);

                        //Reading the optimal solution for the actual link (i,i)
                        linksDistances[position] = cplex.GetValue(x[position]);


                        //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                        if (linksDistances[position] >= 0.5)
                        {
                            /*
                             *Current GNUPlot format is:
                             *-- previus link --
                             *<Blank line>
                             *Xi Yi <index(i)>
                             *Xj Yj <index(i)>
                             *<Blank line> 
                             *-- next link --
                            */
                            file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                            file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");

                            //Updating the model with the current subtours elimination
                            Utility.UpdateCC(cplex, x, rcExpr, bufferCoeffRC, relatedComponents, i, j);
                        }
                    }
                }

                //Only when more than one related components are found they are added to the model
                if (rcExpr.Count > 1)
                {
                    for (int i = 0; i < rcExpr.Count; i++)
                        cplex.AddLe(rcExpr[i], bufferCoeffRC[i] - 1);
                }

                //GNUPlot input file needs to be closed
                file.Close();

                //Accessing GNUPlot to read the file
                if (Program.VERBOSE >= -1)
                    Utility.PrintGNUPlot(process, instance.InputFile, typeSol, instance.BestLb, -1);

                //Blank line
                cplex.Output().WriteLine();

                //Writing the value
                cplex.Output().WriteLine("xOPT = " + instance.BestLb + "\n");

            } while (rcExpr.Count > 1 || epGap || !allEdges); //if there is more then one related components the solution is not optimal 

            instance.BestSol = linksDistances;
            instance.XBest = instance.BestLb;

            //Exporting the updated model
            if (Program.VERBOSE >= -1)
                cplex.ExportModel(instance.InputFile + ".lp");

            //Closing Cplex link
            cplex.End();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -1)
                Utility.PrintGNUPlot(process, instance.InputFile, typeSol, instance.XBest, -1);
        }

        //Handle the callback resolution method
        static void CallBackMethod(Cplex cplex, Instance instance, Stopwatch clock, Process process)
        {
            int typeSol = 1;

            //-1 means that all links are enabled
            INumVar[] x = Utility.BuildModel(cplex, instance, -1);

            //Initializing the vector
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Adding lazycallback
            cplex.Use(new TSPLazyConsCallback(cplex, x, instance, process, true));

            // Turn on traditional search for use with control callbacks
            cplex.SetParam(Cplex.Param.MIP.Strategy.Search, Cplex.MIPSearch.Auto);

            //Setting cplex # of threads
            cplex.SetParam(Cplex.Param.Threads, cplex.GetNumCores());

            //Solving
            cplex.Solve();

            //Init the StreamWriter for the current solution
            StreamWriter file = new StreamWriter(instance.InputFile + ".dat", false);

            //Storing the optimal value of the objective function
            instance.XBest = cplex.ObjValue;

            //Blank line
            cplex.Output().WriteLine();

            //Printing the optimal solution and the GNUPlot input file
            for (int i = 0; i < instance.NNodes; i++)
            {
                for (int j = i + 1; j < instance.NNodes; j++)
                {

                    //Retriving the correct index position for the current link inside z
                    int position = Utility.xPos(i, j, instance.NNodes);

                    //Reading the optimal solution for the actual link (i,i)
                    instance.BestSol[position] = cplex.GetValue(x[position]);

                    //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                    if (instance.BestSol[position] >= 0.5)
                    {
                        /*
                         *Current GNUPlot format is:
                         *-- previus link --
                         *<Blank line>
                         *Xi Yi <index(i)>
                         *Xj Yj <index(i)>
                         *<Blank line> 
                         *-- next link --
                        */
                        file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                        file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");
                    }
                }
            }

            //GNUPlot input file needs to be closed
            file.Close();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -1)
                Utility.PrintGNUPlot(process, instance.InputFile, typeSol, instance.XBest, -1);

            //Blank line
            cplex.Output().WriteLine();

            //Writing the value
            cplex.Output().WriteLine("xOPT = " + instance.XBest + "\n");

            //Exporting the updated model
            if (Program.VERBOSE >= -1)
                cplex.ExportModel(instance.InputFile + ".lp");

        }

        static void MultiStart(Instance instance, Process process, Random rnd, Stopwatch clock, string choice)
        {
            PathStandard incumbentSol = new PathStandard();
            PathStandard heuristicSol;
            int typeSol = 0;
            List<int>[] listArray = Utility.BuildSLComplete(instance);

            do
            {
                heuristicSol = Utility.NearestNeightbor(instance, rnd, listArray);

                TwoOpt(instance, heuristicSol);

                if (incumbentSol.cost > heuristicSol.cost)
                {
                    incumbentSol = heuristicSol;
                    Utility.PrintHeuristicSolution(instance, process, incumbentSol, incumbentSol.cost, typeSol);

                    Console.WriteLine("Incumbed changed");
                }
                else
                    Console.WriteLine("Incumbed not changed");

            } while (clock.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

            Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);
        }

        static void TabuSearch(Instance instance, Process process, Random rnd, Stopwatch clock, string choice)
        {
            int typeSol = 0;
            List<int>[] listArray = Utility.BuildSLComplete(instance);
            PathStandard incumbentSol;
            PathStandard solHeuristic = Utility.NearestNeightbor(instance, rnd, listArray);
            incumbentSol = (PathStandard)solHeuristic.Clone();

            Utility.PrintHeuristicSolution(instance, process, incumbentSol, incumbentSol.cost, typeSol);

            Tabu tabu = new Tabu("A", instance, 100);
            TabuSearch(instance, process, tabu, solHeuristic, incumbentSol, clock);

            solHeuristic = (PathStandard)incumbentSol.Clone();
            TwoOpt(instance, solHeuristic);

            Utility.PrintHeuristicSolution(instance, process, solHeuristic, incumbentSol.cost, typeSol);

            Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);
        }

        static void VNS(Instance instance, Process process, Random rnd, Stopwatch clock, string choice)
        {
            int typeSol = 0;
            List<int>[] listArray = Utility.BuildSLComplete(instance);
            PathStandard incumbentSol;
            PathStandard solHeuristic = Utility.NearestNeightbor(instance, rnd, listArray);
            incumbentSol = (PathStandard)solHeuristic.Clone();
            Utility.PrintHeuristicSolution(instance, process, incumbentSol, incumbentSol.cost, typeSol);

            do
            {
                TwoOpt(instance, solHeuristic);

                if (incumbentSol.cost > solHeuristic.cost)
                {
                    incumbentSol = (PathStandard)solHeuristic.Clone();

                    Utility.PrintHeuristicSolution(instance, process, incumbentSol, incumbentSol.cost, typeSol);

                    Console.WriteLine("Incumbed changed");
                }
                else
                    Console.WriteLine("Incumbed not changed");

                VNS(instance, solHeuristic, rnd);

            } while (clock.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

            Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);
        }

        static void GeneticAlgorithm(Instance instance, Process process, Random rnd, Stopwatch clock, int sizePopulation, string choice)
        {
            PathGenetic incumbentSol = new PathGenetic();
            PathGenetic currentBestPath = null;

            List<PathGenetic> OriginallyPopulated = new List<PathGenetic>();
            List<PathGenetic> ChildPoulation = new List<PathGenetic>();

            List<int>[] listArray = Utility.BuildSLComplete(instance);

            //Generate the first population
            for (int i = 0; i < sizePopulation; i++)
                OriginallyPopulated.Add(Utility.NearestNeightborGenetic(instance, rnd, true, listArray));
            do
            {
                //Generate the child 
                for (int i = 0; i < sizePopulation; i++)
                {
                    if (i % 2 != 0)
                        ChildPoulation.Add(Utility.GenerateChild(instance, rnd, OriginallyPopulated[i], OriginallyPopulated[i - 1], listArray));
                }

                OriginallyPopulated = Utility.NextPopulation(instance, sizePopulation, OriginallyPopulated, ChildPoulation);

                //currentBestPath contains the best path of the current population
                currentBestPath = Utility.BestSolution(OriginallyPopulated, incumbentSol);

                if (currentBestPath.cost < incumbentSol.cost)
                {
                    incumbentSol = (PathGenetic)currentBestPath.Clone();
                    Utility.PrintGeneticSolution(instance, process, incumbentSol);
                }

                // We empty the list that contain the child
                ChildPoulation.RemoveRange(0, ChildPoulation.Count);

            } while (clock.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

            Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);
        }

        public static void TwoOpt(Instance instance, PathStandard pathG)
        {
            int indexStart = 0;
            int cnt = 0;
            bool found = false;

            do
            {
                found = false;
                int a = indexStart;
                int b = pathG.path[a];
                int c = pathG.path[b];
                int d = pathG.path[c];

                for (int i = 0; i < instance.NNodes - 3; i++)
                {
                    double distAC = Point.Distance(instance.Coord[a], instance.Coord[c], instance.EdgeType);
                    double distBD = Point.Distance(instance.Coord[b], instance.Coord[d], instance.EdgeType);
                    double distAD = Point.Distance(instance.Coord[a], instance.Coord[d], instance.EdgeType);
                    double distBC = Point.Distance(instance.Coord[b], instance.Coord[c], instance.EdgeType);

                    double distTotABCD = Point.Distance(instance.Coord[a], instance.Coord[b], instance.EdgeType) +
                        Point.Distance(instance.Coord[c], instance.Coord[d], instance.EdgeType);

                    if (distAC + distBD < distTotABCD)
                    {
                        Utility.SwapRoute(c, b, pathG);

                        pathG.path[a] = c;
                        pathG.path[b] = d;

                        pathG.cost = pathG.cost - distTotABCD + distAC + distBD;

                        indexStart = 0;
                        cnt = 0;
                        found = true;
                        break;
                    }

                    c = d;
                    d = pathG.path[c];
                }

                if (!found)
                {
                    indexStart = b;
                    cnt++;
                }

            } while (cnt < instance.NNodes);
        }

        static void TabuSearch(Instance instance, Process process, Tabu tabu, PathStandard currentSol, PathStandard incumbentPath, Stopwatch clock)
        {
            int typeSol;
            int indexStart = 0;
            string nextBestMove = "";
            string nextWorstMove = "";
            double bestGain = double.MaxValue;
            double worstGain = double.MinValue;
            int a, b, c, d;
            double distAC, distBD, distTotABCD;

            do
            {
                for (int j = 0; j < instance.NNodes; j++, indexStart = b)
                {
                    a = indexStart;
                    b = currentSol.path[a];
                    c = currentSol.path[b];
                    d = currentSol.path[c];

                    for (int i = 0; i < instance.NNodes - 3; i++, c = d, d = currentSol.path[c])
                    {
                        if (!tabu.IsTabu(a, c) && !tabu.IsTabu(b, d))
                        {
                            distAC = Point.Distance(instance.Coord[a], instance.Coord[c], instance.EdgeType);
                            distBD = Point.Distance(instance.Coord[b], instance.Coord[d], instance.EdgeType);

                            distTotABCD = Point.Distance(instance.Coord[a], instance.Coord[b], instance.EdgeType) +
                                Point.Distance(instance.Coord[c], instance.Coord[d], instance.EdgeType);

                            if ((distAC + distBD) - distTotABCD < bestGain && (distAC + distBD) != distTotABCD)
                            {
                                nextBestMove = a + ";" + b + ";" + c + ";" + d;
                                bestGain = (distAC + distBD) - distTotABCD;
                            }

                            if ((distAC + distBD) - distTotABCD > worstGain && (distAC + distBD) != distTotABCD)
                            {
                                nextWorstMove = a + ";" + b + ";" + c + ";" + d;
                                worstGain = (distAC + distBD) - distTotABCD;
                            }
                        }
                    }
                }

                string[] currentElements = nextBestMove.Split(';');
                a = int.Parse(currentElements[0]);
                b = int.Parse(currentElements[1]);
                c = int.Parse(currentElements[2]);
                d = int.Parse(currentElements[3]);

                Utility.SwapRoute(c, b, currentSol);

                currentSol.path[a] = c;
                currentSol.path[b] = d;

                currentSol.cost += bestGain;

                if (incumbentPath.cost > currentSol.cost)
                {
                    incumbentPath = (PathGenetic)currentSol.Clone();
                }

                if (bestGain < 0)
                    typeSol = 0;
                else
                {
                    tabu.AddTabu(a, b, c, d);
                    typeSol = 1;
                }

                Utility.PrintHeuristicSolution(instance, process, currentSol, incumbentPath.cost, typeSol);
                bestGain = double.MaxValue;
                worstGain = double.MinValue;

            } while (clock.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);
        }

        static void VNS(Instance instance, PathStandard currentSol, Random rnd)
        {
            int a, b, c, d, e, f;

            a = rnd.Next(currentSol.path.Length);
            b = currentSol.path[a];

            do
            {
                c = rnd.Next(currentSol.path.Length);
                d = currentSol.path[c];
            } while ((a == c && b == d) || a == d || b == c);

            do
            {
                e = rnd.Next(currentSol.path.Length);
                f = currentSol.path[e];
            } while ((e == a && f == b) || e == b || f == a || (e == c && f == d) || e == d || f == c);

            List<int> order = new List<int>();

            for (int i = 0, index = 0; i < currentSol.path.Length && order.Count != 4; i++, index = currentSol.path[index])
            {
                if (a == index)
                {
                    order.Add(a);
                    order.Add(b);
                    i++;
                    index = currentSol.path[index];
                }
                else if (c == index)
                {
                    order.Add(c);
                    order.Add(d);
                    i++;
                    index = currentSol.path[index];
                }
                else if (e == index)
                {
                    order.Add(e);
                    order.Add(f);
                    i++;
                    index = currentSol.path[index];
                }
            }

            if (order[0] != a && order[2] != a)
            {
                order.Add(a);
                order.Add(b);
            }
            else if (order[0] != c && order[2] != c)
            {
                order.Add(c);
                order.Add(d);
            }
            else
            {
                order.Add(e);
                order.Add(f);
            }

            Utility.SwapRoute(order[2], order[1], currentSol);

            currentSol.path[order[0]] = order[2];

            Utility.SwapRoute(order[4], order[3], currentSol);

            currentSol.path[order[1]] = order[4];

            currentSol.path[order[3]] = order[5];

            currentSol.cost += Point.Distance(instance.Coord[order[0]], instance.Coord[order[2]], instance.EdgeType) +
                Point.Distance(instance.Coord[order[1]], instance.Coord[order[4]], instance.EdgeType) +
                Point.Distance(instance.Coord[order[3]], instance.Coord[order[5]], instance.EdgeType) -
                Point.Distance(instance.Coord[a], instance.Coord[b], instance.EdgeType) -
                Point.Distance(instance.Coord[c], instance.Coord[d], instance.EdgeType) -
                Point.Distance(instance.Coord[e], instance.Coord[f], instance.EdgeType);

        }

        static void HardFixing(Cplex cplex, Instance instance, Process process, Random rnd, Stopwatch clock)
        {
            StreamWriter file;

            //Vector used to encode the path that rappresent the best integer solution note
            double[] currentIncumbentSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];
            
            //Cost of the best integer solution note
            double currentIncumbentCost = Double.MaxValue;

            List<int>[] listArray = Utility.BuildSLComplete(instance);

            //Serve per differenziarsi rispetto alla lazy "normale" in cui stampo ogni soluzione intera(anche che non è un subtour)
            bool BlockPrint = false;

            const int VALUECONSITENOTIMPROV = 3;

            //Defined the max number of consecutive run of cplex whithout finds a improvement of the incumbent
            int consecutiveiterationNotImprov = VALUECONSITENOTIMPROV;

            //Defined the percentage of edge in the current solution that fixed 
            double percentageFixing = 0.8;

            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Create the model
            INumVar[] x = Utility.BuildModel(cplex, instance, -1);

            //Create a heuristic solution
            PathStandard heuristicSol = Utility.NearestNeightbor(instance, rnd, listArray);
            
            //Apply 2-opt algorithm in order to improve the costo o the heiristicSol
            TwoOpt(instance, heuristicSol);

            //The heuristic solution is the Incumbent, translate the encode in the format used by Cplex

            for (int i = 0; i < instance.NNodes; i++)
            {
                int position = Utility.xPos(i, heuristicSol.path[i], instance.NNodes);
                
                //Set to one only the edge that belong to heuristic solution
                currentIncumbentSol[position] = 1;
            }

            currentIncumbentCost = heuristicSol.cost;

            //Installation of the Lazy Constraint CallBack

            TSPLazyConsCallback tspLazy = new TSPLazyConsCallback(cplex, x, instance, process, BlockPrint);
            cplex.Use(tspLazy);
         
            //TSPIncumbent tspInc = new TSPIncumbent();
            //cplex.Use(tspInc);
           
            ////Installation of Informative CallBack
            //cplex.Use(new TSPInformativeCallback(tspInc));
           
            //Provide to Cplex a warm start
             cplex.AddMIPStart(x, currentIncumbentSol, "HeuristicPath");

            //Set the number of thread equal to the number of logical core present in the processor
            cplex.SetParam(Cplex.Param.Threads, 1);

            //cplex.SetParam(Cplex.IntParam.ParallelMode, -1);

            do
            {
                //Modify the Model according to the current Incumbent solution
                Utility.ModifyModel(instance, x, rnd, percentageFixing, currentIncumbentSol);

               // cplex.SetParam(Cplex.DoubleParam.ObjLLim, currentIncumbentCost - 1);
                cplex.SetParam(Cplex.DoubleParam.ObjULim, currentIncumbentCost - 1);

                double solPassata = currentIncumbentCost - 1;

                //Solve the model
                cplex.Solve();

                double tmp = cplex.GetObjValue(Cplex.IncumbentId);

                if (currentIncumbentCost > cplex.GetObjValue(Cplex.IncumbentId))
                {
                    file = new StreamWriter(instance.InputFile + ".dat", false);

                    currentIncumbentCost = cplex.GetObjValue(Cplex.IncumbentId);
                    currentIncumbentSol = cplex.GetValues(x,Cplex.IncumbentId);

                    //Print solution               
                    for (int i = 0; i < instance.NNodes; i++)
                    {
                        for (int j = i + 1; j < instance.NNodes; j++)
                        {
                            int position = Utility.xPos(i, j, instance.NNodes);

                            if (currentIncumbentSol[position] >= 0.5)
                            {
                                file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                                file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");
                            }
                        }
                    }

                    file.Close();

                    Utility.PrintGNUPlot(process, instance.InputFile, 1, currentIncumbentCost, -1);

                    //Restorarion the variable consecutiveIterationNotImprov to the value VALUECONSITENOTIMPROV
                    consecutiveiterationNotImprov = VALUECONSITENOTIMPROV;
                }
                else
                {
                    //If don't have improvement decrease variable consecutiveIterationNotImprov
                    consecutiveiterationNotImprov--;
       
                    //cplex.AddMIPStart(x, currentIncumbentSol, "HeuristicPath");
                }
                                   
                if (consecutiveiterationNotImprov == 0)
                {
                    if (percentageFixing > 0.2)
                    {
                        percentageFixing -= 0.1;
                        consecutiveiterationNotImprov = VALUECONSITENOTIMPROV;
                    }
                    else
                        consecutiveiterationNotImprov = VALUECONSITENOTIMPROV;
                }

            } while (clock.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

            instance.XBest = currentIncumbentCost;
            instance.BestSol = currentIncumbentSol;

            file = new StreamWriter(instance.InputFile + ".dat", false);
            cplex.Output().WriteLine();


            //Print the solution

            for (int i = 0; i < instance.NNodes; i++)
            {
                for (int j = i + 1; j < instance.NNodes; j++)
                {
                    int position = Utility.xPos(i, j, instance.NNodes);

                    if (instance.BestSol[position] >= 0.5)
                    {
                        file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                        file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");
                    }
                }
            }

            file.Close();

            if (Program.VERBOSE >= -1)
                Utility.PrintGNUPlot(process, instance.InputFile, 1, instance.XBest, -1);

            //Empty line
            cplex.Output().WriteLine();

            cplex.Output().WriteLine("xOPT = " + instance.XBest + "\n");

            if (Program.VERBOSE >= -1)
                cplex.ExportModel(instance.InputFile + ".lp");
        }

        static void LocalBranching(Cplex cplex, Instance instance, Process process, Random rnd, Stopwatch clock)
        {
            int[] possibleRange = { 3, 5, 7, 10 };// Fisso poco così inizialmente: al max r = 10
            int currentRange = 0;
            bool BlockPrint = false;
            IRange cut;
            double[] incumbentSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];
            double incumbentCost = double.MaxValue;
            List<int>[] listArray = Utility.BuildSLComplete(instance);

            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];
            INumVar[] x = Utility.BuildModel(cplex, instance, -1);//Costruisco il modello la prima volta

            PathStandard solHeuristic = Utility.NearestNeightbor(instance, rnd, listArray);
            //incumbentSol = ConvertIntArrayToDoubleArray(solHeuristic.path);
            for (int i = 0; i < instance.NNodes; i++)
            {
                int position = Utility.xPos(i, solHeuristic.path[i], instance.NNodes);
                incumbentSol[position] = 1;//Metto ad 1 solo i lati che appartengono al percorso random generato
            }

            TwoOpt(instance, solHeuristic);
            ILinearNumExpr expr = cplex.LinearNumExpr();

            for (int i = 0; i < instance.NNodes; i++)
                expr.AddTerm(x[Utility.xPos(i, solHeuristic.path[i], instance.NNodes)], 1);

            cut = cplex.Ge(expr, instance.NNodes - possibleRange[currentRange], "Local brnching constraint");
            cplex.AddCut(cut);

           //cplex.SetParam(Cplex.Param.Preprocessing.Presolve, false);

            cplex.SetParam(Cplex.Param.MIP.Strategy.Search, Cplex.MIPSearch.Traditional);

            //cplex.SetParam(Cplex.Param.Threads, cplex.GetNumCores());

            cplex.Use(new TSPLazyConsCallback(cplex, x, instance, process, BlockPrint));

            do
            {
                cplex.AddMIPStart(x, incumbentSol);
                cplex.Solve();

                if (incumbentCost > cplex.ObjValue)
                {
                    incumbentCost = cplex.ObjValue;
                    incumbentSol = cplex.GetValues(x);

                    StreamWriter file = new StreamWriter(instance.InputFile + ".dat", false);

                    for (int i = 0; i < instance.NNodes; i++)
                    {
                        for (int j = i + 1; j < instance.NNodes; j++)
                        {
                            int position = Utility.xPos(i, j, instance.NNodes);

                            if (incumbentSol[position] >= 0.5)
                            {
                                file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                                file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");
                            }
                        }
                    }

                    Utility.PrintGNUPlot(process, instance.InputFile, 1, incumbentCost, -1);
                    file.Close();

                    //Eliminate all cuts
                    cplex.ClearCuts();

                    for (int i = 0; i < instance.NNodes; i++)
                    {
                        if (incumbentSol[i] == 1)
                            expr.AddTerm(x[i], 1);
                    }

                    currentRange = 0;

                    cut = cplex.Ge(expr, instance.NNodes - possibleRange[currentRange], "Local branching constraint");//Aggiungo il vincolo nuovo
                    cplex.AddCut(cut);
                }
                else
                {
                    if (possibleRange[currentRange] != 10)
                    {
                        currentRange++;
                        cplex.ClearCuts();
                        cut = cplex.Ge(expr, instance.NNodes - possibleRange[currentRange], "Local branching constraint");//Aggiungo il vincolo nuovo
                        cplex.AddCut(cut);
                    }
                    else
                    {
                        break;
                    }
                }

            } while (clock.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

            instance.BestSol = incumbentSol;
            instance.BestLb = incumbentCost;
        }

        static void Polishing(Cplex cplex, Instance instance, Process process, Random rnd, int sizePopulation, Stopwatch clock)
        {

            PathGenetic incumbentSol = new PathGenetic();
            PathGenetic currentBestPath = null;

            List<PathGenetic> OriginallyPopulated = new List<PathGenetic>();
            List<PathGenetic> ChildPoulation = new List<PathGenetic>();

            List<int>[] listArray = Utility.BuildSLComplete(instance);

            INumVar[] x = Utility.BuildModel(cplex, instance, -1);

            //cplex.SetParam(Cplex.Param.Preprocessing.Presolve, false);
            cplex.SetParam(Cplex.DoubleParam.EpGap, 0.5);
            cplex.SetParam(Cplex.Param.Threads, cplex.GetNumCores());
            cplex.Use(new TSPLazyConsCallback(cplex, x, instance, process, false));//Installo la lazy 

            for (int i = 0; i < sizePopulation; i++)
            {
                OriginallyPopulated.Add(Utility.NearestNeightborGenetic(instance, rnd, false, listArray));
                //OriginallyPopulated[i].path = Utility.InterfaceForTwoOpt(OriginallyPopulated[i].path);

                //TwoOpt(instance, OriginallyPopulated[i]);

                //OriginallyPopulated[i].path = Utility.Reverse(OriginallyPopulated[i].path);
            }

            do
            {
                for (int i = 0; i < sizePopulation; i++)
                {
                    if ((i != 0) && (i % 2 != 0))
                        ChildPoulation.Add(Utility.GenerateChildRins(cplex, instance, process, x, OriginallyPopulated[i], OriginallyPopulated[i - 1]));
                }

                OriginallyPopulated = Utility.NextPopulation(instance, sizePopulation, OriginallyPopulated, ChildPoulation);
                currentBestPath = Utility.BestSolution(OriginallyPopulated, incumbentSol);

                if (currentBestPath.cost < incumbentSol.cost)
                {
                    incumbentSol = (PathGenetic)currentBestPath.Clone();
                    Utility.PrintGeneticSolution(instance, process, incumbentSol);
                }

                ChildPoulation.RemoveRange(0, ChildPoulation.Count);

            } while (clock.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

            Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);
        }

    }
}