using ILOG.Concert;
using ILOG.CPLEX;
using System.IO;
using System.Diagnostics;

namespace TSPCsharp
{
    class TSP
    {
        static int cntCC = 0;
        static int cntSol = 0; //da eliminare ----------------------------------------------------------------------
        static Process process;

        static public bool TSPOpt(Instance instance, Stopwatch clock)
        {

            //Real starting time is stored inside instance
            instance.TStart = clock.ElapsedMilliseconds /1000.0;


            //Cplex is the official class offered by IBM inside the API to use cplex
            //algorithms with C#
            Cplex cplex = new Cplex();


            cplex.SetParam(Cplex.Param.MIP.Strategy.Search, Cplex.MIPSearch.Dynamic);

            //INumVar is a special interface used to stare any kinf of variable compatible with cplex
            //Building the model, z is necessary to access the variabiles via their stored names
            INumVar[] z = BuildModel(cplex, instance);

            //Allocating the correct space to store the optimal solution
            //Only links from node i to j with i < j are considered
            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];

            //Array that stores the related component of each node
            int[] compConn = new int[instance.NNodes];

            //Setting the residual time limit for cplex, it's almost equal to instance.TStart
            MipTimelimit(cplex, instance, clock);


            //Creating the StreamWriter used by GNUPlot to print the solution
            StreamWriter file;

            process = InitProcess();
        
            do
            {

                //Cplex solves the current model
                cplex.Solve();
                
                //Initializing the arrays used to eliminate the subtour
                InitCC(compConn);

                //Setting to 0 the # of related components
                cntCC = 0;

                //# of solutions found ++
                cntSol++;

                //Init the StreamWriter for the current solution
                file = new StreamWriter(instance.InputFile + ".dat", false);

                //Storing the optimal value of the objective function
                instance.ZBest = cplex.ObjValue;

                //Storing the current link's optimal values 
                //Adding a new equation to the model deletes the current solution!!!
                int[] optSol = CopyOpt(cplex, z, (instance.NNodes - 1) * instance.NNodes / 2);

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
                        instance.BestSol[position] = optSol[position];

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
                            UpdateCC(i, j, compConn, cplex, z);
                        }
                    }
                }

                //GNUPlot input file needs to be closed
                file.Close();

                //Accessing GNUPlot to read the file
                if (Program.VERBOSE >= -100)
                    PrintGNUPlot(instance.InputFile, process);

                //Blank line
                cplex.Output().WriteLine();

                //Writing the value
                cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

                //Exporting the updated model
                if (Program.VERBOSE >= -100)
                    cplex.ExportModel(instance.InputFile + ".lp");

                    //cplex.ExportModel(instance.InputFile + cntSol + ".lp");

            } while (cntCC > 1); //if there is more then one related components the solution is not optimal 


            //Closing Cplex link
            cplex.End();

            //Accessing GNUPlot to read the file
            if (Program.VERBOSE >= -100)
                PrintGNUPlot(instance.InputFile, process);

            //Return without errors
            return true;
        }


        //Building initial model
        static INumVar[] BuildModel(Cplex cplex, Instance instance)
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
                //Only links (i,j) with i < j are correct
                for (int j = i + 1; j < instance.NNodes; j++)
                {
                    //zPos return the correct position where to store the variable corresponding to the actual link (i,j)
                    int position = zPos(i, j, instance.NNodes);
                    z[position] = cplex.NumVar(0, 1, NumVarType.Int, "z(" + (i + 1) + "," + (j + 1) + ")");
                    expr.AddTerm(z[position], Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType));
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
        static void PrintGNUPlot(string name, Process process)
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

                process.StandardInput.WriteLine("set style line 1 lc rgb '#0060ad' lt 1 lw 2 pt 7 ps 0.5\nplot '" + name + ".dat' with linespoints ls 1 notitle, '" + name + ".dat' using 1:2:3 with labels point pt 7 offset char 0,0.5 notitle");
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
            for(int i = 0; i < cc.Length; i++)
            {
                cc[i] = i;
            }
        }


        //Updating the related components for the current solution
        static void UpdateCC(int i, int j, int[] cc, Cplex cplex, INumVar[] z)
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
                cplex.AddLe(expr, cnt - 1, "test");
                //Recording that one related component is complete
                cntCC++;
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
    
