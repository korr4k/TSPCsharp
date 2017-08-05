using ILOG.Concert;
using ILOG.CPLEX;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace TSPCsharp
{
    public class TSPLazyConsCallback : Cplex.LazyConstraintCallback
    {
        private bool BlockPrint;
        private Cplex cplex;
        private INumVar[] z;
        private Instance instance;
        private Process process;

        public TSPLazyConsCallback(Cplex cplex, INumVar[] z, Instance instance, Process process, bool BlockPrint)
        {
            this.cplex = cplex;
            this.z = z;
            this.BlockPrint = BlockPrint;
            this.instance = instance;
            this.process = process;
        }

        public override void Main()
        {

            //Init buffers, due to multithreading, using global buffers is incorrect
            List<ILinearNumExpr> ccExprLC = new List<ILinearNumExpr>();
            List<int> bufferCoeffCCLC = new List<int>(); ;

            int[] compConnLC = new int[instance.NNodes];

            Utility.InitCC(compConnLC);

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
                    int position = Utility.zPos(i, j, instance.NNodes);

                    //Only links in the optimal solution (coefficient = 1) are printed in the GNUPlot file
                    if (actualZ[position] >= 0.5)
                    {
                        //Updating the model with the current subtours elimination
                        Utility.UpdateCC(cplex, z, ccExprLC, bufferCoeffCCLC, compConnLC, i, j);

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

            string fileName = instance.InputFile + "_" + nodeId;

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
