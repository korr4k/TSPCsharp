using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILOG.Concert;
using ILOG.CPLEX;

namespace TSPCsharp
{
    class MIPUtilities
    {
        public static void mipSetLevelForAllCuts(Cplex cplex, int level)
        {
            cplex.SetParam(Cplex.IntParam.Cliques, level);
            cplex.SetParam(Cplex.IntParam.Covers, level);
            cplex.SetParam(Cplex.IntParam.DisjCuts, level);
            cplex.SetParam(Cplex.IntParam.FlowCovers, level);
            cplex.SetParam(Cplex.IntParam.FlowPaths, level);
            cplex.SetParam(Cplex.IntParam.FracCuts, level);
            cplex.SetParam(Cplex.IntParam.GUBCovers, level);
            cplex.SetParam(Cplex.IntParam.ImplBd, level);
            cplex.SetParam(Cplex.IntParam.MIRCuts, level);
            cplex.SetParam(Cplex.IntParam.ZeroHalfCuts, level);
            cplex.SetParam(Cplex.IntParam.LiftProjCuts, level);
            cplex.SetParam(Cplex.IntParam.MCFCuts, level);
        }
    }
}
