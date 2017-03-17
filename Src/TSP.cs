using ILOG.Concert;
using ILOG.CPLEX;
using System.IO;
using System.Diagnostics;

namespace TSPCsharp
{
    class TSP
    {

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
            instance.ZBest = 1E20;

            //Setting the residual time limit for cplex, it's almost equal to instance.TStart
            MipTimelimit(cplex, instance, clock);

            //Cplex solves the curretn
            cplex.Solve();

            //Blank line
            cplex.Output().WriteLine();

            //Creating the file used by GNUPlot to print the solution
            StreamWriter file = new StreamWriter(instance.InputFile + ".dat");

            //Printing the optimal solution and the GNUPlot input file
            for (int i = 0; i < instance.NNodes; i++)
            { 
                for (int j = i + 1; j < instance.NNodes; j++)
                {
                    if (i != j)
                    {
                        //Retriving the correct index position for the current link inside z
                        int position = zPos(i, j, instance.NNodes);
                        //Reading the optimal solution for the actual link (i,j)
                        instance.BestSol[position] = cplex.GetValue(z[position]);
                        //Printing the optimal solution for the link (i,j)
                        cplex.Output().WriteLine(z[position].Name + " = " + instance.BestSol[position]);
                        
                        //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                        if(instance.BestSol[position] != 0)
                        {
                            /*
                             *Current GNUPlot format is:
                             *-- previus link --
                             *<Blank line>
                             *Xi Yi
                             *Xj Yj
                             *<Blank line> 
                             *-- next link --
                            */
                            file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y);
                            file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + "\n");
                        }
                    }
                }
            }

            //GNUPlot input file needs to be closed
            file.Close();

            //Accessing GNUPlot to read the file
            PrintGNUPlot(instance.InputFile);

            cplex.Output().WriteLine();

            //Storing the optimal value of the objective function
            instance.ZBest = cplex.ObjValue;

            //Writing the value
            cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");


            //Closing Cplex link
            cplex.End();


            //Return without errors
            return true;
        }


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

        static void PrintGNUPlot(string name)
        {
            
            //Setting values to open Prompt
            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd.exe");
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;

            //Executing the Prompt
            Process process = Process.Start(processStartInfo);

            if (process != null)
            {
                /*
                 *Writing in the prompt:
                 *gnuplot
                 *plot '<file_name>.tsp.dat' with lines
                 */
                process.StandardInput.WriteLine("gnuplot\nplot '" + name + ".dat' with lines");
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
    }
}
    
