//Generating a new heuristic solution for the Genetic Algorithm
public static PathGenetic NearestNeightborGenetic(Instance instance, Random rnd, bool rndStartPoint, List<int>[] listArray)
{
    // heuristicSolution is the path of the current heuristic solution generate
    int[] heuristicSolution = new int[instance.NNodes];

    //Vector 
    bool[] VisitedNodes = new bool[instance.NNodes];

    int firstNode = 0;

    //rndStartPoint define if the starting point is random or always the node 0 
    if (rndStartPoint)
        firstNode = rnd.Next(0, instance.NNodes);

    heuristicSolution[0] = firstNode;
    VisitedNodes[firstNode] = true;

    for (int i = 1; i < instance.NNodes; i++)
    {
        bool found = false;
        int candPos = RndGenetic(rnd);
        int nextNode = listArray[heuristicSolution[i - 1]][candPos];

        do
        {
            //We control that the selected node has never been visited
            if (VisitedNodes[nextNode] == false)
            {
                VisitedNodes[nextNode] = true;
                heuristicSolution[i] = nextNode;
                found = true;
            }
            else
            {
                candPos++;
                if (candPos >= instance.NNodes - 1)
                {
                    nextNode = listArray[heuristicSolution[i - 1]][0];
                    candPos = 0;
                }
                else
                    nextNode = listArray[heuristicSolution[i - 1]][candPos];
            }

        } while (!found);
    }

    return new PathGenetic(heuristicSolution, instance);
}