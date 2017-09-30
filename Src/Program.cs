using System;
using System.IO;
using System.Linq;

namespace TSPCsharp
{
    class Program
    {

        //Setting constant value, to access them use Program.<name>
        public const int VERBOSE = 5;

        static void Main(string[] args)
        {
            if (args.Length < 4)
                throw new System.Exception("Input args should be at least 2");

            //Writing the inpute parameters
            if (VERBOSE >= 9)
            {
                Console.Write("Input parameters: ");
                for (int i = 0; i < args.Length; i++) 
                    Console.Write(args[i].ToString() + " ");
                Console.WriteLine("\n\n\n");
            }

            /*
             * Saving input parameters inside an Instance variable
             * Attention: if file name or timilimit are missing 
             * an exception will be throw 
            */
            Instance inst = new Instance();

            ParseInput(inst,args);

            //Reading the input file and storing its data into inst
            Populate(inst);


            //Starting the elaboration of the TSP problem
            //The boolean returning value tells if everything went fine
            TSP.Solve(inst);
            

            //Only to check the output
            Console.ReadLine();

            foreach (string file in Directory.GetFiles("..\\..\\..\\..\\Output\\", "*.dat").Where(item => item.EndsWith(".dat")))
            {
                File.Delete(file);
            }

            foreach (string file in Directory.GetFiles("..\\..\\..\\..\\Output\\", "*.lp").Where(item => item.EndsWith(".lp")))
            {
                File.Delete(file);
            }
        }

        static void ParseInput(Instance inst, string[] input)
        {
            //input is equal to the args vector, every string is obtained by splitting
            //the input at each blank space
            for (int i = 0; i < input.Length; i++)
            {
                /*
                * Cerco una delle due parole chiavi "-file" e "-timelimit"
                * all'indice successiva è contenuta la relativa informazione
                */
                if (input[i] == "-file")
                {
                    /*
                    * Si aspetta di trovare il nome del file input
                    * (compreso di estensione)
                    */
                    inst.inputFile = input[++i];
                    continue;
                }
                if (input[i] == "-timelimit")
                {
                    //Si aspetta di trovare il tempo limite espresso in secondi)
                    inst.timeLimit = Convert.ToDouble(input[++i]);
                    continue;
                }
            }

            //At least the input file name and the timelimit are needed
            if (inst.inputFile == null || inst.timeLimit == 0)
                throw new Exception("Missing information or bad format inside the command line");
        }

        static void Populate(Instance inst)
        {
            //line will store each lines of the input file
            string line;
            //When true line is storing a point's coordinates of the problem
            bool readingCoordinates = false;
            //Rappresenta il numero di nodi per cui sono state lette le coordinate
            int cnt = 0;

            try
            {
                //StreaReader types need to be disposed at the end
                //the 'using' key automatically handles that necessity
                using (StreamReader sr = new StreamReader("..\\..\\..\\..\\Data\\" + inst.inputFile))
                {
                    //The whole file is read
                    while ((line = sr.ReadLine()) != null)
                    {
                        /*
                        * Il contenuto delle righe con prefisso le seguenti parole chiavi
                        * viene solamente mostrato a video ma non memorizzato dentro
                        * l'oggetto di tipo Istance che si sta popolando
                        * in quanto tali informazioni non risultano rilevanti ai fini
                        * della risoluzione dell'istanza TSP
                        */
                        if (line.StartsWith("NAME") || line.StartsWith("COMMENT") || line.StartsWith("TYPE") || line.StartsWith("EDGE_WEIGHT_FORMAT: FUNCTION") || line.StartsWith("DISPLAY_DATA_TYPE: COORD_DISPLAY"))
                        {
                            if (VERBOSE >= 5)
                                Console.WriteLine(line);

                            //Si passa alla riga successiva del file
                            continue;
                        }

                        /*
                        * Il numero di nodi del grafo è un parametro essenziale
                        * viene memorizzato in Instance.nNodes e
                        * determina la grandezza del vettore Instance.coord
                        * nel quale sono memorizzate le coordinate (x,y)
                        * di ogni nodo
                        */
                        if (line.StartsWith("DIMENSION"))
                        {
                            //Memorizzazione del numero n di nodi del grafo
                            inst.nNodes = Convert.ToInt32(line.Remove(0, line.IndexOf(':') + 2));
                            //Alloco al vettore coord lo spazio di memoria di n oggetti Point
                            inst.coord = new Point[inst.nNodes];

                            if (VERBOSE >= 5)
                                Console.WriteLine(line);

                            //Si passa alla riga successiva del file
                            continue;
                        }

                        /*
                        * Questo parametro determina la formula matematica
                        * da utilizzare per calcolare la distanza tra due oggetti Point
                        * Viene memorizzato in Instance.edgeType
                        */
                        if (line.StartsWith("EDGE_WEIGHT_TYPE"))
                        {
                            string tmp = line.Remove(0, line.IndexOf(':') + 2);
                            //Solo questi tipi sono supportati dalla applicazione
                            if (!(tmp == "EUC_2D" || tmp == "ATT" || tmp == "MAN_2D" || tmp == "GEO" || tmp == "MAX_2D" || tmp == "CEIL_2D"))
                                throw new System.Exception("Format error:  only EDGE_WEIGHT_TYPE == {ATT, MAN_2D, GEO, MAX_2D and CEIL_2D} are implemented");

                            //Se il tipo di peso di nodo è supportato dalla applicazione
                            inst.edgeType = tmp;

                            if (VERBOSE >= 5)
                                Console.WriteLine(line);

                            //Si passa alla riga successiva del file
                            continue;
                        }

                        /*
                        * La parola chiave NODE_COORD_SECTION indica che le prossime n
                        * righe contengono le informazioni riguardanti le coordinate (x,y)
                        * degli n nodi del grafo
                        */
                        if (line.StartsWith("NODE_COORD_SECTION"))
                        {
                            //L'informazione riguardante il numero di nodi deve essere nota
                            if (inst.nNodes <= 0)
                                throw new System.Exception("DIMENSION section should be before NODE_COORD_SECTION section");

                            if (VERBOSE >= 5)
                                Console.WriteLine(line);

                            //Viene settato a true il valore di readingCoordinates
                            readingCoordinates = true;

                            //Si passa alla riga successiva del file
                            continue;
                        }

                        //Questa riga viene ignorata
                        if (line.StartsWith("EDGE_WEIGHT_FORMAT: FUNCTION "))
                        {
                            //Si passa alla riga successiva del file
                            continue;
                        }

                        //La parola chiave EOF indica la terminazione del file
                        if (line.StartsWith("EOF"))
                        {
                            /*
                            * Le informazioni memorizzate riguardanti le coordinate (x,y)
                            * degli n nodi vengono stampate a video
                            */
                            Instance.Print(inst);

                            if (VERBOSE >= 5)
                                Console.WriteLine(line);

                            //Il ciclo di lettura viene interrotto
                            break;
                        }

                        /*
                        * Se il valore readingCoordinates è pari a true significa che
                        * la lettura del file è giunta alla n righe contenenti
                        * le coordinate (x,y) degli n nodi del grafo del problema TSP
                        */
                        if (readingCoordinates)
                        {
                            /*
                            * elements viene settato nel seguente modo
                            * elements[0] -> indice reale del nodo
                            * elements[1] -> coordinata x del nodo
                            * elements[2] -> coordinata y del nodo
                            */
                            string[] elements = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            int i = Convert.ToInt32(elements[0]);
                            //Il valore dell'indice deve essere compreso tra 1 ed n
                            if (i < 0 || i > inst.nNodes)
                                throw new System.Exception("Unknown node in NODE_COORD_SECTION section");
                            /*
                            * Se l'indice reale i è valido
                            * l'oggetto Point che descrive le sue coordinate bidimensionali
                            * viene memorizzato alla posizione i-1 di Instance.coord
                            */
                            inst.coord[i - 1] = new Point(Convert.ToDouble(elements[1].Replace(".", ",")), Convert.ToDouble(elements[2].Replace(".", ",")));

                            //Il contatore dei nodi analizzati è aumentato
                            cnt++;

                            /*
                            * Reperite le informazioni di tutti gli n nodi
                            * readingCoordinates è settato a false
                            */
                            if (cnt == inst.nNodes)
                                readingCoordinates = false;

                            //Si passa alla riga successiva del file
                            continue;
                        }

                        //Se la riga è priva di caratteri si passa alla successiva
                        if (line == "")
                            continue;

                        /*
                        * Giunti a questo punto finale del ciclo while significa 
                        * che il file non rispetto lo standar prefissato
                        * la sua lettura non può essere continuata
                        */
                        throw new System.Exception("The file bad format");
                    }
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
        }
    }
}
