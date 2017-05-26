using System.Collections.Generic;

namespace TSPCsharp
{
    public class Tabu
    {
        string mode;
        string originalMode;
        List<string> tabuVector;
        Instance inst;
        int threshold;

        public Tabu(string mode, Instance inst, int threshold)
        {
            this.mode = mode;
            this.originalMode = mode;
            this.inst = inst;
            this.threshold = threshold;
            this.tabuVector = new List<string>();
        }

        public bool IsTabu(int a, int b)
        {
            if (tabuVector.Contains(a + ";" + b) ||
                tabuVector.Contains(b + ";" + a))
                return true;
            else
                return false;
        }

        public void AddTabu(int a, int b, int c, int d)
        {
            if (mode == "A" && tabuVector.Count >= (threshold - 1))
            {
                mode = "B";

            }
            else if (mode == "B")
            {
                if (tabuVector.Count >= 50)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        tabuVector.RemoveAt(i);
                    }
                }
                else
                    mode = "A";
            }

            if (!tabuVector.Contains(a + ";" + b))
                tabuVector.Add(a + ";" + b);
            if (!tabuVector.Contains(c + ";" + d))
                tabuVector.Add(c + ";" + d);
        }

        public void Clear()
        {
            mode = originalMode;
            tabuVector.Clear();
        }

        public int TabuLenght()
        {
            return tabuVector.Count;
        }
    }
}
