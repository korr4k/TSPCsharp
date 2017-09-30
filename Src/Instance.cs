using System;


namespace TSPCsharp
{
    public class Instance
    {
        //Dati input utente

        //Nome file
        internal string inputFile { get; set; }
        //Tempo limite di esecuzione
        internal double timeLimit { get; set; }


        //Dati ricavati dal file di input

        //Numero di nodi
        internal int nNodes { get; set; }
        //Coordinate (x,y) di ogni nodo
        internal Point[] coord { get; set; }

        //Determina come calcolare la distanza tra due punti
        internal string edgeType { get; set; }


        //Parametri da definire durante la risoluzione
        
        //Costo della migliore soluzione intera trovata
        internal double xBest { get; set; }
        //Valore di ogni lato nella migliore soluzione intera trovata
        internal double[] bestSol { get; set; }
        //Migliore lower bound trovato, utilizzato in alcuni algoritmi
        internal double bestLb { get; set; }

        //Metodo statico che stampa all'interno della Console le coordinate di tutti i punti memorizzati nell'oggeto instance ricevuto
        static public void Print(Instance inst)
        {
            for (int i = 0; i < inst.nNodes; i++)
                Console.WriteLine("Point #" + (i + 1) + "= (" + inst.coord[i].x + ";" + inst.coord[i].y + ")");
        }
    }
}
