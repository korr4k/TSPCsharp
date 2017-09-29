using ILOG.Concert;
using ILOG.CPLEX;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace TSPCsharp
{
    public class TSPLazyConsCallback : Cplex.LazyConstraintCallback
    {
        internal bool BlockPrint;
        internal Cplex cplex;
        internal INumVar[] x;
        internal Instance instance;
        internal Process process;

        public TSPLazyConsCallback(Cplex cplex, INumVar[] x, Instance instance, Process process, bool BlockPrint)
        {
            this.cplex = cplex;
            this.x = x;
            this.BlockPrint = BlockPrint;
            this.instance = instance;
            this.process = process;
        }

        public override void Main()
        {

            //Init buffers, due to multithreading, using global buffers is incorrect
            List<ILinearNumExpr> ccExprLC = new List<ILinearNumExpr>();
            List<int> bufferCoeffCCLC = new List<int>(); ;

            int[] compConnLC = new int[instance.nNodes];

            Utility.InitCC(compConnLC);

            //To call GetValues for each value in x is a lot more expensive for unknown reasons
            double[] actualX = GetValues(x);

            //Node's is that generated the callback, used to create an unique nome for the GNUPlot files
            string nodeId = GetNodeId().ToString();

            StreamWriter fileLC;

            if (Program.VERBOSE >= -1)
            {
                //Init the StreamWriter for the current solution
                fileLC = new StreamWriter(instance.inputFile + "_" + nodeId + ".dat", false);
            }

            for (int i = 0; i < instance.nNodes; i++)
            {
                for (int j = i + 1; j < instance.nNodes; j++)
                {
                    //Retriving the correct index position for the current link inside x
                    int position = Utility.xPos(i, j, instance.nNodes);

                    //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                    if (actualX[position] >= 0.5)
                    {
                        //Updating the model with the current subtours elimination
                        Utility.UpdateCC(cplex, x, ccExprLC, bufferCoeffCCLC, compConnLC, i, j);

                        if (BlockPrint)
                        {
                            fileLC.WriteLine(instance.coord[i].x + " " + instance.coord[i].y + " " + (i + 1));
                            fileLC.WriteLine(instance.coord[j].x + " " + instance.coord[j].y + " " + (j + 1) + "\n");
                        }
                    }
                }
            }

            if (Program.VERBOSE >= -1)
            {
                //GNUPlot input file needs to be closed
                fileLC.Close();
            }

            string fileName = instance.inputFile + "_" + nodeId;

            //Accessing GNUPlot to read the file
            if (BlockPrint)
                Utility.PrintGNUPlot(process, fileName, 1, GetIncumbentObjValue(), GetBestObjValue());

            //cuts stores the user's cut
            IRange[] cuts = new IRange[ccExprLC.Count];

            //if cuts.Length is 1 the graph has only one tour then cuts aren't needed
            if (cuts.Length > 1)
            {
                for (int i = 0; i < cuts.Length; i++)
                {
                    cuts[i] = cplex.Le(ccExprLC[i], bufferCoeffCCLC[i] - 1);
                    Add(cuts[i], 1);
                }
            }
        }
    }
}
