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
            instance.TStart = clock.ElapsedMilliseconds * 1000;

            Cplex cplex = new Cplex();

            cplex.SetParam(Cplex.IntParam.MIPDisplay, 4);

            cplex.SetParam(Cplex.DoubleParam.EpInt, 0);
            cplex.SetParam(Cplex.DoubleParam.EpGap, 1E-9);
            cplex.SetParam(Cplex.DoubleParam.EpRHS, 1E-9);

            cplex.SetParam(Cplex.LongParam.RINSHeur, 10);

            MIPUtilities.mipSetLevelForAllCuts(cplex, 2);

            INumVar[]z = BuildModel(cplex, instance);

            instance.BestSol = new double[(instance.NNodes - 1) * instance.NNodes / 2];
            instance.ZBest = 1E-20;

            cplex.SetParam(Cplex.Param.MIP.Strategy.Search, Cplex.MIPSearch.Traditional);

            //mipTimelimit(env, CPX_INFBOUND, inst);
            //if (timeLimitExpired(inst)) goto EXIT;
            cplex.SetParam(Cplex.LongParam.NodeLim, 0);

            cplex.Solve();

            cplex.Output().WriteLine();

            StreamWriter file = new StreamWriter(instance.InputFile + ".dat");

            for (int i = 0; i < instance.NNodes; i++)
            { 
                for (int j = i + 1; j < instance.NNodes; j++)
                {
                    if (i != j)
                    {
                        int position = zPos(i, j, instance.NNodes);
                        instance.BestSol[position] = cplex.GetValue(z[position]);
                        cplex.Output().WriteLine(z[position].Name + " = " + instance.BestSol[position]);
                        
                        if(instance.BestSol[position] != 0)
                        {
                            file.WriteLine(instance.Coord[i].X + " " + instance.Coord[i].Y);
                            file.WriteLine(instance.Coord[j].X + " " + instance.Coord[j].Y + "\n");
                        }
                    }
                }
            }

            file.Close();

            PrintGNUPlot(instance.InputFile);

            cplex.Output().WriteLine();

            instance.ZBest = cplex.ObjValue;

            cplex.Output().WriteLine("zOPT = " + instance.ZBest + "\n");

            cplex.End();

            return true;
        }


        static INumVar[] BuildModel(Cplex cplex, Instance instance)
        {
            int tot = (instance.NNodes - 1) * instance.NNodes / 2;

            INumVar[] z = new INumVar[tot];

            ILinearNumExpr expr = cplex.LinearNumExpr();

            //populate objective function

            for (int i = 0; i < instance.NNodes; i++)
            {
                for (int j = i + 1; j < instance.NNodes; j++)
                {
                    int position = zPos(i, j, instance.NNodes);
                    expr.AddTerm(z[position] = cplex.NumVar(0, 1, NumVarType.Int, "z(" + (i + 1) + "," + (j + 1) + ")"), Point.Distance(instance.Coord[i], instance.Coord[j], instance.EdgeType));
                }
            }

            cplex.AddMinimize(expr);

            for (int i = 0; i < instance.NNodes; i++)
            {
                expr = cplex.LinearNumExpr();

                for (int j = 0; j < instance.NNodes; j++)
                {
                    if (i != j)
                        expr.AddTerm(z[zPos(i, j, instance.NNodes)], 1);
                }

                cplex.AddEq(expr, 2, "degree(" + (i + 1) + ")");
            }

            if (Program.VERBOSE >= -100)
                cplex.ExportModel(instance.InputFile + ".lp");

            return z;

        }

        static void PrintGNUPlot(string name)
        {

            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd.exe");
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;

            Process process = Process.Start(processStartInfo);

            if (process != null)
            {
                process.StandardInput.WriteLine("gnuplot\nplot '" + name + ".dat' with lines");
            }
        }

        static int zPos(int i, int j, int nNodes)
        {
            if (i == j)
                return -1;

            if (i > j)
                return zPos(j, i, nNodes);

            return i * nNodes + j - (i + 1) * (i + 2) / 2;
        }
    }
}
    
