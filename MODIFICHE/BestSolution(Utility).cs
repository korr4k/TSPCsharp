
//Sostituisci anche la firma al tuo metodo BestSolution perchè ho tolto anche il secondo parametro che gli passavo.
//Questo ti darà due errori in corrispondenza di quando invochi BestSolution: basta togliere il secondo parametro che gli passavi.

public static PathGenetic BestSolution(List<PathGenetic> population)
        {
            PathGenetic currentBestPath = population[0];

            for (int i = 1; i < population.Count; i++)
            {
                if (population[i].cost < currentBestPath.cost)
                    currentBestPath = population[i];
            }

            return currentBestPath;
        }
