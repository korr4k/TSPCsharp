using ILOG.Concert;
using ILOG.CPLEX;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

namespace TSPCsharp
{
    class TSP
    {
        static int cntSol = 0; //da eliminare ----------------------------------------------------------------------
        static Process process;
        static int solveMethod; //0 loop, 1 b&c
        static Cplex cplex;

        static public bool TSPOpt(Instance instance, Stopwatch clock)
        {
            //Cplex is the official class offered by IBM inside the API to use cplex
            //algorithms with C#
            cplex = new Cplex();
            cplex.SetParam(Cplex.Param.MIP.Strategy.Search, Cplex.MIPSearch.Dynamic);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nPress anything to continue, attention: the display will be cleared");
            Console.ReadLine();
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine("Select how you want to procede:\nPress 1 to use the classic loop method, 2 to use the optimal branch & cut\n");

            switch (Console.ReadLine())
            {
                case "1":
                    {
                        solveMethod = 1;
                        Console.WriteLine("Press 1 to not use any heuristic method, 2 to use the % precion method, 3 to use only a # of the nearest edges, 4 to use both 2 and 3\n");
                        switch (Console.ReadLine())
                        {
                            case "1":
                                {
                                    //Real starting time is stored inside instance
                                    instance.TStart = clock.ElapsedMilliseconds / 1000.0;
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    MipTimelimit(cplex, instance, clock);
                                    ClassicLoop(instance);
                                    break;
                                }

                            case "2":
                                {
                                    Console.WriteLine("Write the % precision:");
                                    double percentage = Convert.ToDouble(Console.ReadLine());
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    MipTimelimit(cplex, instance, clock);
                                    //Real starting time is stored inside instance
                                    instance.TStart = clock.ElapsedMilliseconds / 1000.0;
                                    PercLoop(percentage, instance);
                                    break;
                                }

                            case "3":
                                {
                                    Console.WriteLine("Write the # of nearest edges:");
                                    int numb = Convert.ToInt32(Console.ReadLine());
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    MipTimelimit(cplex, instance, clock);
                                    //Real starting time is stored inside instance
                                    instance.TStart = clock.ElapsedMilliseconds / 1000.0;
                                    NearLoop(numb, instance);
                                    break;
                                }

                            case "4":
                                {
                                    Console.WriteLine("Write the % precision:");
                                    double percentage = Convert.ToDouble(Console.ReadLine());
                                    Console.WriteLine("Write the # of nearest edges:");
                                    int numb = Convert.ToInt32(Console.ReadLine());
                                    //Real starting time is stored inside instance
                                    instance.TStart = clock.ElapsedMilliseconds / 1000.0;
                                    //Setting the residual time limit for cplex, it's almost equal to instance.TStart
                                    MipTimelimit(cplex, instance, clock);
                                    HLoop(percentage, numb, instance);
                                    break;
                                }

                            default:
                                throw new System.Exception("Bad argument");
                        }
                        break;
                    }

                case "2":
                    break;

                default:
                    throw new System.Exception("Bad argument");
            }
            return true;
        }

        static void ClassicLoop(Instance instance)
        {
            //listArray contains the nearest availeble edges for each edge
            List < int >[] listArray = BuildSL(instance);

            //INumVar is a special interface used to stare any kinf of variable compatible with cplex
            //Building the model, z is necessary to access the variabiles via their stored names
            INumVar[] z = BuildModelNearEdge(cplex, instance, listArray, -1);

            //Allocating the correct space to store the optimal solution
            //Only links from node i to j with i < j are considered
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Array that stores the related component of each node
            int[] compConn = new int[instance.NNodes];

            //Creating the StreamWriter used by GNUPlot to print the solution
            StreamWriter file;

            process = InitProcess();

            List<ILinearNumExpr> ccExpr = new List<ILinearNumExpr>();
            List<int> bufferCoeffCC = new List<int>();

            int typeSol = 1;

            do
            {

                //Cplex solves the current model
                cplex.Solve();

                //Initializing the arrays used to eliminate the subtour
                InitCC(compConn);

                ccExpr = new List<ILinearNumExpr>();
                bufferCoeffCC = new List<int>();

                //# of solutions found ++
                cntSol++;

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

                        //Reading the optimal solution for the actual link (i,j)
                        instance.BestSol[position] = cplex.GetValue(z[position]);

                        //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                        if (instance.BestSol[position] >= 0.5)
                        {
                            /*
                             *Current GNUPlot format is:
                             *-- previus link --
                             *<Blank line>
                             *Xi Yi <index(i)>
                             *Xj Yj <index(j)>
                             *<Blank line> 
                             *-- next link --
                            */
                            file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                            file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");

                            //Updating the model with the current subtours elimination
                            UpdateCC(i, j, compConn, cplex, z, ccExpr, bufferCoeffCC);
                        }
                    }
                }

                if (ccExpr.Count > 1)
                {
                    for (int i = 0; i < ccExpr.Count; i++)
                        cplex.AddLe(ccExpr[i], bufferCoeffCC[i] - 1);
                }

                //GNUPlot input file needs to be closed
                file.Close();

                //Accessing GNUPlot to read the file
                if (Program.VERBOSE >= -100)
                    PrintGNUPlot(instance.InputFile, process, typeSol);

                //Blank line
                cplex.Output().WriteLine();

                //Writing the value
                cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

                //Exporting the updated model
                if (Program.VERBOSE >= -100)
                    cplex.ExportModel(instance.InputFile + ".lp");

                //cplex.ExportModel(instance.InputFile + cntSol + ".lp");

            } while (ccExpr.Count > 1); //if there is more then one related components the solution is not optimal 


            //Closing Cplex link
            cplex.End();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -100)
                PrintGNUPlot(instance.InputFile, process, typeSol);
        }

        static void PercLoop(double perc, Instance instance)
        {
            cplex.SetParam(Cplex.DoubleParam.EpGap, perc);

            bool epGap = true;

            //listArray contains the nearest availeble edges for each edge
            List<int>[] listArray = BuildSL(instance);

            //INumVar is a special interface used to stare any kinf of variable compatible with cplex
            //Building the model, z is necessary to access the variabiles via their stored names
            INumVar[] z = BuildModelNearEdge(cplex, instance, listArray, -1);

            //Allocating the correct space to store the optimal solution
            //Only links from node i to j with i < j are considered
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Array that stores the related component of each node
            int[] compConn = new int[instance.NNodes];

            //Creating the StreamWriter used by GNUPlot to print the solution
            StreamWriter file;

            process = InitProcess();

            List<ILinearNumExpr> ccExpr = new List<ILinearNumExpr>();
            List<int> bufferCoeffCC = new List<int>();

            int typeSol = 0;

            do
            {
                if (ccExpr.Count == 1)
                {
                    cplex.SetParam(Cplex.DoubleParam.EpGap, 1e-06);
                    epGap = false;
                    typeSol = 1;
                }

                //Cplex solves the current model
                cplex.Solve();

                //Initializing the arrays used to eliminate the subtour
                InitCC(compConn);

                ccExpr = new List<ILinearNumExpr>();
                bufferCoeffCC = new List<int>();

                //# of solutions found ++
                cntSol++;

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

                        //Reading the optimal solution for the actual link (i,j)
                        instance.BestSol[position] = cplex.GetValue(z[position]);

                        //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                        if (instance.BestSol[position] >= 0.5)
                        {
                            /*
                             *Current GNUPlot format is:
                             *-- previus link --
                             *<Blank line>
                             *Xi Yi <index(i)>
                             *Xj Yj <index(j)>
                             *<Blank line> 
                             *-- next link --
                            */
                            file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                            file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");

                            //Updating the model with the current subtours elimination
                            UpdateCC(i, j, compConn, cplex, z, ccExpr, bufferCoeffCC);
                        }
                    }
                }

                if (ccExpr.Count > 1)
                {
                    for (int i = 0; i < ccExpr.Count; i++)
                        cplex.AddLe(ccExpr[i], bufferCoeffCC[i] - 1);
                }

                //GNUPlot input file needs to be closed
                file.Close();

                //Accessing GNUPlot to read the file
                if (Program.VERBOSE >= -100)
                    PrintGNUPlot(instance.InputFile, process, typeSol);

                //Blank line
                cplex.Output().WriteLine();

                //Writing the value
                cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

                //Exporting the updated model
                if (Program.VERBOSE >= -100)
                    cplex.ExportModel(instance.InputFile + ".lp");

                //cplex.ExportModel(instance.InputFile + cntSol + ".lp");

            } while (ccExpr.Count > 1 || epGap); //if there is more then one related components the solution is not optimal 


            //Closing Cplex link
            cplex.End();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -100)
                PrintGNUPlot(instance.InputFile, process, typeSol);
        }

        static void NearLoop(int numb, Instance instance)
        {
            //listArray contains the nearest availeble edges for each edge 
            List<int>[] listArray = BuildSL(instance);

            //INumVar is a special interface used to stare any kinf of variable compatible with cplex
            //Building the model, z is necessary to access the variabiles via their stored names
            INumVar[] z = BuildModelNearEdge(cplex, instance, listArray, numb);

            //Allocating the correct space to store the optimal solution
            //Only links from node i to j with i < j are considered
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Array that stores the related component of each node
            int[] compConn = new int[instance.NNodes];

            //Creating the StreamWriter used by GNUPlot to print the solution
            StreamWriter file;

            process = InitProcess();

            bool allEdges = false;

            List<ILinearNumExpr> ccExpr = new List<ILinearNumExpr>();
            List<int> bufferCoeffCC = new List<int>();

            int typeSol = 0;

            do
            {
                if (ccExpr.Count == 1)
                {
                    ResetVariables(z);
                    allEdges = true;
                    typeSol = 1;
                }

                //Cplex solves the current model
                cplex.Solve();

                //Initializing the arrays used to eliminate the subtour
                InitCC(compConn);

                ccExpr = new List<ILinearNumExpr>();
                bufferCoeffCC = new List<int>();

                //# of solutions found ++
                cntSol++;

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

                        //Reading the optimal solution for the actual link (i,j)
                        instance.BestSol[position] = cplex.GetValue(z[position]);

                        //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                        if (instance.BestSol[position] >= 0.5)
                        {
                            /*
                             *Current GNUPlot format is:
                             *-- previus link --
                             *<Blank line>
                             *Xi Yi <index(i)>
                             *Xj Yj <index(j)>
                             *<Blank line> 
                             *-- next link --
                            */
                            file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                            file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");

                            //Updating the model with the current subtours elimination
                            UpdateCC(i, j, compConn, cplex, z, ccExpr, bufferCoeffCC);
                        }
                    }
                }

                if (ccExpr.Count > 1)
                {
                    for (int i = 0; i < ccExpr.Count; i++)
                        cplex.AddLe(ccExpr[i], bufferCoeffCC[i] - 1);
                }

                //GNUPlot input file needs to be closed
                file.Close();

                //Accessing GNUPlot to read the file
                if (Program.VERBOSE >= -100)
                    PrintGNUPlot(instance.InputFile, process, typeSol);

                //Blank line
                cplex.Output().WriteLine();

                //Writing the value
                cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

                //Exporting the updated model
                if (Program.VERBOSE >= -100)
                    cplex.ExportModel(instance.InputFile + ".lp");

                //cplex.ExportModel(instance.InputFile + cntSol + ".lp");

            } while (ccExpr.Count > 1 || allEdges == false); //if there is more then one related components the solution is not optimal 


            //Closing Cplex link
            cplex.End();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -100)
                PrintGNUPlot(instance.InputFile, process, typeSol);
        }

        static void HLoop(double perc, int numb, Instance instance)
        {
            cplex.SetParam(Cplex.DoubleParam.EpGap, perc);

            bool epGap = true;

            //listArray contains the nearest availeble edges for each edge 
            List<int>[] listArray = BuildSL(instance);

            //INumVar is a special interface used to stare any kinf of variable compatible with cplex
            //Building the model, z is necessary to access the variabiles via their stored names
            INumVar[] z = BuildModelNearEdge(cplex, instance, listArray, numb);

            //Allocating the correct space to store the optimal solution
            //Only links from node i to j with i < j are considered
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Array that stores the related component of each node
            int[] compConn = new int[instance.NNodes];

            //Creating the StreamWriter used by GNUPlot to print the solution
            StreamWriter file;

            process = InitProcess();

            bool allEdges = false;

            List<ILinearNumExpr> ccExpr = new List<ILinearNumExpr>();
            List<int> bufferCoeffCC = new List<int>();

            int typeSol = 0;

            do
            {
                if (ccExpr.Count == 1)
                {
                    ResetVariables(z);
                    allEdges = true;
                    cplex.SetParam(Cplex.DoubleParam.EpGap, 1e-06);
                    epGap = false;
                    typeSol = 1;
                }

                //Cplex solves the current model
                cplex.Solve();

                //Initializing the arrays used to eliminate the subtour
                InitCC(compConn);

                ccExpr = new List<ILinearNumExpr>();
                bufferCoeffCC = new List<int>();

                //# of solutions found ++
                cntSol++;

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

                        //Reading the optimal solution for the actual link (i,j)
                        instance.BestSol[position] = cplex.GetValue(z[position]);

                        //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                        if (instance.BestSol[position] >= 0.5)
                        {
                            /*
                             *Current GNUPlot format is:
                             *-- previus link --
                             *<Blank line>
                             *Xi Yi <index(i)>
                             *Xj Yj <index(j)>
                             *<Blank line> 
                             *-- next link --
                            */
                            file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y + " " + (i + 1));
                            file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + " " + (j + 1) + "\n");

                            //Updating the model with the current subtours elimination
                            UpdateCC(i, j, compConn, cplex, z, ccExpr, bufferCoeffCC);
                        }
                    }
                }

                if (ccExpr.Count > 1)
                {
                    for (int i = 0; i < ccExpr.Count; i++)
                        cplex.AddLe(ccExpr[i], bufferCoeffCC[i] - 1);
                }

                //GNUPlot input file needs to be closed
                file.Close();

                //Accessing GNUPlot to read the file
                if (Program.VERBOSE >= -100)
                    PrintGNUPlot(instance.InputFile, process, typeSol);

                //Blank line
                cplex.Output().WriteLine();

                //Writing the value
                cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

                //Exporting the updated model
                if (Program.VERBOSE >= -100)
                    cplex.ExportModel(instance.InputFile + ".lp");

                //cplex.ExportModel(instance.InputFile + cntSol + ".lp");

            } while (ccExpr.Count > 1 || allEdges == false || epGap == true); //if there is more then one related components the solution is not optimal 


            //Closing Cplex link
            cplex.End();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -100)
                PrintGNUPlot(instance.InputFile, process, typeSol);
        }

        static void ResetVariables(INumVar[] z)
        {
            for (int i = 0; i < z.Length; i++)
                z[i].UB = 1;
        }

        static List<int>[] BuildSL(Instance instance)
        {
            List<itemList>[] SL = new List<itemList>[instance.NNodes];
            List<int>[] L = new List<int>[instance.NNodes];

            for (int i = 0; i < SL.Length; i++)
            {
                SL[i] = new List<itemList>();

                for (int j = i + 1; j < SL.Length; j++)
                {
                    if (i != j)
                        SL[i].Add(new itemList(Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType), j));
                }

                SL[i] = SL[i].OrderBy(itemList => itemList.dist).ToList<itemList>();
                L[i] = SL[i].Select(itemList => itemList.index).ToList<int>();
            }

            return L;
        }

        class itemList
        {
            public itemList(double d, int i)
            {
                dist = d;
                index = i;
            }

            public double dist { get; set; }
            public int index { get; set; }
        }


        //Building initial model
        static INumVar[] BuildModelNearEdge(Cplex cplex, Instance instance, List<int>[] listArray, int n)
        {

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
                    //Only links (i,j) with i < j are correct
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {
                        //zPos return the correct position where to store the variable corresponding to the actual link (i,j)
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
                    //Only links (i,j) with i < j are correct
                    for (int j = i + 1; j < instance.NNodes; j++)
                    {
                        //zPos return the correct position where to store the variable corresponding to the actual link (i,j)
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
                    //For each row i only the links (i,j) or (j,i) have coefficent 1
                    //zPos return the correct position where link is stored inside the vector z
                    if (i != j)//No loops wioth only one node
                        expr.AddTerm(z[zPos(i, j, instance.NNodes)], 1);
                }

                //Adding to Ax the current equation with known term 2 and name degree(<current i node>)
                cplex.AddEq(expr, 2, "degree(" + (i + 1) + ")");
            }

            //Printing the complete model inside the file <name_file.tsp.lp>
            if (Program.VERBOSE >= -100)
                cplex.ExportModel(instance.InputFile + ".lp");

            return z;

        }


        //Print for GNUPlot
        static void PrintGNUPlot(string name, Process process, int typeSol)
        {
            if (process != null)
            {
                /*
                 *Writing in the prompt:
                 *gnuplot
                 *set style line 1 lc rgb '#0060ad' lt 1 lw 2 pt 7 ps 0.5
                 *plot '<name_current_solution>.dat' with linespoints ls 1 notitle, '<name_current_solution>.dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle"
                 */

                if (cntSol == 1)
                    process.StandardInput.WriteLine("gnuplot");

                if(typeSol == 0)
                    process.StandardInput.WriteLine("set style line 1 lc rgb '#0060ad' lt 1 lw 2 pt 7 ps 0.5\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle");
                else if(typeSol == 1)
                    process.StandardInput.WriteLine("set style line 1 lc rgb '#ad0000' lt 1 lw 2 pt 7 ps 0.5\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle");

            }
        }


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
        static void MipTimelimit(Cplex cplex, Instance inst, Stopwatch clock)
        {
            double residualTime = inst.TStart + inst.TimeLimit - clock.ElapsedMilliseconds / 1000.0;

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
        static void UpdateCC(int i, int j, int[] cc, Cplex cplex, INumVar[] z, List<ILinearNumExpr> ccExpr, List<int> bufferCoeffCC)
        {
            if (cc[i] != cc[j])//Same related component, the latter is not closed yet
            {
                for (int k = 0; k < cc.Length; k++)// k>i poichè i > j
                {
                    if ((k != j) && (cc[k] == cc[j]))
                    {
                        //Same as Kruskal
                        cc[k] = cc[i];
                    }
                }

                //Finally also the vallue relative to the Point j are updated
                cc[j] = cc[i];
            }
            else//Here the current releted component is complete and the relative subtout elimination constraint can be added to the model
            {
                ILinearNumExpr expr = cplex.LinearNumExpr();

                int cnt = 0;

                for (int h = 0; h < cc.Length; h++)
                {
                    if (cc[h] == cc[i])
                    {
                        for (int k = h + 1; k < cc.Length; k++)
                        {
                            if (cc[k] == cc[i])
                            {
                                expr.AddTerm(z[zPos(h, k, cc.Length)], 1);
                            }
                        }

                        cnt++;
                    }
                }

                ccExpr.Add(expr);
                bufferCoeffCC.Add(cnt);                
            }
        }


        //Just coping the optimal values of each variable into another array
        static int[] CopyOpt(Cplex cplex, INumVar[] z, int nLinks)
        {
            int[] tmp = new int[nLinks];

            for (int i = 0; i < nLinks; i++)
            {
                tmp[i] = (int)(cplex.GetValue(z[i]) + 0.5);
            }

            return tmp;
        }


        static Process InitProcess()
        {
            //Setting values to open Prompt
            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd.exe");
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;

            //Executing the Prompt
            Process process = Process.Start(processStartInfo);

            return process;
        }

    }
}
    
