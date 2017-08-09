using ILOG.Concert;
using ILOG.CPLEX;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.Windows.Forms;
using System.Linq;

namespace TSPCsharp
{
    public class Utility
    {
        //--------------------------------------------COMMON METHODS--------------------------------------------


        //Setting the current real residual time for Cplex and some relative parameters
        public static void MipTimelimit(Cplex cplex, Instance instance, Stopwatch clock)
        {
            double residualTime = instance.TStart + instance.TimeLimit - clock.ElapsedMilliseconds / 1000.0;

            if (residualTime < 0.0)
                residualTime = 0.0;

            cplex.SetParam(Cplex.IntParam.ClockType, 2);
            cplex.SetParam(Cplex.Param.TimeLimit, residualTime);                            // real time
            cplex.SetParam(Cplex.Param.DetTimeLimit, Program.TICKS_PER_SECOND * cplex.GetParam(Cplex.Param.TimeLimit));			// ticks
        }

        //Creation of the process used to interect with GNUPlot
        public static Process InitProcess(Instance instance)
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

        //Building initial model
        public static INumVar[] BuildModel(Cplex cplex, Instance instance, int n)
        {
            //Init the model's variables
            INumVar[] x = new INumVar[(instance.NNodes - 1) * instance.NNodes / 2];

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
                    List<int>[] listArray = BuildSL(instance);

                    //Only links (i,i) with i < i are correct
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {
                        //xPos return the correct position where to store the variable corresponding to the actual link (i,i)
                        int position = xPos(i, j, instance.NNodes);
                        if ((listArray[i]).IndexOf(j) < n)
                            x[position] = cplex.NumVar(0, 1, NumVarType.Bool, "x(" + (i + 1) + "," + (j + 1) + ")");
                        else
                            x[position] = cplex.NumVar(0, 0, NumVarType.Bool, "x(" + (i + 1) + "," + (j + 1) + ")");
                        expr.AddTerm(x[position], Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType));
                    }
                }
                else
                {
                    //Only links (i,i) with i < i are correct
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {
                        //xPos return the correct position where to store the variable corresponding to the actual link (i,i)
                        int position = xPos(i, j, instance.NNodes);
                        x[position] = cplex.NumVar(0, 1, NumVarType.Bool, "x(" + (i + 1) + "," + (j + 1) + ")");
                        expr.AddTerm(x[position], Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType));
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
                    //xPos return the correct position where link is stored inside the vector x
                    if (i != j)//No loops wioth only one node
                        expr.AddTerm(x[xPos(i, j, instance.NNodes)], 1);
                }

                //Adding to Ax the current equation with known term 2 and name degree(<current i node>)
                cplex.AddEq(expr, 2, "degree(" + (i + 1) + ")");
            }

            //Printing the complete model inside the file <name_file.tsp.lp>
            if (Program.VERBOSE >= -1)
                cplex.ExportModel(instance.InputFile + ".lp");

            return x;

        }

        //Used to evaluete the correct position to store and read the variables for the model
        public static int xPos(int i, int j, int nNodes)
        {
            if (i == j)
                return -1;

            if (i > j)
                return xPos(j, i, nNodes);

            return i * nNodes + j - (i + 1) * (i + 2) / 2;
        }

        //Print for GNUPlot
        public static void PrintGNUPlot(Process process, string name, int typeSol, double currentCost, double incumbentCost)
        {
            if (incumbentCost >= 0)
            {
                //typeSol == 1 => red lines, TypeSol == 0 => blue Lines
                if (typeSol == 0)
                    process.StandardInput.WriteLine("set style line 1 lc rgb '#0060ad' lt 1 lw 1 pt 5 ps 0.5\nset title \"Current best solution: " + incumbentCost + "   Current solution: " + currentCost + "\"\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle\nset autoscale");
                else if (typeSol == 1)
                    process.StandardInput.WriteLine("set style line 1 lc rgb '#ad0000' lt 1 lw 1 pt 5 ps 0.5\nset title \"Current best solution: " + incumbentCost + "   Current solution: " + currentCost + "\"\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle\nset autoscale");
            }else
            {
                //typeSol == 1 => red lines, TypeSol == 0 => blue Lines
                if (typeSol == 0)
                    process.StandardInput.WriteLine("set style line 1 lc rgb '#0060ad' lt 1 lw 1 pt 5 ps 0.5\nset title \"Current solution: " + currentCost + "\"\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle\nset autoscale");
                else if (typeSol == 1)
                    process.StandardInput.WriteLine("set style line 1 lc rgb '#ad0000' lt 1 lw 1 pt 5 ps 0.5\nset title \"Current solution: " + currentCost + "\"\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle\nset autoscale");
            }
        }


        //--------------------------------------------LOOP UTILITYES--------------------------------------------

        //Setting Upper Bounds of each cplex model's variable to 1
        public static void ResetVariables(INumVar[] x)
        {
            for (int i = 0; i < x.Length; i++)
                x[i].UB = 1;
        }

        //Initialization of the arrays used to keep track of the related components
        public static void InitCC(int[] cc)
        {
            for (int i = 0; i < cc.Length; i++)
            {
                cc[i] = i;
            }
        }

        //Updating the related components
        public static void UpdateCC(Cplex cplex, INumVar[] x, List<ILinearNumExpr> rcExpr, List<int> bufferCoeffRC, int[] relatedComponents, int i, int j)
        {
            if (relatedComponents[i] != relatedComponents[j])//Same related component, the latter is not closed yet
            {
                for (int k = 0; k < relatedComponents.Length; k++)
                {
                    if ((k != j) && (relatedComponents[k] == relatedComponents[j]))
                    {
                        //Same as Kruskal
                        relatedComponents[k] = relatedComponents[i];
                    }
                }
                //Finally also the vallue relative to the Point i are updated
                relatedComponents[j] = relatedComponents[i];
            }
            else//Here the current releted component is complete and the relative subtout elimination constraint can be added to the model
            {
                ILinearNumExpr expr = cplex.LinearNumExpr();

                //cnt stores the # of nodes of the current related components
                int cnt = 0;

                for (int h = 0; h < relatedComponents.Length; h++)
                {
                    //Only nodes of the current related components are considered
                    if (relatedComponents[h] == relatedComponents[i])
                    {
                        //Each link involving the node with index h is analized
                        for (int k = h + 1; k < relatedComponents.Length; k++)
                        {
                            //Testing if the link is valid
                            if (relatedComponents[k] == relatedComponents[i])
                            {
                                //Adding the link to the expression with coefficient 1
                                expr.AddTerm(x[xPos(h, k, relatedComponents.Length)], 1);
                            }
                        }

                        cnt++;
                    }
                }

                //Adding the objects to the buffers
                rcExpr.Add(expr);
                bufferCoeffRC.Add(cnt);
            }
        }


        //--------------------------------------------HEURISTIC UTILITYES--------------------------------------------

        public static PathGenetic NearestNeightbor(Instance instance, Random rnd, List<int>[] listArray)
        {
            // heuristicSolution is the path of the current heuristic solution generate
            int[] heuristicSolution = new int[instance.NNodes];
            double distHeuristic = 0;

            int currentIndex = rnd.Next(instance.NNodes);
            int startindex = currentIndex;

            bool[] availableIndexes = new bool[instance.NNodes];

            availableIndexes[currentIndex] = true;

            for (int i = 0; i < instance.NNodes - 1; i++)
            {
                bool found = false;

                int plus = RndPlus(rnd);

                int nextIndex = listArray[currentIndex][0 + plus];

                do
                {
                    if (availableIndexes[nextIndex] == false)
                    {
                        heuristicSolution[currentIndex] = nextIndex;
                        distHeuristic += Point.Distance(instance.Coord[currentIndex], instance.Coord[nextIndex], instance.EdgeType);
                        availableIndexes[nextIndex] = true;
                        currentIndex = nextIndex;
                        found = true;
                    }
                    else
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

            heuristicSolution[currentIndex] = startindex;
            distHeuristic += Point.Distance(instance.Coord[currentIndex], instance.Coord[startindex], instance.EdgeType);

            return new PathGenetic(heuristicSolution, distHeuristic);
        }

        //Generating a new heuristic solution for the Genetic Algorithm
        public static PathGenetic NearestNeightborGenetic(Instance instance, Random rnd, bool rndStartPoint, List<int>[] listArray)
        {
            // heuristicSolution is the path of the current heuristic solution generate
            int[] heuristicSolution = new int[instance.NNodes];

            //List that contains all nodes belonging to the graph 
            List<int> availableNodes = new List<int>(); 
            int currenNode;

            //rndStartPoint define if the starting point is random or always the node 0 
            if (rndStartPoint)
                currenNode = rnd.Next(0, instance.NNodes);
            else
                currenNode = 0;

            heuristicSolution[0] = currenNode;
            availableNodes.Add(currenNode);
            
            for (int i = 1; i < instance.NNodes; i++)
            {
                bool found = false;
                int plus = RndGenetic(rnd);
                int nextNode = listArray[currenNode][plus];

                do
                {
                    //We control that the selected node has never been visited
                    if (availableNodes.Contains(nextNode) == false)
                    {
                        heuristicSolution[i] = nextNode;                     
                        availableNodes.Add(nextNode);
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

            return new PathGenetic(heuristicSolution, instance);
        }

        //Computing the nearest edges for each node
        static List<int>[] BuildSL(Instance instance)
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

        public static List<int>[] BuildSLComplete(Instance instance)
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

        public static void SwapRoute(int c, int b, PathStandard pathG)
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

        static int RndPlus(Random rnd)
        {
            double tmp = rnd.NextDouble();

            if (tmp < 0.9)
                return 0;
            else if (tmp < 0.99)
                return 1;
            else
                return 2;
        }

        static int RndGenetic(Random rnd)
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

        public static void PrintHeuristicSolution(Instance instance, Process process,  PathStandard pathG, double incumbentCost, int typeSol)
        {

            //Init the StreamWriter for the current solution
            StreamWriter file = new StreamWriter(instance.InputFile + ".dat", false);

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
            {
                if(pathG.cost != incumbentCost)
                    PrintGNUPlot(process, instance.InputFile, typeSol, pathG.cost, incumbentCost);
                else
                    PrintGNUPlot(process, instance.InputFile, typeSol, pathG.cost, -1);
            }
        }

        public static PathGenetic GenerateChild(Instance instance, Random rnd, PathGenetic mother, PathGenetic father, List<int>[] listArray)
        {
            PathGenetic child;
            int[] pathChild = new int[instance.NNodes];

            //This variable defines the point of breaking the path of the father and that of the mother
            int crossover = (rnd.Next(0, instance.NNodes));

            for (int i = 0; i < instance.NNodes; i++)
            {
                if (i > crossover)
                    pathChild[i] = mother.path[i];
                else
                    pathChild[i] = father.path[i];
            }


            //With a probability of 0.01 we make a mutation
            if (rnd.Next(0, 101) == 100)
                Mutation(instance, rnd, pathChild);              

            //The repair method ensures that the child is a permissible path
            child = Repair(instance, pathChild, listArray);

            
            if (ProbabilityTwoOpt(instance, rnd) == 1)
            {
                child.path = InterfaceForTwoOpt(child.path);

                TSP.TwoOpt(instance, child);

                child.path = Reverse(child.path);
            }
            return child;
        }

        public static List<PathGenetic> NextPopulation(Instance instance, int sizePopulation, List<PathGenetic> FatherGeneration, List<PathGenetic> ChildGeneration)
        {
            List<PathGenetic> nextGeneration = new List<PathGenetic>();
            //Data structure used to generate a population 
            List<int> roulette = new List<int>();
            Random rouletteValue = new Random();

            //List that contains all number raffled
            List<int> NumbersExtracted = new List<int>();
            bool find = false;
            int numberExtracted;

            for (int i = 0; i < ChildGeneration.Count; i++)
                FatherGeneration.Add(ChildGeneration[i]);

            //FillRoulette return the size of the roultette
            int upperExtremity = FillRoulette(roulette, FatherGeneration);

            for (int i = 0; i < sizePopulation; i++)
            {
                do
                {
                    find = true;
                    numberExtracted = rouletteValue.Next(0, upperExtremity);
                    //A path can't be sorted more than one time
                    if (NumbersExtracted.Contains(roulette[numberExtracted]) == false)
                    {
                        find = false;
                        NumbersExtracted.Add(roulette[numberExtracted]);

                        //We add to the next population the path having nRoulette equal to roulette[numberExtracted]
                        nextGeneration.Add(FatherGeneration.Find(x => x.NRoulette == roulette[numberExtracted]));
                    }
                } while (find);
            }
            return nextGeneration;
        }

        public static PathGenetic BestSolution(List<PathGenetic> percorsi, PathGenetic migliorCamminoAssoluto)
        {
            PathGenetic migliorCamminoGenerazione = migliorCamminoAssoluto;

            for (int i = 1; i < percorsi.Count; i++)
            {
                if (percorsi[i].cost < migliorCamminoGenerazione.cost)
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

        //Thi method create a constant k needed to the method FillRoulette
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

        public static void PrintGeneticSolution(Instance instance, Process process, PathGenetic Heuristic)
        {
            StreamWriter file = new StreamWriter(instance.InputFile + ".dat", false);

            for (int i = 0; i + 1 < instance.NNodes; i++)
            {
                int vertice1 = Heuristic.path[i];
                int vertice2 = Heuristic.path[i + 1];
                file.WriteLine(instance.Coord[vertice1].X + " " + instance.Coord[vertice1].Y + " " + (vertice1 + 1));
                file.WriteLine(instance.Coord[vertice2].X + " " + instance.Coord[vertice2].Y + " " + (vertice2 + 1) + "\n");
            }
            file.WriteLine(instance.Coord[Heuristic.path[0]].X + " " + instance.Coord[Heuristic.path[0]].Y + " " + (Heuristic.path[0] + 1));
            file.WriteLine(instance.Coord[Heuristic.path[instance.NNodes - 1]].X + " " + instance.Coord[Heuristic.path[instance.NNodes - 1]].Y + " " + (Heuristic.path[instance.NNodes - 1] + 1) + "\n");

            //GNUPlot input file needs to be closed
            file.Close();
            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -1)
                PrintGNUPlot(process, instance.InputFile, 1, Heuristic.cost, -1);
        }

        //We modify randomly the path relative to the child
        static void Mutation(Instance instance, Random rnd, int[] pathChild)
        {
            int indexToModify = rnd.Next(0, pathChild.Length);
            int newValue = rnd.Next(0, instance.NNodes);
            pathChild[indexToModify] = newValue;
        }

        static PathGenetic Repair(Instance instance, int[] pathChild, List<int>[] listArray)
        {
            //This list contains the isolated nodes of the child
            List<int> isolatedNodes = new List<int>();

            //nearlestIsolatedNodes[i] contains the nearest node relative to isolatedNodes[i]
            List<int> nearlestIsolatedNodes = new List<int>();

            FindIsolatedNodes(instance, pathChild, isolatedNodes, nearlestIsolatedNodes);

            FindNearestNode(isolatedNodes, nearlestIsolatedNodes, listArray);

            //Is the path without nodes visited more times
            List<int> pathIncomplete = new List<int>();

            //Is the path that it is a solution of TSP
            int[] pathComplete = new int[instance.NNodes];

            for (int i = 0; i < instance.NNodes; i++)
            {              
                if (pathIncomplete.Contains(pathChild[i]) == false)
                    pathIncomplete.Add(pathChild[i]);       
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
            return new PathGenetic(pathComplete, instance);
        }

        //This list method calculate the isolated nodes of the child
        static void FindIsolatedNodes(Instance instance, int[] pathChild, List<int> isolatedNodes, List<int> nearestNeighIsolNode)
        {
            bool nodeIsVisited = false;
            for (int i = 0; i < instance.NNodes; i++)
            {
                for (int j = 0; j < instance.NNodes; j++)
                {
                    if (pathChild[j] == i)
                    {
                        nodeIsVisited = true;
                        //If the node is visited can exit to for
                        break;
                    }
                }

                //If the node has nevere been visited is a isolated noode
                if (nodeIsVisited == false)
                    isolatedNodes.Add(i);

                //Configure nodeIsVisited to its default value
                nodeIsVisited = false;
            }
        }

        static void FindNearestNode(List<int> isolatedNodes, List<int> nearestNeighIsolNode, List<int>[] listArray)
        {
            int nextNode = 0;
            int nearestNode = 0;
            bool find = true;

            for (int i = 0; i < isolatedNodes.Count; i++)
            {
                find = false;
                nextNode = 0;
                do
                {
                    nearestNode = listArray[isolatedNodes[i]][nextNode];

                    /*We don't want that the nearestNeighIsolNode relative to a isolated node is a isolated node or the
                    of another node*/
                    if (((isolatedNodes.Contains(nearestNode)) == false) && (nearestNeighIsolNode.Contains(nearestNode) == false))
                    {
                        nearestNeighIsolNode.Add(nearestNode);
                        find = false;
                    }
                    else
                    {
                        nextNode++;
                        find = true;
                    }
                } while (find);
            }
        }

        static int ProbabilityTwoOpt(Instance instance, Random rnd)
        {
            //The probability to applying TwoOpt is proportional to the number of nodes
            if (rnd.Next(1, instance.NNodes / 2) == 1)
                return 1;
            else
                return 0;
        }

        public static int[] InterfaceForTwoOpt(int[] path)
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

        public static int[] Reverse(int[] path)
        {
            int[] returnGenetic = new int[path.Length];

            returnGenetic[0] = path[0];

            for (int i = 1; i < path.Length; i++)
                returnGenetic[i] = path[returnGenetic[i - 1]];

            return returnGenetic;
        }


        //--------------------------------------------MATH HEURISTIC UTILITYES--------------------------------------------

        public static void ModifyModel(Instance instance, INumVar[] x, Random rnd, int percentageFixing, double[] values, List<int[]> fixedEdges)
        {
            for (int i = 0; i < x.Length; i++)//Inutile la prima volta
            {
                x[i].LB = 0;
                x[i].UB = 1;
            }

            for (int i = 0; i < x.Length; i++)
            {
                if ((values[i] == 1))
                {
                    if (RandomSelect(rnd, percentageFixing) == 1)
                    {
                        x[i].LB = 1;
                        fixedEdges.Add((xPosInv(i, instance.NNodes)));
                    }
                }
            }
        }

        static int RandomSelect(Random rnd, int percentageFixing)
        {
            if (rnd.Next(1, 10) < percentageFixing)
                return 1;
            else
                return 0;
        }

        static int[] xPosInv(int index, int nNodes)
        {
            for (int i = 0; i < nNodes; i++)
            {
                for (int j = i + 1; j < nNodes; j++)
                    if (xPos(i, j, nNodes) == index)
                        return new int[] { i, j };
            }

            return null;
        }

        public static void PreProcessing(Instance instance, INumVar[] x, List<int[]> fixedVariables)
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
                    x[xPos(nodeLeft, nodeRight, instance.NNodes)].UB = 0;
            }
        }

        public static PathGenetic GenerateChildRins(Cplex cplex, Instance instance, Process process, INumVar[] x, PathGenetic mother, PathGenetic father)
        {
            PathGenetic child;
            int[] path = new int[instance.NNodes];

            int[] m = InterfaceForTwoOpt(mother.path);
            //PrintGeneticSolution(instance, process, mother.path);
            int[] f = InterfaceForTwoOpt(father.path);
            //PrintGeneticSolution(instance, process, father.path);

            for (int i = 0; i < m.Length; i++)
            {
                if (m[i] == f[i] || f[m[i]] == i)
                    x[xPos(i, m[i], m.Length)].LB = 1;
            }

            cplex.Solve();

            double[] actualX = cplex.GetValues(x);

            int tmp = 0;
            int[] available = new int[instance.NNodes];

            for (int i = 0; i < instance.NNodes - 1; i++)
            {
                for (int j = 0; j < instance.NNodes; j++)
                {
                    if (tmp != j && available[j] == 0)
                    {
                        int position = xPos(tmp, j, instance.NNodes);

                        if (actualX[position] >= 0.5)
                        {
                            path[tmp] = j;
                            tmp = j;
                            available[j] = 1;
                        }
                    }
                }
            }

            child = new PathGenetic(Reverse(path), instance);
            //PrintGeneticSolution(instance, process, child.path);

            for (int i = 0; i < m.Length; i++)
            {
                if (m[i] == f[i] || i == f[m[i]])
                    x[xPos(i, m[i], m.Length)].LB = 0;
            }

            return child;
        }

        static double[] ConvertIntArrayToDoubleArray(int[] adD)
        {
            return adD.Select(d => (double)d).ToArray();
        }
    }
}
