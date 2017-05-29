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

        public TSPLazyConsCallback(Cplex cplex, Instance instace, Process process, INumVar[] z, bool BlockPrint)
        {
            this.cplex = cplex;
            this.z = z;
            this.BlockPrint = BlockPrint;
            this.instance = instace;
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
                Utility.PrintGNUPlot(process, instance.InputFile + "_" + nodeId, 1, GetIncumbentObjValue(), GetBestObjValue());

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
                                expr.AddTerm(z[Utility.zPos(h, k, compConnLC.Length)], 1);
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
}
