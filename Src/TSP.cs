using ILOG.Concert;
using ILOG.CPLEX;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace TSPCsharp
{
    class TSP
    {
        //--------------------------------------------------------COMMON Objects--------------------------------------------------------
        //Process used to start GNUPlot
        static Process process;
        //StreamWriter used by GNUPlot to print the solution
        static StreamWriter file;
        //Instance of the solution
        static Instance instance;
        //Stopwatch
        static Stopwatch cl;
        //Cplex object
        static Cplex cplex;
        //INumVar is a special interface used to stare any kinf of variable compatible with cplex
        //Building the model, z is necessary to access the variabiles via their stored names
        static INumVar[] z;

        //------------------------------------------------------Loop COMMON OBjects-----------------------------------------------------
        //listArray contains the nearest availeble edges for each edge 
        static List<int>[] listArray;
        //compConn stores the related component of each node
        static int[] compConn;
        //Buffers used, ccExpr buffers the cuts for the loop method, buffeCoeffCC buffers the cuts known term
        static List<ILinearNumExpr> ccExpr;
        static List<int> bufferCoeffCC;
        //If 0 the solution used heuristics methods, otherwhise it is 1, this parameter is used to set print GNUPlot graphs in different colors
        static int typeSol = 1;

        static Random rnd;

        static Tabu tabu;

        private class PathStandard : ICloneable
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


            public PathStandard()//Cammino di default
            {
                path = null;
                cost = -1;
            }

            public PathStandard(int[] path)
            {
                this.path = path;
                cost = CalculateCost(path);
            }

            internal double CalculateCost(int[] path)//-------------------------------tipo di costo
            {
                int costPath = 0;
                for (int i = 0; i < instance.NNodes - 1; i++)
                    costPath += (int)Point.Distance(instance.Coord[path[i]], instance.Coord[path[i + 1]], "EUC_2D");
                costPath += (int)Point.Distance(instance.Coord[path[0]], instance.Coord[path[instance.NNodes - 1]], "EUC_2D");
                return costPath;
            }
        }
        private class PathGenetic : PathStandard
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
            public PathGenetic(int[] path)
            {
                this.path = path;
                this.cost = CalculateCost(path);
                CalculateFitness();
                NRoulette = -1;
            }

            public PathGenetic()
            {
                path = null;
                cost = -1;
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
                fitness = 1 / cost;//Maggiore è il cost minore deve essere la fitness          
            }
        }


        //Define the Lazy Callback
        public class TSPLazyConsCallback : Cplex.LazyConstraintCallback
        {
            private bool BlockPrint;

            public TSPLazyConsCallback(bool BlockPrint)
            {
                this.BlockPrint = BlockPrint;
            }

            public override void Main()
            {

                //Init buffers, due to multithreading, using global buffers is incorrect
                List<ILinearNumExpr> ccExprLC = new List<ILinearNumExpr>();
                List<int> bufferCoeffCCLC = new List<int>(); ;

                int[] compConnLC = new int[instance.NNodes];

                InitCC(compConnLC);

                //To call GetValues for each value in z is a lot more expensive for unknown reasons
                double[] actualZ = GetValues(z);

                //Node's is that generated the callback, used to create an unique nome for the GNUPlot files
                string nodeId = GetNodeId().ToString();

                StreamWriter fileLC;

                if (Program.VERBOSE >= -1)
                {
                    //Init the StreamWriter for the current solution
                    fileLC = new StreamWriter(instance.InputFile + "_" + nodeId + ".dat", false);
                }

                for (int i = 0; i < instance.NNodes; i++)
                {
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {
                        //Retriving the correct index position for the current link inside z
                        int position = zPos(i, j, instance.NNodes);

                        //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                        if (actualZ[position] >= 0.5)
                        {
                            //Updating the model with the current subtours elimination
                            BuildCC(i, j, ccExprLC, bufferCoeffCCLC, compConnLC);

                            if (BlockPrint)
                            {
                                fileLC.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                                fileLC.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");
                            }
                        }
                    }
                }

                if (Program.VERBOSE >= -1)
                {
                    //GNUPlot input file needs to be closed
                    fileLC.Close();
                }

                //Accessing GNUPlot to read the file
                if (BlockPrint)
                    PrintGNUPlot(instance.InputFile + "_" + nodeId, typeSol);

                //cuts will stores the user's cut
                IRange[] cuts = new IRange[ccExprLC.Count];

                if (cuts.Length > 1)
                {
                    for (int i = 0; i < cuts.Length; i++)
                    {
                        cuts[i] = cplex.Le(ccExprLC[i], bufferCoeffCCLC[i] - 1);
                        Add(cuts[i], 1);
                    }
                }
            }

            //Same as
            internal void BuildCC(int i, int j, List<ILinearNumExpr> ccExprLC, List<int> bufferCoeffCCLC, int[] compConnLC)
            {
                if (compConnLC[i] != compConnLC[j])//Same related component, the latter is not closed yet
                {
                    for (int k = 0; k < compConnLC.Length; k++)// k>i poichè i > i
                    {
                        if ((k != j) && (compConnLC[k] == compConnLC[j]))
                        {
                            //Same as Kruskal
                            compConnLC[k] = compConnLC[i];
                        }
                    }

                    //Finally also the vallue relative to the Point i are updated
                    compConnLC[j] = compConnLC[i];
                }
                else//Here the current releted component is complete and the relative subtout elimination constraint can be added to the model
                {
                    ILinearNumExpr expr = cplex.LinearNumExpr();

                    int cnt = 0;

                    for (int h = 0; h < compConnLC.Length; h++)
                    {
                        if (compConnLC[h] == compConnLC[i])
                        {
                            for (int k = h + 1; k < compConnLC.Length; k++)
                            {
                                if (compConnLC[k] == compConnLC[i])
                                {
                                    expr.AddTerm(z[zPos(h, k, compConnLC.Length)], 1);
                                }
                            }

                            cnt++;
                        }
                    }

                    ccExprLC.Add(expr);
                    bufferCoeffCCLC.Add(cnt);
                }

            }
        }


        [DllImport("ConcordeDLL.dll")]
        //public static extern void Concorde(char[] fileName, int timeLimit);
        public static extern int Concorde(StringBuilder fileName, int timeLimit);


        //Support class for BuildSL()
        public class itemList
        {
            public itemList(double d, int i)
            {
                dist = d;
                index = i;
            }

            public double dist { get; set; }
            public int index { get; set; }
        }


        //"Main" method
        static public bool TSPOpt(Instance inst, Stopwatch clock)
        {
            //cl is the global variable used TSP.cs
            cl = clock;

            //Cplex is the official class offered by IBM inside the API to use cplex
            //algorithms with C#
            cplex = new Cplex();

            instance = inst;

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
                                    MipTimelimit(clock);
                                    //Calling the proper resolution method
                                    Loop(-1, -1);
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
                                    MipTimelimit(clock);
                                    //Calling the proper resolution method
                                    Loop(percentage, -1);
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
                                    MipTimelimit(clock);
                                    //Calling the proper resolution method
                                    Loop(-1, numb);
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
                                    MipTimelimit(clock);
                                    //Calling the proper resolution method
                                    Loop(percentage, numb);
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
                                    MipTimelimit(clock);
                                    //Calling the proper resolution method
                                    CallBackMethod();
                                    break;
                                }
                            case "2":
                                {
                                    //Restarting the clock
                                    clock.Restart();
                                    inst.BestLb = Concorde(new StringBuilder(inst.InputFile), (int)inst.TimeLimit);
                                    process = InitProcess();
                                    PrintGNUPlot(instance.InputFile, typeSol);
                                    Console.WriteLine("Best solution: " + inst.BestLb);
                                    break;
                                }
                            default:
                                throw new System.Exception("Bad argument");
                        }
                        break;
                    }
                case "3":
                    {

                        rnd = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);

                        Console.Write("\nInsert 1 to use multi start 2OPT, 2 to use multi start 3OPT, 3 to use Tabu-Search, 4 to use VNS or  5 to use Genetic algorithm: ");
                        switch (Console.ReadLine())
                        {
                            case "1":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    Heuristic("Multi Start 2OPT");
                                    break;
                                }
                            case "2":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    Heuristic("Multi Start 3OPT");
                                    break;
                                }
                            case "3":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    Heuristic("Tabu-Search");
                                    break;
                                }
                            case "4":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    Heuristic("VNS");
                                    break;
                                }
                            case "5":
                                {
                                    Console.Write("\nWrite the size of the population : ");
                                    //storing the percentage selected
                                    instance.SizePopulation = Convert.ToInt32(Console.ReadLine());
                                    clock.Start();
                                    //Calling the proper resolution method
                                    Heuristic("GeneticAlgorithm");
                                    break;
                                }
                            default:
                                throw new System.Exception("Bad argument");
                        }
                        break;
                    }

                case "4":
                    {
                        rnd = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);

                        Console.Write("\nInsert 1 to use HardFixing, 2 to use local branch, 3 Polishing: ");
                        switch (Console.ReadLine())
                        {
                            case "1":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    HardFixing();
                                    break;
                                }
                            case "2":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    LocalBranching();
                                    
                                    break;
                                }
                            case "3":
                                {
                                    //Clock restart
                                    clock.Start();
                                    //Calling the proper resolution method
                                    
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
        static void ModifyModel(int percentageFixing, double[] values, List<int[]> fixedEdges)
        {
            for (int i = 0; i < z.Length; i++)//Inutile la prima volta
            {
                z[i].LB = 0;
                z[i].UB = 1;
            }

            for (int i = 0; i < z.Length; i++)
            {
                if ((values[i] == 1))
                {
                    if (RandomSelect(percentageFixing) == 1)
                    {
                        z[i].LB = 1;
                        fixedEdges.Add((zPosInv(i, instance.NNodes)));
                    }
                }
            }
        }

        static int RandomSelect(int percentageFixing)
        {
            if (rnd.Next(1, 10) < percentageFixing)
                return 1;
            else
                return 0;
        }

        static void LocalBranching()
        {
            int[] possibleRange = { 3, 5, 10, 20 };// Fisso poco così inizialmente: al max r = 10
            int currentRange = 0;
            bool BlockPrint = false;
            IRange cut ;
            double[] incumbentSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];
            double incumbentCost = -1;

            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];
            z = BuildModel(-1);//Costruisco il modello la prima volta

            PathStandard heuristicSol = NearestNeightbor();
            ILinearNumExpr expr = cplex.LinearNumExpr();

            for (int i = 0; i < instance.NNodes; i++)           
                expr.AddTerm(z[zPos(i, heuristicSol.path[i], instance.NNodes)], 1);

            cut = cplex.Ge(expr, instance.NNodes - possibleRange[currentRange], "Local brnching constraint");
            cplex.AddCut(cut);
            process = InitProcess();
   
            cplex.SetParam(Cplex.Param.Threads, cplex.GetNumCores());
            cplex.Use(new TSPLazyConsCallback(BlockPrint));

            do
            {
                cplex.Solve();

                if ((incumbentCost > cplex.ObjValue) || (incumbentCost == -1))
                {
                    incumbentCost = cplex.ObjValue;
                    incumbentSol = cplex.GetValues(z);

                    file = new StreamWriter(instance.InputFile + ".dat", false);

                    for (int i = 0; i < instance.NNodes; i++)
                    {
                        for (int j = i + 1; j < instance.NNodes; j++)
                        {
                            int position = zPos(i, j, instance.NNodes);

                            if (incumbentSol[position] >= 0.5)
                            {
                                file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                                file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");
                            }
                        }
                    }

                    PrintGNUPlot(instance.InputFile, typeSol);
                    file.Close();

                    cplex.ClearCuts();//Elimino tutti i tagli

                    for (int i = 0; i < instance.NNodes; i++)
                    {
                        if (incumbentSol[i] == 1)
                            expr.AddTerm(z[i], 1);
                    }

                    cut = cplex.Ge(expr, instance.NNodes - possibleRange[currentRange], "Local brnching constraint");//Aggiungo il vincolo nuovo
                    cplex.AddCut(cut);
                }
                else
                {
                    if (possibleRange[currentRange] != 20)
                        currentRange++;
                    else
                    {
                        //esci
                    }
                }

            } while (cl.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

            instance.BestSol = incumbentSol;
            instance.BestLb = incumbentCost;
        }

        static void HardFixing()
        {
            double[] incumbentSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];
            double incumbentCost = -1;

            List<int[]> fixedVariables = new List<int[]>();

            bool BlockPrint = false;//Serve per differenziarsi rispetto alla lazy "normale" in cui stampo ogni soluzione intera(anche che non è un subtour)
            int numIterazioni = 10;
            int percentageFixing = 8;

            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            z = BuildModel(-1);

            PathStandard heuristicSol = NearestNeightbor();

            for (int i = 0; i < instance.NNodes; i++)
            {
                int position = zPos(i, heuristicSol.path[i], instance.NNodes);
                incumbentSol[position] = 1;//Metto ad 1 solo i lati che appartengono al percorso random generato
            }
            process = InitProcess();
            //cplex.SetParam(Cplex.Param.MIP.Strategy.Search, Cplex.MIPSearch.Auto);

            cplex.SetParam(Cplex.Param.Threads, cplex.GetNumCores());
            cplex.Use(new TSPLazyConsCallback(BlockPrint));//Installo la lazy   

            do
            {
                ModifyModel(percentageFixing, incumbentSol, fixedVariables);
                if ((fixedVariables.Count != instance.NNodes - 1) && (fixedVariables.Count != instance.NNodes))
                    PreProcessing(fixedVariables);

                cplex.AddMIPStart(z, incumbentSol);          
                cplex.Solve();

                if ((incumbentCost > cplex.ObjValue) || (incumbentCost == -1))
                {
                    incumbentCost = cplex.ObjValue;
                     incumbentSol = cplex.GetValues(z);

                    file = new StreamWriter(instance.InputFile + ".dat", false);

                    for (int i = 0; i < instance.NNodes; i++)
                    {
                        for (int j = i + 1; j < instance.NNodes; j++)
                        {
                            int position = zPos(i, j, instance.NNodes);

                            if (incumbentSol[position] >= 0.5)
                            {
                                file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                                file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");
                            }
                        }
                    }

                    PrintGNUPlot(instance.InputFile, typeSol);
                    file.Close();
                }

                cplex.DeleteMIPStarts(0);
                fixedVariables.RemoveRange(0, fixedVariables.Count);
                numIterazioni--;

                if (numIterazioni == 0)
                {
                    if (percentageFixing > 2)
                    {
                        percentageFixing--;
                        numIterazioni = 10;
                    }
                    else
                        numIterazioni = 10;
                }

            } while (cl.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

            instance.ZBest = incumbentCost;
            instance.BestSol = incumbentSol;

            file = new StreamWriter(instance.InputFile + ".dat", false);
            cplex.Output().WriteLine();

            for (int i = 0; i < instance.NNodes; i++)
            {
                for (int j = i + 1; j < instance.NNodes; j++)
                {
                    int position = zPos(i, j, instance.NNodes);

                    // instance.BestSol[position] = cplex.GetValue(z[position]);

                    if (instance.BestSol[position] >= 0.5)
                    {
                        file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                        file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");
                    }
                }
            }

            file.Close();

            if (Program.VERBOSE >= -1)
                PrintGNUPlot(instance.InputFile, typeSol);

            cplex.Output().WriteLine();
            cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

            if (Program.VERBOSE >= -1)
                cplex.ExportModel(instance.InputFile + ".lp");
        }

        //Handle the resolution method without callbacks
        static void Loop(double perc, int numb)
        {
            //epGap is false when EpGap parameter is at default
            bool epGap = false;

            //allEdges is true when all possible links have their upper bound to 1
            bool allEdges = true;

            //Setting EpGap if a valid percentage is specified
            if (perc >=0 && perc <= 1)
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
            z = BuildModel(numb);

            //Allocating the correct space to store the optimal solution
            //Only links from node i to i with i < i are considered
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Init buffers
            compConn = new int[instance.NNodes];
            ccExpr = new List<ILinearNumExpr>();
            bufferCoeffCC = new List<int>();

            //Initialization of the process that handles GNUPlot
            process = InitProcess();


            do
            {
                //When only one related component is found and ehuristics methods are active they are disabled
                if (ccExpr.Count == 1)
                {
                    epGap = false;

                    allEdges = true;

                    cplex.SetParam(Cplex.DoubleParam.EpGap, 1e-06);

                    ResetVariables(z);

                    typeSol = 1;
                }

                //Cplex solves the current model
                cplex.Solve();

                //Initializing the arrays used to eliminate the subtour
                InitCC(compConn);

                ccExpr = new List<ILinearNumExpr>();
                bufferCoeffCC = new List<int>();

                //Init the StreamWriter for the current solution
                file = new StreamWriter(instance.InputFile + ".dat", false);

                //Storing the optimal value of the objective function
                instance.ZBest = cplex.ObjValue;

                //Blank line
                cplex.Output().WriteLine();

                //Printing the optimal solution and the GNUPlot input file
                for (int i = 0; i < instance.NNodes; i++)
                {
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {

                        //Retriving the correct index position for the current link inside z
                        int position = zPos(i, j, instance.NNodes);

                        //Reading the optimal solution for the actual link (i,i)
                        instance.BestSol[position] = cplex.GetValue(z[position]);

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

                            //Updating the model with the current subtours elimination
                            UpdateCC(i, j);
                        }
                    }
                }

                //Only when more than one related components are found they are added to the model
                if (ccExpr.Count > 1)
                {
                    for (int i = 0; i < ccExpr.Count; i++)
                        cplex.AddLe(ccExpr[i], bufferCoeffCC[i] - 1);
                }

                //GNUPlot input file needs to be closed
                file.Close();

                //Accessing GNUPlot to read the file
                if (Program.VERBOSE >= -1)
                    PrintGNUPlot(instance.InputFile, typeSol);

                //Blank line
                cplex.Output().WriteLine();

                //Writing the value
                cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

                //Exporting the updated model
                if (Program.VERBOSE >= -1)
                    cplex.ExportModel(instance.InputFile + ".lp");

            } while (ccExpr.Count > 1 || epGap || !allEdges); //if there is more then one related components the solution is not optimal 


            //Closing Cplex link
            cplex.End();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -1)
                PrintGNUPlot(instance.InputFile, typeSol);
        }


        //Handle the callback resolution method
        static void CallBackMethod()
        {
            //-1 means that all links are enabled
            z = BuildModel(-1);

            //Initializing the vector
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Creation and initialization of the process that will handle GNUPlot
            process = InitProcess();

            // Turn on traditional search for use with control callbacks
            cplex.SetParam(Cplex.Param.MIP.Strategy.Search, Cplex.MIPSearch.Auto);

            //Setting cplex # of threads
            cplex.SetParam(Cplex.Param.Threads, cplex.GetNumCores());

            //Adding lazycallback
            cplex.Use(new TSPLazyConsCallback(true));

            //Solving
            cplex.Solve();

            //Init the StreamWriter for the current solution
            file = new StreamWriter(instance.InputFile + ".dat", false);

            //Storing the optimal value of the objective function
            instance.ZBest = cplex.ObjValue;

            //Blank line
            cplex.Output().WriteLine();

            //Printing the optimal solution and the GNUPlot input file
            for (int i = 0; i < instance.NNodes; i++)
            {
                for (int j = i + 1; j < instance.NNodes; j++)
                {

                    //Retriving the correct index position for the current link inside z
                    int position = zPos(i, j, instance.NNodes);

                    //Reading the optimal solution for the actual link (i,i)
                    instance.BestSol[position] = cplex.GetValue(z[position]);

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
                PrintGNUPlot(instance.InputFile, typeSol);

            //Blank line
            cplex.Output().WriteLine();

            //Writing the value
            cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

            //Exporting the updated model
            if (Program.VERBOSE >= -1)
                cplex.ExportModel(instance.InputFile + ".lp");
            
        }

        //Handle the heuristic resolution method
        static void Heuristic(string choice)
        {
            //Initialization of the process that handles GNUPlot
            process = InitProcess();

            typeSol = 0;

            switch (choice)
            {
                case "Multi Start 2OPT":
                    {
                        PathStandard incumbentSol = new PathStandard();
                        PathStandard solHeuristic;

                        do
                        {
                            solHeuristic = NearestNeightbor();

                           if( (incumbentSol.cost > solHeuristic.cost) || (incumbentSol.cost == -1))
                            {
                                incumbentSol = (PathStandard)solHeuristic.Clone();

                                PrintHeuristicSolution((PathGenetic)solHeuristic, incumbentSol.cost);

                                Console.WriteLine("Incumbed changed");
                            }

                            TwoOpt((PathGenetic)solHeuristic);

                            if (incumbentSol.cost > solHeuristic.cost)
                            {
                                incumbentSol = solHeuristic;
                                PrintHeuristicSolution((PathGenetic)solHeuristic, incumbentSol.cost);

                                Console.WriteLine("Incumbed changed");
                            }
                            else
                                Console.WriteLine("Incumbed not changed");                            

                        } while (cl.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

                        Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);

                        break;
                    }
                case "Multi Start 3OPT":
                    {/*
                        do
                        {
                            NearestNeightbor();

                            if (incumbentDist > distHeuristic)
                            {
                                incumbentDist = distHeuristic;
                                incumbentZHeuristic = zHeuristic;

                                PrintHeuristicSolution();

                                Console.WriteLine("Incumbed changed");
                            }

                            ThreeOpt();

                            if (incumbentDist > distHeuristic)
                            {
                                incumbentDist = distHeuristic;
                                incumbentZHeuristic = zHeuristic;

                                PrintHeuristicSolution();

                                Console.WriteLine("Incumbed changed");
                            }
                            else
                                Console.WriteLine("Incumbed not changed");

                            zHeuristic = new int[instance.NNodes];

                        } while (cl.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);
                        Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);
                        */
                        break;
                    }
                case "Tabu-Search":
                    {
                        PathStandard incumbentSol;
                        PathStandard solHeuristic = NearestNeightbor();
                        incumbentSol = (PathStandard)solHeuristic.Clone();
                        PrintHeuristicSolution((PathGenetic)solHeuristic, incumbentSol.cost);
                        tabu = new Tabu("A", instance, 100);
                        TabuSearch(solHeuristic, incumbentSol);
                        solHeuristic = incumbentSol;
                        PrintHeuristicSolution((PathGenetic)solHeuristic, incumbentSol.cost);
                        TwoOpt((PathGenetic)solHeuristic);
                        Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);
                        break;
                    }
                case "VNS":
                    {
                        PathStandard incumbentSol;
                        PathStandard solHeuristic = NearestNeightbor();
                        incumbentSol = (PathStandard)solHeuristic.Clone();
                        PrintHeuristicSolution((PathGenetic)solHeuristic, incumbentSol.cost);

                        do
                        {
                            TwoOpt((PathGenetic)solHeuristic);

                            if (incumbentSol.cost > solHeuristic.cost)
                            {
                                incumbentSol =(PathStandard)solHeuristic.Clone();

                                PrintHeuristicSolution((PathGenetic)solHeuristic, incumbentSol.cost);

                                Console.WriteLine("Incumbed changed");
                            }
                            else
                                Console.WriteLine("Incumbed not changed");

                            PrintHeuristicSolution((PathGenetic)solHeuristic, incumbentSol.cost);
                            VNS(solHeuristic);
                        } while (cl.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);
                        Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);

                        
                        break;
                    }
                case "GeneticAlgorithm":
                    {
                        PathGenetic incumbentSol = new PathGenetic();
                        PathGenetic currentBestPath = null;

                        List<PathGenetic> OriginallyPopulated = new List<PathGenetic>();
                        List<PathGenetic> ChildPoulation = new List<PathGenetic>();

                        for (int i = 0; i < instance.SizePopulation; i++)
                            OriginallyPopulated.Add(NearestNeightborGenetic());//IL NEARESTNABLE MI RITORNA SEMPRE LA STESSA SOLUZIONE
                        do
                        {
                            for (int i = 0; i < instance.SizePopulation; i++)
                            {
                                if ((i != 0) && (i % 2 != 0))
                                    ChildPoulation.Add(GenerateChild(OriginallyPopulated[i], OriginallyPopulated[i - 1]));
                            }

                            OriginallyPopulated = NextPopulation(OriginallyPopulated, ChildPoulation);
                            currentBestPath = BestSolution(OriginallyPopulated, incumbentSol);

                            if ((currentBestPath.cost < incumbentSol.cost)||(incumbentSol.cost == -1))
                            {
                                incumbentSol = (PathGenetic)currentBestPath.Clone();
                                PrintGeneticSolution(incumbentSol.path);
                            }

                            ChildPoulation.RemoveRange(0, ChildPoulation.Count);

                        } while (cl.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);

                        Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);

                        break;

                    }
            }

            typeSol = 0;
           
            // Console.WriteLine("Best distance found within the timelit is: " + incumbentSol.cost);
        }
        static List<PathGenetic> NextPopulation(List<PathGenetic> FatherGeneration, List<PathGenetic> ChildGeneration)
        {
            List<PathGenetic> nextGeneration = new List<PathGenetic>();
            List<int> roulette = new List<int>();
            Random rouletteValue = new Random();
            List<int> NumbersExtracted = new List<int>();
            bool go = false;
            int numberExtracted;

            for (int i = 0; i < ChildGeneration.Count; i++)
                FatherGeneration.Add(ChildGeneration[i]);

            int upperExtremity = FillRoulette(roulette, FatherGeneration);

            for (int i = 0; i < instance.SizePopulation; i++)
            {
                do
                {
                    go = true;
                    numberExtracted = rouletteValue.Next(0, upperExtremity);
                    int n = roulette[numberExtracted];
                    if (NumbersExtracted.Contains(n) == false)//Un percorso non può essere sorteggiato più di una volta
                    {
                        PathGenetic h = FatherGeneration.Find(x => x.NRoulette == n);
                        go = false;
                        NumbersExtracted.Add(n);
                        nextGeneration.Add(h);
                    }
                } while (go);
            }
            return nextGeneration;
        }

        static void PrintGeneticSolution(int[] Heuristic)
        {
            file = new StreamWriter(instance.InputFile + ".dat", false);

            for (int i = 0; i + 1 < instance.NNodes; i++)
            {
                int vertice1 = Heuristic[i];
                int vertice2 = Heuristic[i + 1];
                file.WriteLine(instance.Coord[vertice1].X + " " + instance.Coord[vertice1].Y + " " + (vertice1 + 1));
                file.WriteLine(instance.Coord[vertice2].X + " " + instance.Coord[vertice2].Y + " " + (vertice2 + 1) + "\n");
            }
            file.WriteLine(instance.Coord[Heuristic[0]].X + " " + instance.Coord[Heuristic[0]].Y + " " + (Heuristic[0] + 1));
            file.WriteLine(instance.Coord[Heuristic[instance.NNodes - 1]].X + " " + instance.Coord[Heuristic[instance.NNodes - 1]].Y + " " + (Heuristic[instance.NNodes - 1] + 1) + "\n");

            //GNUPlot input file needs to be closed
            file.Close();
            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -1)
                PrintGNUPlot(instance.InputFile, typeSol);
        }

        static PathGenetic BestSolution(List<PathGenetic> percorsi, PathGenetic migliorCamminoAssoluto)
        {
            PathGenetic migliorCamminoGenerazione = migliorCamminoAssoluto;

            for (int i = 1; i < percorsi.Count; i++)
            {
                if ((percorsi[i].cost < (migliorCamminoGenerazione.cost)) || (migliorCamminoGenerazione.cost == -1))
                    migliorCamminoGenerazione = percorsi[i];
            }

            return migliorCamminoGenerazione;
        }

        static int FillRoulette(List<int> roulette, List<PathGenetic> CurrentGeneration)
        {
            int sizeRoulette = 0;
            int proportionalityConstant = Estimate(CurrentGeneration[0].Fitness);

            for (int i = 0; i < CurrentGeneration.Count; i++)
            {
                int prob = (int)(CurrentGeneration[i].Fitness * proportionalityConstant);
                CurrentGeneration[i].NRoulette = i;
                sizeRoulette += prob;
                for (int j = 0; j < prob; j++)
                    roulette.Add(i);
            }
            return sizeRoulette;
        }

        static int Estimate(double sample)
        {
            int k = 1;
            while (sample < 100)
            {
                sample = sample * 10;
                k = k * 10;
            }
            return k;
        }

        static void Mutation(int[] pathChild)
        {
            int indiceDaModificare = rnd.Next(0, pathChild.Length);
            int nuovoValore = rnd.Next(0, instance.NNodes);
            pathChild[indiceDaModificare] = nuovoValore;
        }

        static PathGenetic GenerateChild(PathGenetic mother, PathGenetic father)
        {
            PathGenetic child;
            int[] PercorsoFiglio = new int[instance.NNodes];

            int crossover = (rnd.Next(0, instance.NNodes));

            for (int i = 0; i < instance.NNodes; i++)
            {
                if (i > crossover)
                    PercorsoFiglio[i] = mother.path[i];
                else
                    PercorsoFiglio[i] = father.path[i];
            }

            bool variableMutation = true;

            for (int i = 0; i < 3; i++)
            {
                if ((rnd.Next(2, 4) % 2) != 0)
                {
                    variableMutation = false;
                    break;
                }
            }

            if (variableMutation == true)
                Mutation(PercorsoFiglio);

            child = Repair(PercorsoFiglio);

            if (ProbabilityTwoOpt() == 1)
            {
                child.path = InterfaceForTwoOpt(child.path);

                TwoOpt(child);

                child.path = Reverse(child.path);               
            }
            return child;
        }

        static int[] zPosInv(int index, int nNodes)
        {
            //int i = 0;
            //int cnt = 1;
            //int sup = nNodes - cnt;
            //while(index > sup)
            //{
            //    cnt++;
            //    sup += nNodes - cnt; 
            //}

            //i = cnt - 1;

            //int i = index + (i + 1) * (i + 2) / 2 - i * nNodes;

            //return new int[] { i, i };

            for (int i = 0; i < nNodes; i++)
            {
                for (int j = i + 1; j < nNodes; j++)
                    if (zPos(i, j, nNodes) == index)
                        return new int[] { i, j };
            }

            return null;
        }
        static void PreProcessing(List<int[]> fixedVariables)
        {
            int nodeLeft = 0;
            int nodeRight = 0;

            /* for (int i = 0; i < fixedVariables.Count; i++)
             {
                 int[] x = fixedVariables[i];
                 Console.WriteLine(x[0] + "---" + x[1]);
             }
             */
            for (int i = 0; i < fixedVariables.Count; i++)
            {
                int[] currentEdge = fixedVariables[i];

                nodeLeft = currentEdge[0];
                nodeRight = currentEdge[1];
                fixedVariables.Remove(currentEdge);


                for (int k = 0; k < fixedVariables.Count; k++)
                {
                    int[] fixedEdge = fixedVariables[k];
                    if (nodeLeft == fixedEdge[0])
                    {
                        nodeLeft = fixedEdge[1];
                        fixedVariables.Remove(fixedEdge);
                    }
                }
                for (int k = 0; k < fixedVariables.Count; k++)
                {
                    int[] fixedEdge = fixedVariables[k];
                    if (nodeLeft == fixedEdge[1])
                    {
                        nodeLeft = fixedEdge[0];
                        fixedVariables.Remove(fixedEdge);
                    }
                }

                for (int k = 0; k < fixedVariables.Count; k++)
                {
                    int[] fixedEdge = fixedVariables[k];
                    if (nodeRight == fixedEdge[0])
                    {
                        nodeRight = fixedEdge[1];
                        fixedVariables.Remove(fixedEdge);
                    }

                }

                for (int k = 0; k < fixedVariables.Count; k++)
                {
                    int[] fixedEdge = fixedVariables[k];
                    if (nodeRight == fixedEdge[1])
                    {
                        nodeRight = fixedEdge[0];
                        fixedVariables.Remove(fixedEdge);
                    }

                }
                if (nodeLeft != currentEdge[0] || nodeRight != currentEdge[1])
                    z[zPos(nodeLeft, nodeRight, instance.NNodes)].UB = 0;
            }

            /* for (int i = 0; i < z.Length; i++)
              {
                  if (z[i].LB == 1)
                  {
                      Console.WriteLine("Fissate a 1");
                      int[] x = zPosInv(i, instance.NNodes);
                      Console.WriteLine(x[0] +"-"+x[1]);
                  }
                  if (z[i].UB == 0)
                  {
                      Console.WriteLine("Fissate a 0");
                      int[] x = zPosInv(i, instance.NNodes);
                      Console.WriteLine(x[0] + "-" + x[1]);

                  }
              }*/
        }

        static int ProbabilityTwoOpt()
        {
            if (rnd.Next(1, instance.NNodes / 2) == 1)
                return 1;
            else
                return 0;
        }
        static PathGenetic Repair(int[] pathChild)//Mi consente di togliere tutti i vertici duplicati
        {
            List<int> visitedNodes = new List<int>();//Serve per togliere il fatto che in un nodo incidano due vertici
            List<int> isolatedNodes = new List<int>();//Sono i nodi di Child isolati
            List<int> nearlestIsolatedNodes = new List<int>();

            FindIsolatedNodes(pathChild, isolatedNodes, nearlestIsolatedNodes);

            FindNearestNode(isolatedNodes, nearlestIsolatedNodes);

            List<int> pathIncomplete = new List<int>();

            int[] pathComplete = new int[instance.NNodes];

            for (int i = 0; i < instance.NNodes; i++)
            {
                if (visitedNodes.Contains(pathChild[i]) == false)
                {
                    visitedNodes.Add(pathChild[i]);
                    pathIncomplete.Add(pathChild[i]);
                }
            }

            int positionInsertNode = 0;

            for (int i = 0; i < pathIncomplete.Count; i++)
            {
                if (nearlestIsolatedNodes.Contains(pathIncomplete[i]))
                {
                    pathComplete[positionInsertNode] = pathIncomplete[i];
                    pathComplete[positionInsertNode + 1] = isolatedNodes[nearlestIsolatedNodes.IndexOf(pathIncomplete[i])];
                    positionInsertNode++;
                }
                else
                    pathComplete[positionInsertNode] = pathIncomplete[i];

                positionInsertNode++;
            }

            return new PathGenetic(pathComplete);
        }
        static void FindIsolatedNodes(int[] percorsoFiglio, List<int> nodiIsolati, List<int> piuVicininodiIsolati)
        {
            int cntVolte = 0;
            for (int i = 0; i < instance.NNodes; i++)
            {
                for (int y = 0; y < instance.NNodes; y++)
                {
                    if (percorsoFiglio[y] == i)
                        cntVolte++;
                }

                if (cntVolte == 0)
                    nodiIsolati.Add(i);

                cntVolte = 0;
            }
        }
        static void FindNearestNode(List<int> nodiIsolati, List<int> piuVicininodiIsolati)
        {
            int nextNode = 0;
            int nearestNode = 0;
            bool go = true;

            for (int i = 0; i < nodiIsolati.Count; i++)
            {
                go = false;
                nextNode = 0;
                do
                {
                    nearestNode = listArray[nodiIsolati[i]][nextNode];

                    if (((nodiIsolati.Contains(nearestNode)) == false) && (piuVicininodiIsolati.Contains(nearestNode) == false))
                    {
                        piuVicininodiIsolati.Add(nearestNode);
                        go = false;
                    }
                    else
                    {
                        nextNode++;
                        go = true;
                    }
                } while (go);
            }
        }

        static int[] InterfaceForTwoOpt(int[] path)
        {
            int[] inputTwoOpt = new int[path.Length];

            for (int i = 0; i < path.Length; i++)
            {
                for (int j = 0; j < path.Length; j++)
                {
                    if (j == path.Length - 1)
                    {
                        inputTwoOpt[i] = path[0];
                    }
                    else if (path[j] == i)
                    {
                        inputTwoOpt[i] = path[j + 1];
                        break;
                    }
                }
            }
            return inputTwoOpt;
        }

        static int[] Reverse(int[] path)
        {
            int[] returnGenetic = new int[path.Length];

            returnGenetic[0] = path[0];

            for (int i = 1; i < path.Length; i++)
                returnGenetic[i] = path[returnGenetic[i - 1]];

            return returnGenetic;
        }

        static PathGenetic NearestNeightbor()
        {
            int[] zHeuristic = new int[instance.NNodes];
            double distHeuristic = 0;

            int currentIndex = 0;

            int[] availableIndexes = new int[instance.NNodes];

            for (int i = 0; i < availableIndexes.Length; i++)
                availableIndexes[i] = -1;

            availableIndexes[currentIndex] = 1;

            listArray = BuildSLComplete();

            for (int i = 0; i < instance.NNodes - 1; i++)
            {
                bool found = false;

                int plus = RndPlus();

                int nextIndex = listArray[currentIndex][0 + plus];

                do
                {

                    if (availableIndexes[nextIndex] == -1)
                    {
                        zHeuristic[currentIndex] = nextIndex;
                        distHeuristic += Point.Distance(instance.Coord[currentIndex], instance.Coord[nextIndex], instance.EdgeType);
                        availableIndexes[nextIndex] = 1;
                        currentIndex = nextIndex;
                        found = true;
                    } else
                    {
                        plus++;
                        if (plus >= instance.NNodes - 1)
                        {
                            nextIndex = listArray[currentIndex][0];
                            plus = 0;
                        }
                        else
                            nextIndex = listArray[currentIndex][0 + plus];
                    }

                } while (!found);
            }

            distHeuristic += Point.Distance(instance.Coord[currentIndex], instance.Coord[0], instance.EdgeType);
            
           /*if (incumbentZHeuristic == null)
            {
                incumbentZHeuristic = zHeuristic;
                incumbentDist = distHeuristic;
            }*/

            return new PathGenetic(zHeuristic, distHeuristic);
        }

        static PathGenetic NearestNeightborGenetic()
        {
            int[] HeuristicSolution = new int[instance.NNodes];
            List<int> availableNodes = new List<int>(); //Lista contenente tutti i nodi disponibil
            int currenNode = rnd.Next(0, instance.NNodes);
            HeuristicSolution[0] = currenNode;
            availableNodes.Add(currenNode);

            listArray = BuildSLComplete();//listArray[i][0] contiente il nodoo più vicino del vertice i

            for (int i = 1; i < instance.NNodes; i++)
            {
                bool found = false;
                int plus = RndGenetic();
                int nextNode = listArray[currenNode][0 + plus];

                do
                {
                    if (availableNodes.Contains(nextNode) == false)//Se il nodo scelto è disponibile
                    {
                        HeuristicSolution[i] = nextNode;
                        availableNodes.Add(nextNode);//Setto che il nodo nodoCorrente è stato visitato e quindi non è più disponibile
                        currenNode = nextNode;
                        found = true;
                    }
                    else
                    {
                        plus++;
                        if (plus >= instance.NNodes - 1)
                        {
                            nextNode = listArray[currenNode][0];
                            plus = 0;
                        }
                        else
                            nextNode = listArray[currenNode][0 + plus];
                    }

                } while (!found);
            }

            return new PathGenetic(HeuristicSolution);//Il costo del percorso viene calcolato all' interno del costruttore

        }

        static int RndGenetic()
        {
            double tmp = rnd.NextDouble();
            if (tmp < 0.5)
                return 0;
            else if (tmp < 0.60)
                return 1;
            else if (tmp < 0.80)
                return 2;
            else
                return 3;
        }

        static int RndPlus()
        {
            double tmp = rnd.NextDouble();

            if (tmp < 0.9)
                return 0;
            else if (tmp < 0.99)
                return 1;
            else
                return 2;
        }

        static void PrintHeuristicSolution(PathGenetic pathG,double incumbentCost)
        {

            //Init the StreamWriter for the current solution
            file = new StreamWriter(instance.InputFile + ".dat", false);

            //Printing the optimal solution and the GNUPlot input file
            for (int i = 0; i < instance.NNodes; i++)
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
                file.WriteLine(instance.Coord[pathG.path[i]].X + " " + instance.Coord[pathG.path[i]].Y + " " + (pathG.path[i] + 1) + "\n");
            }

            //GNUPlot input file needs to be closed
            file.Close();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -1)
                PrintGNUPlotHeuristic(instance.InputFile, typeSol, pathG.cost, incumbentCost);
        }

        static void TwoOpt(PathGenetic pathG)
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
                        SwapRoute(c, b, pathG);

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

        static void TabuSearch(PathStandard currentSol, PathStandard incumbentPath)
        {
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

                if (true)
                {

                    string[] currentElements = nextBestMove.Split(';');
                    a = int.Parse(currentElements[0]);
                    b = int.Parse(currentElements[1]);
                    c = int.Parse(currentElements[2]);
                    d = int.Parse(currentElements[3]);

                    SwapRoute(c, b, (PathGenetic)currentSol);

                    currentSol.path[a] = c;
                    currentSol.path[b] = d;

                    currentSol.cost += bestGain;

                    if (incumbentPath.cost > currentSol.cost)
                    {
                        /* incumbentDist = distHeuristic;
                         incumbentZHeuristic = zHeuristic;*/
                        incumbentPath = (PathGenetic)currentSol.Clone();
                    }

                    if (bestGain < 0)
                        typeSol = 0;
                    else
                    {
                        tabu.AddTabu(a, b, c, d);
                        typeSol = 1;
                    }

                }
                else
                {

                    string[] currentElements = nextWorstMove.Split(';');
                    a = int.Parse(currentElements[0]);
                    b = int.Parse(currentElements[1]);
                    c = int.Parse(currentElements[2]);
                    d = int.Parse(currentElements[3]);

                    SwapRoute(c, b, (PathGenetic)currentSol);

                    currentSol.path[a] = c;
                    currentSol.path[b] = d;

                    currentSol.cost += worstGain;

                    tabu.AddTabu(a, b, c, d);
                    typeSol = 1;

                }

                PrintHeuristicSolution((PathGenetic)currentSol, incumbentPath.cost);
                bestGain = double.MaxValue;
                worstGain = double.MinValue;

            } while (cl.ElapsedMilliseconds / 1000.0 < instance.TimeLimit);
        }

        static void VNS(PathStandard currentSol)
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

            if(order[0] != a && order[2] != a)
            {
                order.Add(a);
                order.Add(b);
            }else if (order[0] != c && order[2] != c)
            {
                order.Add(c);
                order.Add(d);
            }else
            {
                order.Add(e);
                order.Add(f);
            }

            SwapRoute(order[2], order[1], (PathGenetic)currentSol);

            currentSol.path[order[0]] = order[2];

            SwapRoute(order[4], order[3],(PathGenetic)currentSol);

            currentSol.path[order[1]] = order[4];

            currentSol.path[order[3]] = order[5];

            currentSol.cost += Point.Distance(instance.Coord[order[0]], instance.Coord[order[2]], instance.EdgeType) +
                Point.Distance(instance.Coord[order[1]], instance.Coord[order[4]], instance.EdgeType) +
                Point.Distance(instance.Coord[order[3]], instance.Coord[order[5]], instance.EdgeType) -
                Point.Distance(instance.Coord[a], instance.Coord[b], instance.EdgeType) -
                Point.Distance(instance.Coord[c], instance.Coord[d], instance.EdgeType) -
                Point.Distance(instance.Coord[e], instance.Coord[f], instance.EdgeType);
                
        }

        //not working
        static void ThreeOpt()
        {
            /*
            int indexStart = 0;
            int cnt = 0;
            bool found = false;

            do
            {
                found = false;
                int a = indexStart;
                int b = zHeuristic[a];
                //int c = zHeuristic[b];
                int c = a;
                int e = c;
                int d = b;
                int f = b;
                //int d = zHeuristic[c];
                //int e = zHeuristic[d];
                //int f = zHeuristic[e];

                for (int i = 0; i < instance.NNodes; i++, c = d, e = c, d = zHeuristic[c], f = d)
                {
                    if (found)
                    {
                        break;
                    }

                    for (int i = 0; i < instance.NNodes; i++, e = f, f = zHeuristic[e])
                    {

                        double distAB = Point.Distance(instance.Coord[a], instance.Coord[b], instance.EdgeType);
                        double distAC = Point.Distance(instance.Coord[a], instance.Coord[c], instance.EdgeType);
                        double distAD = Point.Distance(instance.Coord[a], instance.Coord[d], instance.EdgeType);
                        double distAE = Point.Distance(instance.Coord[a], instance.Coord[e], instance.EdgeType);
                        double distCD = Point.Distance(instance.Coord[c], instance.Coord[d], instance.EdgeType);
                        double distCE = Point.Distance(instance.Coord[c], instance.Coord[e], instance.EdgeType);
                        double distCF = Point.Distance(instance.Coord[c], instance.Coord[f], instance.EdgeType);
                        double distEF = Point.Distance(instance.Coord[e], instance.Coord[f], instance.EdgeType);
                        double distEB = Point.Distance(instance.Coord[e], instance.Coord[b], instance.EdgeType);
                        double distDF = Point.Distance(instance.Coord[d], instance.Coord[f], instance.EdgeType);
                        double distBD = Point.Distance(instance.Coord[b], instance.Coord[d], instance.EdgeType);
                        double distBF = Point.Distance(instance.Coord[b], instance.Coord[f], instance.EdgeType);

                        double distTotABCDEF = distAB + distCD +distEF;

                        double minDist = distTotABCDEF;
                        string minRoute = "ABCDEF";

                        if(distAB + distCE + distDF < minDist)
                        {
                            minDist = distAB + distCE + distDF;
                            minRoute = "ABCEDF";
                        }

                        if (distAE + distCF + distBD < minDist)
                        {
                            minDist = distAE + distCF + distBD;
                            minRoute = "AEDBCF";
                        }

                        if (distAC + distEB + distDF < minDist)
                        {
                            minDist = distAC + distEB + distDF;
                            minRoute = "ACBEDF";
                        }

                        if (distAD + distCF + distEB < minDist)
                        {
                            minDist = distAD + distCF + distEB;
                            minRoute = "ADEBCF";
                        }

                        if (distAC + distBD + distEF < minDist)
                        {
                            minDist = distAC + distBD + distEF;
                            minRoute = "ACBDEF";
                        }

                        if (distAE + distBF + distCD < minDist)
                        {
                            minDist = distAE + distBF + distCD;
                            minRoute = "AEDCBF";
                        }

                        if (distAD + distCE + distBF < minDist)
                        {
                            minDist = distAD + distCE + distBF;
                            minRoute = "ADECBF";
                        }



                        if(minRoute == "ABCDEF")
                        {
                            continue;

                        }else if(minRoute == "ABCEDF")
                        {
                            SwapRoute(e, d);

                            zHeuristic[c] = e;
                            zHeuristic[d] = f;

                            distHeuristic = distHeuristic - distTotABCDEF + minDist;

                            indexStart = 0;
                            cnt = 0;
                            found = true;
                            break;
                        }
                        else if (minRoute == "AEDBCF")
                        {
                            SwapRoute(e, d);

                            zHeuristic[a] = e;
                            zHeuristic[d] = b;
                            zHeuristic[c] = f;

                            distHeuristic = distHeuristic - distTotABCDEF + minDist;

                            indexStart = 0;
                            cnt = 0;
                            found = true;
                            break;

                        }
                        else if (minRoute == "ACBEDF")
                        {
                            SwapRoute(c, b);

                            zHeuristic[a] = c;

                            SwapRoute(e, d);

                            zHeuristic[b] = e;
                            zHeuristic[d] = f;

                            distHeuristic = distHeuristic - distTotABCDEF + minDist;

                            indexStart = 0;
                            cnt = 0;
                            found = true;
                            break;

                        }
                        else if (minRoute == "ADEBCF")
                        {
                            zHeuristic[a] = d;
                            zHeuristic[e] = b;
                            zHeuristic[c] = f;

                            distHeuristic = distHeuristic - distTotABCDEF + minDist;

                            indexStart = 0;
                            cnt = 0;
                            found = true;
                            break;

                        }
                        else if (minRoute == "ACBDEF")
                        {
                            SwapRoute(c, b);

                            zHeuristic[a] = c;
                            zHeuristic[b] = d;

                            distHeuristic = distHeuristic - distTotABCDEF + minDist;

                            indexStart = 0;
                            cnt = 0;
                            found = true;
                            break;

                        }
                        else if (minRoute == "AEDCBF")
                        {
                            SwapRoute(e, d);
                            zHeuristic[a] = e;

                            SwapRoute(c, b);

                            zHeuristic[d] = c;
                            zHeuristic[b] = f;

                            distHeuristic = distHeuristic - distTotABCDEF + minDist;

                            indexStart = 0;
                            cnt = 0;
                            found = true;
                            break;

                        }
                        else if (minRoute == "ADECBF")
                        {
                            zHeuristic[a] = d;

                            SwapRoute(c, b);

                            zHeuristic[e] = c;
                            zHeuristic[b] = f;

                            distHeuristic = distHeuristic - distTotABCDEF + minDist;

                            indexStart = 0;
                            cnt = 0;
                            found = true;
                            break;

                        }
                    }
                }

                if (!found)
                {
                    indexStart = b;
                    cnt++;
                }

            } while (cnt < instance.NNodes);
            */
        }

        static void SwapRoute(int c, int b, PathGenetic pathG)
        {
            int from = b;
            int to = pathG.path[from];
            do
            {
                int tmpTo = pathG.path[to];
                pathG.path[to] = from;
                from = to;
                to = tmpTo;
            } while (from != c);
        }

        //Setting Upper Bounds of each cplex model's variable to 1
        static void ResetVariables(INumVar[] z)
        {
            for (int i = 0; i < z.Length; i++)
                z[i].UB = 1;
        }

        //Computing the nearest edges for each node
        static List<int>[] BuildSL()
        {
            //SL and L stores the information regarding the nearest edges for each node 
            List<itemList>[] SL = new List<itemList>[instance.NNodes];
            List<int>[] L = new List<int>[instance.NNodes];

            for (int i = 0; i < SL.Length; i++)
            {
                SL[i] = new List<itemList>();

                for (int j = i + 1; j < SL.Length; j++)
                {
                    //Simply adding each possible links with its distance
                    if (i != j)
                        SL[i].Add(new itemList(Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType), j));
                }

                //Sorting the list
                SL[i] = SL[i].OrderBy(itemList => itemList.dist).ToList<itemList>();
                //Only the index of the nearest nodes are relevants
                L[i] = SL[i].Select(itemList => itemList.index).ToList<int>();
            }

            return L;
        }

        static List<int>[] BuildSLComplete()
        {
            //SL and L stores the information regarding the nearest edges for each node 
            List<itemList>[] SL = new List<itemList>[instance.NNodes];
            List<int>[] L = new List<int>[instance.NNodes];

            for (int i = 0; i < SL.Length; i++)
            {
                SL[i] = new List<itemList>();

                for (int j = 0; j < SL.Length; j++)
                {
                    //Simply adding each possible links with its distance
                    if (i != j)
                        SL[i].Add(new itemList(Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType), j));
                }

                //Sorting the list
                SL[i] = SL[i].OrderBy(itemList => itemList.dist).ToList<itemList>();
                //Only the index of the nearest nodes are relevants
                L[i] = SL[i].Select(itemList => itemList.index).ToList<int>();
            }

            return L;
        }


        //Building initial model
        static INumVar[] BuildModel(int n)
        {

            //When n is equal to -1 all links have their upper bound to 1, listArray is not needed
            if (n != -1)
                listArray = BuildSL();

            //Init the model's variables
            INumVar[] z = new INumVar[(instance.NNodes - 1) * instance.NNodes / 2];

            /*
             *expr will hold all the expressions that needs to be added to the model
             *initially it will be the optimality's functions
             *later it will be Ax's rows 
            */
            ILinearNumExpr expr = cplex.LinearNumExpr();


            //Populating objective function
            for (int i = 0; i < instance.NNodes; i++)
            {
                if (n >= 0)
                {
                    //Only links (i,i) with i < i are correct
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {
                        //zPos return the correct position where to store the variable corresponding to the actual link (i,i)
                        int position = zPos(i, j, instance.NNodes);
                        if ((listArray[i]).IndexOf(j) < n)
                            z[position] = cplex.NumVar(0, 1, NumVarType.Int, "z(" + (i + 1) + "," + (j + 1) + ")");
                        else
                            z[position] = cplex.NumVar(0, 0, NumVarType.Int, "z(" + (i + 1) + "," + (j + 1) + ")");
                        expr.AddTerm(z[position], Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType));
                    }
                }
                else
                {
                    //Only links (i,i) with i < i are correct
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {
                        //zPos return the correct position where to store the variable corresponding to the actual link (i,i)
                        int position = zPos(i, j, instance.NNodes);
                        z[position] = cplex.NumVar(0, 1, NumVarType.Int, "z(" + (i + 1) + "," + (j + 1) + ")");
                        expr.AddTerm(z[position], Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType));
                    }
                }
            }

            //Setting the optimality's function
            cplex.AddMinimize(expr);


            //Starting to elaborate Ax
            for (int i = 0; i < instance.NNodes; i++)
            {
                //Resetting expr
                expr = cplex.LinearNumExpr();

                for (int j = 0; j < instance.NNodes; j++)
                {
                    //For each row i only the links (i,i) or (i,i) have coefficent 1
                    //zPos return the correct position where link is stored inside the vector z
                    if (i != j)//No loops wioth only one node
                        expr.AddTerm(z[zPos(i, j, instance.NNodes)], 1);
                }

                //Adding to Ax the current equation with known term 2 and name degree(<current i node>)
                cplex.AddEq(expr, 2, "degree(" + (i + 1) + ")");
            }

            //Printing the complete model inside the file <name_file.tsp.lp>
            if (Program.VERBOSE >= -1)
                cplex.ExportModel(instance.InputFile + ".lp");

            return z;

        }


        //Print for GNUPlot
        static void PrintGNUPlot(string name, int typeSol)
        {
            //typeSol == 1 => red lines, TypeSol == 0 => blue Lines
            if (typeSol == 0)
                process.StandardInput.WriteLine("set style line 1 lc rgb '#0060ad' lt 1 lw 1 pt 5 ps 0.5\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle");
            else if (typeSol == 1)
                process.StandardInput.WriteLine("set style line 1 lc rgb '#ad0000' lt 1 lw 1 pt 5 ps 0.5\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle");
        }

        static void PrintGNUPlotHeuristic(string name, int typeSol,double currentCost, double incumbentCost)
        {
            //typeSol == 1 => red lines, TypeSol == 0 => blue Lines
            if (typeSol == 0)
                process.StandardInput.WriteLine("set style line 1 lc rgb '#0060ad' lt 1 lw 1 pt 5 ps 0.5\nset title \"Current best solution: " + incumbentCost + "   Current solution: " + currentCost + "\"\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle");
            else if (typeSol == 1)
                process.StandardInput.WriteLine("set style line 1 lc rgb '#ad0000' lt 1 lw 1 pt 5 ps 0.5\nset title \"Current best solution: " + incumbentCost + "   Current solution: " + currentCost + "\"\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle");
        }


        //Returns physical or virtual cores of the pc
      /*  static int RetriveCoreNumber(int type)
        {
            if (type == 0)
            {
                int coreCount = 0;

                foreach (var item in new System.Management.ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").Get())
                {
                    coreCount += int.Parse(item["NumberOfCores"].ToString());
                }

                return coreCount;
            }
            else
                return Environment.ProcessorCount;
        }*/


        //Used to evaluete the correct position to store and read the variables for the model
        static int zPos(int i, int j, int nNodes)
        {
            if (i == j)
                return -1;

            if (i > j)
                return zPos(j, i, nNodes);

            return i * nNodes + j - (i + 1) * (i + 2) / 2;
        }
        

        //Setting the current real residual time for Cplex and some relative parameters
        static void MipTimelimit(Stopwatch clock)
        {
            double residualTime = instance.TStart + instance.TimeLimit - clock.ElapsedMilliseconds / 1000.0;

            if (residualTime < 0.0)
                residualTime = 0.0;

            cplex.SetParam(Cplex.IntParam.ClockType, 2);
            cplex.SetParam(Cplex.Param.TimeLimit, residualTime);                            // real time
            cplex.SetParam(Cplex.Param.DetTimeLimit, Program.TICKS_PER_SECOND * cplex.GetParam(Cplex.Param.TimeLimit));			// ticks
        }


        //Initialization of the arrays used to keep track of the related components
        static void InitCC(int[] cc)
        {
            for (int i = 0; i < cc.Length; i++)
            {
                cc[i] = i;
            }
        }


        //Updating the related components for the current solution
        static void UpdateCC(int i, int j)
        {
            if (compConn[i] != compConn[j])//Same related component, the latter is not closed yet
            {
                for (int k = 0; k < compConn.Length; k++)// k>i poichè i > i
                {
                    if ((k != j) && (compConn[k] == compConn[j]))
                    {
                        //Same as Kruskal
                        compConn[k] = compConn[i];
                    }
                }

                //Finally also the vallue relative to the Point i are updated
                compConn[j] = compConn[i];
            }
            else//Here the current releted component is complete and the relative subtout elimination constraint can be added to the model
            {
                ILinearNumExpr expr = cplex.LinearNumExpr();

                //cnt stores the # of nodes of the current related components
                int cnt = 0;

                for (int h = 0; h < compConn.Length; h++)
                {
                    //Only nodes of the current related components are considered
                    if (compConn[h] == compConn[i])
                    {
                        //Each link involving the node with index h is analized
                        for (int k = h + 1; k < compConn.Length; k++)
                        {
                            //Testing if the link is valid
                            if (compConn[k] == compConn[i])
                            {
                                //Adding the link to the expression with coefficient 1
                                expr.AddTerm(z[zPos(h, k, compConn.Length)], 1);
                            }
                        }

                        cnt++;
                    }
                }

                //Adding the objects to the buffers
                ccExpr.Add(expr);
                bufferCoeffCC.Add(cnt);                
            }
        }

        //Creation of the process used to interect with GNUPlot
        static Process InitProcess ()
        {
            //Setting values to open Prompt
            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd.exe");
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;

            //Executing the Prompt
            Process process = Process.Start(processStartInfo);

            Object Width = SystemInformation.VirtualScreen.Width;
            Object Height = SystemInformation.VirtualScreen.Height;

            //Enabling GNUPlot commands
            process.StandardInput.WriteLine("gnuplot\nset terminal wxt size {0},{1}\nset lmargin at screen 0.05\nset rmargin at screen 0.95\nset bmargin at screen 0.1\nset tmargin at screen 0.9\nset xrange [{2}:{3}]\nset yrange [{4}:{5}]", Convert.ToDouble(Width.ToString()) - 100, Convert.ToDouble(Height.ToString()) - 100, instance.XMin, instance.XMax, instance.YMin, instance.YMax);

            return process;
        }

        private class Tabu
        {
            string mode;
            string originalMode;
            List<string> tabuVector;
            Instance inst;
            int threshold;

            public Tabu (string mode, Instance inst, int threshold)
            {
                this.mode = mode;
                this.originalMode = mode;
                this.inst = inst;
                this.threshold = threshold;
                this.tabuVector = new List<string>();
            }

            public bool IsTabu(int a, int b)
            {
                if (tabuVector.Contains(a + ";" + b) ||
                    tabuVector.Contains(b + ";" + a))
                    return true;
                else
                    return false;
            }

            public void AddTabu(int a, int b, int c, int d)
            {
                if (mode == "A" && tabuVector.Count >= (threshold - 1))
                {
                    mode = "B";

                }else if(mode == "B")
                {
                    if (tabuVector.Count >= 50)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            tabuVector.RemoveAt(i);
                        }
                    }
                    else
                        mode = "A";
                }

                if(!tabuVector.Contains(a + ";" + b))
                    tabuVector.Add(a + ";" + b);
                if(!tabuVector.Contains(c + ";" + d))
                    tabuVector.Add(c + ";" + d);
            }

            public void Clear()
            {
                mode = originalMode;
                tabuVector.Clear();
            }

            public int TabuLenght()
            {
                return tabuVector.Count;
            }
        }

    }
}
    
